"use client";

import { useQuery } from "@tanstack/react-query";
import { Loader2, Plus, Search } from "lucide-react";
import Link from "next/link";
import { useEffect, useState } from "react";

import { ProductForm } from "@/components/product-form";
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
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
import { formatMoney } from "@/lib/parties";
import { formatQuantity, stockClass } from "@/lib/products";
import type { CategoryDto, PagedResponse, ProductSummaryDto } from "@/lib/types";

const PAGE_SIZE = 20;

/** Ürün/hizmet listesi: arama, kategori, kritik stok ve pasif filtreleri. */
export function ProductList() {
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [categoryId, setCategoryId] = useState("all");
  const [criticalOnly, setCriticalOnly] = useState(false);
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

  const { data: categories } = useQuery({
    queryKey: ["categories"],
    queryFn: () => api<CategoryDto[]>("/api/v1/categories"),
    staleTime: 60_000,
  });

  const params = new URLSearchParams({
    includeInactive: String(includeInactive),
    criticalOnly: String(criticalOnly),
    page: String(page),
    pageSize: String(PAGE_SIZE),
  });
  if (search) {
    params.set("search", search);
  }
  if (categoryId !== "all") {
    params.set("categoryId", categoryId);
  }

  const { data, isPending, isError, error } = useQuery({
    queryKey: ["products", search, categoryId, criticalOnly, includeInactive, page],
    queryFn: () => api<PagedResponse<ProductSummaryDto>>(`/api/v1/products?${params.toString()}`),
  });

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <div className="mx-auto grid w-full max-w-6xl gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Ürünler</h1>
          <p className="text-sm text-muted-foreground">
            {data ? `${data.totalCount} ürün/hizmet` : "Yükleniyor..."}
          </p>
        </div>
        <Button onClick={() => setCreateOpen(true)}>
          <Plus className="size-4" />
          Yeni Ürün
        </Button>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <div className="relative w-full max-w-xs">
          <Search className="absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            aria-label="Ürün ara"
            placeholder="Ad, stok kodu, barkod..."
            className="pl-8"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
          />
        </div>
        <Select
          value={categoryId}
          onValueChange={(value) => {
            setCategoryId(value);
            setPage(1);
          }}
        >
          <SelectTrigger className="w-44" aria-label="Kategori filtresi">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Tüm kategoriler</SelectItem>
            {(categories ?? []).map((category) => (
              <SelectItem key={category.id} value={category.id}>
                {category.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <div className="flex items-center gap-2">
          <Switch
            id="critical-only"
            checked={criticalOnly}
            onCheckedChange={(checked) => {
              setCriticalOnly(checked);
              setPage(1);
            }}
          />
          <Label htmlFor="critical-only" className="text-sm text-muted-foreground">
            Yalnızca kritik stok
          </Label>
        </div>
        <div className="flex items-center gap-2">
          <Switch
            id="include-inactive-products"
            checked={includeInactive}
            onCheckedChange={(checked) => {
              setIncludeInactive(checked);
              setPage(1);
            }}
          />
          <Label htmlFor="include-inactive-products" className="text-sm text-muted-foreground">
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
            {error instanceof Error ? error.message : "Ürün listesi alınamadı."}
          </CardContent>
        </Card>
      ) : data.items.length === 0 ? (
        <Card>
          <CardContent className="grid justify-items-center gap-3 py-10 text-center">
            <p className="text-sm text-muted-foreground">
              {criticalOnly
                ? "Kritik stok eşiğine ulaşan ürün yok."
                : search
                  ? `"${search}" aramasıyla eşleşen ürün yok.`
                  : "Henüz ürün kaydı yok. İlk ürünü oluşturun."}
            </p>
            {!criticalOnly && (
              <Button variant="outline" onClick={() => setCreateOpen(true)}>
                <Plus className="size-4" />
                Yeni Ürün
              </Button>
            )}
          </CardContent>
        </Card>
      ) : (
        <div className="rounded-lg border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Ürün</TableHead>
                <TableHead className="hidden md:table-cell">Kategori</TableHead>
                <TableHead className="hidden sm:table-cell">Stok Kodu</TableHead>
                <TableHead className="text-right">Satış Fiyatı</TableHead>
                <TableHead className="text-right">Stok</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((product) => (
                <TableRow key={product.id} className="cursor-pointer">
                  <TableCell>
                    <Link href={`/products/${product.id}`} className="flex items-center gap-2 font-medium">
                      {product.name}
                      {product.isService && <Badge variant="secondary">Hizmet</Badge>}
                      {!product.isActive && <Badge variant="secondary">Pasif</Badge>}
                      {product.isCritical && !product.isService && (
                        <Badge variant="destructive">Kritik</Badge>
                      )}
                    </Link>
                  </TableCell>
                  <TableCell className="hidden text-muted-foreground md:table-cell">
                    {product.categoryName ?? "—"}
                  </TableCell>
                  <TableCell className="hidden font-mono text-xs text-muted-foreground sm:table-cell">
                    {product.sku ?? "—"}
                  </TableCell>
                  <TableCell className="text-right tabular-nums">
                    {product.isService ? "—" : formatMoney(product.salePrice)}
                  </TableCell>
                  <TableCell
                    className={`text-right tabular-nums ${product.isService ? "text-muted-foreground" : stockClass(product.currentStock, product.isCritical)}`}
                  >
                    {product.isService ? "—" : formatQuantity(product.currentStock)}
                    {!product.isService && product.unitName ? (
                      <span className="ml-1 text-muted-foreground">{product.unitName}</span>
                    ) : null}
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

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>Yeni Ürün</DialogTitle>
            <DialogDescription>
              Ürün oluşturmak stok oluşturmaz; stoğu sayım ya da manuel hareketle başlatın.
              Hizmet satıyorsanız hizmet anahtarını açın.
            </DialogDescription>
          </DialogHeader>
          <ProductForm onDone={() => setCreateOpen(false)} />
        </DialogContent>
      </Dialog>
    </div>
  );
}
