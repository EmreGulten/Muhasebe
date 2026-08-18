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
