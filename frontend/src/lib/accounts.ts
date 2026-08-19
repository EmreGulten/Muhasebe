import type { AccountTypeDto } from "@/lib/types";

export const ACCOUNT_TYPE_LABELS: Record<AccountTypeDto, string> = {
  Cash: "Kasa",
  Bank: "Banka",
  CreditCard: "Kredi Kartı",
  VirtualPOS: "Sanal POS",
};

export const ACCOUNT_TYPES: ReadonlyArray<{ value: AccountTypeDto; label: string }> = (
  Object.keys(ACCOUNT_TYPE_LABELS) as AccountTypeDto[]
).map((value) => ({ value, label: ACCOUNT_TYPE_LABELS[value] }));

/** Hesap hareketi türleri — backend AccountTransactionType değerleri. */
export const TRANSACTION_TYPE_LABELS: Record<string, string> = {
  SaleCollection: "Satış Tahsilatı",
  PurchasePayment: "Alış Ödemesi",
  Income: "Gelir",
  Expense: "Gider",
  Transfer: "Transfer",
  OpeningBalance: "Açılış Bakiyesi",
  Refund: "İade",
  ManualCollection: "Manuel Giriş",
  ManualPayment: "Manuel Çıkış",
};

export function transactionTypeLabel(type: string): string {
  return TRANSACTION_TYPE_LABELS[type] ?? type;
}
