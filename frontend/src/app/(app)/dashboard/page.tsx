"use client";

import { useQuery } from "@tanstack/react-query";
import { CalendarClock, ReceiptText, TrendingUp, Wallet } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect } from "react";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { api } from "@/lib/api";
import { getActiveTenantId } from "@/lib/auth-store";
import type { MeResponse } from "@/lib/types";

const ROLE_LABELS: Record<string, string> = {
  Owner: "Sahip",
  Accountant: "Muhasebeci",
  Employee: "Çalışan",
};

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

  return (
    <div className="mx-auto grid w-full max-w-5xl gap-6">
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
      ) : (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {[
              { title: "Bugünkü Satış", value: "—", icon: TrendingUp },
              { title: "Açık Alacaklar", value: "—", icon: ReceiptText },
              { title: "Kasa Bakiyesi", value: "—", icon: Wallet },
              { title: "Bu Ay KDV", value: "—", icon: CalendarClock },
            ].map((stat) => (
              <Card key={stat.title}>
                <CardHeader className="pb-2">
                  <CardTitle className="flex items-center gap-2 text-sm font-medium text-muted-foreground">
                    <stat.icon className="size-4" />
                    {stat.title}
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <p className="text-2xl font-semibold tabular-nums">{stat.value}</p>
                </CardContent>
              </Card>
            ))}
          </div>

          <Card>
            <CardHeader>
              <CardTitle>Sıradaki adımlar</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-2 text-sm text-muted-foreground">
              <p>
                Bu fazda hesabınız ve işletmeniz hazır. Satış, alış, müşteri ve kasa modülleri sonraki fazlarla
                eklenecek; o zamana dek sayfa gezinmesinde <span className="text-foreground">Yakında</span> etiketini
                göreceksiniz.
              </p>
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );
}
