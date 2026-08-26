"use client";

import { useQuery } from "@tanstack/react-query";
import {
  AlertTriangle,
  ArrowDownLeft,
  ArrowUpRight,
  Banknote,
  CalendarClock,
  Landmark,
  Loader2,
  ReceiptText,
  Scale,
  TrendingUp,
  Users,
} from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect } from "react";

import { PairedBarChart, RankedList, SingleBarChart } from "@/components/report-charts";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { api } from "@/lib/api";
import { getActiveTenantId } from "@/lib/auth-store";
import { MONTH_NAMES } from "@/lib/income-expense";
import { formatMoney } from "@/lib/parties";
import { formatQuantity } from "@/lib/products";
import type { DashboardResponse, MeResponse } from "@/lib/types";

const ROLE_LABELS: Record<string, string> = {
  Owner: "Sahip",
  Accountant: "Muhasebeci",
  Employee: "Çalışan",
};

/** "2026-08-19T..." → "19 Ağu" gibi kısa gün etiketi. */
function dayLabel(iso: string): string {
  const date = new Date(iso);
  return `${date.getUTCDate()} ${(MONTH_NAMES[date.getUTCMonth() + 1] ?? "").slice(0, 3)}`;
}

/** Ay etiketi: "Ağu" (yıl, ay serisi içinde zaten bellidir). */
function shortMonthLabel(month: number): string {
  return (MONTH_NAMES[month - 1] ?? String(month)).slice(0, 3);
}

export default function DashboardPage() {
  const router = useRouter();
  const { data: me } = useQuery({
    queryKey: ["me"],
    queryFn: () => api<MeResponse>("/api/v1/auth/me"),
    staleTime: 5 * 60 * 1000,
    retry: false,
  });

  // İşletmesi olmayan kullanıcıyı işletme oluşturmaya yönlendir.
  useEffect(() => {
    if (me && me.tenants.length === 0) {
      router.replace("/business/new");
    }
  }, [me, router]);

  const activeTenant = me?.tenants.find((tenant) => tenant.tenantId === getActiveTenantId()) ?? me?.tenants[0] ?? null;
  const firstName = me?.user.fullName.split(" ")[0] ?? "";

  // Dashboard verisi yalnızca işletmesi olan kullanıcıda istenir (plan 3.1).
  const dashboard = useQuery({
    queryKey: ["dashboard"],
    queryFn: () => api<DashboardResponse>("/api/v1/reports/dashboard"),
    enabled: activeTenant !== null,
  });

  const kpi = (title: string, value: string, icon: typeof TrendingUp) => ({ title, value, icon });
  const kpis = dashboard.data
    ? [
        kpi("Bugünkü Satış", formatMoney(dashboard.data.dailySales), TrendingUp),
        kpi("Bu Ayki Ciro", formatMoney(dashboard.data.monthlySales), ReceiptText),
        kpi("Bu Ayki Gider", formatMoney(dashboard.data.monthlyExpense), ArrowUpRight),
        kpi("Tahmini Net Kazanç", formatMoney(dashboard.data.estimatedNet), Scale),
        kpi("Toplam Alacak", formatMoney(dashboard.data.totalReceivable), ArrowDownLeft),
        kpi("Toplam Borç", formatMoney(dashboard.data.totalPayable), Users),
        kpi("Kasa", formatMoney(dashboard.data.cashTotal), Banknote),
        kpi("Banka", formatMoney(dashboard.data.bankTotal), Landmark),
        kpi("Kritik Stok", `${dashboard.data.criticalStockCount} ürün`, AlertTriangle),
        kpi("Gecikmiş Alacak", `${dashboard.data.overdueReceivableCount} müşteri`, CalendarClock),
      ]
    : [];

  return (
    <div className="mx-auto grid w-full max-w-6xl gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">
          {firstName ? `Merhaba, ${firstName}` : "Merhaba"}
        </h1>
        <p className="text-sm text-muted-foreground">
          {activeTenant
            ? `${activeTenant.name} · ${ROLE_LABELS[activeTenant.role] ?? activeTenant.role}`
            : "Henüz bir işletmeniz yok."}
        </p>
      </div>

      {!activeTenant ? (
        <Card>
          <CardHeader>
            <CardTitle>İşletme oluşturun</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4">
            <p className="text-sm text-muted-foreground">
              Muhasebe kayıtlarına başlamak için önce bir işletme oluşturmanız gerekiyor.
            </p>
            <Button asChild className="w-fit">
              <Link href="/business/new">İşletme Oluştur</Link>
            </Button>
          </CardContent>
        </Card>
      ) : dashboard.isPending ? (
        <div className="flex justify-center py-16">
          <Loader2 className="size-6 animate-spin text-muted-foreground" />
        </div>
      ) : dashboard.isError ? (
        <Card>
          <CardContent className="py-8 text-center text-sm text-destructive">
            {dashboard.error instanceof Error ? dashboard.error.message : "Dashboard verisi alınamadı."}
          </CardContent>
        </Card>
      ) : (
        <>
          {/* İşletmenin güncel durumunu özetleyen KPI kartları. */}
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-5">
            {kpis.map((stat) => (
              <Card key={stat.title}>
                <CardHeader className="pb-2">
                  <CardTitle className="flex items-center gap-2 text-sm font-medium text-muted-foreground">
                    <stat.icon className="size-4" />
                    {stat.title}
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <p className="truncate text-2xl font-semibold tabular-nums" title={stat.value}>
                    {stat.value}
                  </p>
                </CardContent>
              </Card>
            ))}
          </div>

          {/* Grafik 1 + 2 — son 30 gün akış ve son 12 ay ciro. */}
          <div className="grid gap-4 lg:grid-cols-2">
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-base">Son 30 Gün · Gelir &amp; Gider</CardTitle>
              </CardHeader>
              <CardContent>
                <PairedBarChart
                  items={(dashboard.data?.last30DaysFlow ?? []).map((flow) => ({
                    label: dayLabel(flow.date),
                    title: `${dayLabel(flow.date)} · gelir ${formatMoney(flow.income)} · gider ${formatMoney(flow.expense)}`,
                    primary: flow.income,
                    secondary: flow.expense,
                  }))}
                  primaryLabel="Gelir"
                  secondaryLabel="Gider"
                />
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-base">Son 12 Ay · Ciro</CardTitle>
              </CardHeader>
              <CardContent>
                <SingleBarChart
                  items={(dashboard.data?.last12MonthsRevenue ?? []).map((month) => ({
                    label: shortMonthLabel(month.month),
                    title: `${MONTH_NAMES[month.month - 1]} ${month.year} · ${formatMoney(month.total)}`,
                    primary: month.total,
                  }))}
                />
              </CardContent>
            </Card>
          </div>

          {/* Grafik 3 + 4 + 5 — en çok satan, en kârlı, en borçlu. */}
          <div className="grid gap-4 lg:grid-cols-3">
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-base">En Çok Satan 5 Ürün</CardTitle>
              </CardHeader>
              <CardContent>
                <RankedList
                  items={(dashboard.data?.topSellingProducts ?? []).map((product) => ({
                    id: product.productId,
                    label: product.productName,
                    value: product.total,
                    sub: `${formatQuantity(product.quantity)} adet`,
                  }))}
                  formatValue={formatMoney}
                  emptyText="Henüz onaylı satış yok."
                />
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-base">En Kârlı 5 Ürün</CardTitle>
              </CardHeader>
              <CardContent>
                <RankedList
                  items={(dashboard.data?.mostProfitableProducts ?? []).map((product) => ({
                    id: product.productId,
                    label: product.productName,
                    value: product.estimatedProfit,
                    sub: `ciro ${formatMoney(product.total)}`,
                  }))}
                  formatValue={formatMoney}
                  tone="emerald"
                  emptyText="Henüz onaylı satış yok."
                />
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-base">En Yüksek Borçlu 5 Müşteri</CardTitle>
              </CardHeader>
              <CardContent>
                <RankedList
                  items={(dashboard.data?.topDebtors ?? []).map((debtor) => ({
                    id: debtor.partyId,
                    label: debtor.partyName,
                    value: debtor.balance,
                  }))}
                  formatValue={formatMoney}
                  emptyText="Borçlu müşteri yok."
                />
              </CardContent>
            </Card>
          </div>
        </>
      )}
    </div>
  );
}
