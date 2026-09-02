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

import { InventoryMovementForm } from "@/components/inventory-movement-form";
import { ProductForm } from "@/components/product-form";
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
import { formatDate, formatMoney } from "@/lib/parties";
import { INVENTORY_TYPE_LABELS, formatQuantity, stockClass } from "@/lib/products";
import type {
  InventoryTransactionDto,
  PagedResponse,
  ProductResponse,
  ProductStockResponse,
} from "@/lib/types";

const INVENTORY_PAGE_SIZE = 50;

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="grid gap-0.5">
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="text-sm">{value}</dd>
    </div>
  );
}

/** Ürün kartı detayı: stok özeti, kart bilgileri, depo dökümü ve hareket geçmişi. */
export function ProductDetail({ productId }: { productId: string }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [editOpen, setEditOpen] = useState(false);
  const [movementOpen, setMovementOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);

  const { data: product, isPending, isError, error } = useQuery({
    queryKey: ["product", productId],
    queryFn: () => api<ProductResponse>(`/api/v1/products/${productId}`),
  });

  const { data: stock } = useQuery({
    queryKey: ["product-stock", productId],
    queryFn: () => api<ProductStockResponse>(`/api/v1/products/${productId}/stock`),
    enabled: Boolean(product) && !product?.isService,
  });

  const { data: movements } = useQuery({
    queryKey: ["product-inventory", productId, page],
    queryFn: () =>
      api<PagedResponse<InventoryTransactionDto>>(
        `/api/v1/products/${productId}/inventory?page=${page}&pageSize=${INVENTORY_PAGE_SIZE}`,
      ),
    enabled: Boolean(product) && !product?.isService,
  });

  const remove = useMutation({
    mutationFn: () => api<void>(`/api/v1/products/${productId}`, { method: "DELETE" }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["products"] });
      toast.success("Ürün silindi.");
      router.replace("/products");
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : "Ürün silinemedi.");
    },
  });

  if (isPending) {
    return (
      <div className="flex justify-center py-16">
        <Loader2 className="size-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !product) {
    return (
      <div className="mx-auto grid w-full max-w-3xl gap-4">
        <Card>
          <CardContent className="grid justify-items-center gap-3 py-10 text-center">
            <p className="text-sm text-destructive">
              {error instanceof Error ? error.message : "Ürün bulunamadı."}
            </p>
            <Button asChild variant="outline">
              <Link href="/products">
                <ArrowLeft className="size-4" />
                Listeye Dön
              </Link>
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  const unitSuffix = product.unitName ? ` ${product.unitName}` : "";
  const totalPages = movements ? Math.max(1, Math.ceil(movements.totalCount / movements.pageSize)) : 1;

  return (
    <div className="mx-auto grid w-full max-w-6xl gap-4">
      <div className="flex flex-wrap items-center gap-2">
        <Button asChild variant="ghost" size="sm" className="gap-1 text-muted-foreground">
          <Link href="/products">
            <ArrowLeft className="size-4" />
            Listeye Dön
          </Link>
        </Button>
      </div>

      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex items-center gap-3">
          <h1 className="text-2xl font-semibold tracking-tight">{product.name}</h1>
          {product.isService && <Badge variant="outline">Hizmet</Badge>}
          {!product.isActive && <Badge variant="secondary">Pasif</Badge>}
          {product.isCritical && !product.isService && <Badge variant="destructive">Kritik stok</Badge>}
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => setEditOpen(true)}>
            <Pencil className="size-4" />
            Düzenle
          </Button>
          <Button
            onClick={() => setMovementOpen(true)}
            disabled={product.isService || !product.isActive}
          >
            <Plus className="size-4" />
            Stok Hareketi Ekle
          </Button>
          <Button variant="outline" className="text-destructive" onClick={() => setDeleteOpen(true)}>
            <Trash2 className="size-4" />
            Sil
          </Button>
        </div>
      </div>

      {product.isService ? (
        <Card>
          <CardContent className="py-6 text-center text-sm text-muted-foreground">
            Hizmet kaydında stok takibi yapılmaz.
          </CardContent>
        </Card>
      ) : (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">Güncel Stok</CardTitle>
              </CardHeader>
              <CardContent>
                <p className={`text-2xl font-semibold tabular-nums ${stockClass(product.currentStock, product.isCritical)}`}>
                  {formatQuantity(product.currentStock)}
                  {unitSuffix}
                </p>
                {product.isCritical && (
                  <p className="text-xs text-destructive">Kritik eşiğin altında.</p>
                )}
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">Kritik Eşik</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-2xl font-semibold tabular-nums">
                  {product.minimumStock > 0 ? `${formatQuantity(product.minimumStock)}${unitSuffix}` : "—"}
                </p>
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">Satış Fiyatı</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-2xl font-semibold tabular-nums">{formatMoney(product.salePrice)}</p>
                <p className="text-xs text-muted-foreground">KDV %{formatQuantity(product.vatRate)}</p>
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">Alış Fiyatı</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-2xl font-semibold tabular-nums">{formatMoney(product.purchasePrice)}</p>
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">Depo Bazında Stok</CardTitle>
            </CardHeader>
            <CardContent>
              {stock && stock.warehouses.length > 0 ? (
                <dl className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                  {stock.warehouses.map((warehouse) => (
                    <div key={warehouse.warehouseId} className="flex items-center justify-between rounded-md border px-3 py-2">
                      <dt className="text-sm">{warehouse.warehouseName}</dt>
                      <dd className="text-sm font-medium tabular-nums">
                        {formatQuantity(warehouse.stock)}
                        {unitSuffix}
                      </dd>
                    </div>
                  ))}
                </dl>
              ) : (
                <p className="py-4 text-center text-sm text-muted-foreground">Henüz depo hareketi yok.</p>
              )}
            </CardContent>
          </Card>
        </>
      )}

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Kart Bilgileri</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <InfoRow label="Stok Kodu (SKU)" value={product.sku ?? "—"} />
            <InfoRow label="Barkod" value={product.barcode ?? "—"} />
            <InfoRow label="Kategori" value={product.categoryName ?? "—"} />
            <InfoRow label="Birim" value={product.unitName ?? "—"} />
            <InfoRow label="KDV Oranı" value={`%${formatQuantity(product.vatRate)}`} />
            <InfoRow label="Kayıt Tarihi" value={formatDate(product.createdAtUtc)} />
          </dl>
          {product.description && (
            <div className="mt-4 rounded-md bg-muted p-3 text-sm text-muted-foreground">{product.description}</div>
          )}
        </CardContent>
      </Card>

      {!product.isService && (
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Stok Hareketleri</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-3">
            {movements && movements.items.length === 0 ? (
              <p className="py-6 text-center text-sm text-muted-foreground">Henüz stok hareketi yok.</p>
            ) : (
              <div className="rounded-lg border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Tarih</TableHead>
                      <TableHead>Tür</TableHead>
                      <TableHead className="hidden md:table-cell">Depo</TableHead>
                      <TableHead className="hidden sm:table-cell">Açıklama</TableHead>
                      <TableHead className="text-right">Miktar</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {movements?.items.map((movement) => (
                      <TableRow key={movement.id}>
                        <TableCell className="tabular-nums">{formatDate(movement.date)}</TableCell>
                        <TableCell>{INVENTORY_TYPE_LABELS[movement.type] ?? movement.type}</TableCell>
                        <TableCell className="hidden text-muted-foreground md:table-cell">
                          {movement.warehouseName}
                        </TableCell>
                        <TableCell className="hidden max-w-64 truncate text-muted-foreground sm:table-cell">
                          {movement.description ?? "—"}
                        </TableCell>
                        <TableCell
                          className={`text-right font-medium tabular-nums ${
                            movement.quantity < 0 ? "text-destructive" : "text-emerald-600 dark:text-emerald-400"
                          }`}
                        >
                          {movement.quantity > 0 ? "+" : ""}
                          {formatQuantity(movement.quantity)}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}

            {movements && movements.totalCount > movements.pageSize && (
              <div className="flex flex-col gap-3 text-sm sm:flex-row sm:items-center sm:justify-between">
                <span className="text-muted-foreground">
                  {movements.totalCount} hareket · Sayfa {movements.page} / {totalPages}
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
      )}

      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>Ürünü Düzenle</DialogTitle>
            <DialogDescription>
              Stok hareketleri değiştirilemez; miktar düzeltmeleri sayım hareketiyle yapılır.
            </DialogDescription>
          </DialogHeader>
          <ProductForm product={product} onDone={() => setEditOpen(false)} />
        </DialogContent>
      </Dialog>

      <Dialog open={movementOpen} onOpenChange={setMovementOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-xl">
          <DialogHeader>
            <DialogTitle>Stok Hareketi Ekle</DialogTitle>
            <DialogDescription>
              {product.name} · Güncel stok {formatQuantity(product.currentStock)}
              {unitSuffix}
            </DialogDescription>
          </DialogHeader>
          <InventoryMovementForm product={product} onDone={() => setMovementOpen(false)} />
        </DialogContent>
      </Dialog>

      <Dialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Ürünü Sil</DialogTitle>
            <DialogDescription>
              {product.name} silinecek. Stok hareket geçmişi olan ürünler silinemez; bu durumda
              ürünü pasifleştirmeniz önerilir.
            </DialogDescription>
          </DialogHeader>
          <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end [&>button]:w-full sm:[&>button]:w-auto">
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
