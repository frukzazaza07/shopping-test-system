// Concurrency test: fires many simultaneous orders that compete for the same stock row
// (the product itself AND combos that deduct from it) and verifies nothing is oversold.
//
// The API is not reachable from the host, so run it inside the frontend container:
//   docker compose exec -T frontend node --input-type=module - < scripts/race-test.mjs
//   kubectl -n stock-cart exec -i deploy/frontend -- node --input-type=module - < scripts/race-test.mjs
// Optional: -e PRODUCT_CODE=MEL-010 -e REQUESTS=30

const base = process.env.API_INTERNAL_URL;
const headers = { "Content-Type": "application/json", "X-Api-Key": process.env.API_KEY };
const code = process.env.PRODUCT_CODE ?? "MEL-010";
const requests = Number(process.env.REQUESTS ?? 30);

const getProducts = async () => (await fetch(`${base}/api/products`, { headers })).json();

const products = await getProducts();
const target = products.find((p) => p.productCode === code);
if (!target || target.stockRemain === null) throw new Error(`${code} is not a stock product`);

// Every product that consumes the target's stock (itself + combos containing it).
const consumers = products
  .map((p) => ({ p, per: p.deductions.find((d) => d.stockProductId === target.id)?.quantity ?? 0 }))
  .filter((c) => c.per > 0);

const before = target.stockRemain;
console.log(`Target ${code} "${target.productName}" stock before: ${before}`);
console.log(`Competing products: ${consumers.map((c) => `${c.p.productCode} (x${c.per})`).join(", ")}`);
console.log(`Firing ${requests} concurrent orders...`);

const results = await Promise.all(
  Array.from({ length: requests }, async (_, i) => {
    const { p, per } = consumers[i % consumers.length];
    const res = await fetch(`${base}/api/orders`, {
      method: "POST",
      headers,
      body: JSON.stringify({ paymentMethod: "Cash", items: [{ productId: p.id, quantity: 1 }] }),
    });
    return { status: res.status, per };
  }),
);

const created = results.filter((r) => r.status === 201);
const consumed = created.reduce((sum, r) => sum + r.per, 0);
const statusCounts = Object.groupBy(results, (r) => r.status);
const after = (await getProducts()).find((p) => p.id === target.id).stockRemain;

console.log(`Status codes: ${Object.entries(statusCounts).map(([s, list]) => `${s} x${list.length}`).join(", ")}`);
console.log(`Stock consumed by successful orders: ${consumed}`);
console.log(`Stock after: ${after}  (expected ${before - consumed})`);

const ok = after === before - consumed && after >= 0 && consumed <= before;
console.log(ok ? "PASS - no overselling, stock is consistent" : "FAIL - stock is inconsistent");
process.exit(ok ? 0 : 1);
