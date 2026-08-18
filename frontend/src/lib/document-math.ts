/** Backend LineMath ile aynı yuvarlama — satış ve alış formlarında canlı toplam tutarlılığı için. */
function round2(value: number): number {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

export interface DocumentLineMath {
  gross: number;
  net: number;
  vat: number;
  total: number;
}

/**
 * Kalem hesabı: brüt = round2(miktar × fiyat); net = round2(brüt × (1 − iskonto));
 * KDV = round2(net × oran). Backend'in LineMath yansıması.
 */
export function computeDocumentLine(
  quantity: number,
  unitPrice: number,
  discountRate: number,
  vatRate: number,
): DocumentLineMath {
  const gross = round2(quantity * unitPrice);
  const net = round2(gross * (1 - discountRate / 100));
  const vat = round2((net * vatRate) / 100);
  return { gross, net, vat, total: round2(net + vat) };
}

export interface DocumentTotals {
  subTotal: number;
  discountTotal: number;
  vatTotal: number;
  total: number;
}

/** Belge baş toplamları — kalemlerin canlı hesabından türetilir. */
export function computeDocumentTotals(
  lines: ReadonlyArray<{ quantity: number; unitPrice: number; discountRate: number; vatRate: number }>,
): DocumentTotals {
  let subTotal = 0;
  let discountTotal = 0;
  let vatTotal = 0;
  for (const line of lines) {
    const math = computeDocumentLine(line.quantity, line.unitPrice, line.discountRate, line.vatRate);
    subTotal = round2(subTotal + math.net);
    discountTotal = round2(discountTotal + (math.gross - math.net));
    vatTotal = round2(vatTotal + math.vat);
  }
  return { subTotal, discountTotal, vatTotal, total: round2(subTotal + vatTotal) };
}
