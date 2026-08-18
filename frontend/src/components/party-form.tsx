"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2 } from "lucide-react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { Controller, type Control } from "react-hook-form";
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
import { PARTY_TYPE_LABELS, parseMoneyInput, toMoneyInput } from "@/lib/parties";
import type { PartyResponse, PartyTypeDto } from "@/lib/types";

const optionalText = (max: number, label: string) =>
  z.string().trim().max(max, `${label} en fazla ${max} karakter olabilir.`);

const moneyInput = (message: string) =>
  z.string().refine((value) => value.trim() === "" || parseMoneyInput(value) !== null, message);

const partySchema = z.object({
  name: z
    .string()
    .trim()
    .min(2, "Cari adı en az 2 karakter olmalı.")
    .max(200, "Cari adı en fazla 200 karakter olabilir."),
  type: z.enum(["Customer", "Supplier", "Both"]),
  taxNumber: optionalText(20, "Vergi/TCKN"),
  taxOffice: optionalText(60, "Vergi dairesi"),
  phone: optionalText(30, "Telefon"),
  email: z
    .string()
    .trim()
    .max(150, "E-posta en fazla 150 karakter olabilir.")
    .refine((value) => value === "" || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value), "Geçerli bir e-posta adresi girin."),
  address: optionalText(300, "Adres"),
  city: optionalText(60, "İl"),
  district: optionalText(60, "İlçe"),
  contactName: optionalText(120, "Yetkili adı"),
  openingBalance: moneyInput("Açılış bakiyesi en fazla 2 basamak ondalık içerebilir (örn. 1250,50)."),
  creditLimit: moneyInput("Kredi limiti en fazla 2 basamak ondalık içerebilir.")
    .refine((value) => value.trim() === "" || (parseMoneyInput(value) ?? -1) >= 0, "Kredi limiti negatif olamaz."),
  notes: optionalText(1000, "Not"),
  isActive: z.boolean(),
});

type PartyFormValues = z.infer<typeof partySchema>;

/** Radix Select register ile çalışmadığından tür alanı Controller ile bağlanır. */
function TypeSelect({ control }: { control: Control<PartyFormValues> }) {
  return (
    <Controller
      control={control}
      name="type"
      render={({ field }) => (
        <Select value={field.value} onValueChange={field.onChange}>
          <SelectTrigger id="party-type">
            <SelectValue placeholder="Tür seçin" />
          </SelectTrigger>
          <SelectContent>
            {(Object.keys(PARTY_TYPE_LABELS) as PartyTypeDto[]).map((type) => (
              <SelectItem key={type} value={type}>
                {PARTY_TYPE_LABELS[type]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      )}
    />
  );
}

const emptyForm = (type: PartyTypeDto): PartyFormValues => ({
  name: "",
  type,
  taxNumber: "",
  taxOffice: "",
  phone: "",
  email: "",
  address: "",
  city: "",
  district: "",
  contactName: "",
  openingBalance: "",
  creditLimit: "",
  notes: "",
  isActive: true,
});

function fromParty(party: PartyResponse): PartyFormValues {
  return {
    name: party.name,
    type: party.type,
    taxNumber: party.taxNumber ?? "",
    taxOffice: party.taxOffice ?? "",
    phone: party.phone ?? "",
    email: party.email ?? "",
    address: party.address ?? "",
    city: party.city ?? "",
    district: party.district ?? "",
    contactName: party.contactName ?? "",
    openingBalance: "",
    creditLimit: toMoneyInput(party.creditLimit),
    notes: party.notes ?? "",
    isActive: party.isActive,
  };
}

/** Yeni cari kartı / mevcut kart düzenleme formu (Dialog içinde kullanılır). */
export function PartyForm({
  mode,
  defaultType,
  party,
  onDone,
}: {
  mode: "create" | "edit";
  defaultType: PartyTypeDto;
  party?: PartyResponse;
  onDone: () => void;
}) {
  const queryClient = useQueryClient();
  const isEdit = mode === "edit" && party;

  const {
    register,
    handleSubmit,
    control,
    formState: { errors, isSubmitting },
  } = useForm<PartyFormValues>({
    resolver: zodResolver(partySchema),
    defaultValues: isEdit ? fromParty(party) : emptyForm(defaultType),
  });

  const save = useMutation({
    mutationFn: (values: PartyFormValues) => {
      const body = isEdit
        ? {
            name: values.name,
            type: values.type,
            taxNumber: values.taxNumber || null,
            taxOffice: values.taxOffice || null,
            phone: values.phone || null,
            email: values.email || null,
            address: values.address || null,
            city: values.city || null,
            district: values.district || null,
            contactName: values.contactName || null,
            creditLimit: parseMoneyInput(values.creditLimit) ?? 0,
            notes: values.notes || null,
            isActive: values.isActive,
          }
        : {
            name: values.name,
            type: values.type,
            taxNumber: values.taxNumber || null,
            taxOffice: values.taxOffice || null,
            phone: values.phone || null,
            email: values.email || null,
            address: values.address || null,
            city: values.city || null,
            district: values.district || null,
            contactName: values.contactName || null,
            openingBalance: parseMoneyInput(values.openingBalance) ?? 0,
            creditLimit: parseMoneyInput(values.creditLimit) ?? 0,
            notes: values.notes || null,
          };
      return api(isEdit ? `/api/v1/parties/${party!.id}` : "/api/v1/parties", {
        method: isEdit ? "PUT" : "POST",
        body: JSON.stringify(body),
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["parties"] });
      if (isEdit) {
        queryClient.invalidateQueries({ queryKey: ["party", party!.id] });
      }
      toast.success(isEdit ? "Cari kartı güncellendi." : "Cari kartı oluşturuldu.");
      onDone();
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : "Cari kartı kaydedilemedi.");
    },
  });

  const onSubmit = handleSubmit((values) => save.mutateAsync(values).catch(() => undefined));

  return (
    <form onSubmit={onSubmit} className="grid gap-4" noValidate>
      <div className="grid gap-2">
        <Label htmlFor="party-name">Cari Adı *</Label>
        <Input id="party-name" placeholder="Yılmaz Ticaret" autoFocus={!isEdit} {...register("name")} />
        {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="grid gap-2">
          <Label htmlFor="party-type">Tür *</Label>
          <TypeSelect control={control} />
          {errors.type && <p className="text-sm text-destructive">{errors.type.message}</p>}
        </div>
        <div className="grid gap-2">
          <Label htmlFor="party-phone">Telefon</Label>
          <Input id="party-phone" placeholder="0 532 000 00 00" {...register("phone")} />
          {errors.phone && <p className="text-sm text-destructive">{errors.phone.message}</p>}
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="grid gap-2">
          <Label htmlFor="party-email">E-posta</Label>
          <Input id="party-email" type="email" placeholder="muhasebe@firma.com" {...register("email")} />
          {errors.email && <p className="text-sm text-destructive">{errors.email.message}</p>}
        </div>
        <div className="grid gap-2">
          <Label htmlFor="party-contact">Yetkili</Label>
          <Input id="party-contact" placeholder="Ad Soyad" {...register("contactName")} />
          {errors.contactName && <p className="text-sm text-destructive">{errors.contactName.message}</p>}
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="grid gap-2">
          <Label htmlFor="party-tax-number">Vergi / TCKN No</Label>
          <Input id="party-tax-number" placeholder="1234567890" {...register("taxNumber")} />
          {errors.taxNumber && <p className="text-sm text-destructive">{errors.taxNumber.message}</p>}
        </div>
        <div className="grid gap-2">
          <Label htmlFor="party-tax-office">Vergi Dairesi</Label>
          <Input id="party-tax-office" placeholder="Kadıköy" {...register("taxOffice")} />
          {errors.taxOffice && <p className="text-sm text-destructive">{errors.taxOffice.message}</p>}
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="grid gap-2">
          <Label htmlFor="party-city">İl</Label>
          <Input id="party-city" placeholder="İstanbul" {...register("city")} />
          {errors.city && <p className="text-sm text-destructive">{errors.city.message}</p>}
        </div>
        <div className="grid gap-2">
          <Label htmlFor="party-district">İlçe</Label>
          <Input id="party-district" placeholder="Kadıköy" {...register("district")} />
          {errors.district && <p className="text-sm text-destructive">{errors.district.message}</p>}
        </div>
      </div>

      <div className="grid gap-2">
        <Label htmlFor="party-address">Adres</Label>
        <Input id="party-address" placeholder="Cadde, sokak, no" {...register("address")} />
        {errors.address && <p className="text-sm text-destructive">{errors.address.message}</p>}
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        {!isEdit && (
          <div className="grid gap-2">
            <Label htmlFor="party-opening">Açılış Bakiyesi</Label>
            <Input id="party-opening" inputMode="decimal" placeholder="0,00" {...register("openingBalance")} />
            <p className="text-xs text-muted-foreground">
              Pozitif = taraf size borçlu, negatif = tarafa borçlusunuz. Sonradan değiştirilemez.
            </p>
            {errors.openingBalance && <p className="text-sm text-destructive">{errors.openingBalance.message}</p>}
          </div>
        )}
        <div className="grid gap-2">
          <Label htmlFor="party-credit-limit">Kredi Limiti</Label>
          <Input id="party-credit-limit" inputMode="decimal" placeholder="0,00" {...register("creditLimit")} />
          {errors.creditLimit && <p className="text-sm text-destructive">{errors.creditLimit.message}</p>}
        </div>
      </div>

      <div className="grid gap-2">
        <Label htmlFor="party-notes">Notlar</Label>
        <Textarea id="party-notes" rows={3} placeholder="Ödeme koşulları, özel bilgiler..." {...register("notes")} />
        {errors.notes && <p className="text-sm text-destructive">{errors.notes.message}</p>}
      </div>

      {isEdit && (
        <div className="flex items-center justify-between rounded-md border p-3">
          <div>
            <Label htmlFor="party-active">Aktif</Label>
            <p className="text-xs text-muted-foreground">Pasif carilere hareket girilemez.</p>
          </div>
          <Controller
            control={control}
            name="isActive"
            render={({ field }) => (
              <Switch id="party-active" checked={field.value} onCheckedChange={field.onChange} />
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
          {isEdit ? "Kaydet" : "Cariyi Oluştur"}
        </Button>
      </div>
    </form>
  );
}
