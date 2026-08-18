"use client";

import { useQuery } from "@tanstack/react-query";
import { Loader2 } from "lucide-react";
import Link from "next/link";
import { use } from "react";

import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { SaleForm } from "@/components/sale-form";
import { api } from "@/lib/api";
import type { SaleResponse } from "@/lib/types";

/** Taslak satışı düzenle — onaylı/iptal belge düzenlenemez, iptal + yeni belge gerekir. */
export default function EditSalePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data: sale, isPending, isError, error } = useQuery({
    queryKey: ["sale", id],
    queryFn: () => api<SaleResponse>(`/api/v1/sales/${id}`),
  });

  if (isPending) {
    return (
      <div className="flex justify-center py-12">
        <Loader2 className="size-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !sale) {
    return (
      <Card className="mx-auto max-w-3xl">
        <CardContent className="grid justify-items-center gap-3 py-10 text-center">
          <p className="text-sm text-destructive">
            {error instanceof Error ? error.message : "Satış bulunamadı."}
          </p>
          <Button asChild variant="outline">
            <Link href="/sales">Satış listesine dön</Link>
          </Button>
        </CardContent>
      </Card>
    );
  }

  if (sale.status !== "Draft") {
    return (
      <Card className="mx-auto max-w-3xl">
        <CardContent className="grid justify-items-center gap-3 py-10 text-center">
          <p className="text-sm text-muted-foreground">
            Onaylanmış ya da iptal edilmiş belge düzenlenemez. Düzeltme için belgeyi iptal edip yenisini
            oluşturun.
          </p>
          <Button asChild variant="outline">
            <Link href={`/sales/${id}`}>Belgeye dön</Link>
          </Button>
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="mx-auto grid w-full max-w-5xl gap-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Satışı Düzenle</h1>
        <p className="text-sm text-muted-foreground">
          {sale.number} · taslak belge — onayda stok ve cari etkisi oluşur.
        </p>
      </div>
      <SaleForm key={sale.id} sale={sale} />
    </div>
  );
}
