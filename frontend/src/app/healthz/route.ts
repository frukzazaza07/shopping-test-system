/**
 * Liveness/readiness probe for the Next.js server itself. Deliberately does NOT call the API:
 * if the API is down we still want these pods alive (they render the error page) instead of
 * k8s restarting the whole frontend tier.
 */
export function GET() {
  return Response.json({ status: "ok" });
}
