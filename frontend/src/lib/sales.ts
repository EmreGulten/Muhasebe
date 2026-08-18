import type { SaleStatusDto } from "@/lib/types";

/** Backend SaleMath.Line ile aynı yuvarlama — canlı toplam tutarlılığı için. */
function round2(value: number): number {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

export interface SaleLineMath {
  gross: number;
  net: number;
  vat: number;
  total: number;
}

/**
 * Kalem hesabı: brüt = round2(miktar × fiyat); net = round2(brüt × (1 − iskonto));
 * KDV = round2(net × oran). Backend'in SaleMath.Line yansıması.
 */
export function computeSaleLine(
  quantity: number,
  unitPrice: number,
  discountRate: number,
  vatRate: number,
): SaleLineMath {
  const gross = round2(quantity * unitPrice);
  const net = round2(gross * (1 - discountRate / 100));
  const vat = round2((net * vatRate) / 100);
  return { gross, net, vat, total: round2(net + vat) };
}

export interface SaleTotals {
  subTotal: number;
  discountTotal: number;
  vatTotal: number;
  total: number;
}

/** Belge baş toplamları — kalemlerin canlı hesabından türetilir. */
export function computeSaleTotals(
  lines: ReadonlyArray<{ quantity: number; unitPrice: number; discountRate: number; vatRate: number }>,
): SaleTotals {
  let subTotal = 0;
  let discountTotal = 0;
  let vatTotal = 0;
  for (const line of lines) {
    const math = computeSaleLine(line.quantity, line.unitPrice, line.discountRate, line.vatRate);
    subTotal = round2(subTotal + math.net);
    discountTotal = round2(discountTotal + (math.gross - math.net));
    vatTotal = round2(vatTotal + math.vat);
  }
  return { subTotal, discountTotal, vatTotal, total: round2(subTotal + vatTotal) };
}

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
