"use server";

import { refresh } from "next/cache";
import { createOrder } from "@/lib/api";
import { PAYMENT_METHODS, type CreateOrderInput } from "@/lib/types";

export type PlaceOrderResult = { ok: true; orderId: string } | { ok: false; message: string };

/** Server Actions are public endpoints: re-validate the shape of everything the browser sends. */
function parseInput(input: unknown): CreateOrderInput | null {
  if (typeof input !== "object" || input === null) return null;
  const { paymentMethod, items } = input as Record<string, unknown>;

  if (!PAYMENT_METHODS.some((m) => m.value === paymentMethod)) return null;
  if (!Array.isArray(items) || items.length === 0 || items.length > 100) return null;

  const valid = items.every(
    (i) =>
      Number.isInteger(i?.productId) &&
      i.productId > 0 &&
      Number.isInteger(i?.quantity) &&
      i.quantity > 0 &&
      i.quantity <= 1000,
  );
  if (!valid) return null;

  return {
    paymentMethod: paymentMethod as CreateOrderInput["paymentMethod"],
    items: items.map((i) => ({ productId: i.productId, quantity: i.quantity })),
  };
}

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * @param idempotencyKey generated once per checkout attempt in the browser, so a double click
 * or a retry after a timeout returns the same order instead of creating (and charging) a second one.
 */
export async function placeOrder(input: unknown, idempotencyKey: unknown): Promise<PlaceOrderResult> {
  const order = parseInput(input);
  if (!order || typeof idempotencyKey !== "string" || !UUID.test(idempotencyKey)) {
    return { ok: false, message: "Invalid order." };
  }

  try {
    const result = await createOrder(order, idempotencyKey);
    if (result.ok) return { ok: true, orderId: result.order.id };

    // Someone bought the stock first: re-render the page with fresh stock so the cart re-clamps.
    if (result.status === 409) refresh();
    return { ok: false, message: result.message };
  } catch (error) {
    console.error("placeOrder failed", error);
    return { ok: false, message: "Service unavailable, please try again." };
  }
}
