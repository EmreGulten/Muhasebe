"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Calculator, Check, ChevronsUpDown, Loader2, LogOut, Menu, Plus } from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { toast } from "sonner";

import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Separator } from "@/components/ui/separator";
import { api } from "@/lib/api";
import { authStore, clearActiveTenantId, getActiveTenantId, setActiveTenantId } from "@/lib/auth-store";
import { NAV_ITEMS } from "@/lib/nav";
import type { MeResponse, MessageResponse, TenantMembershipDto } from "@/lib/types";

function initials(fullName: string): string {
  return fullName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]!.toUpperCase())
    .join("");
}

const ROLE_LABELS: Record<string, string> = {
  Owner: "Sahip",
  Accountant: "Muhasebeci",
  Employee: "Çalışan",
};

function TenantSwitcher({ tenants, activeTenantId }: { tenants: TenantMembershipDto[]; activeTenantId: string | null }) {
  const queryClient = useQueryClient();
  const active = tenants.find((tenant) => tenant.tenantId === activeTenantId) ?? null;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="outline"
          className="h-10 w-full max-w-[min(12rem,calc(100vw-8rem))] justify-between gap-2 overflow-hidden md:h-9 md:max-w-56"
        >
          <span className="truncate">{active ? active.name : "İşletme Seç"}</span>
          <ChevronsUpDown className="size-4 shrink-0 opacity-50" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-56">
        <DropdownMenuLabel>İşletmeleriniz</DropdownMenuLabel>
        {tenants.map((tenant) => (
          <DropdownMenuItem
            key={tenant.tenantId}
            onClick={() => {
              setActiveTenantId(tenant.tenantId);
              queryClient.invalidateQueries();
              toast.success(`${tenant.name} işletmesine geçildi.`);
            }}
          >
            <Check className={`size-4 ${tenant.tenantId === activeTenantId ? "opacity-100" : "opacity-0"}`} />
            <span className="flex-1 truncate">{tenant.name}</span>
            <span className="text-xs text-muted-foreground">{ROLE_LABELS[tenant.role] ?? tenant.role}</span>
          </DropdownMenuItem>
        ))}
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link href="/business/new">
            <Plus className="size-4" />
            Yeni İşletme
          </Link>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function UserMenu({ fullName, email }: { fullName: string; email: string }) {
  const router = useRouter();
  const queryClient = useQueryClient();

  const logout = useMutation({
    mutationFn: () => api<MessageResponse>("/api/v1/auth/logout", { method: "POST" }),
    onSuccess: () => {
      authStore.clear();
      clearActiveTenantId();
      queryClient.clear();
      router.replace("/login");
    },
    onError: (error) => {
      // Oturum zaten yoksa da temizle.
      authStore.clear();
      clearActiveTenantId();
      queryClient.clear();
      router.replace("/login");
      toast.error(error instanceof Error ? error.message : "Çıkış yapılamadı.");
    },
  });

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" className="h-9 gap-2 px-2 hover:bg-primary/5">
          <Avatar className="size-7">
            <AvatarFallback className="bg-gradient-to-br from-violet-500 to-indigo-600 text-xs font-semibold text-white shadow-sm">
              {initials(fullName)}
            </AvatarFallback>
          </Avatar>
          <span className="hidden max-w-40 truncate text-sm font-medium sm:inline">{fullName}</span>
          <ChevronsUpDown className="size-4 opacity-50" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel className="font-normal">
          <p className="text-sm font-medium">{fullName}</p>
          <p className="text-xs text-muted-foreground truncate">{email}</p>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem disabled={logout.isPending} onClick={() => logout.mutate()}>
          {logout.isPending ? <Loader2 className="size-4 animate-spin" /> : <LogOut className="size-4" />}
          Çıkış Yap
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function SidebarNav() {
  const pathname = usePathname();

  return (
    <nav className="grid gap-1 px-2 py-2" aria-label="Ana gezinme">
      {NAV_ITEMS.map((item) => {
        const isActive = pathname.startsWith(item.href);
        if (!item.enabled) {
          return (
            <span
              key={item.href}
              aria-disabled="true"
              className="flex items-center gap-3 rounded-md px-3 py-2 text-sm text-muted-foreground/60 cursor-not-allowed"
              title="Yakında"
            >
              <item.icon className="size-4" />
              <span className="flex-1">{item.label}</span>
              <span className="text-[10px] uppercase tracking-wide">Yakında</span>
            </span>
          );
        }
        return (
          <Link
            key={item.href}
            href={item.href}
            aria-current={isActive ? "page" : undefined}
            className={`flex items-center gap-3 rounded-lg border-l-2 px-3 py-2 text-sm transition-all duration-200 ${
              isActive
                ? "border-primary bg-primary/10 font-semibold text-primary shadow-sm shadow-primary/5"
                : "border-transparent text-muted-foreground hover:bg-primary/5 hover:text-foreground"
            }`}
          >
            <item.icon className={`size-4 ${isActive ? "text-primary" : ""}`} />
            {item.label}
          </Link>
        );
      })}
    </nav>
  );
}

function MobileNav() {
  const pathname = usePathname();
  const primaryItems = NAV_ITEMS.slice(0, 4);
  const moreItems = NAV_ITEMS.slice(4);
  const moreIsActive = moreItems.some((item) => pathname.startsWith(item.href));

  return (
    <nav
      className="fixed inset-x-0 bottom-0 z-40 grid grid-cols-5 border-t bg-background/95 px-1 pb-[env(safe-area-inset-bottom)] shadow-[0_-8px_24px_-18px_rgba(0,0,0,0.45)] backdrop-blur-xl md:hidden"
      aria-label="Ana gezinme"
    >
      {primaryItems.map((item) => {
        const isActive = pathname.startsWith(item.href);
        return (
          <Link
            key={item.href}
            href={item.href}
            aria-current={isActive ? "page" : undefined}
            className={`flex min-h-14 min-w-0 flex-col items-center justify-center gap-1 rounded-lg px-1 text-[10px] transition-colors ${
              isActive ? "font-semibold text-primary" : "text-muted-foreground"
            }`}
          >
            <item.icon className="size-5" />
            <span className="max-w-full truncate">{item.label}</span>
          </Link>
        );
      })}

      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <button
            type="button"
            className={`flex min-h-14 min-w-0 flex-col items-center justify-center gap-1 rounded-lg px-1 text-[10px] outline-none transition-colors ${
              moreIsActive ? "font-semibold text-primary" : "text-muted-foreground"
            }`}
            aria-label="Diğer bölümler"
          >
            <Menu className="size-5" />
            <span>Diğer</span>
          </button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" side="top" sideOffset={10} className="mb-1 w-56">
          <DropdownMenuLabel>Diğer bölümler</DropdownMenuLabel>
          {moreItems.map((item) => (
            <DropdownMenuItem key={item.href} asChild>
              <Link href={item.href} className={pathname.startsWith(item.href) ? "bg-accent font-medium" : undefined}>
                <item.icon className="size-4" />
                {item.label}
              </Link>
            </DropdownMenuItem>
          ))}
        </DropdownMenuContent>
      </DropdownMenu>
    </nav>
  );
}

export function AppShell({ children }: { children: React.ReactNode }) {
  const { data: me, isPending } = useQuery({
    queryKey: ["me"],
    queryFn: () => api<MeResponse>("/api/v1/auth/me"),
    staleTime: 5 * 60 * 1000,
    retry: false,
  });

  // api() 401'de otomatik /login'e yönlendirir; burada yalnızca yüklenme durumu kalır.
  if (isPending) {
    return (
      <div className="flex min-h-svh items-center justify-center">
        <Loader2 className="size-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!me) {
    return (
      <div className="flex min-h-svh flex-col items-center justify-center gap-4 p-6 text-center">
        <p className="text-sm text-muted-foreground">Oturum bilgisi alınamadı.</p>
        <Button asChild variant="outline">
          <Link href="/login">Giriş sayfasına dön</Link>
        </Button>
      </div>
    );
  }

  return (
    <div className="flex min-h-svh w-full">
      <aside className="sticky top-0 hidden h-svh w-60 shrink-0 flex-col border-r bg-sidebar/95 text-sidebar-foreground shadow-sm backdrop-blur md:flex">
        <div className="flex h-14 items-center gap-2 border-b border-sidebar-border px-4 font-bold tracking-tight">
          <span className="flex size-8 items-center justify-center rounded-xl bg-gradient-to-br from-violet-500 to-indigo-600 text-primary-foreground shadow-sm shadow-primary/20">
            <Calculator className="size-4" />
          </span>
          Muhasebe
        </div>
        <div className="flex-1 overflow-y-auto">
          <SidebarNav />
        </div>
        <Separator />
        <div className="p-2">
          <TenantSwitcher tenants={me.tenants} activeTenantId={getActiveTenantId()} />
        </div>
      </aside>
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-10 flex h-14 items-center gap-3 border-b bg-background/85 px-4 shadow-xs backdrop-blur-xl supports-[backdrop-filter]:bg-background/70">
          <div className="flex items-center gap-2 font-semibold md:hidden">
            <span className="flex size-8 items-center justify-center rounded-xl bg-gradient-to-br from-violet-500 to-indigo-600 text-primary-foreground shadow-sm">
              <Calculator className="size-4" />
            </span>
          </div>
          <div className="md:hidden">
            <TenantSwitcher tenants={me.tenants} activeTenantId={getActiveTenantId()} />
          </div>
          <div className="ml-auto">
            <UserMenu fullName={me.user.fullName} email={me.user.email} />
          </div>
        </header>
        <MobileNav />
        <main className="flex-1 p-3 pb-24 sm:p-4 sm:pb-24 md:p-6">{children}</main>
      </div>
    </div>
  );
}
