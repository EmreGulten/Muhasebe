"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { BadgeCheck, CalendarClock, CreditCard, Loader2, Sparkles } from "lucide-react";
import { toast } from "sonner";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { api } from "@/lib/api";
import { getActiveTenantId } from "@/lib/auth-store";
import { formatDate } from "@/lib/parties";
import type { MeResponse, SubscriptionPlanDto, SubscriptionResponse } from "@/lib/types";

/** Plan özellik anahtarı → Türkçe etiket. */
const FEATURE_LABELS: Record<string, string> = {
  core: "Cari · Gelir/Gider · Kasa · Temel Satış · Temel Raporlar",
  stock: "Stok yönetimi",
  purchases: "Alış yönetimi",
  reports_advanced: "Gelişmiş raporlar",
  ai_assistant: "AI Asistan",
  multi_warehouse: "Çoklu depo",
  api: "API erişimi",
  integrations: "E-ticaret entegrasyonları",
  quotes: "Teklif yönetimi",
};

/** Plan kartında görünen özellik sırası. */
const FEATURE_ORDER = ["core", "stock", "purchases", "reports_advanced", "ai_assistant", "quotes", "multi_warehouse", "api", "integrations"];

const STATUS_LABELS: Record<string, string> = {
  Trialing: "Deneme",
  Active: "Aktif",
  PastDue: "Ödeme bekliyor",
  Cancelled: "İptal edildi",
  Expired: "Dönemi doldu",
};

function warehouseLabel(max: number): string {
  return max < 0 ? "Sınırsız depo" : `${max} depo`;
}

/** Abonelik: mevcut plan durumu + bölüm 29'daki üç plan kartı. */
export function SubscriptionView() {
  const queryClient = useQueryClient();

  const me = useQuery({
    queryKey: ["me"],
    queryFn: () => api<MeResponse>("/api/v1/auth/me"),
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
  const activeTenant =
    me.data?.tenants.find((tenant) => tenant.tenantId === getActiveTenantId()) ?? me.data?.tenants[0] ?? null;
  const isOwner = activeTenant?.role === "Owner";

  const subscription = useQuery({
    queryKey: ["subscription"],
    queryFn: () => api<SubscriptionResponse>("/api/v1/subscription"),
    enabled: activeTenant !== null,
  });

  const plans = useQuery({
    queryKey: ["subscription-plans"],
    queryFn: () => api<SubscriptionPlanDto[]>("/api/v1/subscription/plans"),
    enabled: activeTenant !== null,
  });

  const changePlan = useMutation({
    mutationFn: (planCode: string) =>
      api<SubscriptionResponse>("/api/v1/subscription/change", {
        method: "POST",
        body: JSON.stringify({ planCode }),
      }),
    onSuccess: (response) => {
      toast.success(`Planınız "${response.plan.name}" olarak güncellendi.`);
      queryClient.invalidateQueries({ queryKey: ["subscription"] });
    },
    onError: (mutationError) => {
      toast.error(mutationError instanceof Error ? mutationError.message : "Plan değiştirilemedi.");
    },
  });

  if (subscription.isPending || plans.isPending || me.isPending) {
    return (
      <div className="flex justify-center py-16">
        <Loader2 className="size-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const current = subscription.data;
  const currentCode = current?.plan.code;

  return (
    <div className="mx-auto grid w-full max-w-5xl gap-6">
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="text-2xl font-semibold tracking-tight">Abonelik</h1>
        {current && (
          <Badge variant={current.isActive ? "default" : "secondary"}>
            {STATUS_LABELS[current.status] ?? current.status}
          </Badge>
        )}
        <p className="w-full text-sm text-muted-foreground">
          Planınıza göre modüller ve limitler belirlenir. Plan değişikliği yalnızca işletme sahibi yapabilir.
        </p>
      </div>

      {current && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-base">
              <CreditCard className="size-4" />
              Mevcut planınız: {current.plan.name}
              {current.isTrial && (
                <Badge variant="secondary" className="gap-1">
                  <Sparkles className="size-3" /> Deneme
                </Badge>
              )}
            </CardTitle>
          </CardHeader>
          <CardContent className="grid gap-2 text-sm text-muted-foreground">
            <p className="flex items-center gap-2">
              <CalendarClock className="size-4" />
              {current.isTrial && current.trialEndsAtUtc
                ? `Deneme ${formatDate(current.trialEndsAtUtc)} tarihinde bitiyor · ${current.daysRemaining} gün kaldı`
                : `Dönem ${formatDate(current.currentPeriodEndUtc)} tarihinde bitiyor · ${current.daysRemaining} gün kaldı`}
            </p>
            <p className="flex items-center gap-2">
              <BadgeCheck className="size-4" />
              {warehouseLabel(current.plan.maxWarehouses)} · {current.plan.maxUsers} kullanıcı
              {current.plan.aiMonthlyQuestionLimit > 0
                ? ` · Aylık ${current.plan.aiMonthlyQuestionLimit} AI sorusu`
                : ""}
            </p>
            {!current.isActive && (
              <p className="text-destructive">
                Abonelik döneminiz doldu; temel özelliklerle çalışıyorsunuz. Bir plan seçerek devam edin.
              </p>
            )}
          </CardContent>
        </Card>
      )}

      <div className="grid gap-4 md:grid-cols-3">
        {(plans.data ?? []).map((plan) => {
          const isCurrent = plan.code === currentCode;
          return (
            <Card key={plan.code} className={isCurrent ? "border-primary" : undefined}>
              <CardHeader className="pb-2">
                <CardTitle className="text-base">{plan.name}</CardTitle>
                <p className="text-2xl font-semibold">
                  {plan.monthlyPrice.toLocaleString("tr-TR", { minimumFractionDigits: 0 })} TL
                  <span className="text-sm font-normal text-muted-foreground"> / ay</span>
                </p>
              </CardHeader>
              <CardContent className="grid gap-4">
                <ul className="grid gap-1.5 text-sm">
                  {FEATURE_ORDER.filter((feature) => plan.features.includes(feature)).map((feature) => (
                    <li key={feature} className="flex items-start gap-2">
                      <BadgeCheck className="mt-0.5 size-4 shrink-0 text-primary" />
                      {FEATURE_LABELS[feature] ?? feature}
                    </li>
                  ))}
                </ul>
                <p className="text-xs text-muted-foreground">
                  {warehouseLabel(plan.maxWarehouses)} · {plan.maxUsers} kullanıcı
                  {plan.aiMonthlyQuestionLimit > 0 ? ` · ${plan.aiMonthlyQuestionLimit} AI sorusu/ay` : ""}
                </p>
                {isOwner ? (
                  <Button
                    variant={isCurrent ? "outline" : "default"}
                    disabled={isCurrent || changePlan.isPending}
                    onClick={() => changePlan.mutate(plan.code)}
                  >
                    {changePlan.isPending && changePlan.variables === plan.code ? (
                      <Loader2 className="size-4 animate-spin" />
                    ) : null}
                    {isCurrent ? "Mevcut planınız" : "Bu plana geç"}
                  </Button>
                ) : (
                  <p className="text-xs text-muted-foreground">Plan değişikliği için işletme sahibi onayı gerekir.</p>
                )}
              </CardContent>
            </Card>
          );
        })}
      </div>
    </div>
  );
}
