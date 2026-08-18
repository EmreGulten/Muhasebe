import type { ManualTransactionTypeDto, PartyTypeDto } from "@/lib/types";

/** Para biçimi: Türk lirası, 2 basamak. */
const moneyFormat = new Intl.NumberFormat("tr-TR", {
  style: "currency",
  currency: "TRY",
  minimumFractionDigits: 2,
});

const dateFormat = new Intl.DateTimeFormat("tr-TR", { dateStyle: "medium" });

export function formatMoney(value: number): string {
  return moneyFormat.format(value);
}

export function formatDate(iso: string | null | undefined): string {
  if (!iso) return "—";
  return dateFormat.format(new Date(iso));
}

/** "2026-08-18" → "2026-08-18T00:00:00.000Z" (Input type=date değerinden API'ye). */
export function dateInputToIso(value: string): string {
  return new Date(`${value}T00:00:00Z`).toISOString();
}

/** API'den Input type=date değerine: yerel saat dilimine göre olmadan, gün bazında. */
export function isoToDateInput(iso: string): string {
  return new Date(iso).toISOString().slice(0, 10);
}

/** Virgül ya da nokta ondalıklı metni sayıya çevirir; en fazla 2 basamak. */
export function parseMoneyInput(value: string): number | null {
  const normalized = value.trim().replace(/\s/g, "").replace(",", ".");
  if (!/^[+-]?\d+(\.\d{1,2})?$/.test(normalized)) return null;
  const parsed = Number(normalized);
  return Number.isFinite(parsed) ? parsed : null;
}

export function toMoneyInput(value: number): string {
  // Girdi alanı için sabit nokta, 2 basamak (örn. "1250.50").
  return value === 0 ? "" : value.toFixed(2);
}

export const PARTY_TYPE_LABELS: Record<PartyTypeDto, string> = {
  Customer: "Müşteri",
  Supplier: "Tedarikçi",
  Both: "Müşteri + Tedarikçi",
};

export const TRANSACTION_TYPE_LABELS: Record<string, string> = {
  OpeningBalance: "Açılış Bakiyesi",
  Debit: "Borçlandırma",
  Credit: "Alacaklandırma",
  Adjustment: "Düzeltme",
  Sale: "Satış",
  Collection: "Tahsilat",
  Purchase: "Alış",
  Payment: "Ödeme",
};

/**
 * Manuel hareket formundaki türler ve tutar işareti davranışı.
 * Credit pozitif girilen tutarı alacak (negatif) olarak gönderir;
 * OpeningBalance ve Adjustment işaretli girişe izin verir.
 */
export const MANUAL_TRANSACTION_TYPES: ReadonlyArray<{
  value: ManualTransactionTypeDto;
  label: string;
  signed: boolean;
}> = [
  { value: "Debit", label: "Borçlandırma (+)", signed: false },
  { value: "Credit", label: "Alacaklandırma (−)", signed: false },
  { value: "Adjustment", label: "Düzeltme (işaretli)", signed: true },
  { value: "OpeningBalance", label: "Açılış Bakiyesi (işaretli)", signed: true },
];

/** Cari bakiyesi işaretli: pozitif = taraf bize borçlu, negatif = biz borçluyuz. */
export function balanceLabel(balance: number): string {
  if (balance === 0) return "Bakiye sıfır";
  return balance > 0
    ? `${formatMoney(balance)} — taraf size borçlu`
    : `${formatMoney(-balance)} — tarafa borçlusunuz`;
}
