"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Loader2, Pencil, Plus, Trash2 } from "lucide-react";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";

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
import type { CategoryDto, UnitDto, WarehouseDto } from "@/lib/types";

/** Kategori/birim/depo tanımları: kullanım sayılarıyla birlikte satır içi yönetim. */
export function SettingsDefinitions() {
  return (
    <div className="mx-auto grid w-full max-w-4xl gap-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Tanımlar</h1>
        <p className="text-sm text-muted-foreground">
          Ürün kartlarında kullanılan kategori, birim ve depo tanımları.
        </p>
      </div>
      <CategorySection />
      <UnitSection />
      <WarehouseSection />
    </div>
  );
}

function useDeleteDefinition(basePath: string, invalidateKey: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api<void>(`${basePath}/${id}`, { method: "DELETE" }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [invalidateKey] });
      queryClient.invalidateQueries({ queryKey: ["products"] });
      toast.success("Tanım silindi.");
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : "Tanım silinemedi.");
    },
  });
}

function DeleteButton({
  disabled,
  hint,
  onClick,
}: {
  disabled?: boolean;
  hint: string;
  onClick: () => void;
}) {
  return (
    <Button
      variant="ghost"
      size="icon"
      className="text-destructive"
      disabled={disabled}
      title={disabled ? hint : "Sil"}
      onClick={onClick}
    >
      <Trash2 className="size-4" />
    </Button>
  );
}

// ---- Kategoriler

function CategorySection() {
  const [edit, setEdit] = useState<CategoryDto | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<CategoryDto | null>(null);

  const { data: categories, isPending } = useQuery({
    queryKey: ["categories"],
    queryFn: () => api<CategoryDto[]>("/api/v1/categories"),
  });
  const remove = useDeleteDefinition("/api/v1/categories", "categories");

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-3">
        <CardTitle className="text-base">Kategoriler</CardTitle>
        <Button variant="outline" size="sm" onClick={() => setCreateOpen(true)}>
          <Plus className="size-4" />
          Kategori Ekle
        </Button>
      </CardHeader>
      <CardContent>
        <DefinitionTable
          isPending={isPending}
          rows={categories ?? []}
          renderName={(c) => c.name}
          renderMeta={(c) => (c.productCount > 0 ? `${c.productCount} ürün` : "Kullanılmıyor")}
          onEdit={setEdit}
          onDelete={setDeleteTarget}
          deleteDisabled={(c) => c.productCount > 0}
          deleteHint="Kullanımdaki kategori silinemez."
          emptyText="Henüz kategori yok."
        />

        {(createOpen || edit) && (
          <NameDialog
            title={edit ? "Kategoriyi Düzenle" : "Yeni Kategori"}
            description="Ürün kartlarında seçilir; aynı ad tekrar kullanılamaz."
            initialName={edit?.name}
            savePath={edit ? `/api/v1/categories/${edit.id}` : "/api/v1/categories"}
            method={edit ? "PUT" : "POST"}
            invalidateKeys={["categories"]}
            onDone={() => {
              setCreateOpen(false);
              setEdit(null);
            }}
          />
        )}

        <ConfirmDeleteDialog
          target={deleteTarget}
          name={(c) => c.name}
          onClose={() => setDeleteTarget(null)}
          onConfirm={(id) => remove.mutate(id)}
          pending={remove.isPending}
          what="Kategori"
        />
      </CardContent>
    </Card>
  );
}

// ---- Birimler

function UnitSection() {
  const [edit, setEdit] = useState<UnitDto | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<UnitDto | null>(null);

  const { data: units, isPending } = useQuery({
    queryKey: ["units"],
    queryFn: () => api<UnitDto[]>("/api/v1/units"),
  });
  const remove = useDeleteDefinition("/api/v1/units", "units");

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-3">
        <CardTitle className="text-base">Birimler</CardTitle>
        <Button variant="outline" size="sm" onClick={() => setCreateOpen(true)}>
          <Plus className="size-4" />
          Birim Ekle
        </Button>
      </CardHeader>
      <CardContent>
        <DefinitionTable
          isPending={isPending}
          rows={units ?? []}
          renderName={(u) => (u.code ? `${u.name} (${u.code})` : u.name)}
          renderMeta={(u) => (u.productCount > 0 ? `${u.productCount} ürün` : "Kullanılmıyor")}
          onEdit={setEdit}
          onDelete={setDeleteTarget}
          deleteDisabled={(u) => u.productCount > 0}
          deleteHint="Kullanımdaki birim silinemez."
          emptyText="Henüz birim yok. (Adet, Kg, Mt...)"
        />

        {(createOpen || edit) && (
          <UnitDialog
            unit={edit ?? undefined}
            onDone={() => {
              setCreateOpen(false);
              setEdit(null);
            }}
          />
        )}

        <ConfirmDeleteDialog
          target={deleteTarget}
          name={(u) => u.name}
          onClose={() => setDeleteTarget(null)}
          onConfirm={(id) => remove.mutate(id)}
          pending={remove.isPending}
          what="Birim"
        />
      </CardContent>
    </Card>
  );
}

// ---- Depolar

function WarehouseSection() {
  const [edit, setEdit] = useState<WarehouseDto | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<WarehouseDto | null>(null);

  const { data: warehouses, isPending } = useQuery({
    queryKey: ["warehouses"],
    queryFn: () => api<WarehouseDto[]>("/api/v1/warehouses"),
  });
  const remove = useDeleteDefinition("/api/v1/warehouses", "warehouses");

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-3">
        <CardTitle className="text-base">Depolar</CardTitle>
        <Button variant="outline" size="sm" onClick={() => setCreateOpen(true)}>
          <Plus className="size-4" />
          Depo Ekle
        </Button>
      </CardHeader>
      <CardContent>
        {isPending ? (
          <div className="flex justify-center py-6">
            <Loader2 className="size-5 animate-spin text-muted-foreground" />
          </div>
        ) : (warehouses ?? []).length === 0 ? (
          <p className="py-4 text-center text-sm text-muted-foreground">Henüz depo yok.</p>
        ) : (
          <div className="rounded-lg border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Depo</TableHead>
                  <TableHead className="hidden sm:table-cell">Adres</TableHead>
                  <TableHead className="text-right">Durum</TableHead>
                  <TableHead className="w-20" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {(warehouses ?? []).map((warehouse) => (
                  <TableRow key={warehouse.id}>
                    <TableCell className="font-medium">{warehouse.name}</TableCell>
                    <TableCell className="hidden max-w-64 truncate text-muted-foreground sm:table-cell">
                      {warehouse.address ?? "—"}
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-1">
                        {warehouse.isDefault && <Badge>Varsayılan</Badge>}
                        {!warehouse.isActive && <Badge variant="secondary">Pasif</Badge>}
                      </div>
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-1">
                        <Button variant="ghost" size="icon" onClick={() => setEdit(warehouse)}>
                          <Pencil className="size-4" />
                        </Button>
                        <DeleteButton
                          disabled={warehouse.isDefault}
                          hint="Varsayılan depo silinemez."
                          onClick={() => setDeleteTarget(warehouse)}
                        />
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}

        {(createOpen || edit) && (
          <WarehouseDialog
            warehouse={edit ?? undefined}
            onDone={() => {
              setCreateOpen(false);
              setEdit(null);
            }}
          />
        )}

        <ConfirmDeleteDialog
          target={deleteTarget}
          name={(w) => w.name}
          onClose={() => setDeleteTarget(null)}
          onConfirm={(id) => remove.mutate(id)}
          pending={remove.isPending}
          what="Depo"
        />
      </CardContent>
    </Card>
  );
}

// ---- Paylaşılan parçalar

function DefinitionTable<T extends { id: string }>({
  isPending,
  rows,
  renderName,
  renderMeta,
  onEdit,
  onDelete,
  deleteDisabled,
  deleteHint,
  emptyText,
}: {
  isPending: boolean;
  rows: T[];
  renderName: (row: T) => string;
  renderMeta: (row: T) => string;
  onEdit: (row: T) => void;
  onDelete: (row: T) => void;
  deleteDisabled: (row: T) => boolean;
  deleteHint: string;
  emptyText: string;
}) {
  if (isPending) {
    return (
      <div className="flex justify-center py-6">
        <Loader2 className="size-5 animate-spin text-muted-foreground" />
      </div>
    );
  }
  if (rows.length === 0) {
    return <p className="py-4 text-center text-sm text-muted-foreground">{emptyText}</p>;
  }
  return (
    <div className="rounded-lg border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Ad</TableHead>
            <TableHead>Kullanım</TableHead>
            <TableHead className="w-20" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((row) => (
            <TableRow key={row.id}>
              <TableCell className="font-medium">{renderName(row)}</TableCell>
              <TableCell className="text-muted-foreground">{renderMeta(row)}</TableCell>
              <TableCell className="text-right">
                <div className="flex justify-end gap-1">
                  <Button variant="ghost" size="icon" onClick={() => onEdit(row)}>
                    <Pencil className="size-4" />
                  </Button>
                  <DeleteButton
                    disabled={deleteDisabled(row)}
                    hint={deleteHint}
                    onClick={() => onDelete(row)}
                  />
                </div>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

const nameSchema = z.object({
  name: z
    .string()
    .trim()
    .min(2, "Ad en az 2 karakter olmalı.")
    .max(100, "Ad en fazla 100 karakter olabilir."),
});

/** Tek alanlı (ad) tanım diyaloğu — kategori. */
function NameDialog({
  title,
  description,
  initialName,
  savePath,
  method,
  invalidateKeys,
  onDone,
}: {
  title: string;
  description: string;
  initialName?: string;
  savePath: string;
  method: "POST" | "PUT";
  invalidateKeys: string[];
  onDone: () => void;
}) {
  const queryClient = useQueryClient();
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<z.infer<typeof nameSchema>>({
    resolver: zodResolver(nameSchema),
    defaultValues: { name: initialName ?? "" },
  });

  const save = useMutation({
    mutationFn: (values: { name: string }) =>
      api(savePath, { method, body: JSON.stringify(values) }),
    onSuccess: () => {
      for (const key of invalidateKeys) {
        queryClient.invalidateQueries({ queryKey: [key] });
      }
      toast.success(method === "POST" ? "Tanım eklendi." : "Tanım güncellendi.");
      onDone();
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : "Kaydedilemedi.");
    },
  });

  return (
    <Dialog open onOpenChange={(open) => !open && onDone()}>
      <DialogContent className="sm:max-w-sm">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit((values) => save.mutateAsync(values).catch(() => undefined))} className="grid gap-3" noValidate>
          <div className="grid gap-2">
            <Label htmlFor="definition-name">Ad *</Label>
            <Input id="definition-name" autoFocus {...register("name")} />
            {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
          </div>
          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={onDone} disabled={isSubmitting}>
              Vazgeç
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting && <Loader2 className="size-4 animate-spin" />}
              Kaydet
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

const unitSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, "Birim adı gereklidir.")
    .max(50, "Birim adı en fazla 50 karakter olabilir."),
  code: z.string().trim().max(20, "Kısa kod en fazla 20 karakter olabilir."),
});

/** Birim diyaloğu (ad + kısa kod). */
function UnitDialog({ unit, onDone }: { unit?: UnitDto; onDone: () => void }) {
  const queryClient = useQueryClient();
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<z.infer<typeof unitSchema>>({
    resolver: zodResolver(unitSchema),
    defaultValues: { name: unit?.name ?? "", code: unit?.code ?? "" },
  });

  const save = useMutation({
    mutationFn: (values: z.infer<typeof unitSchema>) => {
      const body = { name: values.name, code: values.code || null };
      return api(unit ? `/api/v1/units/${unit.id}` : "/api/v1/units", {
        method: unit ? "PUT" : "POST",
        body: JSON.stringify(body),
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["units"] });
      toast.success(unit ? "Birim güncellendi." : "Birim eklendi.");
      onDone();
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : "Birim kaydedilemedi.");
    },
  });

  return (
    <Dialog open onOpenChange={(open) => !open && onDone()}>
      <DialogContent className="sm:max-w-sm">
        <DialogHeader>
          <DialogTitle>{unit ? "Birimi Düzenle" : "Yeni Birim"}</DialogTitle>
          <DialogDescription>
            Kısa kod (adet, kg, mt) ürün listelerinde birim adının yerine gösterilir.
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit((values) => save.mutateAsync(values).catch(() => undefined))} className="grid gap-3" noValidate>
          <div className="grid gap-2">
            <Label htmlFor="unit-name">Ad *</Label>
            <Input id="unit-name" placeholder="Adet" autoFocus {...register("name")} />
            {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
          </div>
          <div className="grid gap-2">
            <Label htmlFor="unit-code">Kısa Kod</Label>
            <Input id="unit-code" placeholder="adet" {...register("code")} />
            {errors.code && <p className="text-sm text-destructive">{errors.code.message}</p>}
          </div>
          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={onDone} disabled={isSubmitting}>
              Vazgeç
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting && <Loader2 className="size-4 animate-spin" />}
              Kaydet
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

const warehouseSchema = z.object({
  name: z
    .string()
    .trim()
    .min(2, "Depo adı en az 2 karakter olmalı.")
    .max(100, "Depo adı en fazla 100 karakter olabilir."),
  address: z.string().trim().max(300, "Adres en fazla 300 karakter olabilir."),
  isDefault: z.boolean(),
  isActive: z.boolean(),
});

/** Depo diyaloğu (ad, adres, varsayılan; düzenlemede ayrıca aktif). */
function WarehouseDialog({ warehouse, onDone }: { warehouse?: WarehouseDto; onDone: () => void }) {
  const queryClient = useQueryClient();
  const {
    register,
    handleSubmit,
    control,
    formState: { errors, isSubmitting },
  } = useForm<z.infer<typeof warehouseSchema>>({
    resolver: zodResolver(warehouseSchema),
    defaultValues: {
      name: warehouse?.name ?? "",
      address: warehouse?.address ?? "",
      isDefault: warehouse?.isDefault ?? false,
      isActive: warehouse?.isActive ?? true,
    },
  });

  const save = useMutation({
    mutationFn: (values: z.infer<typeof warehouseSchema>) => {
      const body = warehouse
        ? values
        : { name: values.name, address: values.address || null, isDefault: values.isDefault };
      return api(warehouse ? `/api/v1/warehouses/${warehouse.id}` : "/api/v1/warehouses", {
        method: warehouse ? "PUT" : "POST",
        body: JSON.stringify(body),
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["warehouses"] });
      toast.success(warehouse ? "Depo güncellendi." : "Depo eklendi.");
      onDone();
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : "Depo kaydedilemedi.");
    },
  });

  return (
    <Dialog open onOpenChange={(open) => !open && onDone()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{warehouse ? "Depoyu Düzenle" : "Yeni Depo"}</DialogTitle>
          <DialogDescription>
            Varsayılan depo, stok hareketlerinde ilk seçenek olarak önerilir.
            {warehouse?.isDefault && " Varsayılan depo bu özelliği kaybedemez."}
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit((values) => save.mutateAsync(values).catch(() => undefined))} className="grid gap-3" noValidate>
          <div className="grid gap-2">
            <Label htmlFor="warehouse-name">Ad *</Label>
            <Input id="warehouse-name" placeholder="Mağaza" autoFocus {...register("name")} />
            {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
          </div>
          <div className="grid gap-2">
            <Label htmlFor="warehouse-address">Adres</Label>
            <Input id="warehouse-address" placeholder="Cadde, no, il" {...register("address")} />
            {errors.address && <p className="text-sm text-destructive">{errors.address.message}</p>}
          </div>
          <div className="flex items-center justify-between rounded-md border p-3">
            <div>
              <Label htmlFor="warehouse-default">Varsayılan</Label>
              <p className="text-xs text-muted-foreground">Yeni varsayılan atanınca diğeri kaldırılır.</p>
            </div>
            <Controller
              control={control}
              name="isDefault"
              render={({ field }) => (
                <Switch
                  id="warehouse-default"
                  checked={field.value}
                  onCheckedChange={field.onChange}
                  disabled={warehouse?.isDefault}
                />
              )}
            />
          </div>
          {warehouse && (
            <div className="flex items-center justify-between rounded-md border p-3">
              <div>
                <Label htmlFor="warehouse-active">Aktif</Label>
                <p className="text-xs text-muted-foreground">Pasif depoya stok hareketi girilemez.</p>
              </div>
              <Controller
                control={control}
                name="isActive"
                render={({ field }) => (
                  <Switch id="warehouse-active" checked={field.value} onCheckedChange={field.onChange} />
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
              Kaydet
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function ConfirmDeleteDialog<T extends { id: string }>({
  target,
  name,
  onClose,
  onConfirm,
  pending,
  what,
}: {
  target: T | null;
  name: (row: T) => string;
  onClose: () => void;
  onConfirm: (id: string) => void;
  pending: boolean;
  what: string;
}) {
  return (
    <Dialog open={target !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-sm">
        <DialogHeader>
          <DialogTitle>{what} Sil</DialogTitle>
          <DialogDescription>
            {target ? name(target) : ""} silinecek. Kullanımında geçmiş kayıt olan tanımlar silinemez.
          </DialogDescription>
        </DialogHeader>
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onClose} disabled={pending}>
            Vazgeç
          </Button>
          <Button variant="destructive" disabled={pending} onClick={() => target && onConfirm(target.id)}>
            {pending && <Loader2 className="size-4 animate-spin" />}
            Sil
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
