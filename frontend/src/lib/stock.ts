import type { CartItem, Product } from "@/lib/types";

export type StockMap = Map<number, number>;
export type ProductMap = Map<number, Product>;

/** productId -> physical stock remaining. */
export function buildStockMap(products: Product[]): StockMap {
  return new Map(products.filter((p) => p.stockRemain !== null).map((p) => [p.id, p.stockRemain!]));
}

function addUsage(used: StockMap, product: Product | undefined, quantity: number) {
  if (!product) return;
  for (const d of product.deductions) {
    used.set(d.stockProductId, (used.get(d.stockProductId) ?? 0) + d.quantity * quantity);
  }
}

/** How many units of `product` fit into what is left after `used` has been taken. */
function capacity(product: Product, stock: StockMap, used: StockMap): number {
  if (product.deductions.length === 0) return 0;
  const perComponent = product.deductions.map((d) =>
    Math.floor(((stock.get(d.stockProductId) ?? 0) - (used.get(d.stockProductId) ?? 0)) / d.quantity),
  );
  return Math.max(0, Math.min(...perComponent));
}

/**
 * Max quantity of `product` the cart may hold, taking into account stock already
 * consumed by the OTHER cart lines - e.g. "Cola" and "Burger Combo" share Cola stock.
 */
export function maxQuantity(product: Product, items: CartItem[], products: ProductMap, stock: StockMap): number {
  const used: StockMap = new Map();
  for (const item of items) {
    if (item.productId !== product.id) addUsage(used, products.get(item.productId), item.quantity);
  }
  return capacity(product, stock, used);
}

/**
 * Clamp a (possibly stale) cart to the latest stock. Lines are satisfied in cart order;
 * unknown or sold-out products are removed.
 */
export function reconcileCart(items: CartItem[], products: ProductMap, stock: StockMap) {
  const used: StockMap = new Map();
  const result: CartItem[] = [];
  let changed = false;

  for (const item of items) {
    const product = products.get(item.productId);
    if (!product) {
      changed = true;
      continue;
    }
    const quantity = Math.min(item.quantity, capacity(product, stock, used));
    if (quantity !== item.quantity) changed = true;
    if (quantity > 0) {
      result.push({ productId: item.productId, quantity });
      addUsage(used, product, quantity);
    }
  }

  return { items: result, changed };
}
