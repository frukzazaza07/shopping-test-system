import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { Suspense } from "react";
import { CircleCheckIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { getOrder } from "@/lib/api";
import { formatDateTime, formatPrice } from "@/lib/format";
import { PAYMENT_METHODS } from "@/lib/types";

export const metadata: Metadata = { title: "Order summary · Stock & Cart" };

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

async function OrderSummary({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!UUID.test(id)) notFound();

  const order = await getOrder(id);
  if (!order) notFound();

  const paymentLabel = PAYMENT_METHODS.find((m) => m.value === order.paymentMethod)?.label ?? order.paymentMethod;

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2 text-green-600 dark:text-green-500">
          <CircleCheckIcon className="size-5" />
          <span className="text-sm font-medium">Order confirmed</span>
        </div>
        <CardTitle className="text-xl">Order {order.orderNo}</CardTitle>
        <CardDescription>{formatDateTime(order.createdAt)}</CardDescription>
      </CardHeader>

      <CardContent>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="border-b text-left text-muted-foreground">
              <tr>
                <th className="py-2 font-medium">Product</th>
                <th className="py-2 text-right font-medium">Unit price</th>
                <th className="py-2 text-right font-medium">Qty</th>
                <th className="py-2 text-right font-medium">Amount</th>
              </tr>
            </thead>
            <tbody>
              {order.details.map((d) => (
                <tr key={d.productId} className="border-b last:border-0">
                  <td className="py-2">
                    <div className="font-medium">{d.productName}</div>
                    <div className="font-mono text-xs text-muted-foreground">{d.productCode}</div>
                  </td>
                  <td className="py-2 text-right tabular-nums">{formatPrice(d.unitPrice)}</td>
                  <td className="py-2 text-right tabular-nums">{d.quantity}</td>
                  <td className="py-2 text-right tabular-nums">{formatPrice(d.lineTotal)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="border-t">
                <td className="pt-3 text-muted-foreground">Payment</td>
                <td colSpan={3} className="pt-3 text-right">
                  <Badge variant="secondary">{paymentLabel}</Badge>
                </td>
              </tr>
              <tr>
                <td className="pt-2 font-semibold">Total ({order.totalQuantity} items)</td>
                <td colSpan={3} className="pt-2 text-right text-lg font-semibold tabular-nums">
                  {formatPrice(order.totalAmount)}
                </td>
              </tr>
            </tfoot>
          </table>
        </div>
      </CardContent>

      <CardFooter>
        <Link href="/" className={buttonVariants({ variant: "outline" })}>
          Continue shopping
        </Link>
      </CardFooter>
    </Card>
  );
}

export default function OrderPage({ params }: PageProps<"/orders/[id]">) {
  return (
    <main className="mx-auto flex w-full max-w-3xl flex-col gap-6 px-4 py-8">
      <h1 className="text-2xl font-bold">Order summary</h1>
      <Suspense fallback={<Skeleton className="h-96 w-full" />}>
        <OrderSummary params={params} />
      </Suspense>
    </main>
  );
}
