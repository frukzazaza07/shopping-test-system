import "server-only";
import { connection } from "next/server";
import type { Category, CreateOrderInput, Order, Product } from "@/lib/types";

/**
 * Server-side API client. Runs only on the Next.js server, so the API key and the
 * internal backend URL (from env, never hardcoded) are never sent to the browser.
 */
function config() {
  const baseUrl = process.env.API_INTERNAL_URL;
  const apiKey = process.env.API_KEY;
  if (!baseUrl || !apiKey) throw new Error("API_INTERNAL_URL and API_KEY must be configured.");
  return { baseUrl, apiKey };
}

async function apiFetch(path: string, init?: RequestInit) {
  // Stock changes constantly: always fetch at request time, never during prerender.
  await connection();
  const { baseUrl, apiKey } = config();
  return fetch(`${baseUrl}${path}`, {
    ...init,
    headers: { "Content-Type": "application/json", "X-Api-Key": apiKey, ...init?.headers },
    signal: AbortSignal.timeout(10_000),
  });
}

async function getJson<T>(path: string): Promise<T> {
  const res = await apiFetch(path);
  if (!res.ok) throw new Error(`API ${path} failed with ${res.status}`);
  return res.json() as Promise<T>;
}

export const getCategories = () => getJson<Category[]>("/api/categories");

export const getProducts = () => getJson<Product[]>("/api/products");

export async function getOrder(id: string): Promise<Order | null> {
  const res = await apiFetch(`/api/orders/${encodeURIComponent(id)}`);
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`API /api/orders failed with ${res.status}`);
  return res.json();
}

export type CreateOrderResult =
  | { ok: true; order: Order }
  | { ok: false; status: number; message: string };

/** Same `idempotencyKey` => the API returns the original order instead of creating a duplicate. */
export async function createOrder(input: CreateOrderInput, idempotencyKey: string): Promise<CreateOrderResult> {
  const res = await apiFetch("/api/orders", {
    method: "POST",
    body: JSON.stringify(input),
    headers: { "Idempotency-Key": idempotencyKey },
  });
  if (res.ok) return { ok: true, order: await res.json() };

  const problem = (await res.json().catch(() => null)) as { detail?: string; title?: string } | null;
  return {
    ok: false,
    status: res.status,
    message: problem?.detail ?? problem?.title ?? "Could not place the order.",
  };
}
