import { Badge } from "@/components/ui/badge";

export function StockBadge({ available }: { available: number }) {
  if (available <= 0) return <Badge variant="destructive">Out of stock</Badge>;
  if (available <= 5) return <Badge variant="outline">Only {available} left</Badge>;
  return <Badge variant="secondary">{available} in stock</Badge>;
}
