// Backend Accounting.Contracts ile hizalı istemci tipleri.

export interface UserDto {
  id: string;
  email: string;
  fullName: string;
}

export interface TenantMembershipDto {
  tenantId: string;
  name: string;
  role: string;
  joinedAtUtc: string;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  user: UserDto;
  tenants: TenantMembershipDto[];
}

export interface MeResponse {
  user: UserDto;
  tenants: TenantMembershipDto[];
}

export interface MessageResponse {
  message: string;
}

export interface TenantResponse {
  id: string;
  name: string;
  role: string;
  joinedAtUtc: string;
}

// ---- Cari hesaplar (PHASE 2)

export type PartyTypeDto = "Customer" | "Supplier" | "Both";
export type ManualTransactionTypeDto = "OpeningBalance" | "Debit" | "Credit" | "Adjustment";

export interface PartySummaryDto {
  id: string;
  type: PartyTypeDto;
  name: string;
  phone: string | null;
  email: string | null;
  city: string | null;
  balance: number;
  isActive: boolean;
  lastTransactionDateUtc: string | null;
}

export interface PartyResponse {
  id: string;
  type: PartyTypeDto;
  name: string;
  taxNumber: string | null;
  taxOffice: string | null;
  phone: string | null;
  email: string | null;
  address: string | null;
  city: string | null;
  district: string | null;
  contactName: string | null;
  openingBalance: number;
  creditLimit: number;
  notes: string | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  balance: number;
  totalDebit: number;
  totalCredit: number;
  lastTransactionDateUtc: string | null;
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface PartyTransactionDto {
  id: string;
  type: string;
  date: string;
  dueDate: string | null;
  debit: number;
  credit: number;
  description: string | null;
  referenceType: string | null;
  referenceId: string | null;
  balance: number;
  createdAtUtc: string;
}

export interface PartyStatementResponse {
  partyId: string;
  partyName: string;
  balanceBeforePage: number;
  page: number;
  pageSize: number;
  totalCount: number;
  items: PartyTransactionDto[];
}
