"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useLayoutEffect, useRef, useState, useTransition } from "react";
import { BanknoteIcon, CreditCardIcon, Loader2Icon, QrCodeIcon, ShoppingBagIcon, Trash2Icon } from "lucide-react";
import { toast } from "sonner";
import { placeOrder } from "@/app/actions";
import { QuantityStepper } from "@/components/quantity-stepper";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { useCartSync } from "@/hooks/use-cart-sync";
import { formatPrice } from "@/lib/format";
import { newIdempotencyKey } from "@/lib/id";
import { maxQuantity } from "@/lib/stock";
import { PAYMENT_METHODS, type PaymentMethod, type Product } from "@/lib/types";
import { cn } from "@/lib/utils";
import { useCartStore } from "@/store/cart-store";

const PAYMENT_ICONS = { Cash: BanknoteIcon, CreditCard: CreditCardIcon, PromptPay: QrCodeIcon } as const;

export function CartView({ products }: { products: Product[] }) {
  const { productMap, stock, items, hydrated } = useCartSync(products);
  const setQuantity = useCartStore((s) => s.setQuantity);
  const remove = useCartStore((s) => s.remove);
  const clear = useCartStore((s) => s.clear);
  const router = useRouter();

  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>("Cash");
  const [placing, startTransition] = useTransition();
  const [placedOrderId, setPlacedOrderId] = useState<string | null>(null);

  // Next 16 (Cache Components) doesn't unmount this page on navigation - it hides it with
  // React <Activity> and restores its state later. The "order placed" state is one-shot, so
  // reset it when the page is hidden; otherwise coming back to /cart for the next purchase
  // would show the stale "opening your summary..." message forever.
  useLayoutEffect(() => () => setPlacedOrderId(null), []);

  // One idempotency key per checkout intent: retries of the same cart reuse it,
  // any change to the cart or payment method starts a fresh one.
  const idempotencyKey = useRef<string | null>(null);
  useEffect(() => {
    idempotencyKey.current = null;
  }, [items, paymentMethod]);

  if (!hydrated) return <Skeleton className="h-64 w-full" />;

  if (placedOrderId) {
    return (
      <p className="flex items-center gap-2 text-muted-foreground">
        <Loader2Icon className="size-4 animate-spin" /> Order placed - opening your summary...
      </p>
    );
  }

  const lines = items.flatMap((item) => {
    const product = productMap.get(item.productId);
    return product ? [{ item, product }] : [];
  });

  if (lines.length === 0) {
    return (
      <Card className="items-center py-12 text-center">
        <ShoppingBagIcon className="size-10 text-muted-foreground" />
        <p className="text-muted-foreground">Your cart is empty.</p>
        <Link href="/" className={buttonVariants()}>
          Browse products
        </Link>
      </Card>
    );
  }

  const totalQuantity = lines.reduce((sum, l) => sum + l.item.quantity, 0);
  const totalAmount = lines.reduce((sum, l) => sum + l.product.productPriceUnit * l.item.quantity, 0);

  const submit = () =>
    startTransition(async () => {
      idempotencyKey.current ??= newIdempotencyKey();
      const result = await placeOrder({ paymentMethod, items }, idempotencyKey.current);
      if (result.ok) {
        idempotencyKey.current = null; // the next purchase is a new intent
        setPlacedOrderId(result.orderId);
        clear();
        toast.success("Order placed successfully.");
        router.push(`/orders/${result.orderId}`);
      } else {
        toast.error(result.message);
      }
    });

  return (
    <div className="grid gap-6 lg:grid-cols-3">
      <Card className="lg:col-span-2">
        <CardHeader>
          <CardTitle>Items ({totalQuantity})</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col">
          {lines.map(({ item, product }, index) => {
            const max = maxQuantity(product, items, productMap, stock);
            return (
              <div key={product.id}>
                {index > 0 && <Separator className="my-3" />}
                <div className="flex flex-wrap items-center gap-3">
                  <div className="min-w-0 flex-1">
                    <p className="font-medium">{product.productName}</p>
                    <p className="text-xs text-muted-foreground">
                      {product.productCode} · {formatPrice(product.productPriceUnit)} each · max {max}
                    </p>
                  </div>
                  <QuantityStepper
                    value={item.quantity}
                    max={max}
                    onChange={(q) => setQuantity(product.id, q, max)}
                    label={product.productName}
                  />
                  <span className="w-24 text-right font-medium tabular-nums">
                    {formatPrice(product.productPriceUnit * item.quantity)}
                  </span>
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    onClick={() => remove(product.id)}
                    aria-label={`Remove ${product.productName}`}
                  >
                    <Trash2Icon />
                  </Button>
                </div>
              </div>
            );
          })}
        </CardContent>
      </Card>

      <Card className="h-fit lg:sticky lg:top-20">
        <CardHeader>
          <CardTitle>Order summary</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <fieldset className="flex flex-col gap-2">
            <legend className="mb-2 text-sm font-medium">Payment method</legend>
            {PAYMENT_METHODS.map((m) => {
              const Icon = PAYMENT_ICONS[m.value];
              const selected = paymentMethod === m.value;
              return (
                <button
                  key={m.value}
                  type="button"
                  aria-pressed={selected}
                  onClick={() => setPaymentMethod(m.value)}
                  className={cn(
                    "flex items-center gap-2 rounded-lg border px-3 py-2 text-left text-sm transition-colors",
                    selected ? "border-primary bg-primary/5 font-medium" : "hover:bg-muted",
                  )}
                >
                  <Icon className="size-4" />
                  {m.label}
                </button>
              );
            })}
          </fieldset>

          <Separator />

          <dl className="flex flex-col gap-1 text-sm">
            <div className="flex justify-between">
              <dt className="text-muted-foreground">Items</dt>
              <dd className="tabular-nums">{totalQuantity}</dd>
            </div>
            <div className="flex justify-between text-base font-semibold">
              <dt>Total</dt>
              <dd className="tabular-nums">{formatPrice(totalAmount)}</dd>
            </div>
          </dl>
        </CardContent>
        <CardFooter>
          <Button size="lg" className="w-full" onClick={submit} disabled={placing}>
            {placing && <Loader2Icon className="animate-spin" />}
            {placing ? "Placing order..." : "Place order"}
          </Button>
        </CardFooter>
      </Card>
    </div>
  );
}
