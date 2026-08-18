"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2 } from "lucide-react";
import { useState } from "react";
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
import { Textarea } from "@/components/ui/textarea";
import { api } from "@/lib/api";
import {
  MANUAL_TRANSACTION_TYPES,
  dateInputToIso,
  isoToDateInput,
  parseMoneyInput,
} from "@/lib/parties";
import type { ManualTransactionTypeDto, PartyTransactionDto } from "@/lib/types";

const transactionSchema = z
  .object({
    type: z.enum(["Debit", "Credit", "Adjustment", "OpeningBalance"]),
    date: z
      .string()
      .min(1, "Hareket tarihi gereklidir.")
      .regex(/^\d{4}-\d{2}-\d{2}$/, "Geçerli bir tarih seçin."),
    amount: z
      .string()
      .refine((value) => parseMoneyInput(value) !== null, "Tutar en fazla 2 basamak ondalık içerebilir (örn. 1250,50).")
      .refine((value) => (parseMoneyInput(value) ?? 0) !== 0, "Hareket tutarı sıfır olamaz."),
    dueDate: z
      .string()
      .refine((value) => value === "" || /^\d{4}-\d{2}-\d{2}$/.test(value), "Geçerli bir vade tarihi seçin."),
    description: z.string().trim().max(300, "Açıklama en fazla 300 karakter olabilir."),
  })
  .superRefine((values, ctx) => {
    if (values.dueDate && values.dueDate < values.date) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["dueDate"],
        message: "Vade tarihi hareket tarihinden önce olamaz.",
      });
    }
  });

type TransactionFormValues = z.infer<typeof transactionSchema>;

/** Manuel cari hareketi formu (Dialog içinde kullanılır). */
export function PartyTransactionForm({
  partyId,
  onDone,
}: {
  partyId: string;
  onDone: () => void;
}) {
  const queryClient = useQueryClient();
  const today = isoToDateInput(new Date().toISOString());
  const [type, setType] = useState<ManualTransactionTypeDto>("Debit");

  const {
    register,
    handleSubmit,
    control,
    formState: { errors, isSubmitting },
  } = useForm<TransactionFormValues>({
    resolver: zodResolver(transactionSchema),
    defaultValues: { type: "Debit", date: today, amount: "", dueDate: "", description: "" },
  });

  const create = useMutation({
    mutationFn: (values: TransactionFormValues) => {
      const parsed = parseMoneyInput(values.amount)!;
      // Credit pozitif girilir, alacak (negatif) olarak gönderilir.
      const amount = values.type === "Credit" ? -Math.abs(parsed) : parsed;
      return api<PartyTransactionDto>(`/api/v1/parties/${partyId}/transactions`, {
        method: "POST",
        body: JSON.stringify({
          type: values.type,
          date: dateInputToIso(values.date),
          amount,
          dueDate: values.dueDate ? dateInputToIso(values.dueDate) : null,
          description: values.description || null,
        }),
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["party", partyId] });
      queryClient.invalidateQueries({ queryKey: ["party-statement", partyId] });
      queryClient.invalidateQueries({ queryKey: ["parties"] });
      toast.success("Cari hareketi kaydedildi.");
      onDone();
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : "Hareket kaydedilemedi.");
    },
  });

  const onSubmit = handleSubmit((values) => create.mutateAsync(values).catch(() => undefined));

  const isSigned = MANUAL_TRANSACTION_TYPES.find((t) => t.value === type)?.signed ?? false;

  return (
    <form onSubmit={onSubmit} className="grid gap-4" noValidate>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="grid gap-2">
          <Label htmlFor="tx-type">Hareket Türü *</Label>
          <Controller
            control={control}
            name="type"
            render={({ field }) => (
              <Select
                value={field.value}
                onValueChange={(value) => {
                  field.onChange(value);
                  setType(value as ManualTransactionTypeDto);
                }}
              >
                <SelectTrigger id="tx-type">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {MANUAL_TRANSACTION_TYPES.map((option) => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
          <p className="text-xs text-muted-foreground">
            Satış/tahsilat/alış/ödeme hareketleri ilgili modüllerde oluşur; buradan girilemez.
          </p>
        </div>
        <div className="grid gap-2">
          <Label htmlFor="tx-amount">Tutar *</Label>
          <Input id="tx-amount" inputMode="decimal" placeholder="1250,50" {...register("amount")} />
          <p className="text-xs text-muted-foreground">
            {type === "Credit"
              ? "Alacak tutarını pozitif girin; sistem negatif kaydeder."
              : isSigned
                ? "Pozitif = borç, negatif = alacak."
                : "Borç tutarını pozitif girin."}
          </p>
          {errors.amount && <p className="text-sm text-destructive">{errors.amount.message}</p>}
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="grid gap-2">
          <Label htmlFor="tx-date">Tarih *</Label>
          <Input id="tx-date" type="date" {...register("date")} />
          {errors.date && <p className="text-sm text-destructive">{errors.date.message}</p>}
        </div>
        <div className="grid gap-2">
          <Label htmlFor="tx-due-date">Vade Tarihi</Label>
          <Input id="tx-due-date" type="date" {...register("dueDate")} />
          {errors.dueDate && (
            <p className="text-sm text-destructive">{errors.dueDate.message}</p>
          )}
        </div>
      </div>

      <div className="grid gap-2">
        <Label htmlFor="tx-description">Açıklama</Label>
        <Textarea
          id="tx-description"
          rows={2}
          placeholder="Örn. nakit tahsilat farkı düzeltmesi"
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
