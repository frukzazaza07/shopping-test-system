"use client";

import { useEffect } from "react";
import { CART_STORAGE_KEY, useCartStore } from "@/store/cart-store";

/** Loads the cart from localStorage after mount and keeps open tabs in sync. */
export function CartHydrator() {
  useEffect(() => {
    void useCartStore.persist.rehydrate();

    const onStorage = (e: StorageEvent) => {
      if (e.key === CART_STORAGE_KEY) void useCartStore.persist.rehydrate();
    };
    window.addEventListener("storage", onStorage);
    return () => window.removeEventListener("storage", onStorage);
  }, []);

  return null;
}
