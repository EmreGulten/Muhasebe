import type { PurchaseStatusDto } from "@/lib/types";

export const PURCHASE_STATUS_LABELS: Record<PurchaseStatusDto, string> = {
  Draft: "Taslak",
  Confirmed: "Onaylandı",
  PartiallyPaid: "Kısmi Ödeme",
  Paid: "Ödendi",
  Cancelled: "İptal",
};

export const PURCHASE_STATUSES: ReadonlyArray<{ value: PurchaseStatusDto; label: string }> = (
  Object.keys(PURCHASE_STATUS_LABELS) as PurchaseStatusDto[]
).map((value) => ({ value, label: PURCHASE_STATUS_LABELS[value] }));

/** Durum rozeti: taslak gri, onay/ödeme default, iptal kırmızı. */
export function purchaseStatusVariant(status: PurchaseStatusDto): "default" | "secondary" | "destructive" {
  switch (status) {
    case "Draft":
      return "secondary";
    case "Cancelled":
      return "destructive";
    default:
      return "default";
  }
}
