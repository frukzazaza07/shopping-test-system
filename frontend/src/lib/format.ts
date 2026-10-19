const currency = new Intl.NumberFormat("th-TH", { style: "currency", currency: "THB" });

export const formatPrice = (value: number) => currency.format(value);

export const formatDateTime = (iso: string) =>
  new Date(iso).toLocaleString("en-GB", {
    timeZone: "Asia/Bangkok",
    dateStyle: "medium",
    timeStyle: "short",
  });
