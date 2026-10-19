# Stock & Cart

Interview test: a stock and cart system.

| Layer | Tech |
|---|---|
| Backend | .NET 10 Web API, Clean Architecture, EF Core (migrations) + Npgsql |
| Frontend | Next.js 16 (App Router, Server Components / Server Actions), Tailwind CSS v4, shadcn/ui, Zustand |
| Database | PostgreSQL 17 (Docker volume / k8s PVC) — **the source of truth for stock** |
| Cache | Redis 8: read cache + stock gate in front of Postgres (optional; the system works without it) |
| Infra | Docker Compose + Nginx, Kubernetes manifests (Kustomize) for horizontal scaling |

## Quick start (Docker Compose)

```bash
cp .env.example .env
# Fill in the secrets:
#   POSTGRES_PASSWORD, API_KEY, REDIS_PASSWORD -> openssl rand -hex 32
#   NEXT_SERVER_ACTIONS_ENCRYPTION_KEY    -> openssl rand -base64 32
docker compose up -d --build
```

Open **http://localhost:8088** (change `NGINX_PORT` / `FRONTEND_ORIGIN` in `.env` to use a different port).

The one-off `migrate` service applies EF Core migrations and seeds **5 categories / 50 products**:
40 single items plus 10 combos/packs that deduct their components' stock. It runs once, and only then do the API replicas start.
Some items are seeded with low or zero stock (e.g. *Cheese Danish* = 1, *Energy Drink* = 0) so the stock limits are easy to see.

Reset everything (including the DB volume): `docker compose down -v`.

## Architecture

```
 Browser ──► :8088 nginx ──► frontend (Next.js :3000) ──► backend (.NET :8080) ──┬─► db (Postgres :5432)   source of truth
            └─ network: frontend ─┘      └────── network: backend (internal: true) ├─► redis (:6379)          cache + stock gate
                                                                  migrate (one-off) ──┘
```

- **Only nginx publishes a port.** The API, Postgres and Redis have no `ports:`. They sit on an `internal` Docker network that the host and nginx cannot reach, and that has no internet access.
- The browser never calls the API. Next.js **Server Components fetch data** and a **Server Action places orders**, both server-to-server over the internal network.

```
backend/src
├── StockCart.Domain          Entities + enums (no dependencies)
├── StockCart.Application     Use cases (ProductService, OrderService), DTOs, repository/UoW interfaces
├── StockCart.Infrastructure  EF Core DbContext, migrations, repositories, seed, DB health check,
│                             Redis/ (stock gate Lua scripts, cache, Postgres -> Redis stock sync)
└── StockCart.Api             Controllers, API-key middleware, ProblemDetails, CORS, health endpoints, --migrate mode
frontend/src
├── app/                      pages (/, /cart, /orders/[id]), actions.ts (Server Action), healthz
├── components/               UI (shadcn/ui in components/ui)
├── lib/api.ts                server-only API client (holds the API key)
├── lib/stock.ts              cart-vs-stock calculations
└── store/cart-store.ts       Zustand cart persisted to localStorage
k8s/                          Kubernetes manifests (Kustomize)
scripts/                      race-test.mjs, k8s-up.sh
```

## Database

| Table | Purpose |
|---|---|
| `product_category` | Groups products for display |
| `product` | `product_code` (unique), `product_name`, `product_price_unit` |
| `product_stock` | `product_id` (PK/FK), `stock_remain` with `CHECK (stock_remain >= 0)` |
| `product_deduction` | Selling 1 unit of `product_id` deducts `deduct_quantity` from `deduct_product_id`. A single item deducts itself ×1. A combo (e.g. *Burger Combo*) deducts each of its components. |
| `order` | Order header: `order_no`, `total_amount`, `total_quantity`, `payment_method`, `created_at`, `idempotency_key` (unique). The id is a random UUID, so it can't be enumerated. |
| `order_detail` | Order lines: `product_id`, `quantity`, `unit_price` (price snapshot), `line_total` |

Schema changes are EF Core migrations in `backend/src/StockCart.Infrastructure/Persistence/Migrations`
(`dotnet ef migrations add <Name> --project src/StockCart.Infrastructure --startup-project src/StockCart.Infrastructure`).

## API

All `/api/*` routes require the `X-Api-Key` header.

| Method | Route | |
|---|---|---|
| GET | `/api/categories` | categories |
| GET | `/api/products` | products with `stockRemain`, computed `available`, and `deductions` |
| POST | `/api/orders` | `{ "paymentMethod": "Cash" \| "CreditCard" \| "PromptPay", "items": [{ "productId": 1, "quantity": 2 }] }`. Optional `Idempotency-Key` header. Returns `201` created / `200` replay of the same key / `400` / `404` / `409` out of stock. |
| GET | `/api/orders/{id}` | order summary |
| GET | `/health/live` | process is up (does not touch the DB) |
| GET | `/health/ready` | DB reachable **and** no pending migrations |

Errors use RFC 7807 ProblemDetails.

### Swagger (Development only)

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build
```

Open **http://localhost:8088/swagger** → **Authorize** → paste `API_KEY` from `.env` → **Try it out**.

| | Development (`docker-compose.dev.yml`) | Production (`docker-compose.yml`, k8s) |
|---|---|---|
| API serves `/swagger` | yes (`ASPNETCORE_ENVIRONMENT=Development`) | no — not even registered (404) |
| nginx routes `/swagger` → API | yes (`nginx/dev/swagger.conf` mounted into `/etc/nginx/extra`) | no — the include folder is empty |
| nginx on the backend network | yes | no — nginx can't resolve `backend` at all |

- Everything stays under `/swagger`: the UI, `/swagger/v1/swagger.json`, and the "Try it out" calls (`/swagger/try/api/...` → `/api/...`).
- `/api` itself is still not published, and `X-Api-Key` is still required.
- Switch back to production mode with `docker compose up -d`.

## How stock deduction stays correct under concurrency

`OrderService.CreateAsync` ([backend/src/StockCart.Application/Services/OrderService.cs](backend/src/StockCart.Application/Services/OrderService.cs)):

1. Validate the request and merge duplicate lines. **Prices and deduction rules are read from the DB**; the client's prices are never trusted.
2. Add up the stock needed for each stock product **across all lines**. For example, *Cola ×2 + Burger Combo ×1* needs 3 Cola.
3. Open a transaction, then for each stock product **in ascending id order** run one atomic, parameterized statement:

   ```sql
   UPDATE product_stock SET stock_remain = stock_remain - @qty, updated_at = @now
   WHERE product_id = @id AND stock_remain >= @qty
   ```

   - 0 rows affected → `InsufficientStockException` → **rollback** → HTTP 409.
   - The UPDATE takes the row lock and holds it until commit or rollback. A competing order waits. In READ COMMITTED, Postgres then **re-checks the WHERE clause against the newly committed value**, so the last unit can't be sold twice.
   - It does the check and the write in one round trip, so it is simpler and shorter-locking than `SELECT … FOR UPDATE` followed by `UPDATE`.
   - Locking rows in a fixed order (ascending id) means two orders touching the same products can't deadlock.
4. Insert `order` and `order_detail`, then commit.

The `CHECK (stock_remain >= 0)` constraint is a second safety net at the DB level.
**The lock lives in Postgres, not in the API process**, so this stays correct with any number of API replicas.

### Verify it

The test fires concurrent orders at one stock product **and** at the combos that use it, then checks that stock was never oversold and that the remaining stock matches the successful orders:

```bash
docker compose exec -T frontend node --input-type=module - < scripts/race-test.mjs
docker compose exec -T -e PRODUCT_CODE=SNK-010 -e REQUESTS=40 frontend node --input-type=module - < scripts/race-test.mjs
```

```
Target MEL-010 "Fried Chicken Wings (3 pcs)" stock before: 4
Firing 30 concurrent orders...
Status codes: 201 x4, 409 x26
PASS - no overselling, stock is consistent
```

## Redis: read cache + stock gate (Postgres stays the source of truth)

**Rule: Redis may only make the system faster, never more or less correct.** Every order is still decided by the Postgres transaction above. Redis is optional: without `Redis:ConnectionString`, or while Redis is down, everything runs on Postgres alone.

### 1. Read cache — `GET /api/products`, `GET /api/categories`

- The **catalog** (names, prices, deduction rules) is cached in Redis for 60 s.
- **Stock is not cached.** It is read live from the stock-gate counters (one `MGET`), so a sale never needs a cache invalidation and a page load during a flash sale doesn't touch Postgres at all.
- The displayed stock is informational only. Prices and deduction rules used by an order are always read from Postgres.

### 2. Stock gate — `POST /api/orders`

```
 ① Redis Lua script (atomic, all items of the cart at once):
      every counter >= needed?  yes → DECRBY all of them  ("reserved")
                                no  → 409 immediately — Postgres is never touched
                                key missing / Redis down → skip the gate ("unavailable")
 ② Postgres transaction (unchanged): conditional UPDATE per stock row → INSERT order → COMMIT
 ③ ② did not commit (out of stock, duplicate Idempotency-Key, crash, cancel) → INCRBY back (release)
```

During a flash sale, most buyers lose. The gate turns them away in Redis in well under a millisecond, instead of letting each one queue for the same Postgres row lock while holding a DB connection.

### Why a wrong Redis counter can never oversell

| Redis counter vs Postgres | Effect | Repaired by |
|---|---|---|
| **Higher** (e.g. sync ran while orders were in flight) | More requests pass the gate; Postgres rejects the extra ones. **No overselling**, just less filtering. | next sync |
| **Lower** (e.g. a release was lost when a pod crashed) | Some sellable units are temporarily refused (409). | next sync |
| **Missing** (Redis restarted empty) | Gate returns "unavailable" → Postgres only. | immediate re-sync on reconnect |
| **Redis down** | Reads come from Postgres, orders go straight to the Postgres transaction. `BacklogPolicy.FailFast` + 500 ms timeouts mean no extra latency. Redis is **not** part of `/health/ready`, so pods stay in service. | — |

`StockSyncService` copies the committed stock from Postgres into Redis (`MSET`) at startup, every `Redis:StockSyncSeconds` (30 s), and right after Redis reconnects. Copying committed DB values can only produce a counter that is equal or **higher** (the safe direction), never lower.

Other details:
- Counter keys use the hash tag `{stock}:<id>`, so the multi-key Lua scripts stay valid on Redis Cluster.
- Redis runs without persistence. `volatile-lru` evicts only TTL'd cache entries, never stock counters.
- It is password-protected and internal-only.

### Verified (3 API replicas)

| Scenario | Result |
|---|---|
| 30 concurrent orders for 1 *Cheese Danish*, gate on | 201 ×1, 409 ×29 — **0 of the 29 rejects reached Postgres** (`pg_stat_database.xact_rollback`) |
| 40 concurrent orders for 16 *Tuna Sandwich*, Redis stopped | 201 ×16, 409 ×24 — all 24 rejected by Postgres, no overselling |
| Redis counter forced to **1000** for *Fresh Milk* (real stock 3), 40 orders | 201 ×3 — Postgres stock 0, **no overselling** |
| Redis counter forced to **0** for *Orange Juice* (real stock 40) | 409 until the next sync (~30 s), then 201 |
| Redis restarted (empty) | all 40 counters re-synced on reconnect |
| 20 concurrent orders with one Idempotency-Key | 1 order; losers released their reservation → Redis = Postgres |

```bash
docker compose exec -T redis redis-cli --no-auth-warning MGET "{stock}:1" "{stock}:2"   # counters
docker compose stop redis   # site keeps working on Postgres; `docker compose start redis` re-syncs
```

## Scaling (peak sales) and Kubernetes

### What makes it horizontally scalable

| Concern | How it is handled |
|---|---|
| Overselling with N API replicas | The row lock is in Postgres (see above). There are no in-process locks, so replicas can't disagree. |
| Schema migration race when many pods start together | The API never migrates at startup. A single **migration Job** (`--migrate` mode, same image) applies EF migrations. Seeding is guarded by a Postgres advisory lock. |
| Traffic sent to pods that aren't ready | `/health/ready` checks DB reachability **and pending migrations**, so new pods get no traffic until the Job has finished. `/health/live` never touches the DB, so a DB outage doesn't restart every pod. |
| Too many DB connections | Each pod is capped at `Maximum Pool Size=20`. HPA max 6 pods × 20 = 120, below Postgres `max_connections=200`. |
| Duplicate orders from retries or double clicks | The browser creates an **Idempotency-Key** per checkout. The key is unique in the DB: a repeat returns the original order (HTTP 200) and deducts nothing. Verified with 20 concurrent requests sharing one key across 3 replicas → exactly 1 order. |
| Next.js "Failed to find Server Action" across pods or deploys | `NEXT_SERVER_ACTIONS_ENCRYPTION_KEY` is fixed and passed at build time as a BuildKit secret, so it isn't in `docker history`. `NEXT_DEPLOYMENT_ID` per build enables version-skew protection during rolling updates. |
| Pods killed mid-order on scale-in or deploy | `preStop` sleep lets kube-proxy stop routing to the pod. .NET then drains in-flight requests (`ShutdownTimeout` 20 s, `terminationGracePeriodSeconds` 30). Rolling updates use `maxUnavailable: 0`. |
| Autoscaling and availability | HPA on CPU (2–6 pods, scales up fast, scales down after 5 min). A PodDisruptionBudget keeps ≥1 pod of each tier during node drains. |
| Hot row: thousands of buyers for one product | The Redis stock gate rejects the losers before they wait on the Postgres row lock (see above). |
| Read load on every page view | Catalog served from the Redis cache, stock from the Redis counters. |
| Isolation | NetworkPolicies mirror the compose networks: nginx → frontend → backend → db / redis (plus migrate → db). |

Cart state lives in the browser (`localStorage`) and there are no server sessions, so any pod can serve any request.

### Try scaling with Docker Compose

```bash
docker compose up -d --build --scale backend=3 --scale frontend=2
docker compose exec -T -e PRODUCT_CODE=DRK-001 -e REQUESTS=60 frontend node --input-type=module - < scripts/race-test.mjs
docker compose logs backend | grep "created (" | awk '{print $1}' | sort | uniq -c   # orders handled per replica
```

### Deploy to Kubernetes (Docker Desktop)

1. Docker Desktop → Settings → Kubernetes → enable it (single-node / kubeadm, so local images are shared).
2. Run the deploy script:

   ```bash
   ./scripts/k8s-up.sh      # builds images, generates k8s/secrets.env, runs the migration Job, rolls out
   ```

3. Open **http://localhost:8088** (nginx `LoadBalancer` Service).

```bash
kubectl -n stock-cart get pods,hpa
kubectl -n stock-cart scale deployment/backend --replicas=4          # manual scale (HPA keeps it within 2..6)
kubectl -n stock-cart exec -i deploy/frontend -- node --input-type=module - < scripts/race-test.mjs
kubectl delete namespace stock-cart                                  # tear down (deletes the PVC too)
```

| File | |
|---|---|
| `k8s/kustomization.yaml` | namespace, ConfigMap, Secret generated from `k8s/secrets.env` (git-ignored, hash-suffixed so changes roll pods), image tags |
| `k8s/postgres.yaml` | StatefulSet + PVC + headless Service `db` |
| `k8s/redis.yaml` | Redis Deployment + Service (no PVC: counters are rebuilt from Postgres) |
| `k8s/migrate-job.yaml` | migration + seed Job |
| `k8s/backend.yaml`, `k8s/frontend.yaml` | Deployments + Services, probes, resources, non-root security context |
| `k8s/nginx.yaml` | reverse proxy (same image/config as compose) behind a `LoadBalancer` |
| `k8s/autoscaling.yaml` | HPA + PodDisruptionBudget |
| `k8s/network-policy.yaml` | default-deny + allowed paths |

Notes:
- HPA needs **metrics-server**, which Docker Desktop doesn't include by default.
- NetworkPolicies are only enforced by a CNI that supports them (Calico, Cilium, …).
- In production, Postgres would be a managed DB (or an operator such as CloudNativePG), images would use immutable registry tags (`kustomize edit set image`), and nginx would usually be replaced by the cluster's Gateway API / Ingress controller.

### Limits and next steps for a flash sale

Already in place: a short Postgres lock (UPDATE → INSERT → COMMIT), the Redis stock gate that filters out losing buyers, and the Redis read cache. The buyers who **do** pass the gate still commit one by one on the same row, which is fine for realistic sales. Beyond that:

1. **PgBouncer** (transaction pooling) between the pods and Postgres, so many more pods can share a small number of real connections.
2. **`lock_timeout`** (e.g. 2 s) so requests fail fast with "busy, retry" instead of piling up behind the hot row.
3. **Redis HA** (Sentinel / managed Redis), so the gate stays up during a node failure. Correctness doesn't depend on it.
4. **Queue-based checkout** (RabbitMQ/Kafka): accept the order as *pending* and let a worker deduct stock sequentially. The user then sees "processing", and the DB load stays flat however big the spike.

Not chosen on purpose: making **Redis the source of truth** and syncing to Postgres later. It is faster, but Redis can lose the last second of writes on a crash. For stock and money that means lost or oversold orders, and it is hard to audit.

## Security

- **No secrets in code.** The DB password, Redis password, API key and Server Actions key come from `.env` or the k8s Secret. The API refuses to start without a key of at least 32 characters. Next.js reads `API_KEY` / `API_INTERNAL_URL` only on the server (no `NEXT_PUBLIC_` prefix, plus the `server-only` guard).
- **SQL injection:** every query goes through EF Core LINQ, or the parameterized `ExecuteSqlAsync($"…")`. There is no raw SQL string building.
- **Only our Next.js can call the API:**
  - The API is reachable only on the internal network or through NetworkPolicy.
  - It requires a shared secret `X-Api-Key`, compared in constant time.
  - CORS allows only `FRONTEND_ORIGIN`, with methods GET/POST and only the needed headers.
- **Input validation** in both the Server Action and the API: payment-method enum (integer values rejected), 1–100 lines, quantity 1–1000, positive ids, and the Idempotency-Key format. Order ids must be UUIDs. Request body limit is 64 KB at the API and 1 MB at nginx.
- **CSRF:** Next.js Server Actions compare `Origin` with `Host`. nginx forwards `$http_host`, so the check works behind the proxy, and requests from foreign origins are rejected.
- **nginx:** per-IP rate limit, `server_tokens off`, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`.
- Containers run as non-root (`app` / `node`), and k8s pods drop all capabilities. Unknown errors return a generic message only; stack traces stay in the logs.

## Frontend behaviour

- **Server-side fetching.** Pages render a static shell and stream the stock data at request time (`<Suspense>` + `connection()`, Next 16 Cache Components), so stock is always fresh.
- **Zustand cart** persisted to `localStorage`, so it survives a refresh. It is hydrated after mount to avoid SSR mismatches, synced across tabs, and stored data is sanitised on load.
- **Stock limits in the cart.** The `+` button stops at the remaining stock. The limit accounts for **shared components**: if the cart holds Cola ×118 (of 120), a *Burger Combo* (which uses 1 Cola) can be added at most twice.
- **Stale carts are clamped automatically.** Whenever fresh stock arrives (page load, or a refresh after a 409 because someone else bought it first), the cart is adjusted and the user sees a toast.
- **Pages:** `/` (products by category), `/cart` (review, payment method, place order), `/orders/[id]` (order summary).

## Local development without Docker (optional)

```bash
# Postgres must be running locally
cd backend
export ConnectionStrings__Default="Host=localhost;Database=stockcart;Username=...;Password=..."
dotnet run --project src/StockCart.Api -- --migrate          # migrations + seed, then exits
Security__ApiKey="<32+ chars>" Cors__AllowedOrigins__0="http://localhost:3000" \
ASPNETCORE_URLS="http://localhost:5080" dotnet run --project src/StockCart.Api

cd frontend
cp .env.example .env.local   # same API_KEY, API_INTERNAL_URL=http://localhost:5080
npm install && npm run dev
```
