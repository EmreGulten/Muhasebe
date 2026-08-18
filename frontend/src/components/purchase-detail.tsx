"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Ban, CheckCircle2, Loader2, Pencil, Plus, Trash2 } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import { api } from "@/lib/api";
import { dateInputToIso, formatDate, formatMoney, isoToDateInput, parseMoneyInput } from "@/lib/parties";
import { formatQuantity } from "@/lib/products";
import { PURCHASE_STATUS_LABELS, purchaseStatusVariant } from "@/lib/purchases";
import type { PurchaseResponse } from "@/lib/types";

const todayInput = () => isoToDateInput(new Date().toISOString());

/** Belge ile değişen her şeyi geçersiz kıl: liste, belge, stok ve cari ekranları. */
function invalidatePurchaseQueries(queryClient: ReturnType<typeof useQueryClient>, purchaseId: string) {
  for (const key of [
    "purchases",
    ["purchase", purchaseId],
    "products",
    "product",
    "product-stock",
    "product-inventory",
    "parties",
    "party",
  ]) {
    queryClient.invalidateQueries({ queryKey: typeof key === "string" ? [key] : key });
  }
}

/** Alış belgesi detayı: kalemler, ödemeler, onay/iptal/ödeme eylemleri. */
export function PurchaseDetail({ purchaseId }: { purchaseId: string }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [paymentOpen, setPaymentOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);

  // Onay diyaloğu — anlık ödeme opsiyonel.
  const [confirmDate, setConfirmDate] = useState(todayInput());
  const [confirmAmount, setConfirmAmount] = useState("");
  const [confirmDescription, setConfirmDescription] = useState("");

  // Ödeme diyaloğu.
  const [paymentDate, setPaymentDate] = useState(todayInput());
  const [paymentAmount, setPaymentAmount] = useState("");
  const [paymentDescription, setPaymentDescription] = useState("");

  // İptal diyaloğu.
  const [cancelReason, setCancelReason] = useState("");

  const { data: purchase, isPending, isError, error } = useQuery({
    queryKey: ["purchase", purchaseId],
    queryFn: () => api<PurchaseResponse>(`/api/v1/purchases/${purchaseId}`),
  });

  const onSettled = () => {
    invalidatePurchaseQueries(queryClient, purchaseId);
    setConfirmOpen(false);
    setCancelOpen(false);
    setPaymentOpen(false);
    setDeleteOpen(false);
    setConfirmAmount("");
    setConfirmDescription("");
    setPaymentAmount("");
    setPaymentDescription("");
    setCancelReason("");
  };

  const confirmPurchase = useMutation({
    mutationFn: () => {
      const amount = confirmAmount.trim() ? parseMoneyInput(confirmAmount) : null;
      const body = {
        payment:
          amount && amount > 0
            ? {
                date: dateInputToIso(confirmDate),
                amount,
                description: confirmDescription || null,
              }
            : null,
      };
      return api<PurchaseResponse>(`/api/v1/purchases/${purchaseId}/confirm`, {
        method: "POST",
        body: JSON.stringify(body),
      });
    },
    onSuccess: () => toast.success("Alış onaylandı — stok girişi yapıldı, tedarikçi borcu yazıldı."),
    onError: (mutationError) =>
      toast.error(mutationError instanceof Error ? mutationError.message : "Onaylanamadı."),
    onSettled,
  });

  const cancelPurchase = useMutation({
    mutationFn: () =>
      api<PurchaseResponse>(`/api/v1/purchases/${purchaseId}/cancel`, {
        method: "POST",
        body: JSON.stringify({ reason: cancelReason }),
      }),
    onSuccess: () => toast.success("Alış iptal edildi — ters hareketler yazıldı."),
    onError: (mutationError) =>
      toast.error(mutationError instanceof Error ? mutationError.message : "İptal edilemedi."),
    onSettled,
  });

  const addPayment = useMutation({
    mutationFn: () => {
      const amount = parseMoneyInput(paymentAmount);
      if (amount === null || amount <= 0) {
        throw new Error("Ödeme tutarı 0'dan büyük olmalı.");
      }
      return api<PurchaseResponse>(`/api/v1/purchases/${purchaseId}/payments`, {
        method: "POST",
        body: JSON.stringify({
          date: dateInputToIso(paymentDate),
          amount,
          description: paymentDescription || null,
        }),
      });
    },
    onSuccess: () => toast.success("Ödeme kaydedildi."),
    onError: (mutationError) =>
      toast.error(mutationError instanceof Error ? mutationError.message : "Ödeme kaydedilemedi."),
    onSettled,
  });

  const deletePurchase = useMutation({
    mutationFn: () => api(`/api/v1/purchases/${purchaseId}`, { method: "DELETE" }),
    onSuccess: () => {
      toast.success("Taslak alış silindi.");
      queryClient.invalidateQueries({ queryKey: ["purchases"] });
      router.push("/purchases");
    },
    onError: (mutationError) =>
      toast.error(mutationError instanceof Error ? mutationError.message : "Silinemedi."),
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

  const isDraft = purchase.status === "Draft";
  const isCancelled = purchase.status === "Cancelled";
  const canPay = purchase.status === "Confirmed" || purchase.status === "PartiallyPaid";

  // Onay anındaki ödeme toplamı aşmamalı.
  const confirmAmountInvalid =
    confirmAmount.trim() !== "" &&
    (parseMoneyInput(confirmAmount) === null || (parseMoneyInput(confirmAmount) ?? 0) > purchase.total);
  const paymentAmountInvalid =
    paymentAmount.trim() === "" ||
    parseMoneyInput(paymentAmount) === null ||
    (parseMoneyInput(paymentAmount) ?? 0) <= 0 ||
    (parseMoneyInput(paymentAmount) ?? 0) > purchase.dueAmount;

  return (
    <div className="mx-auto grid w-full max-w-5xl gap-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="font-mono text-2xl font-semibold tracking-tight">{purchase.number}</h1>
            <Badge variant={purchaseStatusVariant(purchase.status)}>
              {PURCHASE_STATUS_LABELS[purchase.status]}
            </Badge>
          </div>
          <p className="text-sm text-muted-foreground">
            {formatDate(purchase.date)}
            {purchase.partyName ? ` · ${purchase.partyName}` : " · Nakit alış"} · {purchase.warehouseName}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {isDraft && (
            <>
              <Button
                onClick={() => {
                  setConfirmDate(todayInput());
                  setConfirmOpen(true);
                }}
              >
                <CheckCircle2 className="size-4" />
                Onayla
              </Button>
              <Button asChild variant="outline">
                <Link href={`/purchases/${purchase.id}/edit`}>
                  <Pencil className="size-4" />
                  Düzenle
                </Link>
              </Button>
              <Button variant="outline" onClick={() => setDeleteOpen(true)}>
                <Trash2 className="size-4" />
                Sil
              </Button>
            </>
          )}
          {canPay && (
            <Button
              onClick={() => {
                setPaymentDate(todayInput());
                setPaymentOpen(true);
              }}
            >
              <Plus className="size-4" />
              Ödeme Ekle
            </Button>
          )}
          {(canPay || purchase.status === "Paid") && (
            <Button variant="outline" onClick={() => setCancelOpen(true)}>
              <Ban className="size-4" />
              İptal Et
            </Button>
          )}
        </div>
      </div>

      {isCancelled && (
        <Card>
          <CardContent className="flex flex-wrap items-center gap-2 text-sm text-destructive">
            <Ban className="size-4" />
            <span>
              Belge {formatDate(purchase.cancelledAtUtc)} tarihinde iptal edildi: {purchase.cancelReason ?? "—"}
            </span>
          </CardContent>
        </Card>
      )}

      <div className="grid gap-4 sm:grid-cols-3">
        <Card>
          <CardContent className="grid gap-1">
            <span className="text-xs text-muted-foreground">Genel Toplam</span>
            <span className="text-xl font-semibold tabular-nums">{formatMoney(purchase.total)}</span>
            <span className="text-xs text-muted-foreground">
              Net {formatMoney(purchase.subTotal)} + KDV {formatMoney(purchase.vatTotal)}
            </span>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="grid gap-1">
            <span className="text-xs text-muted-foreground">Ödenen</span>
            <span className="text-xl font-semibold tabular-nums">{formatMoney(purchase.paidAmount)}</span>
            <span className="text-xs text-muted-foreground">{purchase.payments.length} ödeme kaydı</span>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="grid gap-1">
            <span className="text-xs text-muted-foreground">Kalan Borç</span>
            <span
              className={`text-xl font-semibold tabular-nums ${!isCancelled && purchase.dueAmount > 0 ? "text-destructive" : ""}`}
            >
              {formatMoney(purchase.dueAmount)}
            </span>
            {purchase.dueDate && (
              <span className="text-xs text-muted-foreground">Vade: {formatDate(purchase.dueDate)}</span>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardContent className="grid gap-2 text-sm sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <span className="text-xs text-muted-foreground">Tedarikçi</span>
            <p className="font-medium">{purchase.partyName ?? "Nakit (tedarikçisiz)"}</p>
          </div>
          <div>
            <span className="text-xs text-muted-foreground">Depo</span>
            <p className="font-medium">{purchase.warehouseName}</p>
          </div>
          <div>
            <span className="text-xs text-muted-foreground">Belge Tarihi</span>
            <p className="font-medium">{formatDate(purchase.date)}</p>
          </div>
          <div>
            <span className="text-xs text-muted-foreground">Onay / İptal</span>
            <p className="font-medium">
              {purchase.confirmedAtUtc ? formatDate(purchase.confirmedAtUtc) : "—"}
              {purchase.cancelledAtUtc ? ` → ${formatDate(purchase.cancelledAtUtc)}` : ""}
            </p>
          </div>
          {purchase.description && (
            <div className="sm:col-span-2 lg:col-span-4">
              <span className="text-xs text-muted-foreground">Açıklama</span>
              <p>{purchase.description}</p>
            </div>
          )}
        </CardContent>
      </Card>

      <div className="rounded-lg border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Ürün / Hizmet</TableHead>
              <TableHead className="text-right">Miktar</TableHead>
              <TableHead className="hidden text-right sm:table-cell">Birim Alış</TableHead>
              <TableHead className="hidden text-right md:table-cell">İskonto</TableHead>
              <TableHead className="hidden text-right md:table-cell">Net</TableHead>
              <TableHead className="hidden text-right sm:table-cell">KDV</TableHead>
              <TableHead className="text-right">Satır Toplamı</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {purchase.items.map((item) => (
              <TableRow key={item.id}>
                <TableCell>
                  <Link href={`/products/${item.productId}`} className="font-medium">
                    {item.productName}
                  </Link>
                </TableCell>
                <TableCell className="text-right tabular-nums">{formatQuantity(item.quantity)}</TableCell>
                <TableCell className="hidden text-right tabular-nums sm:table-cell">
                  {formatMoney(item.unitPrice)}
                </TableCell>
                <TableCell className="hidden text-right tabular-nums md:table-cell">
                  {item.discountRate > 0 ? `%${item.discountRate}` : "—"}
                </TableCell>
                <TableCell className="hidden text-right tabular-nums md:table-cell">
                  {formatMoney(item.netAmount)}
                </TableCell>
                <TableCell className="hidden text-right tabular-nums sm:table-cell">
                  {item.vatRate > 0 ? `${formatMoney(item.vatAmount)} (%${item.vatRate})` : "—"}
                </TableCell>
                <TableCell className="text-right font-medium tabular-nums">
                  {formatMoney(item.lineTotal)}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      <Card>
        <CardContent className="ml-auto grid w-full max-w-xs gap-2 text-sm">
          <div className="flex justify-between">
            <span className="text-muted-foreground">Ara Toplam</span>
            <span className="tabular-nums">{formatMoney(purchase.subTotal)}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">İskonto</span>
            <span className="tabular-nums text-destructive">−{formatMoney(purchase.discountTotal)}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">KDV</span>
            <span className="tabular-nums">{formatMoney(purchase.vatTotal)}</span>
          </div>
          <Separator />
          <div className="flex justify-between text-base font-medium">
            <span>Genel Toplam</span>
            <span className="tabular-nums">{formatMoney(purchase.total)}</span>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-2">
        <h2 className="text-sm font-medium">Ödemeler</h2>
        {purchase.payments.length === 0 ? (
          <p className="rounded-lg border border-dashed px-4 py-6 text-center text-sm text-muted-foreground">
            {canPay
              ? "Henüz ödeme yok. Kalan borç için ödeme ekleyin."
              : "Bu belgede ödeme yok."}
          </p>
        ) : (
          <div className="rounded-lg border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Tarih</TableHead>
                  <TableHead>Hesap</TableHead>
                  <TableHead className="text-right">Tutar</TableHead>
                  <TableHead className="hidden md:table-cell">Açıklama</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {purchase.payments.map((payment) => (
                  <TableRow key={payment.id}>
                    <TableCell className="text-muted-foreground">{formatDate(payment.date)}</TableCell>
                    <TableCell>
                      {payment.accountName}
                      {payment.paidOnConfirm && (
                        <Badge variant="secondary" className="ml-2">
                          Onay anında
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {formatMoney(payment.amount)}
                    </TableCell>
                    <TableCell className="hidden max-w-64 truncate text-muted-foreground md:table-cell">
                      {payment.description ?? "—"}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </div>

      {/* Onaylama — opsiyonel anlık ödeme ile birlikte, tek işlemde. */}
      <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Alışı Onayla</DialogTitle>
            <DialogDescription>
              Onay; stok girişi{purchase.partyId ? ", tedarikçi borcu" : ""}
              {confirmAmount.trim() ? " ve anlık ödemeyi tek işlemde yazar" : ""}. Sonrasında belge
              değiştirilemez.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4">
            <div className="grid gap-2">
              <Label htmlFor="confirm-amount">Anlık Ödeme (opsiyonel)</Label>
              <Input
                id="confirm-amount"
                inputMode="decimal"
                placeholder={`0,00 — belge toplamı ${formatMoney(purchase.total)}`}
                value={confirmAmount}
                onChange={(event) => setConfirmAmount(event.target.value)}
              />
              {confirmAmountInvalid && (
                <p className="text-sm text-destructive">
                  Tutar en fazla 2 basamak ondalıklı ve {formatMoney(purchase.total)} değerini aşmamalı.
                </p>
              )}
            </div>
            {confirmAmount.trim() !== "" && !confirmAmountInvalid && (
              <>
                <div className="grid gap-2">
                  <Label htmlFor="confirm-date">Ödeme Tarihi</Label>
                  <Input
                    id="confirm-date"
                    type="date"
                    value={confirmDate}
                    onChange={(event) => setConfirmDate(event.target.value)}
                  />
                </div>
                <div className="grid gap-2">
                  <Label htmlFor="confirm-description">Ödeme Açıklaması</Label>
                  <Input
                    id="confirm-description"
                    placeholder="Peşin, havale..."
                    value={confirmDescription}
                    onChange={(event) => setConfirmDescription(event.target.value)}
                  />
                </div>
              </>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmOpen(false)} disabled={confirmPurchase.isPending}>
              Vazgeç
            </Button>
            <Button
              disabled={confirmPurchase.isPending || confirmAmountInvalid}
              onClick={() => confirmPurchase.mutate()}
            >
              {confirmPurchase.isPending && <Loader2 className="size-4 animate-spin" />}
              Onayla
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Ödeme ekleme. */}
      <Dialog open={paymentOpen} onOpenChange={setPaymentOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Ödeme Ekle</DialogTitle>
            <DialogDescription>
              Kalan borç {formatMoney(purchase.dueAmount)} — ödeme kasadan çıkış ve cariye borç yazar.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4">
            <div className="grid gap-2">
              <Label htmlFor="payment-amount">Tutar *</Label>
              <Input
                id="payment-amount"
                inputMode="decimal"
                placeholder="0,00"
                value={paymentAmount}
                onChange={(event) => setPaymentAmount(event.target.value)}
              />
              {paymentAmount.trim() !== "" && paymentAmountInvalid && (
                <p className="text-sm text-destructive">
                  {"Tutar 0'dan büyük, en fazla "}
                  {formatMoney(purchase.dueAmount)}
                  {" olabilir."}
                </p>
              )}
            </div>
            <div className="grid gap-2">
              <Label htmlFor="payment-date">Tarih *</Label>
              <Input
                id="payment-date"
                type="date"
                value={paymentDate}
                onChange={(event) => setPaymentDate(event.target.value)}
              />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="payment-description">Açıklama</Label>
              <Input
                id="payment-description"
                placeholder="Havale, nakit..."
                value={paymentDescription}
                onChange={(event) => setPaymentDescription(event.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setPaymentOpen(false)} disabled={addPayment.isPending}>
              Vazgeç
            </Button>
            <Button disabled={addPayment.isPending || paymentAmountInvalid} onClick={() => addPayment.mutate()}>
              {addPayment.isPending && <Loader2 className="size-4 animate-spin" />}
              Ödemeyi Kaydet
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* İptal — gerekçeli, ters hareketler yazılır. */}
      <Dialog open={cancelOpen} onOpenChange={setCancelOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Alışı İptal Et</DialogTitle>
            <DialogDescription>
              Stok geri düşülür{purchase.partyId ? ", tedarikçi borcu kapatılır" : ""}
              {purchase.payments.length > 0 ? ", ödemeler kasaya iade edilir" : ""}. Kayıtlar silinmez;
              belge terminal duruma geçer.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-2">
            <Label htmlFor="cancel-reason">İptal Gerekçesi *</Label>
            <Textarea
              id="cancel-reason"
              rows={3}
              placeholder="Tedarikçi iptal etti, yanlış belge..."
              value={cancelReason}
              onChange={(event) => setCancelReason(event.target.value)}
            />
            {cancelReason.length > 300 && (
              <p className="text-sm text-destructive">Gerekçe en fazla 300 karakter olabilir.</p>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelOpen(false)} disabled={cancelPurchase.isPending}>
              Vazgeç
            </Button>
            <Button
              variant="destructive"
              disabled={
                cancelPurchase.isPending || cancelReason.trim().length === 0 || cancelReason.length > 300
              }
              onClick={() => cancelPurchase.mutate()}
            >
              {cancelPurchase.isPending && <Loader2 className="size-4 animate-spin" />}
              Belgeyi İptal Et
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Taslak silme. */}
      <Dialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Taslağı Sil</DialogTitle>
            <DialogDescription>
              {purchase.number} numaralı taslak kalıcı olarak silinir. Onaylanmış belgeler silinemez; iptal
              edilir.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteOpen(false)} disabled={deletePurchase.isPending}>
              Vazgeç
            </Button>
            <Button
              variant="destructive"
              disabled={deletePurchase.isPending}
              onClick={() => deletePurchase.mutate()}
            >
              {deletePurchase.isPending && <Loader2 className="size-4 animate-spin" />}
              Sil
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
