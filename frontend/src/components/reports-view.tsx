"use client";

import { useQuery } from "@tanstack/react-query";
import { AlertTriangle, Loader2, ReceiptText, Warehouse } from "lucide-react";
import { useState } from "react";

import { SingleBarChart } from "@/components/report-charts";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { api } from "@/lib/api";
import { formatDate, formatMoney, isoToDateInput } from "@/lib/parties";
import { formatQuantity } from "@/lib/products";
import type {
  ReceivablesReportResponse,
  SalesReportResponse,
  StockReportResponse,
} from "@/lib/types";

/** Dönem varsayılanı: içinde bulunulan ay (backend varsayılanıyla aynı). */
function defaultFrom(): string {
  const now = new Date();
  return isoToDateInput(new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1)).toISOString());
}

/** Raporlar: alacaklar, stok ve satış raporu — tümü salt okunur. */
export function ReportsView() {
  const [from, setFrom] = useState(defaultFrom);
  const [to, setTo] = useState(() => isoToDateInput(new Date().toISOString()));

  const receivables = useQuery({
    queryKey: ["reports-receivables"],
    queryFn: () => api<ReceivablesReportResponse>("/api/v1/reports/receivables"),
  });

  const stock = useQuery({
    queryKey: ["reports-stock"],
    queryFn: () => api<StockReportResponse>("/api/v1/reports/stock"),
  });

  const sales = useQuery({
    queryKey: ["reports-sales", from, to],
    queryFn: () =>
      api<SalesReportResponse>(`/api/v1/reports/sales?from=${from}T00:00:00Z&to=${to}T00:00:00Z`),
  });

  return (
    <div className="mx-auto grid w-full max-w-6xl gap-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Raporlar</h1>
        <p className="text-sm text-muted-foreground">
          Alacaklar, stok değeri ve satış dökümü — onaylı belgelerden, iptaller hariç.
        </p>
      </div>

      {/* ---- Alacaklar */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="flex items-center gap-2 text-base">
            <ReceiptText className="size-4" />
            Alacaklar
          </CardTitle>
        </CardHeader>
        <CardContent className="grid gap-3">
          {receivables.isPending ? (
            <div className="flex justify-center py-8">
              <Loader2 className="size-5 animate-spin text-muted-foreground" />
            </div>
          ) : receivables.isError ? (
            <p className="py-4 text-center text-sm text-destructive">
              {receivables.error instanceof Error ? receivables.error.message : "Rapor alınamadı."}
            </p>
          ) : receivables.data.items.length === 0 ? (
            <p className="py-4 text-center text-sm text-muted-foreground">Borçlu müşteri yok.</p>
          ) : (
            <>
              <p className="text-sm text-muted-foreground">
                Toplam alacak <span className="font-medium text-foreground">{formatMoney(receivables.data.totalReceivable)}</span>
                {" · "}gecikmiş <span className="font-medium text-foreground">{formatMoney(receivables.data.totalOverdue)}</span>{" "}
                ({receivables.data.overdueCount} müşteri)
              </p>
              <div className="rounded-lg border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Müşteri</TableHead>
                      <TableHead className="hidden sm:table-cell">Telefon</TableHead>
                      <TableHead className="text-right">Bakiye</TableHead>
                      <TableHead className="text-right">Gecikmiş</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {receivables.data.items.map((row) => (
                      <TableRow key={row.partyId}>
                        <TableCell>{row.partyName}</TableCell>
                        <TableCell className="hidden text-muted-foreground sm:table-cell">
                          {row.phone ?? "—"}
                        </TableCell>
                        <TableCell className="text-right tabular-nums">{formatMoney(row.balance)}</TableCell>
                        <TableCell className="text-right tabular-nums">
                          {row.overdueAmount > 0 ? (
                            <span className="text-destructive">{formatMoney(row.overdueAmount)}</span>
                          ) : (
                            "—"
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            </>
          )}
        </CardContent>
      </Card>

      {/* ---- Stok */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="flex items-center gap-2 text-base">
            <Warehouse className="size-4" />
            Stok
          </CardTitle>
        </CardHeader>
        <CardContent className="grid gap-3">
          {stock.isPending ? (
            <div className="flex justify-center py-8">
              <Loader2 className="size-5 animate-spin text-muted-foreground" />
            </div>
          ) : stock.isError ? (
            <p className="py-4 text-center text-sm text-destructive">
              {stock.error instanceof Error ? stock.error.message : "Rapor alınamadı."}
            </p>
          ) : stock.data.items.length === 0 ? (
            <p className="py-4 text-center text-sm text-muted-foreground">Stoklu ürün yok.</p>
          ) : (
            <>
              <p className="text-sm text-muted-foreground">
                Toplam stok değeri (maliyet){" "}
                <span className="font-medium text-foreground">{formatMoney(stock.data.totalValue)}</span>
                {" · "}kritik stok <span className="font-medium text-foreground">{stock.data.criticalCount}</span> ürün
              </p>
              <div className="rounded-lg border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Ürün</TableHead>
                      <TableHead className="hidden sm:table-cell">Stok Kodu</TableHead>
                      <TableHead className="text-right">Eldeki</TableHead>
                      <TableHead className="hidden text-right md:table-cell">Eşik</TableHead>
                      <TableHead className="text-right">Değer</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {stock.data.items.map((row) => (
                      <TableRow key={row.productId}>
                        <TableCell>
                          <span className="flex items-center gap-2">
                            {row.productName}
                            {row.isCritical && (
                              <Badge variant="outline" className="gap-1 border-destructive/40 text-destructive">
                                <AlertTriangle className="size-3" />
                                Kritik
                              </Badge>
                            )}
                          </span>
                        </TableCell>
                        <TableCell className="hidden text-muted-foreground sm:table-cell">{row.sku ?? "—"}</TableCell>
                        <TableCell className="text-right tabular-nums">{formatQuantity(row.onHand)}</TableCell>
                        <TableCell className="hidden text-right tabular-nums text-muted-foreground md:table-cell">
                          {row.criticalLevel > 0 ? formatQuantity(row.criticalLevel) : "—"}
                        </TableCell>
                        <TableCell className="text-right tabular-nums">{formatMoney(row.stockValue)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            </>
          )}
        </CardContent>
      </Card>

      {/* ---- Satış raporu */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Satış Raporu</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4">
          <div className="flex flex-wrap items-end gap-3">
            <div className="grid gap-1">
              <span className="text-xs text-muted-foreground">Başlangıç</span>
              <Input
                aria-label="Başlangıç tarihi"
                type="date"
                className="w-40"
                value={from}
                onChange={(event) => setFrom(event.target.value)}
              />
            </div>
            <span className="pb-2 text-sm text-muted-foreground">–</span>
            <div className="grid gap-1">
              <span className="text-xs text-muted-foreground">Bitiş</span>
              <Input
                aria-label="Bitiş tarihi"
                type="date"
                className="w-40"
                value={to}
                onChange={(event) => setTo(event.target.value)}
              />
            </div>
          </div>

          {sales.isPending ? (
            <div className="flex justify-center py-8">
              <Loader2 className="size-5 animate-spin text-muted-foreground" />
            </div>
          ) : sales.isError ? (
            <p className="py-4 text-center text-sm text-destructive">
              {sales.error instanceof Error ? sales.error.message : "Rapor alınamadı."}
            </p>
          ) : sales.data.totalCount === 0 ? (
            <p className="py-4 text-center text-sm text-muted-foreground">Bu dönemde onaylı satış yok.</p>
          ) : (
            <>
              {/* Dönem özeti. */}
              <div className="grid gap-4 sm:grid-cols-4">
                {[
                  { label: "Satış Adedi", value: String(sales.data.totalCount) },
                  { label: "Toplam Tutar", value: formatMoney(sales.data.totalAmount) },
                  { label: "Toplam KDV", value: formatMoney(sales.data.totalVat) },
                  { label: "Ortalama Satış", value: formatMoney(sales.data.averageSale) },
                ].map((item) => (
                  <div key={item.label} className="grid gap-1 rounded-lg border p-3">
                    <span className="text-xs text-muted-foreground">{item.label}</span>
                    <span className="text-lg font-semibold tabular-nums">{item.value}</span>
                  </div>
                ))}
              </div>

              {/* Günlük döküm grafiği. */}
              <SingleBarChart
                items={sales.data.byDay.map((day) => ({
                  label: formatDate(day.date),
                  title: `${formatDate(day.date)} · ${day.count} satış · ${formatMoney(day.total)}`,
                  primary: day.total,
                }))}
              />

              <div className="grid gap-4 lg:grid-cols-2">
                {/* Müşteri bazlı. */}
                <div className="rounded-lg border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Müşteri</TableHead>
                        <TableHead className="text-right">Adet</TableHead>
                        <TableHead className="text-right">Tutar</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {sales.data.byCustomer.map((row) => (
                        <TableRow key={row.partyId ?? "cash"}>
                          <TableCell>{row.partyName}</TableCell>
                          <TableCell className="text-right tabular-nums text-muted-foreground">{row.count}</TableCell>
                          <TableCell className="text-right tabular-nums">{formatMoney(row.total)}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>

                {/* Ürün bazlı. */}
                <div className="rounded-lg border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Ürün</TableHead>
                        <TableHead className="text-right">Miktar</TableHead>
                        <TableHead className="text-right">Tutar</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {sales.data.byProduct.map((row) => (
                        <TableRow key={row.productId}>
                          <TableCell>{row.productName}</TableCell>
                          <TableCell className="text-right tabular-nums text-muted-foreground">
                            {formatQuantity(row.quantity)}
                          </TableCell>
                          <TableCell className="text-right tabular-nums">{formatMoney(row.total)}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
