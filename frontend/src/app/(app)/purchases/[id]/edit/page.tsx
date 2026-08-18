"use client";

import { useQuery } from "@tanstack/react-query";
import { Loader2 } from "lucide-react";
import Link from "next/link";
import { use } from "react";

import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { PurchaseForm } from "@/components/purchase-form";
import { api } from "@/lib/api";
import type { PurchaseResponse } from "@/lib/types";

/** Taslak alışı düzenle — onaylı/iptal belge düzenlenemez, iptal + yeni belge gerekir. */
export default function EditPurchasePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data: purchase, isPending, isError, error } = useQuery({
    queryKey: ["purchase", id],
    queryFn: () => api<PurchaseResponse>(`/api/v1/purchases/${id}`),
  });

  if (isPending) {
    return (
      <div className="flex justify-center py-12">
        <Loader2 className="size-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !purchase) {
    return (
      <Card className="mx-auto max-w-3xl">
        <CardContent className="grid justify-items-center gap-3 py-10 text-center">
          <p className="text-sm text-destructive">
            {error instanceof Error ? error.message : "Alış bulunamadı."}
          </p>
          <Button asChild variant="outline">
            <Link href="/purchases">Alış listesine dön</Link>
          </Button>
        </CardContent>
      </Card>
    );
  }

  if (purchase.status !== "Draft") {
    return (
      <Card className="mx-auto max-w-3xl">
        <CardContent className="grid justify-items-center gap-3 py-10 text-center">
          <p className="text-sm text-muted-foreground">
            Onaylanmış ya da iptal edilmiş belge düzenlenemez. Düzeltme için belgeyi iptal edip yenisini
            oluşturun.
          </p>
          <Button asChild variant="outline">
            <Link href={`/purchases/${id}`}>Belgeye dön</Link>
          </Button>
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="mx-auto grid w-full max-w-5xl gap-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Alışı Düzenle</h1>
        <p className="text-sm text-muted-foreground">
          {purchase.number} · taslak belge — onayda stok ve cari etkisi oluşur.
        </p>
      </div>
      <PurchaseForm key={purchase.id} purchase={purchase} />
    </div>
  );
}
