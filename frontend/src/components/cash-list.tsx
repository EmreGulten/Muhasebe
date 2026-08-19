"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeftRight, Landmark, Loader2, Plus } from "lucide-react";
import Link from "next/link";
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
import { api } from "@/lib/api";
import { ACCOUNT_TYPES, ACCOUNT_TYPE_LABELS } from "@/lib/accounts";
import { formatMoney, isoToDateInput, parseMoneyInput } from "@/lib/parties";
import type { AccountDto, AccountTypeDto } from "@/lib/types";

/** Hesap ile değişen her şeyi geçersiz kıl: liste, hesap ve ekstreler. */
function invalidateAccountQueries(queryClient: ReturnType<typeof useQueryClient>) {
  queryClient.invalidateQueries({ queryKey: ["accounts"] });
  queryClient.invalidateQueries({ queryKey: ["account"] });
  queryClient.invalidateQueries({ queryKey: ["account-statement"] });
}

/** Kasa & Banka listesi: hesap kartları, yeni hesap ve transfer diyaloğu. */
export function CashList() {
  const queryClient = useQueryClient();
  const [createOpen, setCreateOpen] = useState(false);
  const [transferOpen, setTransferOpen] = useState(false);

  const { data: accounts, isPending, isError, error } = useQuery({
    queryKey: ["accounts"],
    queryFn: () => api<AccountDto[]>("/api/v1/accounts"),
  });

  // Yeni hesap formu.
  const [name, setName] = useState("");
  const [type, setType] = useState<AccountTypeDto>("Bank");
  const [openingBalance, setOpeningBalance] = useState("");
  const nameInvalid = name.trim().length === 0 || name.trim().length > 100;
  const openingInvalid =
    openingBalance.trim() !== "" &&
    (parseMoneyInput(openingBalance) === null || (parseMoneyInput(openingBalance) ?? -1) < 0);

  const createAccount = useMutation({
    mutationFn: () =>
      api<AccountDto>("/api/v1/accounts", {
        method: "POST",
        body: JSON.stringify({
          name: name.trim(),
          type,
          currency: null,
          openingBalance: parseMoneyInput(openingBalance) ?? 0,
        }),
      }),
    onSuccess: (account) => {
      invalidateAccountQueries(queryClient);
      toast.success(`Hesap oluşturuldu: ${account.name}`);
      setCreateOpen(false);
      setName("");
      setType("Bank");
      setOpeningBalance("");
    },
    onError: (mutationError) =>
      toast.error(mutationError instanceof Error ? mutationError.message : "Hesap oluşturulamadı."),
  });

  // Transfer formu.
  const activeAccounts = (accounts ?? []).filter((account) => account.isActive);
  const [fromId, setFromId] = useState("");
  const [toId, setToId] = useState("");
  const [amount, setAmount] = useState("");
  const [date, setDate] = useState(() => isoToDateInput(new Date().toISOString()));
  const [description, setDescription] = useState("");
  const amountInvalid = parseMoneyInput(amount) === null || (parseMoneyInput(amount) ?? 0) <= 0;
  const transferInvalid =
    !fromId || !toId || fromId === toId || amountInvalid || activeAccounts.length < 2;

  const transfer = useMutation({
    mutationFn: () =>
      api("/api/v1/accounts/transfer", {
        method: "POST",
        body: JSON.stringify({
          fromAccountId: fromId,
          toAccountId: toId,
          date: `${date}T00:00:00Z`,
          amount: parseMoneyInput(amount),
          description: description || null,
        }),
      }),
    onSuccess: () => {
      invalidateAccountQueries(queryClient);
      toast.success("Transfer gerçekleştirildi.");
      setTransferOpen(false);
      setFromId("");
      setToId("");
      setAmount("");
      setDescription("");
    },
    onError: (mutationError) =>
      toast.error(mutationError instanceof Error ? mutationError.message : "Transfer yapılamadı."),
  });

  const totalBalance = (accounts ?? [])
    .filter((account) => account.isActive)
    .reduce((sum, account) => sum + account.currentBalance, 0);

  return (
    <div className="mx-auto grid w-full max-w-6xl gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Kasa & Banka</h1>
          <p className="text-sm text-muted-foreground">
            {accounts
              ? `${accounts.length} hesap · toplam ${formatMoney(totalBalance)}`
              : "Yükleniyor..."}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => setTransferOpen(true)}>
            <ArrowLeftRight className="size-4" />
            Transfer
          </Button>
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="size-4" />
            Yeni Hesap
          </Button>
        </div>
      </div>

      {isPending ? (
        <div className="flex justify-center py-12">
          <Loader2 className="size-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError ? (
        <Card>
          <CardContent className="py-8 text-center text-sm text-destructive">
            {error instanceof Error ? error.message : "Hesap listesi alınamadı."}
          </CardContent>
        </Card>
      ) : accounts.length === 0 ? (
        <Card>
          <CardContent className="grid justify-items-center gap-3 py-10 text-center">
            <p className="text-sm text-muted-foreground">
              Henüz hesap yok. İlk tahsilat ya da ödemede &quot;Kasa&quot; otomatik oluşur; banka hesabı
              ekleyebilirsiniz.
            </p>
            <Button asChild variant="outline">
              <Link href="/sales/new">
                <Plus className="size-4" />
                İlk Satışı Yap
              </Link>
            </Button>
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {accounts.map((account) => (
            <Link key={account.id} href={`/cash/${account.id}`} className="group">
              <Card className="h-full transition-colors group-hover:border-primary/50">
                <CardContent className="grid gap-2">
                  <div className="flex items-center justify-between gap-2">
                    <div className="flex items-center gap-2">
                      <Landmark className="size-4 text-muted-foreground" />
                      <span className="font-medium">{account.name}</span>
                    </div>
                    <Badge variant="secondary">{ACCOUNT_TYPE_LABELS[account.type]}</Badge>
                  </div>
                  <span
                    className={`text-2xl font-semibold tabular-nums ${account.currentBalance < 0 ? "text-destructive" : ""}`}
                  >
                    {formatMoney(account.currentBalance)}
                  </span>
                  <p className="text-xs text-muted-foreground">
                    {account.currency} · {account.transactionCount} hareket
                    {account.openingBalance > 0 ? ` · açılış ${formatMoney(account.openingBalance)}` : ""}
                  </p>
                  <div className="flex flex-wrap gap-1">
                    {account.isDefault && <Badge variant="outline">Varsayılan</Badge>}
                    {!account.isActive && <Badge variant="destructive">Pasif</Badge>}
                  </div>
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      )}

      {/* Yeni hesap. */}
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Yeni Hesap</DialogTitle>
            <DialogDescription>
              Açılış bakiyesi verirseniz hesap tek seferlik açılış hareketiyle deftere girer.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4">
            <div className="grid gap-2">
              <Label htmlFor="account-name">Hesap Adı *</Label>
              <Input
                id="account-name"
                placeholder="Garanti Vadesiz, Kasa 2..."
                value={name}
                onChange={(event) => setName(event.target.value)}
              />
              {name.trim().length > 100 && (
                <p className="text-sm text-destructive">Hesap adı en fazla 100 karakter olabilir.</p>
              )}
            </div>
            <div className="grid gap-2">
              <Label htmlFor="account-type">Tür *</Label>
              <Select value={type} onValueChange={(value) => setType(value as AccountTypeDto)}>
                <SelectTrigger id="account-type">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {ACCOUNT_TYPES.map((entry) => (
                    <SelectItem key={entry.value} value={entry.value}>
                      {entry.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label htmlFor="account-opening">Açılış Bakiyesi</Label>
              <Input
                id="account-opening"
                inputMode="decimal"
                placeholder="0,00"
                value={openingBalance}
                onChange={(event) => setOpeningBalance(event.target.value)}
              />
              {openingInvalid && (
                <p className="text-sm text-destructive">
                  Bakiye 0 ya da pozitif, en fazla 2 basamak ondalıklı olmalı.
                </p>
              )}
            </div>
            <p className="text-xs text-muted-foreground">Para birimi: TRY</p>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)} disabled={createAccount.isPending}>
              Vazgeç
            </Button>
            <Button
              disabled={createAccount.isPending || nameInvalid || openingInvalid}
              onClick={() => createAccount.mutate()}
            >
              {createAccount.isPending && <Loader2 className="size-4 animate-spin" />}
              Oluştur
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Hesaplar arası transfer. */}
      <Dialog open={transferOpen} onOpenChange={setTransferOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Transfer</DialogTitle>
            <DialogDescription>
              Tek işlemde çıkış + giriş çifti yazılır; kayıtlar sonradan değiştirilemez.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4">
            <div className="grid gap-2">
              <Label htmlFor="transfer-from">Kaynak Hesap *</Label>
              <Select value={fromId || undefined} onValueChange={setFromId}>
                <SelectTrigger id="transfer-from">
                  <SelectValue placeholder="Hesap seçin" />
                </SelectTrigger>
                <SelectContent>
                  {activeAccounts.map((account) => (
                    <SelectItem key={account.id} value={account.id}>
                      {account.name} ({formatMoney(account.currentBalance)})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label htmlFor="transfer-to">Hedef Hesap *</Label>
              <Select value={toId || undefined} onValueChange={setToId}>
                <SelectTrigger id="transfer-to">
                  <SelectValue placeholder="Hesap seçin" />
                </SelectTrigger>
                <SelectContent>
                  {activeAccounts.map((account) => (
                    <SelectItem key={account.id} value={account.id}>
                      {account.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {fromId !== "" && fromId === toId && (
                <p className="text-sm text-destructive">Kaynak ve hedef hesap aynı olamaz.</p>
              )}
            </div>
            <div className="grid gap-2">
              <Label htmlFor="transfer-amount">Tutar *</Label>
              <Input
                id="transfer-amount"
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
              <Label htmlFor="transfer-date">Tarih *</Label>
              <Input
                id="transfer-date"
                type="date"
                value={date}
                onChange={(event) => setDate(event.target.value)}
              />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="transfer-description">Açıklama</Label>
              <Input
                id="transfer-description"
                placeholder="Bankaya yatırım..."
                value={description}
                onChange={(event) => setDescription(event.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setTransferOpen(false)} disabled={transfer.isPending}>
              Vazgeç
            </Button>
            <Button
              disabled={transfer.isPending || transferInvalid}
              onClick={() => transfer.mutate()}
            >
              {transfer.isPending && <Loader2 className="size-4 animate-spin" />}
              Transferi Yap
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
