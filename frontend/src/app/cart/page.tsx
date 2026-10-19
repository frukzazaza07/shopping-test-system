import type { Metadata } from "next";
import { Suspense } from "react";
import { CartView } from "@/components/cart-view";
import { Skeleton } from "@/components/ui/skeleton";
import { getProducts } from "@/lib/api";

export const metadata: Metadata = { title: "Cart · Stock & Cart" };

/** Fresh stock is fetched on the server so the cart can be clamped before checkout. */
async function CartWithStock() {
  const products = await getProducts();
  return <CartView products={products} />;
}

export default function CartPage() {
  return (
    <main className="mx-auto flex w-full max-w-6xl flex-col gap-6 px-4 py-8">
      <h1 className="text-2xl font-bold">Your cart</h1>
      <Suspense fallback={<Skeleton className="h-64 w-full" />}>
        <CartWithStock />
      </Suspense>
    </main>
  );
}
