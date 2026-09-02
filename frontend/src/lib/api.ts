"use client";

import { authStore, clearActiveTenantId, getActiveTenantId } from "@/lib/auth-store";

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

const AUTH_PATHS = ["/api/v1/auth/refresh", "/api/v1/auth/login", "/api/v1/auth/register"];

let refreshInFlight: Promise<boolean> | null = null;

/** Refresh cookie'sini kullanarak sessizce yeni access token alır. */
async function refreshSession(): Promise<boolean> {
  if (!refreshInFlight) {
    refreshInFlight = fetch("/api/v1/auth/refresh", {
      method: "POST",
      credentials: "same-origin",
    })
      .then(async (response) => {
        if (!response.ok) return false;
        const data = (await response.json()) as { accessToken: string };
        authStore.setToken(data.accessToken);
        return true;
      })
      .catch(() => false)
      .finally(() => {
        refreshInFlight = null;
      });
  }
  return refreshInFlight;
}

async function parseError(response: Response): Promise<string> {
  try {
    const problem = await response.json();
    if (problem?.detail) return problem.detail as string;
    if (problem?.title) return problem.title as string;
    if (problem?.errors) {
      const first = Object.values(problem.errors as Record<string, string[]>).flat()[0];
      if (first) return first;
    }
  } catch {
    // gövde JSON değilse aşağıdaki genel mesaja düş
  }
  return "İstek başarısız oldu. Lütfen tekrar deneyin.";
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const response = await authenticatedFetch(path, init);

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

async function authenticatedFetch(path: string, init: RequestInit = {}): Promise<Response> {
  const doFetch = () =>
    fetch(path, {
      ...init,
      headers: {
        "Content-Type": "application/json",
        ...(authStore.getToken() ? { Authorization: `Bearer ${authStore.getToken()}` } : {}),
        ...(getActiveTenantId() ? { "X-Tenant-Id": getActiveTenantId()! } : {}),
        ...init.headers,
      },
      credentials: "same-origin",
    });

  let response = await doFetch();

  // Access token süresi dolduysa bir kez yenile ve tekrar dene.
  if (response.status === 401 && !AUTH_PATHS.includes(path)) {
    const refreshed = await refreshSession();
    if (refreshed) {
      response = await doFetch();
    } else {
      authStore.clear();
      clearActiveTenantId();
      // Hard navigasyon: tüm React + query state'i sıfırlar.
      window.location.replace("/login");
      throw new ApiError("Oturumunuz sona erdi. Lütfen tekrar giriş yapın.", 401);
    }
  }

  if (!response.ok) {
    throw new ApiError(await parseError(response), response.status);
  }
  return response;
}

export async function apiDownload(path: string): Promise<{ blob: Blob; fileName: string }> {
  const response = await authenticatedFetch(path);
  const disposition = response.headers.get("content-disposition") ?? "";
  const encoded = disposition.match(/filename\*=UTF-8''([^;]+)/i)?.[1];
  const plain = disposition.match(/filename="?([^";]+)"?/i)?.[1];
  return {
    blob: await response.blob(),
    fileName: encoded ? decodeURIComponent(encoded) : plain ?? "isletme-yedegi.json",
  };
}

export async function apiRestoreBackup(file: File): Promise<{
  importedRowCount: number;
  importedTableCount: number;
}> {
  const response = await authenticatedFetch("/api/v1/tenants/current/backup/restore", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: await file.arrayBuffer(),
  });
  return response.json();
}
