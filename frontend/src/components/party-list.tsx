"use client";

import { useQuery } from "@tanstack/react-query";
import { Loader2, Plus, Search } from "lucide-react";
import Link from "next/link";
import { useEffect, useState } from "react";

import { PartyForm } from "@/components/party-form";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { api } from "@/lib/api";
import { formatDate, formatMoney, PARTY_TYPE_LABELS } from "@/lib/parties";
import type { PagedResponse, PartySummaryDto, PartyTypeDto } from "@/lib/types";

const PAGE_SIZE = 20;

function balanceClass(balance: number): string {
  if (balance > 0) return "text-emerald-600 dark:text-emerald-400";
  if (balance < 0) return "text-destructive";
  return "text-muted-foreground";
}

/** Müşteri (/customers) ve tedarikçi (/suppliers) listelerinin paylaşılan görünümü. */
export function PartyList({
  type,
  basePath,
  title,
  singular,
}: {
  type: "Customer" | "Supplier";
  basePath: string;
  title: string;
  singular: string;
}) {
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [includeInactive, setIncludeInactive] = useState(true);
  const [page, setPage] = useState(1);
  const [createOpen, setCreateOpen] = useState(false);

  // Aramayı 300 ms beklet — her tuş vuruşunda istek atma.
  useEffect(() => {
    const timer = setTimeout(() => {
      setSearch(searchInput.trim());
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [searchInput]);

  const params = new URLSearchParams({
    type,
    includeInactive: String(includeInactive),
    page: String(page),
    pageSize: String(PAGE_SIZE),
  });
  if (search) {
    params.set("search", search);
  }

  const { data, isPending, isError, error } = useQuery({
    queryKey: ["parties", type, search, includeInactive, page],
    queryFn: () => api<PagedResponse<PartySummaryDto>>(`/api/v1/parties?${params.toString()}`),
  });

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <div className="mx-auto grid w-full max-w-6xl gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
          <p className="text-sm text-muted-foreground">
            {data ? `${data.totalCount} kayıt` : "Yükleniyor..."}
          </p>
        </div>
        <Button onClick={() => setCreateOpen(true)}>
          <Plus className="size-4" />
          Yeni {singular}
        </Button>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <div className="relative w-full max-w-xs">
          <Search className="absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            aria-label="Cari ara"
            placeholder="Ad, telefon, e-posta, vergi no..."
            className="pl-8"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
          />
        </div>
        <div className="flex items-center gap-2">
          <Switch
            id="include-inactive"
            checked={includeInactive}
            onCheckedChange={(checked) => {
              setIncludeInactive(checked);
              setPage(1);
            }}
          />
          <Label htmlFor="include-inactive" className="text-sm text-muted-foreground">
            Pasifleri göster
          </Label>
        </div>
      </div>

      {isPending ? (
        <div className="flex justify-center py-12">
          <Loader2 className="size-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError ? (
        <Card>
          <CardContent className="py-8 text-center text-sm text-destructive">
            {error instanceof Error ? error.message : "Cari listesi alınamadı."}
          </CardContent>
        </Card>
      ) : data.items.length === 0 ? (
        <Card>
          <CardContent className="grid justify-items-center gap-3 py-10 text-center">
            <p className="text-sm text-muted-foreground">
              {search
                ? `"${search}" aramasıyla eşleşen cari yok.`
                : `Henüz ${singular.toLowerCase()} kaydı yok. İlk ${singular.toLowerCase()}yı oluşturun.`}
            </p>
            <Button variant="outline" onClick={() => setCreateOpen(true)}>
              <Plus className="size-4" />
              Yeni {singular}
            </Button>
          </CardContent>
        </Card>
      ) : (
        <div className="rounded-lg border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Ad</TableHead>
                <TableHead className="hidden md:table-cell">Tür</TableHead>
                <TableHead className="hidden lg:table-cell">Telefon</TableHead>
                <TableHead className="hidden sm:table-cell">Şehir</TableHead>
                <TableHead className="text-right">Bakiye</TableHead>
                <TableHead className="hidden text-right sm:table-cell">Son Hareket</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((party) => (
                <TableRow key={party.id} className="cursor-pointer">
                  <TableCell>
                    <Link href={`${basePath}/${party.id}`} className="flex items-center gap-2 font-medium">
                      {party.name}
                      {!party.isActive && <Badge variant="secondary">Pasif</Badge>}
                    </Link>
                  </TableCell>
                  <TableCell className="hidden text-muted-foreground md:table-cell">
                    {PARTY_TYPE_LABELS[party.type as PartyTypeDto] ?? party.type}
                  </TableCell>
                  <TableCell className="hidden text-muted-foreground lg:table-cell">
                    {party.phone ?? "—"}
                  </TableCell>
                  <TableCell className="hidden text-muted-foreground sm:table-cell">
                    {party.city ?? "—"}
                  </TableCell>
                  <TableCell className={`text-right font-medium tabular-nums ${balanceClass(party.balance)}`}>
                    {formatMoney(party.balance)}
                  </TableCell>
                  <TableCell className="hidden text-right text-muted-foreground tabular-nums sm:table-cell">
                    {formatDate(party.lastTransactionDateUtc)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      {data && data.totalCount > data.pageSize && (
        <div className="flex items-center justify-between text-sm">
          <span className="text-muted-foreground">
            Sayfa {data.page} / {totalPages}
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

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>Yeni {singular}</DialogTitle>
            <DialogDescription>
              Zorunlu alan yalnızca addır. Açılış bakiyesi girerseniz tek seferlik açılış hareketi oluşur.
            </DialogDescription>
          </DialogHeader>
          <PartyForm mode="create" defaultType={type} onDone={() => setCreateOpen(false)} />
        </DialogContent>
      </Dialog>
    </div>
  );
}
