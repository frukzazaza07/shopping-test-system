import Link from "next/link";
import { buttonVariants } from "@/components/ui/button";

export default function NotFound() {
  return (
    <main className="mx-auto flex w-full max-w-6xl flex-col items-start gap-4 px-4 py-16">
      <h1 className="text-2xl font-bold">Not found</h1>
      <p className="text-muted-foreground">The page or order you are looking for does not exist.</p>
      <Link href="/" className={buttonVariants()}>
        Back to products
      </Link>
    </main>
  );
}
