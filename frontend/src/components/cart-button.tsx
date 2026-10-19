"use client";

import Link from "next/link";
import { ShoppingCartIcon } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";
import { selectTotalQuantity, useCartStore } from "@/store/cart-store";

export function CartButton() {
  const count = useCartStore(selectTotalQuantity);
  const hydrated = useCartStore((s) => s.hydrated);

  return (
    <Link href="/cart" className={buttonVariants({ variant: "outline" })} aria-label={`Cart (${count} items)`}>
      <ShoppingCartIcon />
      Cart
      {hydrated && count > 0 && (
        <span className="ml-1 rounded-full bg-primary px-1.5 text-xs leading-5 text-primary-foreground tabular-nums">
          {count}
        </span>
      )}
    </Link>
  );
}
