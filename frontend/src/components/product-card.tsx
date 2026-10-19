"use client";

import { ShoppingCartIcon } from "lucide-react";
import { QuantityStepper } from "@/components/quantity-stepper";
import { StockBadge } from "@/components/stock-badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { formatPrice } from "@/lib/format";
import type { ProductMap } from "@/lib/stock";
import type { Product } from "@/lib/types";

interface ProductCardProps {
  product: Product;
  productMap: ProductMap;
  quantity: number;
  /** Max this product may have in the cart given the rest of the cart. */
  max: number;
  hydrated: boolean;
  onChange: (quantity: number) => void;
}

export function ProductCard({ product, productMap, quantity, max, hydrated, onChange }: ProductCardProps) {
  const isBundle = product.stockRemain === null;
  const atLimit = quantity > 0 && quantity >= max;

  return (
    <Card size="sm" className="h-full">
      <CardHeader>
        <CardDescription className="font-mono text-xs">{product.productCode}</CardDescription>
        <CardTitle className="leading-snug">{product.productName}</CardTitle>
      </CardHeader>

      <CardContent className="flex flex-1 flex-col gap-2">
        {isBundle && (
          <ul className="text-xs text-muted-foreground">
            {product.deductions.map((d) => (
              <li key={d.stockProductId}>
                {d.quantity} × {productMap.get(d.stockProductId)?.productName ?? `#${d.stockProductId}`}
              </li>
            ))}
          </ul>
        )}
        <div className="mt-auto flex items-center justify-between gap-2">
          <span className="text-base font-semibold tabular-nums">{formatPrice(product.productPriceUnit)}</span>
          <StockBadge available={product.available} />
        </div>
      </CardContent>

      <CardFooter className="flex min-h-12 items-center justify-between gap-2">
        {!hydrated ? (
          <div className="h-7" />
        ) : quantity > 0 ? (
          <>
            <QuantityStepper value={quantity} max={max} onChange={onChange} label={product.productName} />
            {atLimit && <span className="text-xs text-muted-foreground">Max reached</span>}
          </>
        ) : (
          <Button className="w-full" onClick={() => onChange(1)} disabled={max <= 0}>
            <ShoppingCartIcon />
            {product.available <= 0 ? "Out of stock" : max <= 0 ? "Stock used by cart" : "Add to cart"}
          </Button>
        )}
      </CardFooter>
    </Card>
  );
}
