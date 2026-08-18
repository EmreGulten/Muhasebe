import type { ManualInventoryTypeDto } from "@/lib/types";

/** Miktar biçimi: 4 basamağa kadar kesir (kg, mt gibi birimler için). */
const quantityFormat = new Intl.NumberFormat("tr-TR", {
  minimumFractionDigits: 0,
  maximumFractionDigits: 4,
});

export function formatQuantity(value: number): string {
  return quantityFormat.format(value);
}

/** Virgül ya da nokta ondalıklı miktar metnini sayıya çevirir; en fazla 4 basamak. */
export function parseQuantityInput(value: string): number | null {
  const normalized = value.trim().replace(/\s/g, "").replace(",", ".");
  if (!/^[+-]?\d+(\.\d{1,4})?$/.test(normalized)) return null;
  const parsed = Number(normalized);
  return Number.isFinite(parsed) ? parsed : null;
}

export function toQuantityInput(value: number): string {
  return value === 0 ? "" : String(value);
}

export const INVENTORY_TYPE_LABELS: Record<string, string> = {
  Purchase: "Alış",
  Sale: "Satış",
  Count: "Sayım",
  ManualIn: "Manuel Giriş",
  ManualOut: "Manuel Çıkış",
  Return: "İade",
  Transfer: "Transfer",
};

/**
 * Manuel stok hareketi türleri. ManualOut pozitif girilen miktarı çıkış
 * (negatif) olarak gönderir; Count mutlak sayım sonucunu gönderir, sunucu
 * fark hareketini yazar.
 */
export const MANUAL_INVENTORY_TYPES: ReadonlyArray<{
  value: ManualInventoryTypeDto | "Transfer";
  label: string;
  hint: string;
}> = [
  { value: "ManualIn", label: "Manuel Giriş (+)", hint: "Stoğa ekleme (hediye, fire iadesi vb.). Pozitif girin." },
  { value: "ManualOut", label: "Manuel Çıkış (−)", hint: "Stoktan düşüm (fire, zayi). Pozitif girin, sistem negatif yazar." },
  { value: "Count", label: "Sayım", hint: "Sayılan güncel miktarı girin; fark hareketi otomatik oluşur." },
  { value: "Return", label: "İade (+)", hint: "Müşteri iadesi stoğa ekler. Pozitif girin." },
  { value: "Transfer", label: "Depo Transferi", hint: "Kaynaktan hedef depoya taşınır; iki hareket oluşur." },
];

export function stockClass(stock: number, isCritical: boolean): string {
  if (isCritical) return "text-destructive font-medium";
  if (stock < 0) return "text-destructive";
  return "tabular-nums";
}
