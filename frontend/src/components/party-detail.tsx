"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowLeft,
  Loader2,
  Pencil,
  Plus,
  Trash2,
} from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";

import { PartyForm } from "@/components/party-form";
import { PartyTransactionForm } from "@/components/party-transaction-form";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { api } from "@/lib/api";
import {
  formatDate,
  formatMoney,
  PARTY_TYPE_LABELS,
  TRANSACTION_TYPE_LABELS,
  balanceLabel,
} from "@/lib/parties";
import type { PartyResponse, PartyStatementResponse, PartyTypeDto } from "@/lib/types";

const STATEMENT_PAGE_SIZE = 50;

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="grid gap-0.5">
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="text-sm">{value}</dd>
    </div>
  );
}

/** Cari kart detayı: özet kartları, kart bilgileri, ekstre ve hareket girişi. */
export function PartyDetail({ partyId, basePath }: { partyId: string; basePath: string }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [editOpen, setEditOpen] = useState(false);
  const [txOpen, setTxOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);

  const { data: party, isPending, isError, error } = useQuery({
    queryKey: ["party", partyId],
    queryFn: () => api<PartyResponse>(`/api/v1/parties/${partyId}`),
  });

  const { data: statement } = useQuery({
    queryKey: ["party-statement", partyId, page],
    queryFn: () =>
      api<PartyStatementResponse>(
        `/api/v1/parties/${partyId}/statement?page=${page}&pageSize=${STATEMENT_PAGE_SIZE}`,
      ),
    enabled: Boolean(party),
  });

  const remove = useMutation({
    mutationFn: () => api<void>(`/api/v1/parties/${partyId}`, { method: "DELETE" }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["parties"] });
      toast.success("Cari kartı silindi.");
      router.replace(basePath);
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : "Cari kartı silinemedi.");
    },
  });

  if (isPending) {
    return (
      <div className="flex justify-center py-16">
        <Loader2 className="size-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !party) {
    return (
      <div className="mx-auto grid w-full max-w-3xl gap-4">
        <Card>
          <CardContent className="grid justify-items-center gap-3 py-10 text-center">
            <p className="text-sm text-destructive">
              {error instanceof Error ? error.message : "Cari kartı bulunamadı."}
            </p>
            <Button asChild variant="outline">
              <Link href={basePath}>
                <ArrowLeft className="size-4" />
                Listeye Dön
              </Link>
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  const overLimit = party.creditLimit > 0 && party.balance > party.creditLimit;
  const totalPages = statement ? Math.max(1, Math.ceil(statement.totalCount / statement.pageSize)) : 1;

  return (
    <div className="mx-auto grid w-full max-w-6xl gap-4">
      <div className="flex flex-wrap items-center gap-2">
        <Button asChild variant="ghost" size="sm" className="gap-1 text-muted-foreground">
          <Link href={basePath}>
            <ArrowLeft className="size-4" />
            Listeye Dön
          </Link>
        </Button>
      </div>

      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex items-center gap-3">
          <h1 className="text-2xl font-semibold tracking-tight">{party.name}</h1>
          <Badge variant="outline">{PARTY_TYPE_LABELS[party.type as PartyTypeDto] ?? party.type}</Badge>
          {!party.isActive && <Badge variant="secondary">Pasif</Badge>}
          {overLimit && <Badge variant="destructive">Kredi limiti aşıldı</Badge>}
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => setEditOpen(true)}>
            <Pencil className="size-4" />
            Düzenle
          </Button>
          <Button onClick={() => setTxOpen(true)} disabled={!party.isActive}>
            <Plus className="size-4" />
            Hareket Ekle
          </Button>
          <Button variant="outline" className="text-destructive" onClick={() => setDeleteOpen(true)}>
            <Trash2 className="size-4" />
            Sil
          </Button>
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Bakiye</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-semibold tabular-nums">{formatMoney(party.balance)}</p>
            <p className="text-xs text-muted-foreground">{balanceLabel(party.balance)}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Toplam Borç</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-semibold tabular-nums">{formatMoney(party.totalDebit)}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Toplam Alacak</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-semibold tabular-nums">{formatMoney(party.totalCredit)}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Kredi Limiti</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-semibold tabular-nums">
              {party.creditLimit > 0 ? formatMoney(party.creditLimit) : "—"}
            </p>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Kart Bilgileri</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <InfoRow label="Vergi / TCKN" value={party.taxNumber ?? "—"} />
            <InfoRow label="Vergi Dairesi" value={party.taxOffice ?? "—"} />
            <InfoRow label="Yetkili" value={party.contactName ?? "—"} />
            <InfoRow label="Telefon" value={party.phone ?? "—"} />
            <InfoRow label="E-posta" value={party.email ?? "—"} />
            <InfoRow label="Açılış Bakiyesi" value={formatMoney(party.openingBalance)} />
            <InfoRow label="İl / İlçe" value={[party.city, party.district].filter(Boolean).join(" / ") || "—"} />
            <InfoRow label="Adres" value={party.address ?? "—"} />
            <InfoRow label="Son Hareket" value={formatDate(party.lastTransactionDateUtc)} />
          </dl>
          {party.notes && (
            <div className="mt-4 rounded-md bg-muted p-3 text-sm text-muted-foreground">{party.notes}</div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Cari Ekstre</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-3">
          {statement && statement.items.length === 0 ? (
            <p className="py-6 text-center text-sm text-muted-foreground">Henüz hareket yok.</p>
          ) : (
            <div className="rounded-lg border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Tarih</TableHead>
                    <TableHead className="hidden md:table-cell">Tür</TableHead>
                    <TableHead className="hidden sm:table-cell">Açıklama</TableHead>
                    <TableHead className="text-right">Borç</TableHead>
                    <TableHead className="text-right">Alacak</TableHead>
                    <TableHead className="text-right">Bakiye</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {statement?.items.map((item) => (
                    <TableRow key={item.id}>
                      <TableCell className="tabular-nums">{formatDate(item.date)}</TableCell>
                      <TableCell className="hidden md:table-cell">
                        {TRANSACTION_TYPE_LABELS[item.type] ?? item.type}
                      </TableCell>
                      <TableCell className="hidden max-w-64 truncate text-muted-foreground sm:table-cell">
                        {item.description ?? "—"}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {item.debit > 0 ? formatMoney(item.debit) : "—"}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {item.credit > 0 ? formatMoney(item.credit) : "—"}
                      </TableCell>
                      <TableCell className="text-right font-medium tabular-nums">
                        {formatMoney(item.balance)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}

          {statement && statement.balanceBeforePage !== 0 && (
            <p className="text-xs text-muted-foreground">
              Sayfa öncesi birikmiş bakiye: {formatMoney(statement.balanceBeforePage)}
            </p>
          )}

          {statement && statement.totalCount > statement.pageSize && (
            <div className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">
                {statement.totalCount} hareket · Sayfa {statement.page} / {totalPages}
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
        </CardContent>
      </Card>

      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>Cari Kartını Düzenle</DialogTitle>
            <DialogDescription>
              Açılış bakiyesi ve hareketler değiştirilemez; düzeltmeler &quot;Hareket Ekle&quot; ile yapılır.
            </DialogDescription>
          </DialogHeader>
          <PartyForm mode="edit" defaultType={party.type} party={party} onDone={() => setEditOpen(false)} />
        </DialogContent>
      </Dialog>

      <Dialog open={txOpen} onOpenChange={setTxOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-xl">
          <DialogHeader>
            <DialogTitle>Cari Hareketi Ekle</DialogTitle>
            <DialogDescription>
              {party.name} · Güncel bakiye {formatMoney(party.balance)}
            </DialogDescription>
          </DialogHeader>
          <PartyTransactionForm partyId={party.id} onDone={() => setTxOpen(false)} />
        </DialogContent>
      </Dialog>

      <Dialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Cari Kartını Sil</DialogTitle>
            <DialogDescription>
              {party.name} silinecek. Hareket geçmişi olan cariler silinemez; bu durumda kartı
              pasifleştirmeniz önerilir.
            </DialogDescription>
          </DialogHeader>
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => setDeleteOpen(false)} disabled={remove.isPending}>
              Vazgeç
            </Button>
            <Button
              variant="destructive"
              disabled={remove.isPending}
              onClick={() => remove.mutate()}
            >
              {remove.isPending && <Loader2 className="size-4 animate-spin" />}
              Sil
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
