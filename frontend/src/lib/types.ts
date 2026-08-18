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

// ---- Ürün / Stok (PHASE 3)

export interface CategoryDto {
  id: string;
  name: string;
  productCount: number;
}

export interface UnitDto {
  id: string;
  name: string;
  code: string | null;
  productCount: number;
}

export interface WarehouseDto {
  id: string;
  name: string;
  address: string | null;
  isDefault: boolean;
  isActive: boolean;
}

export interface ProductSummaryDto {
  id: string;
  name: string;
  sku: string | null;
  barcode: string | null;
  categoryName: string | null;
  unitName: string | null;
  salePrice: number;
  currentStock: number;
  isCritical: boolean;
  isService: boolean;
  isActive: boolean;
}

export interface ProductResponse {
  id: string;
  name: string;
  sku: string | null;
  barcode: string | null;
  description: string | null;
  categoryId: string | null;
  categoryName: string | null;
  unitId: string | null;
  unitName: string | null;
  purchasePrice: number;
  salePrice: number;
  vatRate: number;
  minimumStock: number;
  isService: boolean;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  currentStock: number;
  isCritical: boolean;
}

export interface WarehouseStockDto {
  warehouseId: string;
  warehouseName: string;
  stock: number;
}

export interface ProductStockResponse {
  productId: string;
  productName: string;
  totalStock: number;
  warehouses: WarehouseStockDto[];
}

export type ManualInventoryTypeDto = "Count" | "ManualIn" | "ManualOut" | "Return";

export interface InventoryTransactionDto {
  id: string;
  productId: string;
  productName: string;
  warehouseId: string;
  warehouseName: string;
  type: string;
  date: string;
  quantity: number;
  description: string | null;
  referenceType: string | null;
  referenceId: string | null;
  createdAtUtc: string;
}

export interface CriticalStockItemDto {
  productId: string;
  name: string;
  sku: string | null;
  currentStock: number;
  minimumStock: number;
  unitName: string | null;
}
