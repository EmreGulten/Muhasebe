"use client";

// Access token yalnızca bellekte tutulur (XSS'e karşı); kalıcı oturum
// httpOnly refresh cookie'si üzerinden sessizce yenilenir. Aktif işletme
// seçimi localStorage'da saklanır.

const TENANT_KEY = "muhasebe.activeTenantId";

let accessToken: string | null = null;

export const authStore = {
  getToken(): string | null {
    return accessToken;
  },
  setToken(token: string) {
    accessToken = token;
  },
  clear() {
    accessToken = null;
    window.localStorage.removeItem(TENANT_KEY);
  },
};

export function getActiveTenantId(): string | null {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem(TENANT_KEY);
}

export function setActiveTenantId(tenantId: string) {
  window.localStorage.setItem(TENANT_KEY, tenantId);
}

export function clearActiveTenantId() {
  window.localStorage.removeItem(TENANT_KEY);
}
