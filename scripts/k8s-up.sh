#!/usr/bin/env bash
# Build images and deploy to the current kubectl context (e.g. Docker Desktop Kubernetes).
#   ./scripts/k8s-up.sh            then open http://localhost:8088
# Docker Desktop's single-node (kubeadm) cluster uses the local Docker images directly.
# For kind:  kind load docker-image stock-cart-backend:local stock-cart-frontend:local stock-cart-nginx:local
set -euo pipefail
cd "$(dirname "$0")/.."

NS=stock-cart
DEPLOYMENT_ID="$(date +%Y%m%d%H%M%S)"

# 1. Runtime secrets (k8s/secrets.env is git-ignored)
if [ ! -f k8s/secrets.env ]; then
  echo "Generating k8s/secrets.env with random secrets"
  sed -e "s/^POSTGRES_PASSWORD=.*/POSTGRES_PASSWORD=$(openssl rand -hex 16)/" \
      -e "s/^API_KEY=.*/API_KEY=$(openssl rand -hex 32)/" \
      -e "s/^REDIS_PASSWORD=.*/REDIS_PASSWORD=$(openssl rand -hex 24)/" \
      k8s/secrets.env.example > k8s/secrets.env
fi

# 2. Build-time key for Next.js Server Actions (same key for every build -> no skew between pods)
if [ -z "${NEXT_SERVER_ACTIONS_ENCRYPTION_KEY:-}" ] && [ -f .env ]; then
  NEXT_SERVER_ACTIONS_ENCRYPTION_KEY="$(grep -E '^NEXT_SERVER_ACTIONS_ENCRYPTION_KEY=' .env | cut -d= -f2- || true)"
fi
export NEXT_SERVER_ACTIONS_ENCRYPTION_KEY="${NEXT_SERVER_ACTIONS_ENCRYPTION_KEY:-$(openssl rand -base64 32)}"

# 3. Images (same Dockerfiles as docker-compose)
docker build -t stock-cart-backend:local backend
docker build -t stock-cart-nginx:local nginx
docker build -t stock-cart-frontend:local \
  --build-arg NEXT_DEPLOYMENT_ID="$DEPLOYMENT_ID" \
  --secret id=next_actions_key,env=NEXT_SERVER_ACTIONS_ENCRYPTION_KEY \
  frontend

# 4. Jobs are immutable -> replace the migration Job on every deploy
kubectl delete job db-migrate -n "$NS" --ignore-not-found
kubectl apply -k k8s

# Same ":local" tag was rebuilt -> restart so pods pick up the new image
# (with registry tags per release this is just `kustomize edit set image`).
kubectl -n "$NS" rollout restart deployment/backend deployment/frontend deployment/nginx

kubectl -n "$NS" wait --for=condition=complete job/db-migrate --timeout=300s
for d in redis backend frontend nginx; do
  kubectl -n "$NS" rollout status "deployment/$d" --timeout=300s
done

kubectl -n "$NS" get pods,svc,hpa
echo
echo "Ready: http://localhost:8088"
