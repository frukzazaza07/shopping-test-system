"use client";

import { useEffect, useMemo } from "react";
import { toast } from "sonner";
import { buildStockMap, reconcileCart } from "@/lib/stock";
import type { Product } from "@/lib/types";
import { useCartStore } from "@/store/cart-store";

/**
 * Joins the persisted cart with fresh server data. Whenever new stock arrives
 * (page load, router refresh after a failed order, ...) the cart is clamped to it.
 */
export function useCartSync(products: Product[]) {
  const productMap = useMemo(() => new Map(products.map((p) => [p.id, p])), [products]);
  const stock = useMemo(() => buildStockMap(products), [products]);
  const items = useCartStore((s) => s.items);
  const hydrated = useCartStore((s) => s.hydrated);

  useEffect(() => {
    if (!hydrated) return;
    const { items: current, replaceAll } = useCartStore.getState();
    const { items: next, changed } = reconcileCart(current, productMap, stock);
    if (changed) {
      replaceAll(next);
      toast.warning("Your cart was adjusted to match the stock available.");
    }
  }, [hydrated, productMap, stock]);

  return { productMap, stock, items, hydrated };
}
