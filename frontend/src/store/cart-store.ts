import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import type { CartItem } from "@/lib/types";

export const CART_STORAGE_KEY = "stock-cart";

interface CartState {
  items: CartItem[];
  /** False until the cart has been loaded from localStorage (avoids SSR hydration mismatch). */
  hydrated: boolean;
  /** Sets a line's quantity, clamped to [0, max]. 0 removes the line. */
  setQuantity: (productId: number, quantity: number, max: number) => void;
  remove: (productId: number) => void;
  replaceAll: (items: CartItem[]) => void;
  clear: () => void;
}

/** localStorage is user-controlled: keep only well-formed lines. */
function sanitize(value: unknown): CartItem[] {
  if (!Array.isArray(value)) return [];
  return value.filter(
    (i): i is CartItem =>
      Number.isInteger(i?.productId) && i.productId > 0 && Number.isInteger(i?.quantity) && i.quantity > 0,
  );
}

export const useCartStore = create<CartState>()(
  persist(
    (set) => ({
      items: [],
      hydrated: false,

      setQuantity: (productId, quantity, max) =>
        set((state) => {
          const q = Math.max(0, Math.min(Math.floor(quantity), max));
          if (q === 0) return { items: state.items.filter((i) => i.productId !== productId) };

          const exists = state.items.some((i) => i.productId === productId);
          return {
            items: exists
              ? state.items.map((i) => (i.productId === productId ? { ...i, quantity: q } : i))
              : [...state.items, { productId, quantity: q }],
          };
        }),

      remove: (productId) => set((state) => ({ items: state.items.filter((i) => i.productId !== productId) })),
      replaceAll: (items) => set({ items }),
      clear: () => set({ items: [] }),
    }),
    {
      name: CART_STORAGE_KEY,
      version: 1,
      storage: createJSONStorage(() => localStorage),
      partialize: (state) => ({ items: state.items }),
      merge: (persisted, current) => ({
        ...current,
        items: sanitize((persisted as { items?: unknown } | undefined)?.items),
      }),
      // Rehydrated manually in <CartHydrator /> after mount so server and first client render match.
      skipHydration: true,
      onRehydrateStorage: () => () => useCartStore.setState({ hydrated: true }),
    },
  ),
);

export const selectTotalQuantity = (state: CartState) =>
  state.items.reduce((sum, item) => sum + item.quantity, 0);
