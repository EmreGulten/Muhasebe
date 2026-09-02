"use client";

import { useQuery } from "@tanstack/react-query";
import { Loader2, Plus, Search } from "lucide-react";
import Link from "next/link";
import { useEffect, useState } from "react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
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
import { api } from "@/lib/api";
import { formatDate, formatMoney } from "@/lib/parties";
import { SALE_STATUSES, SALE_STATUS_LABELS, saleStatusVariant } from "@/lib/sales";
import type { PagedResponse, SaleStatusDto, SaleSummaryDto } from "@/lib/types";

const PAGE_SIZE = 20;

/** Satış listesi: durum filtresi, numara/müşteri araması, sayfalama. */
export function SaleList() {
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("all");
  const [page, setPage] = useState(1);

  useEffect(() => {
    const timer = setTimeout(() => {
      setSearch(searchInput.trim());
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [searchInput]);

  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(PAGE_SIZE),
  });
  if (search) {
    params.set("search", search);
  }
  if (status !== "all") {
    params.set("status", status);
  }

  const { data, isPending, isError, error } = useQuery({
    queryKey: ["sales", search, status, page],
    queryFn: () => api<PagedResponse<SaleSummaryDto>>(`/api/v1/sales?${params.toString()}`),
  });

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <div className="mx-auto grid w-full max-w-6xl gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Satışlar</h1>
          <p className="text-sm text-muted-foreground">
            {data ? `${data.totalCount} belge` : "Yükleniyor..."}
          </p>
        </div>
        <Button asChild>
          <Link href="/sales/new">
            <Plus className="size-4" />
            Yeni Satış
          </Link>
        </Button>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <div className="relative w-full max-w-xs">
          <Search className="absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            aria-label="Satış ara"
            placeholder="Belge no ya da müşteri adı..."
            className="pl-8"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
          />
        </div>
        <Select
          value={status}
          onValueChange={(value) => {
            setStatus(value);
            setPage(1);
          }}
        >
          <SelectTrigger className="w-44" aria-label="Durum filtresi">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Tüm durumlar</SelectItem>
            {SALE_STATUSES.map((entry) => (
              <SelectItem key={entry.value} value={entry.value}>
                {entry.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {isPending ? (
        <div className="flex justify-center py-12">
          <Loader2 className="size-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError ? (
        <Card>
          <CardContent className="py-8 text-center text-sm text-destructive">
            {error instanceof Error ? error.message : "Satış listesi alınamadı."}
          </CardContent>
        </Card>
      ) : data.items.length === 0 ? (
        <Card>
          <CardContent className="grid justify-items-center gap-3 py-10 text-center">
            <p className="text-sm text-muted-foreground">
              {status !== "all"
                ? `"${SALE_STATUS_LABELS[status as SaleStatusDto]}" durumunda belge yok.`
                : search
                  ? `"${search}" aramasıyla eşleşen belge yok.`
                  : "Henüz satış belgesi yok. İlk satışı oluşturun."}
            </p>
            {status === "all" && !search && (
              <Button asChild variant="outline">
                <Link href="/sales/new">
                  <Plus className="size-4" />
                  Yeni Satış
                </Link>
              </Button>
            )}
          </CardContent>
        </Card>
      ) : (
        <div className="rounded-lg border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Belge No</TableHead>
                <TableHead>Tarih</TableHead>
                <TableHead className="hidden sm:table-cell">Müşteri</TableHead>
                <TableHead className="text-right">Tutar</TableHead>
                <TableHead className="hidden text-right md:table-cell">Tahsil</TableHead>
                <TableHead>Durum</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((sale) => (
                <TableRow key={sale.id}>
                  <TableCell>
                    <Link href={`/sales/${sale.id}`} className="font-mono text-xs font-medium">
                      {sale.number}
                    </Link>
                  </TableCell>
                  <TableCell className="text-muted-foreground">{formatDate(sale.date)}</TableCell>
                  <TableCell className="hidden sm:table-cell">
                    {sale.partyName ?? <span className="text-muted-foreground">Nakit</span>}
                  </TableCell>
                  <TableCell className="text-right tabular-nums">{formatMoney(sale.total)}</TableCell>
                  <TableCell className="hidden text-right tabular-nums md:table-cell">
                    {formatMoney(sale.paidAmount)}
                  </TableCell>
                  <TableCell>
                    <Badge variant={saleStatusVariant(sale.status)}>{SALE_STATUS_LABELS[sale.status]}</Badge>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      {data && data.totalCount > data.pageSize && (
        <div className="flex flex-col gap-3 text-sm sm:flex-row sm:items-center sm:justify-between">
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
    </div>
  );
}
