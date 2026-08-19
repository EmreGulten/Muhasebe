"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowDownCircle, ArrowUpCircle, Loader2, Pencil, Trash2 } from "lucide-react";
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Switch } from "@/components/ui/switch";
import { api } from "@/lib/api";
import { ACCOUNT_TYPE_LABELS, transactionTypeLabel } from "@/lib/accounts";
import { formatDate, formatMoney, isoToDateInput, parseMoneyInput } from "@/lib/parties";
import type { AccountDto, AccountStatementResponse } from "@/lib/types";

const PAGE_SIZE = 20;

/** Hesap ekstresi — tarih sırası, sayfa içi çalışan bakiye ve hesap eylemleri. */
export function CashDetail({ accountId }: { accountId: string }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [movementOpen, setMovementOpen] = useState(false);
  const [editOpen, setEditOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);

  const { data: account, isPending, isError, error } = useQuery({
    queryKey: ["account", accountId],
    queryFn: () => api<AccountDto>(`/api/v1/accounts/${accountId}`),
  });

  const { data: statement } = useQuery({
    queryKey: ["account-statement", accountId, page],
    queryFn: () =>
      api<AccountStatementResponse>(`/api/v1/accounts/${accountId}/statement?page=${page}&pageSize=${PAGE_SIZE}`),
  });

  // Hareket ekleme formu.
  const [direction, setDirection] = useState<"In" | "Out">("In");
  const [amount, setAmount] = useState("");
  const [date, setDate] = useState(() => isoToDateInput(new Date().toISOString()));
  const [description, setDescription] = useState("");
  const amountInvalid = parseMoneyInput(amount) === null || (parseMoneyInput(amount) ?? 0) <= 0;

  // Düzenleme formu.
  const [editName, setEditName] = useState("");
  const [editActive, setEditActive] = useState(true);

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ["accounts"] });
    queryClient.invalidateQueries({ queryKey: ["account", accountId] });
    queryClient.invalidateQueries({ queryKey: ["account-statement", accountId] });
  };

  const onSettled = () => {
    invalidate();
    setMovementOpen(false);
    setEditOpen(false);
    setDeleteOpen(false);
    setAmount("");
    setDescription("");
  };

  const addMovement = useMutation({
    mutationFn: () =>
      api(`/api/v1/accounts/${accountId}/transactions`, {
        method: "POST",
        body: JSON.stringify({
          direction,
          date: `${date}T00:00:00Z`,
          amount: parseMoneyInput(amount),
          description: description || null,
        }),
      }),
    onSuccess: () => toast.success("Hareket kaydedildi."),
    onError: (mutationError) =>
      toast.error(mutationError instanceof Error ? mutationError.message : "Hareket kaydedilemedi."),
    onSettled,
  });

  const editAccount = useMutation({
    mutationFn: () =>
      api<AccountDto>(`/api/v1/accounts/${accountId}`, {
        method: "PUT",
        body: JSON.stringify({ name: editName.trim(), isActive: editActive }),
      }),
    onSuccess: (updated) => {
      toast.success(`Hesap güncellendi: ${updated.name}`);
      onSettled();
    },
    onError: (mutationError) =>
      toast.error(mutationError instanceof Error ? mutationError.message : "Hesap güncellenemedi."),
    onSettled,
  });

  const deleteAccount = useMutation({
    mutationFn: () => api(`/api/v1/accounts/${accountId}`, { method: "DELETE" }),
    onSuccess: () => {
      toast.success("Hesap silindi.");
      queryClient.invalidateQueries({ queryKey: ["accounts"] });
      queryClient.invalidateQueries({ queryKey: ["account-statement", accountId] });
      router.push("/cash");
    },
    onError: (mutationError) =>
      toast.error(mutationError instanceof Error ? mutationError.message : "Hesap silinemedi."),
  });

  if (isPending) {
    return (
      <div className="flex justify-center py-12">
        <Loader2 className="size-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !account) {
    return (
      <Card className="mx-auto max-w-3xl">
        <CardContent className="grid justify-items-center gap-3 py-10 text-center">
          <p className="text-sm text-destructive">
            {error instanceof Error ? error.message : "Hesap bulunamadı."}
          </p>
          <Button asChild variant="outline">
            <Link href="/cash">Hesap listesine dön</Link>
          </Button>
        </CardContent>
      </Card>
    );
  }

  const totalPages = statement ? Math.max(1, Math.ceil(statement.totalCount / statement.pageSize)) : 1;

  return (
    <div className="mx-auto grid w-full max-w-5xl gap-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-2xl font-semibold tracking-tight">{account.name}</h1>
            <Badge variant="secondary">{ACCOUNT_TYPE_LABELS[account.type]}</Badge>
            {account.isDefault && <Badge variant="outline">Varsayılan</Badge>}
            {!account.isActive && <Badge variant="destructive">Pasif</Badge>}
          </div>
          <p className="text-sm text-muted-foreground">
            {account.currency} · {account.transactionCount} hareket
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button onClick={() => setMovementOpen(true)} disabled={!account.isActive}>
            <ArrowUpCircle className="size-4" />
            Hareket Ekle
          </Button>
          <Button
            variant="outline"
            onClick={() => {
              setEditName(account.name);
              setEditActive(account.isActive);
              setEditOpen(true);
            }}
          >
            <Pencil className="size-4" />
            Düzenle
          </Button>
          {!account.isDefault && (
            <Button variant="outline" onClick={() => setDeleteOpen(true)}>
              <Trash2 className="size-4" />
              Sil
            </Button>
          )}
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-3">
        <Card>
          <CardContent className="grid gap-1">
            <span className="text-xs text-muted-foreground">Güncel Bakiye</span>
            <span
              className={`text-xl font-semibold tabular-nums ${account.currentBalance < 0 ? "text-destructive" : ""}`}
            >
              {formatMoney(account.currentBalance)}
            </span>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="grid gap-1">
            <span className="text-xs text-muted-foreground">Açılış Bakiyesi</span>
            <span className="text-xl font-semibold tabular-nums">{formatMoney(account.openingBalance)}</span>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="grid gap-1">
            <span className="text-xs text-muted-foreground">Açılış Tarihi</span>
            <span className="text-xl font-semibold">{formatDate(account.createdAtUtc)}</span>
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-2">
        <h2 className="text-sm font-medium">Hareketler</h2>
        {statement && statement.items.length > 0 ? (
          <div className="rounded-lg border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Tarih</TableHead>
                  <TableHead>Tür</TableHead>
                  <TableHead className="hidden md:table-cell">Açıklama</TableHead>
                  <TableHead className="text-right">Tutar</TableHead>
                  <TableHead className="text-right">Bakiye</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {statement.items.map((transaction) => (
                  <TableRow key={transaction.id}>
                    <TableCell className="text-muted-foreground">{formatDate(transaction.date)}</TableCell>
                    <TableCell>{transactionTypeLabel(transaction.type)}</TableCell>
                    <TableCell className="hidden max-w-72 truncate text-muted-foreground md:table-cell">
                      {transaction.description ?? "—"}
                    </TableCell>
                    <TableCell
                      className={`text-right tabular-nums ${transaction.amount < 0 ? "text-destructive" : ""}`}
                    >
                      {formatMoney(transaction.amount)}
                    </TableCell>
                    <TableCell className="text-right font-medium tabular-nums">
                      {formatMoney(transaction.balance)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        ) : (
          <p className="rounded-lg border border-dashed px-4 py-6 text-center text-sm text-muted-foreground">
            Bu hesapta henüz hareket yok.
          </p>
        )}
      </div>

      {statement && statement.totalCount > statement.pageSize && (
        <div className="flex items-center justify-between text-sm">
          <span className="text-muted-foreground">
            Sayfa {statement.page} / {totalPages}
          </span>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
              Önceki
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={page >= totalPages}
              onClick={() => setPage(page + 1)}
            >
              Sonraki
            </Button>
          </div>
        </div>
      )}

      {/* Manuel hareket — giriş (tahsilat) / çıkış (ödeme). */}
      <Dialog open={movementOpen} onOpenChange={setMovementOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Hareket Ekle</DialogTitle>
            <DialogDescription>
              Giriş kasaya para koyar (tahsilat), çıkış kasadan para düşer (ödeme). Hareketler sonradan
              değiştirilemez; düzeltme ters hareketle yapılır.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4">
            <div className="grid gap-2">
              <Label htmlFor="movement-direction">Yön *</Label>
              <Select value={direction} onValueChange={(value) => setDirection(value as "In" | "Out")}>
                <SelectTrigger id="movement-direction">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="In">
                    <span className="flex items-center gap-2">
                      <ArrowDownCircle className="size-4" />
                      Giriş (Tahsilat)
                    </span>
                  </SelectItem>
                  <SelectItem value="Out">
                    <span className="flex items-center gap-2">
                      <ArrowUpCircle className="size-4" />
                      Çıkış (Ödeme)
                    </span>
                  </SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label htmlFor="movement-amount">Tutar *</Label>
              <Input
                id="movement-amount"
                inputMode="decimal"
                placeholder="0,00"
                value={amount}
                onChange={(event) => setAmount(event.target.value)}
              />
              {amount.trim() !== "" && amountInvalid && (
                <p className="text-sm text-destructive">Tutar 0&apos;dan büyük olmalı.</p>
              )}
            </div>
            <div className="grid gap-2">
              <Label htmlFor="movement-date">Tarih *</Label>
              <Input
                id="movement-date"
                type="date"
                value={date}
                onChange={(event) => setDate(event.target.value)}
              />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="movement-description">Açıklama</Label>
              <Input
                id="movement-description"
                placeholder="Kira ödemesi, çek tahsilatı..."
                value={description}
                onChange={(event) => setDescription(event.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setMovementOpen(false)} disabled={addMovement.isPending}>
              Vazgeç
            </Button>
            <Button disabled={addMovement.isPending || amountInvalid} onClick={() => addMovement.mutate()}>
              {addMovement.isPending && <Loader2 className="size-4 animate-spin" />}
              Kaydet
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Hesap düzenleme — ad + aktiflik. */}
      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Hesabı Düzenle</DialogTitle>
            <DialogDescription>
              Tür ve açılış bakiyesi değiştirilemez; düzeltme hareketle yapılır.
              {account.isDefault && " Varsayılan kasa pasifleştirilemez."}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4">
            <div className="grid gap-2">
              <Label htmlFor="edit-name">Hesap Adı *</Label>
              <Input
                id="edit-name"
                value={editName}
                onChange={(event) => setEditName(event.target.value)}
              />
            </div>
            <div className="flex items-center justify-between">
              <Label htmlFor="edit-active">Aktif</Label>
              <Switch
                id="edit-active"
                checked={editActive}
                disabled={account.isDefault}
                onCheckedChange={setEditActive}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditOpen(false)} disabled={editAccount.isPending}>
              Vazgeç
            </Button>
            <Button
              disabled={editAccount.isPending || editName.trim().length === 0}
              onClick={() => editAccount.mutate()}
            >
              {editAccount.isPending && <Loader2 className="size-4 animate-spin" />}
              Kaydet
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Hesap silme — yalnızca hareketsiz. */}
      <Dialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Hesabı Sil</DialogTitle>
            <DialogDescription>
              {account.name} kalıcı olarak silinir. Hareketi olan hesap silinemez; pasifleştirin.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteOpen(false)} disabled={deleteAccount.isPending}>
              Vazgeç
            </Button>
            <Button variant="destructive" disabled={deleteAccount.isPending} onClick={() => deleteAccount.mutate()}>
              {deleteAccount.isPending && <Loader2 className="size-4 animate-spin" />}
              Sil
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
