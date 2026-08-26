"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Check, ChevronsUpDown, Loader2, LogOut, Plus } from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { toast } from "sonner";

import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { BrandMark } from "@/components/brand-mark";
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
        <Button variant="outline" className="h-9 w-full max-w-56 justify-between gap-2 overflow-hidden">
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
        <Button variant="ghost" className="h-9 gap-2 px-2">
          <Avatar className="size-7">
            <AvatarFallback className="text-xs">{initials(fullName)}</AvatarFallback>
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
            className={`flex items-center gap-3 rounded-md px-3 py-2 text-sm transition-colors ${
              isActive
                ? "bg-sidebar-accent font-medium text-sidebar-accent-foreground shadow-sm"
                : "text-muted-foreground hover:bg-accent hover:text-accent-foreground"
            }`}
          >
            <item.icon className="size-4" />
            {item.label}
          </Link>
        );
      })}
    </nav>
  );
}

function MobileNav() {
  const pathname = usePathname();
  return (
    <nav className="flex items-center gap-1 overflow-x-auto px-2" aria-label="Ana gezinme">
      {NAV_ITEMS.map((item) => {
        const isActive = pathname.startsWith(item.href);
        if (!item.enabled) {
          return (
            <span
              key={item.href}
              aria-disabled="true"
              className="flex shrink-0 items-center gap-1.5 rounded-md px-2.5 py-1.5 text-xs text-muted-foreground/60"
            >
              <item.icon className="size-3.5" />
              {item.label}
            </span>
          );
        }
        return (
          <Link
            key={item.href}
            href={item.href}
            aria-current={isActive ? "page" : undefined}
            className={`flex shrink-0 items-center gap-1.5 rounded-md px-2.5 py-1.5 text-xs transition-colors ${
              isActive ? "bg-primary/10 font-medium text-primary" : "text-muted-foreground hover:bg-accent"
            }`}
          >
            <item.icon className="size-3.5" />
            {item.label}
          </Link>
        );
      })}
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
      <aside className="sticky top-0 hidden h-svh w-60 shrink-0 flex-col border-r bg-sidebar text-sidebar-foreground md:flex">
        <div className="flex h-16 items-center gap-3 border-b px-4 font-semibold tracking-tight">
          <BrandMark />
          <span>
            Muhasebe
            <span className="block text-[10px] font-medium uppercase tracking-[0.18em] text-sidebar-foreground/50">İşletme paneli</span>
          </span>
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
        <header className="sticky top-0 z-10 flex h-16 items-center gap-3 border-b bg-background/90 px-4 backdrop-blur-xl supports-[backdrop-filter]:bg-background/75">
          <div className="flex items-center gap-2 font-semibold md:hidden">
            <BrandMark className="size-8" />
          </div>
          <div className="md:hidden">
            <TenantSwitcher tenants={me.tenants} activeTenantId={getActiveTenantId()} />
          </div>
          <div className="ml-auto">
            <UserMenu fullName={me.user.fullName} email={me.user.email} />
          </div>
        </header>
        <div className="border-b md:hidden">
          <MobileNav />
        </div>
        <main className="flex-1 p-4 md:p-6">{children}</main>
      </div>
    </div>
  );
}
