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
import { SALE_STATUS_LABELS, saleStatusVariant } from "@/lib/sales";
import type { SaleResponse } from "@/lib/types";

const todayInput = () => isoToDateInput(new Date().toISOString());

/** Belge ile değişen her şeyi geçersiz kıl: liste, belge, stok ve cari ekranları. */
function invalidateSaleQueries(queryClient: ReturnType<typeof useQueryClient>, saleId: string) {
  for (const key of ["sales", ["sale", saleId], "products", "product", "product-stock", "product-inventory", "parties", "party"]) {
    queryClient.invalidateQueries({ queryKey: typeof key === "string" ? [key] : key });
  }
}

/** Satış belgesi detayı: kalemler, tahsilatlar, onay/iptal/tahsilat eylemleri. */
export function SaleDetail({ saleId }: { saleId: string }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [paymentOpen, setPaymentOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);

  // Onay diyaloğu — anlık tahsilat opsiyonel.
  const [confirmDate, setConfirmDate] = useState(todayInput());
  const [confirmAmount, setConfirmAmount] = useState("");
  const [confirmDescription, setConfirmDescription] = useState("");

  // Tahsilat diyaloğu.
  const [paymentDate, setPaymentDate] = useState(todayInput());
  const [paymentAmount, setPaymentAmount] = useState("");
  const [paymentDescription, setPaymentDescription] = useState("");

  // İptal diyaloğu.
  const [cancelReason, setCancelReason] = useState("");

  const { data: sale, isPending, isError, error } = useQuery({
    queryKey: ["sale", saleId],
    queryFn: () => api<SaleResponse>(`/api/v1/sales/${saleId}`),
  });

  const onSettled = () => {
    invalidateSaleQueries(queryClient, saleId);
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

  const confirmSale = useMutation({
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
      return api<SaleResponse>(`/api/v1/sales/${saleId}/confirm`, {
        method: "POST",
        body: JSON.stringify(body),
      });
    },
    onSuccess: () => toast.success("Satış onaylandı — stok düşürüldü, cari borç yazıldı."),
    onError: (mutationError) =>
      toast.error(mutationError instanceof Error ? mutationError.message : "Onaylanamadı."),
    onSettled,
  });

  const cancelSale = useMutation({
    mutationFn: () =>
      api<SaleResponse>(`/api/v1/sales/${saleId}/cancel`, {
        method: "POST",
        body: JSON.stringify({ reason: cancelReason }),
      }),
    onSuccess: () => toast.success("Satış iptal edildi — ters hareketler yazıldı."),
    onError: (mutationError) =>
      toast.error(mutationError instanceof Error ? mutationError.message : "İptal edilemedi."),
    onSettled,
  });

  const addPayment = useMutation({
    mutationFn: () => {
      const amount = parseMoneyInput(paymentAmount);
      if (amount === null || amount <= 0) {
        throw new Error("Tahsilat tutarı 0'dan büyük olmalı.");
      }
      return api<SaleResponse>(`/api/v1/sales/${saleId}/payments`, {
        method: "POST",
        body: JSON.stringify({
          date: dateInputToIso(paymentDate),
          amount,
          description: paymentDescription || null,
        }),
      });
    },
    onSuccess: () => toast.success("Tahsilat kaydedildi."),
    onError: (mutationError) =>
      toast.error(mutationError instanceof Error ? mutationError.message : "Tahsilat kaydedilemedi."),
    onSettled,
  });

  const deleteSale = useMutation({
    mutationFn: () => api(`/api/v1/sales/${saleId}`, { method: "DELETE" }),
    onSuccess: () => {
      toast.success("Taslak satış silindi.");
      queryClient.invalidateQueries({ queryKey: ["sales"] });
      router.push("/sales");
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

  const isDraft = sale.status === "Draft";
  const isCancelled = sale.status === "Cancelled";
  const canCollect = sale.status === "Confirmed" || sale.status === "PartiallyPaid";

  // Onay anındaki tahsilat toplamı aşmamalı.
  const confirmAmountInvalid =
    confirmAmount.trim() !== "" &&
    (parseMoneyInput(confirmAmount) === null || (parseMoneyInput(confirmAmount) ?? 0) > sale.total);
  const paymentAmountInvalid =
    paymentAmount.trim() === "" ||
    parseMoneyInput(paymentAmount) === null ||
    (parseMoneyInput(paymentAmount) ?? 0) <= 0 ||
    (parseMoneyInput(paymentAmount) ?? 0) > sale.dueAmount;

  return (
    <div className="mx-auto grid w-full max-w-5xl gap-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="font-mono text-2xl font-semibold tracking-tight">{sale.number}</h1>
            <Badge variant={saleStatusVariant(sale.status)}>{SALE_STATUS_LABELS[sale.status]}</Badge>
          </div>
          <p className="text-sm text-muted-foreground">
            {formatDate(sale.date)}
            {sale.partyName ? ` · ${sale.partyName}` : " · Nakit satış"} · {sale.warehouseName}
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
                <Link href={`/sales/${sale.id}/edit`}>
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
          {canCollect && (
            <Button
              onClick={() => {
                setPaymentDate(todayInput());
                setPaymentOpen(true);
              }}
            >
              <Plus className="size-4" />
              Tahsilat Ekle
            </Button>
          )}
          {(canCollect || sale.status === "Paid") && (
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
              Belge {formatDate(sale.cancelledAtUtc)} tarihinde iptal edildi: {sale.cancelReason ?? "—"}
            </span>
          </CardContent>
        </Card>
      )}

      <div className="grid gap-4 sm:grid-cols-3">
        <Card>
          <CardContent className="grid gap-1">
            <span className="text-xs text-muted-foreground">Genel Toplam</span>
            <span className="text-xl font-semibold tabular-nums">{formatMoney(sale.total)}</span>
            <span className="text-xs text-muted-foreground">
              Net {formatMoney(sale.subTotal)} + KDV {formatMoney(sale.vatTotal)}
            </span>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="grid gap-1">
            <span className="text-xs text-muted-foreground">Tahsil Edilen</span>
            <span className="text-xl font-semibold tabular-nums">{formatMoney(sale.paidAmount)}</span>
            <span className="text-xs text-muted-foreground">{sale.payments.length} tahsilat kaydı</span>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="grid gap-1">
            <span className="text-xs text-muted-foreground">Kalan</span>
            <span
              className={`text-xl font-semibold tabular-nums ${!isCancelled && sale.dueAmount > 0 ? "text-destructive" : ""}`}
            >
              {formatMoney(sale.dueAmount)}
            </span>
            {sale.dueDate && (
              <span className="text-xs text-muted-foreground">Vade: {formatDate(sale.dueDate)}</span>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardContent className="grid gap-2 text-sm sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <span className="text-xs text-muted-foreground">Müşteri</span>
            <p className="font-medium">{sale.partyName ?? "Nakit (müşterisiz)"}</p>
          </div>
          <div>
            <span className="text-xs text-muted-foreground">Depo</span>
            <p className="font-medium">{sale.warehouseName}</p>
          </div>
          <div>
            <span className="text-xs text-muted-foreground">Belge Tarihi</span>
            <p className="font-medium">{formatDate(sale.date)}</p>
          </div>
          <div>
            <span className="text-xs text-muted-foreground">Onay / İptal</span>
            <p className="font-medium">
              {sale.confirmedAtUtc ? formatDate(sale.confirmedAtUtc) : "—"}
              {sale.cancelledAtUtc ? ` → ${formatDate(sale.cancelledAtUtc)}` : ""}
            </p>
          </div>
          {sale.description && (
            <div className="sm:col-span-2 lg:col-span-4">
              <span className="text-xs text-muted-foreground">Açıklama</span>
              <p>{sale.description}</p>
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
              <TableHead className="hidden text-right sm:table-cell">Birim Fiyat</TableHead>
              <TableHead className="hidden text-right md:table-cell">İskonto</TableHead>
              <TableHead className="hidden text-right md:table-cell">Net</TableHead>
              <TableHead className="hidden text-right sm:table-cell">KDV</TableHead>
              <TableHead className="text-right">Satır Toplamı</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {sale.items.map((item) => (
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
            <span className="tabular-nums">{formatMoney(sale.subTotal)}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">İskonto</span>
            <span className="tabular-nums text-destructive">−{formatMoney(sale.discountTotal)}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">KDV</span>
            <span className="tabular-nums">{formatMoney(sale.vatTotal)}</span>
          </div>
          <Separator />
          <div className="flex justify-between text-base font-medium">
            <span>Genel Toplam</span>
            <span className="tabular-nums">{formatMoney(sale.total)}</span>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-2">
        <h2 className="text-sm font-medium">Tahsilatlar</h2>
        {sale.payments.length === 0 ? (
          <p className="rounded-lg border border-dashed px-4 py-6 text-center text-sm text-muted-foreground">
            {canCollect
              ? "Henüz tahsilat yok. Kalan borç için tahsilat ekleyin."
              : "Bu belgede tahsilat yok."}
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
                {sale.payments.map((payment) => (
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

      {/* Onaylama — opsiyonel anlık tahsilat ile birlikte, tek işlemde. */}
      <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Satışı Onayla</DialogTitle>
            <DialogDescription>
              Onay; stok düşümü{sale.partyId ? ", cari borç" : ""}
              {confirmAmount.trim() ? " ve anlık tahsilatı tek işlemde yazar" : ""}. Sonrasında belge değiştirilemez.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4">
            <div className="grid gap-2">
              <Label htmlFor="confirm-amount">Anlık Tahsilat (opsiyonel)</Label>
              <Input
                id="confirm-amount"
                inputMode="decimal"
                placeholder={`0,00 — belge toplamı ${formatMoney(sale.total)}`}
                value={confirmAmount}
                onChange={(event) => setConfirmAmount(event.target.value)}
              />
              {confirmAmountInvalid && (
                <p className="text-sm text-destructive">
                  Tutar en fazla 2 basamak ondalıklı ve {formatMoney(sale.total)} değerini aşmamalı.
                </p>
              )}
            </div>
            {confirmAmount.trim() !== "" && !confirmAmountInvalid && (
              <>
                <div className="grid gap-2">
                  <Label htmlFor="confirm-date">Tahsilat Tarihi</Label>
                  <Input
                    id="confirm-date"
                    type="date"
                    value={confirmDate}
                    onChange={(event) => setConfirmDate(event.target.value)}
                  />
                </div>
                <div className="grid gap-2">
                  <Label htmlFor="confirm-description">Tahsilat Açıklaması</Label>
                  <Input
                    id="confirm-description"
                    placeholder="Peşin, kredi kartı..."
                    value={confirmDescription}
                    onChange={(event) => setConfirmDescription(event.target.value)}
                  />
                </div>
              </>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmOpen(false)} disabled={confirmSale.isPending}>
              Vazgeç
            </Button>
            <Button
              disabled={confirmSale.isPending || confirmAmountInvalid}
              onClick={() => confirmSale.mutate()}
            >
              {confirmSale.isPending && <Loader2 className="size-4 animate-spin" />}
              Onayla
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Tahsilat ekleme. */}
      <Dialog open={paymentOpen} onOpenChange={setPaymentOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Tahsilat Ekle</DialogTitle>
            <DialogDescription>
              Kalan borç {formatMoney(sale.dueAmount)} — tahsilat kasaya giriş ve cariye alacak yazar.
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
                  {formatMoney(sale.dueAmount)}
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
              Tahsilatı Kaydet
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* İptal — gerekçeli, ters hareketler yazılır. */}
      <Dialog open={cancelOpen} onOpenChange={setCancelOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Satışı İptal Et</DialogTitle>
            <DialogDescription>
              Stok geri eklenir{sale.partyId ? ", cari borç kapatılır" : ""}
              {sale.payments.length > 0 ? ", tahsilatlar kasadan iade edilir" : ""}. Kayıtlar silinmez;
              belge terminal duruma geçer.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-2">
            <Label htmlFor="cancel-reason">İptal Gerekçesi *</Label>
            <Textarea
              id="cancel-reason"
              rows={3}
              placeholder="Müşteri vazgeçti, yanlış belge..."
              value={cancelReason}
              onChange={(event) => setCancelReason(event.target.value)}
            />
            {cancelReason.length > 300 && (
              <p className="text-sm text-destructive">Gerekçe en fazla 300 karakter olabilir.</p>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelOpen(false)} disabled={cancelSale.isPending}>
              Vazgeç
            </Button>
            <Button
              variant="destructive"
              disabled={cancelSale.isPending || cancelReason.trim().length === 0 || cancelReason.length > 300}
              onClick={() => cancelSale.mutate()}
            >
              {cancelSale.isPending && <Loader2 className="size-4 animate-spin" />}
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
              {sale.number} numaralı taslak kalıcı olarak silinir. Onaylanmış belgeler silinemez; iptal
              edilir.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteOpen(false)} disabled={deleteSale.isPending}>
              Vazgeç
            </Button>
            <Button
              variant="destructive"
              disabled={deleteSale.isPending}
              onClick={() => deleteSale.mutate()}
            >
              {deleteSale.isPending && <Loader2 className="size-4 animate-spin" />}
              Sil
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
