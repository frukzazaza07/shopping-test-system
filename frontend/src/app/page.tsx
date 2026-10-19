import { Suspense } from "react";
import { ProductCatalog } from "@/components/product-catalog";
import { Skeleton } from "@/components/ui/skeleton";
import { getCategories, getProducts } from "@/lib/api";

/** Data is fetched on the server (Next.js -> backend over the internal Docker network). */
async function Catalog() {
  const [categories, products] = await Promise.all([getCategories(), getProducts()]);
  return <ProductCatalog categories={categories} products={products} />;
}

function CatalogSkeleton() {
  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
      {Array.from({ length: 8 }, (_, i) => (
        <Skeleton key={i} className="h-52" />
      ))}
    </div>
  );
}

export default function HomePage() {
  return (
    <main className="mx-auto flex w-full max-w-6xl flex-col gap-6 px-4 py-8">
      <div>
        <h1 className="text-2xl font-bold">Products</h1>
        <p className="text-sm text-muted-foreground">Quantities are limited to the stock available.</p>
      </div>
      <Suspense fallback={<CatalogSkeleton />}>
        <Catalog />
      </Suspense>
    </main>
  );
}
