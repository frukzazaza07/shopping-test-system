export type PaymentMethod = "Cash" | "CreditCard" | "PromptPay";

export const PAYMENT_METHODS: { value: PaymentMethod; label: string }[] = [
  { value: "Cash", label: "Cash" },
  { value: "CreditCard", label: "Credit Card" },
  { value: "PromptPay", label: "PromptPay QR" },
];

export interface Category {
  id: number;
  categoryName: string;
  sortOrder: number;
}

/** Selling 1 unit deducts `quantity` units from the stock of `stockProductId`. */
export interface Deduction {
  stockProductId: number;
  quantity: number;
}

export interface Product {
  id: number;
  categoryId: number;
  productCode: string;
  productName: string;
  productPriceUnit: number;
  /** Physical stock of this product, null for bundle-only products (combos). */
  stockRemain: number | null;
  /** Units sellable right now (computed by the API from the deduction rules). */
  available: number;
  deductions: Deduction[];
}

export interface OrderDetail {
  productId: number;
  productCode: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface Order {
  id: string;
  orderNo: string;
  totalAmount: number;
  totalQuantity: number;
  paymentMethod: PaymentMethod;
  createdAt: string;
  details: OrderDetail[];
}

export interface CartItem {
  productId: number;
  quantity: number;
}

export interface CreateOrderInput {
  paymentMethod: PaymentMethod;
  items: CartItem[];
}
