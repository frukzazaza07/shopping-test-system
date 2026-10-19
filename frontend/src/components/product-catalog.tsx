"use client";

import { useState } from "react";
import { ProductCard } from "@/components/product-card";
import { Button } from "@/components/ui/button";
import { useCartSync } from "@/hooks/use-cart-sync";
import { maxQuantity } from "@/lib/stock";
import type { Category, Product } from "@/lib/types";
import { useCartStore } from "@/store/cart-store";

export function ProductCatalog({ categories, products }: { categories: Category[]; products: Product[] }) {
  const { productMap, stock, items, hydrated } = useCartSync(products);
  const setQuantity = useCartStore((s) => s.setQuantity);
  const [activeCategory, setActiveCategory] = useState<number | "all">("all");

  const visibleCategories = categories.filter((c) => activeCategory === "all" || c.id === activeCategory);

  return (
    <div className="flex flex-col gap-8">
      <nav className="flex flex-wrap gap-2" aria-label="Categories">
        <Button variant={activeCategory === "all" ? "default" : "outline"} onClick={() => setActiveCategory("all")}>
          All
        </Button>
        {categories.map((c) => (
          <Button
            key={c.id}
            variant={activeCategory === c.id ? "default" : "outline"}
            onClick={() => setActiveCategory(c.id)}
          >
            {c.categoryName}
          </Button>
        ))}
      </nav>

      {visibleCategories.map((category) => {
        const categoryProducts = products.filter((p) => p.categoryId === category.id);
        if (categoryProducts.length === 0) return null;

        return (
          <section key={category.id} aria-labelledby={`cat-${category.id}`} className="flex flex-col gap-3">
            <h2 id={`cat-${category.id}`} className="text-lg font-semibold">
              {category.categoryName}
            </h2>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
              {categoryProducts.map((product) => {
                const max = maxQuantity(product, items, productMap, stock);
                return (
                  <ProductCard
                    key={product.id}
                    product={product}
                    productMap={productMap}
                    quantity={items.find((i) => i.productId === product.id)?.quantity ?? 0}
                    max={max}
                    hydrated={hydrated}
                    onChange={(q) => setQuantity(product.id, q, max)}
                  />
                );
              })}
            </div>
          </section>
        );
      })}
    </div>
  );
}
