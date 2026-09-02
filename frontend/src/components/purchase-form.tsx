"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Loader2, Plus, Trash2 } from "lucide-react";
import { useRouter } from "next/navigation";
import { Controller, useFieldArray, useForm, useWatch } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Separator } from "@/components/ui/separator";
import { Textarea } from "@/components/ui/textarea";
import { api } from "@/lib/api";
import { computeDocumentTotals } from "@/lib/document-math";
import { dateInputToIso, formatDate, formatMoney, isoToDateInput, parseMoneyInput, toMoneyInput } from "@/lib/parties";
import { parseQuantityInput } from "@/lib/products";
import type {
  PartySummaryDto,
  PagedResponse,
  ProductSummaryDto,
  PurchaseResponse,
  WarehouseDto,
} from "@/lib/types";

const positiveQuantity = z
  .string()
  .refine((value) => parseQuantityInput(value) !== null && (parseQuantityInput(value) ?? 0) > 0, {
    message: "Miktar 0'dan büyük, en fazla 4 basamak ondalık olmalı.",
  });

const nonNegativeMoney = z.string().refine((value) => parseMoneyInput(value) !== null && (parseMoneyInput(value) ?? -1) >= 0, {
  message: "En fazla 2 basamak ondalıklı, 0 ya da pozitif değer girin.",
});

const rate = z
  .string()
  .refine(
    (value) => {
      const parsed = parseMoneyInput(value);
      return parsed !== null && parsed >= 0 && parsed <= 100;
    },
    { message: "0 ile 100 arasında bir oran girin." },
  );

const purchaseSchema = z
  .object({
    partyId: z.string(),
    warehouseId: z.string(),
    date: z.string().min(1, "Belge tarihi zorunlu."),
    dueDate: z.string(),
    description: z.string().trim().max(500, "Açıklama en fazla 500 karakter olabilir."),
    items: z
      .array(
        z.object({
          productId: z.string().min(1, "Ürün seçin."),
          quantity: positiveQuantity,
          unitPrice: nonNegativeMoney,
          discountRate: rate,
          vatRate: rate,
        }),
      )
      .min(1, "En az bir satır kalemi gerekli."),
  })
  .superRefine((data, ctx) => {
    if (data.dueDate && data.dueDate < data.date) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["dueDate"],
        message: "Vade tarihi belge tarihinden önce olamaz.",
      });
    }
  });

type PurchaseFormValues = z.infer<typeof purchaseSchema>;

const todayInput = () => isoToDateInput(new Date().toISOString());

/** Alış belgesi oluştur-düzenle formu. Düzenleme yalnızca taslak belgede açılır. */
export function PurchaseForm({ purchase }: { purchase?: PurchaseResponse }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const isEdit = Boolean(purchase);

  const { data: productPage } = useQuery({
    queryKey: ["products", "for-purchase"],
    queryFn: () =>
      api<PagedResponse<ProductSummaryDto>>("/api/v1/products?includeInactive=false&page=1&pageSize=100"),
    staleTime: 60_000,
  });
  const { data: partyPage } = useQuery({
    queryKey: ["parties", "suppliers-for-purchase"],
    queryFn: () =>
      api<PagedResponse<PartySummaryDto>>("/api/v1/parties?type=Supplier&includeInactive=false&page=1&pageSize=100"),
    staleTime: 60_000,
  });
  const { data: warehouses } = useQuery({
    queryKey: ["warehouses"],
    queryFn: () => api<WarehouseDto[]>("/api/v1/warehouses"),
    staleTime: 60_000,
  });

  const {
    register,
    handleSubmit,
    control,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<PurchaseFormValues>({
    resolver: zodResolver(purchaseSchema),
    defaultValues: purchase
      ? {
          partyId: purchase.partyId ?? "none",
          warehouseId: purchase.warehouseId,
          date: isoToDateInput(purchase.date),
          dueDate: purchase.dueDate ? isoToDateInput(purchase.dueDate) : "",
          description: purchase.description ?? "",
          items: purchase.items.map((item) => ({
            productId: item.productId,
            quantity: String(item.quantity),
            unitPrice: toMoneyInput(item.unitPrice),
            discountRate: toMoneyInput(item.discountRate),
            vatRate: toMoneyInput(item.vatRate),
          })),
        }
      : {
          partyId: "none",
          warehouseId: "default",
          date: todayInput(),
          dueDate: "",
          description: "",
          items: [{ productId: "", quantity: "1", unitPrice: "", discountRate: "", vatRate: "" }],
        },
  });

  const { fields, append, remove } = useFieldArray({ control, name: "items" });
  const watchedItems = useWatch({ control, name: "items" }) ?? [];
  const watchedProducts = productPage?.items ?? [];

  // Canlı toplamlar — backend LineMath ile aynı yuvarlama.
  const totals = computeDocumentTotals(
    watchedItems.map((item) => ({
      quantity: parseQuantityInput(item?.quantity ?? "") ?? 0,
      unitPrice: parseMoneyInput(item?.unitPrice ?? "") ?? 0,
      discountRate: parseMoneyInput(item?.discountRate ?? "") ?? 0,
      vatRate: parseMoneyInput(item?.vatRate ?? "") ?? 0,
    })),
  );

  const save = useMutation({
    mutationFn: (values: PurchaseFormValues) => {
      const body = {
        partyId: values.partyId === "none" ? null : values.partyId,
        warehouseId: values.warehouseId === "default" ? null : values.warehouseId,
        date: dateInputToIso(values.date),
        dueDate: values.dueDate ? dateInputToIso(values.dueDate) : null,
        description: values.description || null,
        items: values.items.map((item) => ({
          productId: item.productId,
          quantity: parseQuantityInput(item.quantity) ?? 0,
          unitPrice: parseMoneyInput(item.unitPrice) ?? 0,
          discountRate: parseMoneyInput(item.discountRate) ?? 0,
          vatRate: parseMoneyInput(item.vatRate) ?? 0,
        })),
      };
      return api<PurchaseResponse>(isEdit ? `/api/v1/purchases/${purchase!.id}` : "/api/v1/purchases", {
        method: isEdit ? "PUT" : "POST",
        body: JSON.stringify(body),
      });
    },
    onSuccess: (saved) => {
      queryClient.invalidateQueries({ queryKey: ["purchases"] });
      queryClient.invalidateQueries({ queryKey: ["purchase", saved.id] });
      toast.success(isEdit ? "Alış güncellendi." : `Alış oluşturuldu (${saved.number}). Onayla ile stok girer.`);
      router.push(`/purchases/${saved.id}`);
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : "Alış kaydedilemedi.");
    },
  });

  const onSubmit = handleSubmit((values) => save.mutateAsync(values).catch(() => undefined));

  /** Ürün seçilince boş alanlara ürünün varsayılanlarını yaz (alış fiyatı, KDV). */
  const onProductChange = (index: number, productId: string) => {
    setValue(`items.${index}.productId`, productId, { shouldValidate: true });
    const product = watchedProducts.find((candidate) => candidate.id === productId);
    if (!product) return;
    if (!watchedItems[index]?.unitPrice) {
      setValue(`items.${index}.unitPrice`, toMoneyInput(product.purchasePrice));
    }
    if (!watchedItems[index]?.vatRate) {
      setValue(`items.${index}.vatRate`, toMoneyInput(product.vatRate));
    }
  };

  return (
    <form onSubmit={onSubmit} className="grid gap-6" noValidate>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div className="grid gap-2">
          <Label htmlFor="purchase-party">Tedarikçi</Label>
          <Controller
            control={control}
            name="partyId"
            render={({ field }) => (
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger id="purchase-party">
                  <SelectValue placeholder="Tedarikçi seçin" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">Nakit (tedarikçisiz)</SelectItem>
                  {(partyPage?.items ?? []).map((party) => (
                    <SelectItem key={party.id} value={party.id}>
                      {party.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
          <p className="text-xs text-muted-foreground">Tedarikçisiz alışta cari hareketi yazılmaz.</p>
        </div>

        <div className="grid gap-2">
          <Label htmlFor="purchase-warehouse">Depo</Label>
          <Controller
            control={control}
            name="warehouseId"
            render={({ field }) => (
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger id="purchase-warehouse">
                  <SelectValue placeholder="Depo seçin" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="default">Varsayılan depo</SelectItem>
                  {(warehouses ?? [])
                    .filter((warehouse) => warehouse.isActive)
                    .map((warehouse) => (
                      <SelectItem key={warehouse.id} value={warehouse.id}>
                        {warehouse.name}
                        {warehouse.isDefault ? " (varsayılan)" : ""}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            )}
          />
        </div>

        <div className="grid gap-2">
          <Label htmlFor="purchase-date">Belge Tarihi *</Label>
          <Input id="purchase-date" type="date" {...register("date")} />
          {errors.date && <p className="text-sm text-destructive">{errors.date.message}</p>}
        </div>

        <div className="grid gap-2">
          <Label htmlFor="purchase-due-date">Vade Tarihi</Label>
          <Input id="purchase-due-date" type="date" {...register("dueDate")} />
          {errors.dueDate && <p className="text-sm text-destructive">{errors.dueDate.message}</p>}
        </div>
      </div>

      <div className="grid gap-3">
        <div className="flex items-center justify-between">
          <h2 className="text-sm font-medium">Kalemler</h2>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => append({ productId: "", quantity: "1", unitPrice: "", discountRate: "", vatRate: "" })}
          >
            <Plus className="size-4" />
            Satır Ekle
          </Button>
        </div>
        {typeof errors.items?.message === "string" && (
          <p className="text-sm text-destructive">{errors.items.message}</p>
        )}

        {fields.map((field, index) => {
          const product = watchedProducts.find(
            (candidate) => candidate.id === (watchedItems[index]?.productId ?? ""),
          );
          const line =
            product || watchedItems[index]?.productId
              ? computeDocumentTotals([
                  {
                    quantity: parseQuantityInput(watchedItems[index]?.quantity ?? "") ?? 0,
                    unitPrice: parseMoneyInput(watchedItems[index]?.unitPrice ?? "") ?? 0,
                    discountRate: parseMoneyInput(watchedItems[index]?.discountRate ?? "") ?? 0,
                    vatRate: parseMoneyInput(watchedItems[index]?.vatRate ?? "") ?? 0,
                  },
                ])
              : null;
          return (
            <Card key={field.id}>
              <CardContent className="grid gap-3">
                <div className="grid grid-cols-2 gap-3 md:grid-cols-12">
                  <div className="col-span-2 grid gap-2 md:col-span-4">
                    <Label htmlFor={`pitem-product-${index}`} className="text-xs text-muted-foreground">
                      Ürün / Hizmet *
                    </Label>
                    <Controller
                      control={control}
                      name={`items.${index}.productId`}
                      render={({ field: itemField }) => (
                        <Select value={itemField.value || undefined} onValueChange={(value) => onProductChange(index, value)}>
                          <SelectTrigger id={`pitem-product-${index}`} className="w-full">
                            <SelectValue placeholder="Ürün seçin" />
                          </SelectTrigger>
                          <SelectContent>
                            {watchedProducts.map((candidate) => (
                              <SelectItem key={candidate.id} value={candidate.id}>
                                {candidate.name}
                                {candidate.isService ? " — Hizmet" : ""}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      )}
                    />
                    {errors.items?.[index]?.productId && (
                      <p className="text-sm text-destructive">{errors.items[index]?.productId?.message}</p>
                    )}
                  </div>

                  <div className="grid gap-2 md:col-span-2">
                    <Label htmlFor={`pitem-quantity-${index}`} className="text-xs text-muted-foreground">
                      Miktar *
                    </Label>
                    <Input
                      id={`pitem-quantity-${index}`}
                      inputMode="decimal"
                      placeholder="1"
                      {...register(`items.${index}.quantity`)}
                    />
                    {errors.items?.[index]?.quantity && (
                      <p className="text-sm text-destructive">{errors.items[index]?.quantity?.message}</p>
                    )}
                  </div>

                  <div className="grid gap-2 md:col-span-2">
                    <Label htmlFor={`pitem-price-${index}`} className="text-xs text-muted-foreground">
                      Birim Alış Fiyatı *
                    </Label>
                    <Input
                      id={`pitem-price-${index}`}
                      inputMode="decimal"
                      placeholder="0,00"
                      {...register(`items.${index}.unitPrice`)}
                    />
                    {errors.items?.[index]?.unitPrice && (
                      <p className="text-sm text-destructive">{errors.items[index]?.unitPrice?.message}</p>
                    )}
                  </div>

                  <div className="grid gap-2 md:col-span-1">
                    <Label htmlFor={`pitem-discount-${index}`} className="text-xs text-muted-foreground">
                      İskonto %
                    </Label>
                    <Input
                      id={`pitem-discount-${index}`}
                      inputMode="decimal"
                      placeholder="0"
                      {...register(`items.${index}.discountRate`)}
                    />
                    {errors.items?.[index]?.discountRate && (
                      <p className="text-sm text-destructive">{errors.items[index]?.discountRate?.message}</p>
                    )}
                  </div>

                  <div className="grid gap-2 md:col-span-1">
                    <Label htmlFor={`pitem-vat-${index}`} className="text-xs text-muted-foreground">
                      KDV %
                    </Label>
                    <Input
                      id={`pitem-vat-${index}`}
                      inputMode="decimal"
                      placeholder="20"
                      {...register(`items.${index}.vatRate`)}
                    />
                    {errors.items?.[index]?.vatRate && (
                      <p className="text-sm text-destructive">{errors.items[index]?.vatRate?.message}</p>
                    )}
                  </div>

                  <div className="col-span-2 flex items-end justify-end md:col-span-2">
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      aria-label="Satırı kaldır"
                      disabled={fields.length <= 1}
                      onClick={() => remove(index)}
                    >
                      <Trash2 className="size-4 text-muted-foreground" />
                    </Button>
                  </div>
                </div>

                {line && (
                  <p className="text-xs text-muted-foreground">
                    Satır tutarı: <span className="tabular-nums">{formatMoney(line.total)}</span>
                    {product?.isService ? (
                      <Badge variant="secondary" className="ml-2">
                        Hizmet — stoğa girmez
                      </Badge>
                    ) : null}
                  </p>
                )}
              </CardContent>
            </Card>
          );
        })}
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <div className="grid gap-2">
          <Label htmlFor="purchase-description">Açıklama</Label>
          <Textarea
            id="purchase-description"
            rows={3}
            placeholder="Belgeye not (fatura no, teslim bilgisi...)"
            {...register("description")}
          />
          {errors.description && <p className="text-sm text-destructive">{errors.description.message}</p>}
        </div>

        <Card>
          <CardContent className="grid gap-2 text-sm">
            <div className="flex justify-between">
              <span className="text-muted-foreground">Ara Toplam</span>
              <span className="tabular-nums">{formatMoney(totals.subTotal)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">İskonto Toplamı</span>
              <span className="tabular-nums text-destructive">−{formatMoney(totals.discountTotal)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">KDV Toplamı</span>
              <span className="tabular-nums">{formatMoney(totals.vatTotal)}</span>
            </div>
            <Separator />
            <div className="flex justify-between text-base font-medium">
              <span>Genel Toplam</span>
              <span className="tabular-nums">{formatMoney(totals.total)}</span>
            </div>
            {isEdit && purchase && (
              <p className="text-xs text-muted-foreground">
                {purchase.number} · {formatDate(purchase.date)} — taslak düzenleniyor, onayda stok girer.
              </p>
            )}
          </CardContent>
        </Card>
      </div>

      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end [&>button]:w-full sm:[&>button]:w-auto">
        <Button type="button" variant="outline" onClick={() => router.push("/purchases")} disabled={isSubmitting}>
          Vazgeç
        </Button>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting && <Loader2 className="size-4 animate-spin" />}
          {isEdit ? "Kaydet" : "Taslağı Oluştur"}
        </Button>
      </div>
    </form>
  );
}
