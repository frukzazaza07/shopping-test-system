"use client";

import { Button } from "@/components/ui/button";

export default function Error({ reset }: { error: Error; reset: () => void }) {
  return (
    <main className="mx-auto flex w-full max-w-6xl flex-col items-start gap-4 px-4 py-16">
      <h1 className="text-2xl font-bold">Something went wrong</h1>
      <p className="text-muted-foreground">We could not reach the store service. Please try again.</p>
      <Button onClick={reset}>Try again</Button>
    </main>
  );
}
