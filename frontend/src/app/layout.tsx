import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import { CartHydrator } from "@/components/cart-hydrator";
import { SiteHeader } from "@/components/site-header";
import { Toaster } from "@/components/ui/sonner";
import "./globals.css";

const geistSans = Geist({ variable: "--font-sans", subsets: ["latin"] });
const geistMono = Geist_Mono({ variable: "--font-geist-mono", subsets: ["latin"] });

export const metadata: Metadata = {
  title: "Stock & Cart",
  description: "Stock and cart system - interview test",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en" className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}>
      <body className="flex min-h-full flex-col bg-muted/30">
        <SiteHeader />
        {children}
        <CartHydrator />
        <Toaster richColors position="top-center" />
      </body>
    </html>
  );
}
