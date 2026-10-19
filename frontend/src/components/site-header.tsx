import Link from "next/link";
import { StoreIcon } from "lucide-react";
import { CartButton } from "@/components/cart-button";

export function SiteHeader() {
  return (
    <header className="sticky top-0 z-40 border-b bg-background/90 backdrop-blur">
      <div className="mx-auto flex h-14 max-w-6xl items-center justify-between px-4">
        <Link href="/" className="flex items-center gap-2 font-semibold">
          <StoreIcon className="size-5" />
          Stock &amp; Cart
        </Link>
        <CartButton />
      </div>
    </header>
  );
}
