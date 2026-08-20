"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Loader2 } from "lucide-react";
import { useEffect, useMemo } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
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
import { Textarea } from "@/components/ui/textarea";
import { api } from "@/lib/api";
import { dateInputToIso, isoToDateInput } from "@/lib/parties";
import { MANUAL_INVENTORY_TYPES, formatQuantity, parseQuantityInput } from "@/lib/products";
import type { InventoryTransactionDto, ProductResponse, WarehouseDto } from "@/lib/types";

const movementSchema = z
  .object({
    type: z.enum(["ManualIn", "ManualOut", "Count", "Return", "Transfer"]),
    date: z
      .string()
      .min(1, "Hareket tarihi gereklidir.")
      .regex(/^\d{4}-\d{2}-\d{2}$/, "Geçerli bir tarih seçin."),
    quantity: z
      .string()
      .refine((value) => parseQuantityInput(value) !== null, "Miktar en fazla 4 basamak ondalık içerebilir (örn. 12,5)."),
    // Depo alanlarının zorunluluğu hareket türüne göre superRefine'da denetlenir;
    // temel şemada zorunlu yapılırsa transfer dışı hareketlerde görünmeyen
    // "Hedef depo" hatası formu sessizce engeller.
    warehouseId: z.string(),
    sourceWarehouseId: z.string(),
    targetWarehouseId: z.string(),
    description: z.string().trim().max(300, "Açıklama en fazla 300 karakter olabilir."),
  })
  .superRefine((values, ctx) => {
    const parsed = parseQuantityInput(values.quantity);
    const needsPositive = values.type !== "Count";
    if (parsed !== null && needsPositive && parsed <= 0) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["quantity"], message: "Miktar pozitif olmalıdır." });
    }
    if (parsed !== null && !needsPositive && parsed < 0) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["quantity"], message: "Sayım sonucu negatif olamaz." });
    }
    if (values.type === "Transfer") {
      if (!values.sourceWarehouseId) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["sourceWarehouseId"], message: "Kaynak depo seçin." });
      }
      if (!values.targetWarehouseId) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["targetWarehouseId"], message: "Hedef depo seçin." });
      }
      if (values.sourceWarehouseId && values.sourceWarehouseId === values.targetWarehouseId) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["targetWarehouseId"], message: "Hedef depo kaynak depodan farklı olmalıdır." });
      }
    } else if (!values.warehouseId) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["warehouseId"], message: "Depo seçin." });
    }
  });

type MovementFormValues = z.infer<typeof movementSchema>;

/**
 * Manuel stok hareketi formu (Dialog içinde kullanılır). Alış/satış hareketleri
 * ilgili modüllerde oluşur; burada sadece manuel türler ve depo transferi var.
 */
export function InventoryMovementForm({
  product,
  onDone,
}: {
  product: ProductResponse;
  onDone: () => void;
}) {
  const queryClient = useQueryClient();
  const today = isoToDateInput(new Date().toISOString());

  const { data: warehouses } = useQuery({
    queryKey: ["warehouses"],
    queryFn: () => api<WarehouseDto[]>("/api/v1/warehouses"),
    staleTime: 60_000,
  });
  const activeWarehouses = useMemo(
    () => (warehouses ?? []).filter((w) => w.isActive),
    [warehouses],
  );

  const {
    register,
    handleSubmit,
    control,
    getValues,
    setValue,
    formState: { errors, isSubmitting, isSubmitted },
  } = useForm<MovementFormValues>({
    resolver: zodResolver(movementSchema),
    defaultValues: {
      type: "ManualIn",
      date: today,
      quantity: "",
      warehouseId: activeWarehouses.find((w) => w.isDefault)?.id ?? activeWarehouses[0]?.id ?? "",
      sourceWarehouseId: activeWarehouses.find((w) => w.isDefault)?.id ?? activeWarehouses[0]?.id ?? "",
      targetWarehouseId: "",
      description: "",
    },
  });

  const type = useWatch({ control, name: "type" });
  const selected = MANUAL_INVENTORY_TYPES.find((t) => t.value === type);

  // Depo listesi ilk sorguda geç gelir; boş alanlara default depoyu yaz.
  const defaultWarehouse = useMemo(
    () => activeWarehouses.find((w) => w.isDefault) ?? activeWarehouses[0],
    [activeWarehouses],
  );

  useEffect(() => {
    if (!defaultWarehouse) return;
    if (!getValues("warehouseId")) setValue("warehouseId", defaultWarehouse.id);
    if (!getValues("sourceWarehouseId")) setValue("sourceWarehouseId", defaultWarehouse.id);
  }, [defaultWarehouse, getValues, setValue]);

  const save = useMutation({
    mutationFn: (values: MovementFormValues) => {
      const parsed = parseQuantityInput(values.quantity)!;
      // ManualOut pozitif girilir, çıkış (negatif) olarak gönderilir.
      const quantity = values.type === "ManualOut" ? -Math.abs(parsed) : parsed;
      const common = {
        productId: product.id,
        date: dateInputToIso(values.date),
        quantity,
        description: values.description || null,
      };
      const isTransfer = values.type === "Transfer";
      const path = isTransfer ? "/api/v1/inventory/transfers" : "/api/v1/inventory/transactions";
      const body = isTransfer
        ? {
            ...common,
            sourceWarehouseId: values.sourceWarehouseId,
            targetWarehouseId: values.targetWarehouseId,
          }
        : {
            ...common,
            warehouseId: values.warehouseId,
            type: values.type,
          };
      // Sunucu tek hareket DTO'su ya da transfer çifti listesi döndürür; gövde kullanılmıyor.
      return api<InventoryTransactionDto | InventoryTransactionDto[]>(path, {
        method: "POST",
        body: JSON.stringify(body),
      });
    },
    onSuccess: (_data, values) => {
      queryClient.invalidateQueries({ queryKey: ["products"] });
      queryClient.invalidateQueries({ queryKey: ["product", product.id] });
      queryClient.invalidateQueries({ queryKey: ["product-stock", product.id] });
      queryClient.invalidateQueries({ queryKey: ["product-inventory", product.id] });
      toast.success(
        values.type === "Transfer"
          ? "Depo transferi kaydedildi."
          : values.type === "Count"
            ? "Sayım farkı işlendi."
            : "Stok hareketi kaydedildi.",
      );
      onDone();
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : "Stok hareketi kaydedilemedi.");
    },
  });

  const onSubmit = handleSubmit((values) => save.mutateAsync(values).catch(() => undefined));

  const unitSuffix = product.unitName ? ` ${product.unitName}` : "";

  return (
    <form onSubmit={onSubmit} className="grid gap-4" noValidate>
      <p className="rounded-md border bg-muted/40 px-3 py-2 text-sm">
        <span className="font-medium">{product.name}</span> — güncel stok:{" "}
        <span className="tabular-nums">{formatQuantity(product.currentStock)}</span>
        {unitSuffix}
      </p>

      {isSubmitted && Object.keys(errors).length > 0 && (
        <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          <p className="font-medium">Hareket kaydedilemedi — şu alanları düzeltin:</p>
          <ul className="mt-1 list-inside list-disc">
            {Object.values(errors)
              .map((error) => error?.message)
              .filter((message): message is string => Boolean(message))
              .map((message) => (
                <li key={message}>{message}</li>
              ))}
          </ul>
        </div>
      )}

      {save.isError && (
        <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          <p className="font-medium">Hareket kaydedilemedi:</p>
          <p className="mt-1">{save.error instanceof Error ? save.error.message : "Bilinmeyen bir hata oluştu."}</p>
        </div>
      )}

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="grid gap-2">
          <Label htmlFor="movement-type">Hareket Türü *</Label>
          <Controller
            control={control}
            name="type"
            render={({ field }) => (
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger id="movement-type">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {MANUAL_INVENTORY_TYPES.map((option) => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
          <p className="text-xs text-muted-foreground">
            {selected?.hint ?? "Alış/satış hareketleri ilgili modüllerde oluşur."}
          </p>
        </div>

        <div className="grid gap-2">
          <Label htmlFor="movement-quantity">Miktar *</Label>
          <Input id="movement-quantity" inputMode="decimal" placeholder="12,5" {...register("quantity")} />
          {type === "Count" && (
            <p className="text-xs text-muted-foreground">
              Saydığınız güncel miktarı girin; fark ({formatQuantity(product.currentStock)} → girdiğiniz değer) otomatik işlenir.
            </p>
          )}
          {errors.quantity && <p className="text-sm text-destructive">{errors.quantity.message}</p>}
        </div>
      </div>

      {type === "Transfer" ? (
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="grid gap-2">
            <Label htmlFor="movement-source">Kaynak Depo *</Label>
            <Controller
              control={control}
              name="sourceWarehouseId"
              render={({ field }) => (
                <Select value={field.value} onValueChange={field.onChange}>
                  <SelectTrigger id="movement-source">
                    <SelectValue placeholder="Depo seçin" />
                  </SelectTrigger>
                  <SelectContent>
                    {activeWarehouses.map((warehouse) => (
                      <SelectItem key={warehouse.id} value={warehouse.id}>
                        {warehouse.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
            {errors.sourceWarehouseId && <p className="text-sm text-destructive">{errors.sourceWarehouseId.message}</p>}
          </div>
          <div className="grid gap-2">
            <Label htmlFor="movement-target">Hedef Depo *</Label>
            <Controller
              control={control}
              name="targetWarehouseId"
              render={({ field }) => (
                <Select value={field.value} onValueChange={field.onChange}>
                  <SelectTrigger id="movement-target">
                    <SelectValue placeholder="Depo seçin" />
                  </SelectTrigger>
                  <SelectContent>
                    {activeWarehouses.map((warehouse) => (
                      <SelectItem key={warehouse.id} value={warehouse.id}>
                        {warehouse.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
            {errors.targetWarehouseId && <p className="text-sm text-destructive">{errors.targetWarehouseId.message}</p>}
          </div>
        </div>
      ) : (
        <div className="grid gap-2">
          <Label htmlFor="movement-warehouse">Depo *</Label>
          <Controller
            control={control}
            name="warehouseId"
            render={({ field }) => (
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger id="movement-warehouse">
                  <SelectValue placeholder="Depo seçin" />
                </SelectTrigger>
                <SelectContent>
                  {activeWarehouses.map((warehouse) => (
                    <SelectItem key={warehouse.id} value={warehouse.id}>
                      {warehouse.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
          {errors.warehouseId && <p className="text-sm text-destructive">{errors.warehouseId.message}</p>}
        </div>
      )}

      <div className="grid gap-2">
        <Label htmlFor="movement-date">Tarih *</Label>
        <Input id="movement-date" type="date" {...register("date")} />
        {errors.date && <p className="text-sm text-destructive">{errors.date.message}</p>}
      </div>

      <div className="grid gap-2">
        <Label htmlFor="movement-description">Açıklama</Label>
        <Textarea
          id="movement-description"
          rows={2}
          placeholder="Örn. raf sayımı, fire, hediye ekleme..."
          {...register("description")}
        />
        {errors.description && <p className="text-sm text-destructive">{errors.description.message}</p>}
      </div>

      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={onDone} disabled={isSubmitting}>
          Vazgeç
        </Button>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting && <Loader2 className="size-4 animate-spin" />}
          Hareketi Kaydet
        </Button>
      </div>
    </form>
  );
}
