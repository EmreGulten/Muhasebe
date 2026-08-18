import type { SaleStatusDto } from "@/lib/types";

export const SALE_STATUS_LABELS: Record<SaleStatusDto, string> = {
  Draft: "Taslak",
  Confirmed: "Onaylandı",
  PartiallyPaid: "Kısmi Tahsil",
  Paid: "Tahsil Edildi",
  Cancelled: "İptal",
};

export const SALE_STATUSES: ReadonlyArray<{ value: SaleStatusDto; label: string }> = (
  Object.keys(SALE_STATUS_LABELS) as SaleStatusDto[]
).map((value) => ({ value, label: SALE_STATUS_LABELS[value] }));

/** Durum rozeti: taslak gri, onay/tahsil yeşilimsi (default), iptal kırmızı. */
export function saleStatusVariant(status: SaleStatusDto): "default" | "secondary" | "destructive" {
  switch (status) {
    case "Draft":
      return "secondary";
    case "Cancelled":
      return "destructive";
    default:
      return "default";
  }
}
