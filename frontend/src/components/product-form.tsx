"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Loader2 } from "lucide-react";
import { Controller, useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";

import { Button } from "@/components/ui/button";
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
import { Textarea } from "@/components/ui/textarea";
import { api } from "@/lib/api";
import { parseMoneyInput, toMoneyInput } from "@/lib/parties";
import { parseQuantityInput, toQuantityInput } from "@/lib/products";
import type { CategoryDto, ProductResponse, UnitDto } from "@/lib/types";

const moneyInput = (message: string) =>
  z.string().refine((value) => value.trim() === "" || parseMoneyInput(value) !== null, message);

const productSchema = z.object({
  name: z
    .string()
    .trim()
    .min(2, "Ürün adı en az 2 karakter olmalı.")
    .max(200, "Ürün adı en fazla 200 karakter olabilir."),
  sku: z.string().trim().max(50, "Stok kodu en fazla 50 karakter olabilir."),
  barcode: z.string().trim().max(50, "Barkod en fazla 50 karakter olabilir."),
  description: z.string().trim().max(500, "Açıklama en fazla 500 karakter olabilir."),
  categoryId: z.string(),
  unitId: z.string(),
  purchasePrice: moneyInput("Alış fiyatı en fazla 2 basamak ondalık içerebilir."),
  salePrice: moneyInput("Satış fiyatı en fazla 2 basamak ondalık içerebilir."),
  vatRate: moneyInput("KDV oranı en fazla 2 basamak ondalık içerebilir.")
    .refine((value) => value.trim() === "" || ((parseMoneyInput(value) ?? -1) >= 0 && (parseMoneyInput(value) ?? 101) <= 100), "KDV oranı 0 ile 100 arasında olmalı."),
  minimumStock: z.string().refine((value) => value.trim() === "" || parseQuantityInput(value) !== null, "Kritik stok eşiği en fazla 4 basamak ondalık içerebilir."),
  isService: z.boolean(),
  isActive: z.boolean(),
});

type ProductFormValues = z.infer<typeof productSchema>;

/** Ürün/hizmet oluştur-düzenle formu (Dialog içinde kullanılır). */
export function ProductForm({
  product,
  onDone,
}: {
  product?: ProductResponse;
  onDone: () => void;
}) {
  const queryClient = useQueryClient();
  const isEdit = Boolean(product);

  const { data: categories } = useQuery({
    queryKey: ["categories"],
    queryFn: () => api<CategoryDto[]>("/api/v1/categories"),
    staleTime: 60_000,
  });
  const { data: units } = useQuery({
    queryKey: ["units"],
    queryFn: () => api<UnitDto[]>("/api/v1/units"),
    staleTime: 60_000,
  });

  const {
    register,
    handleSubmit,
    control,
    formState: { errors, isSubmitting },
  } = useForm<ProductFormValues>({
    resolver: zodResolver(productSchema),
    defaultValues: product
      ? {
          name: product.name,
          sku: product.sku ?? "",
          barcode: product.barcode ?? "",
          description: product.description ?? "",
          categoryId: product.categoryId ?? "none",
          unitId: product.unitId ?? "none",
          purchasePrice: toMoneyInput(product.purchasePrice),
          salePrice: toMoneyInput(product.salePrice),
          vatRate: toMoneyInput(product.vatRate),
          minimumStock: toQuantityInput(product.minimumStock),
          isService: product.isService,
          isActive: product.isActive,
        }
      : {
          name: "",
          sku: "",
          barcode: "",
          description: "",
          categoryId: "none",
          unitId: "none",
          purchasePrice: "",
          salePrice: "",
          vatRate: "20",
          minimumStock: "",
          isService: false,
          isActive: true,
        },
  });

  const save = useMutation({
    mutationFn: (values: ProductFormValues) => {
      const body = {
        name: values.name,
        sku: values.sku || null,
        barcode: values.barcode || null,
        description: values.description || null,
        categoryId: values.categoryId === "none" ? null : values.categoryId,
        unitId: values.unitId === "none" ? null : values.unitId,
        purchasePrice: parseMoneyInput(values.purchasePrice) ?? 0,
        salePrice: parseMoneyInput(values.salePrice) ?? 0,
        vatRate: parseMoneyInput(values.vatRate) ?? 0,
        minimumStock: parseQuantityInput(values.minimumStock) ?? 0,
        isService: values.isService,
        ...(isEdit ? { isActive: values.isActive } : {}),
      };
      return api(isEdit ? `/api/v1/products/${product!.id}` : "/api/v1/products", {
        method: isEdit ? "PUT" : "POST",
        body: JSON.stringify(body),
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["products"] });
      if (isEdit) {
        queryClient.invalidateQueries({ queryKey: ["product", product!.id] });
      }
      toast.success(isEdit ? "Ürün güncellendi." : "Ürün oluşturuldu.");
      onDone();
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : "Ürün kaydedilemedi.");
    },
  });

  const onSubmit = handleSubmit((values) => save.mutateAsync(values).catch(() => undefined));

  return (
    <form onSubmit={onSubmit} className="grid gap-4" noValidate>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="grid gap-2 sm:col-span-2">
          <Label htmlFor="product-name">Ürün Adı *</Label>
          <Input id="product-name" placeholder="Tükenmez Kalem Mavi" autoFocus={!isEdit} {...register("name")} />
          {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
        </div>

        <div className="grid gap-2">
          <Label htmlFor="product-sku">Stok Kodu (SKU)</Label>
          <Input id="product-sku" placeholder="KLM-001" {...register("sku")} />
          <p className="text-xs text-muted-foreground">İşletmeniz içinde benzersiz; boş bırakılabilir.</p>
          {errors.sku && <p className="text-sm text-destructive">{errors.sku.message}</p>}
        </div>

        <div className="grid gap-2">
          <Label htmlFor="product-barcode">Barkod</Label>
          <Input id="product-barcode" placeholder="8690000000000" {...register("barcode")} />
          {errors.barcode && <p className="text-sm text-destructive">{errors.barcode.message}</p>}
        </div>

        <div className="grid gap-2">
          <Label htmlFor="product-category">Kategori</Label>
          <Controller
            control={control}
            name="categoryId"
            render={({ field }) => (
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger id="product-category">
                  <SelectValue placeholder="Kategori seçin" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">Kategorisiz</SelectItem>
                  {(categories ?? []).map((category) => (
                    <SelectItem key={category.id} value={category.id}>
                      {category.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
        </div>

        <div className="grid gap-2">
          <Label htmlFor="product-unit">Birim</Label>
          <Controller
            control={control}
            name="unitId"
            render={({ field }) => (
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger id="product-unit">
                  <SelectValue placeholder="Birim seçin" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">Birimsiz</SelectItem>
                  {(units ?? []).map((unit) => (
                    <SelectItem key={unit.id} value={unit.id}>
                      {unit.code ? `${unit.name} (${unit.code})` : unit.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
        </div>

        <div className="grid gap-2">
          <Label htmlFor="product-purchase-price">Alış Fiyatı</Label>
          <Input id="product-purchase-price" inputMode="decimal" placeholder="0,00" {...register("purchasePrice")} />
          {errors.purchasePrice && <p className="text-sm text-destructive">{errors.purchasePrice.message}</p>}
        </div>

        <div className="grid gap-2">
          <Label htmlFor="product-sale-price">Satış Fiyatı</Label>
          <Input id="product-sale-price" inputMode="decimal" placeholder="0,00" {...register("salePrice")} />
          {errors.salePrice && <p className="text-sm text-destructive">{errors.salePrice.message}</p>}
        </div>

        <div className="grid gap-2">
          <Label htmlFor="product-vat">KDV Oranı (%)</Label>
          <Input id="product-vat" inputMode="decimal" placeholder="20" {...register("vatRate")} />
          {errors.vatRate && <p className="text-sm text-destructive">{errors.vatRate.message}</p>}
        </div>

        <div className="grid gap-2">
          <Label htmlFor="product-min-stock">Kritik Stok Eşiği</Label>
          <Input id="product-min-stock" inputMode="decimal" placeholder="5" {...register("minimumStock")} />
          <p className="text-xs text-muted-foreground">0 = uyarı yok. Stok bu değerin altına inince uyarılır.</p>
          {errors.minimumStock && <p className="text-sm text-destructive">{errors.minimumStock.message}</p>}
        </div>

        <div className="grid gap-2 sm:col-span-2">
          <Label htmlFor="product-description">Açıklama</Label>
          <Textarea id="product-description" rows={2} placeholder="Renk, boyut, model notları..." {...register("description")} />
          {errors.description && <p className="text-sm text-destructive">{errors.description.message}</p>}
        </div>
      </div>

      <div className="flex items-center justify-between rounded-md border p-3">
        <div>
          <Label htmlFor="product-service">Hizmet</Label>
          <p className="text-xs text-muted-foreground">Hizmetlerde stok takibi yapılmaz.</p>
        </div>
        <Controller
          control={control}
          name="isService"
          render={({ field }) => (
            <Switch id="product-service" checked={field.value} onCheckedChange={field.onChange} />
          )}
        />
      </div>

      {isEdit && (
        <div className="flex items-center justify-between rounded-md border p-3">
          <div>
            <Label htmlFor="product-active">Aktif</Label>
            <p className="text-xs text-muted-foreground">Pasif ürüne stok hareketi girilemez.</p>
          </div>
          <Controller
            control={control}
            name="isActive"
            render={({ field }) => (
              <Switch id="product-active" checked={field.value} onCheckedChange={field.onChange} />
            )}
          />
        </div>
      )}

      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={onDone} disabled={isSubmitting}>
          Vazgeç
        </Button>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting && <Loader2 className="size-4 animate-spin" />}
          {isEdit ? "Kaydet" : "Ürünü Oluştur"}
        </Button>
      </div>
    </form>
  );
}
