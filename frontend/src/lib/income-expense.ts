import type { IncomeExpenseSideDto } from "@/lib/types";

export const SIDE_LABELS: Record<IncomeExpenseSideDto, string> = {
  Income: "Gelir",
  Expense: "Gider",
};

export const SIDES: ReadonlyArray<{ value: IncomeExpenseSideDto; label: string }> = [
  { value: "Income", label: "Gelir" },
  { value: "Expense", label: "Gider" },
];

export const MONTH_NAMES = [
  "Ocak",
  "Şubat",
  "Mart",
  "Nisan",
  "Mayıs",
  "Haziran",
  "Temmuz",
  "Ağustos",
  "Eylül",
  "Ekim",
  "Kasım",
  "Aralık",
];

export function monthLabel(year: number, month: number): string {
  return `${MONTH_NAMES[month - 1] ?? month} ${year}`;
}
