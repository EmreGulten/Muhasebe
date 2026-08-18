"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Building2, Loader2 } from "lucide-react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { api } from "@/lib/api";
import { setActiveTenantId } from "@/lib/auth-store";
import type { TenantResponse } from "@/lib/types";

const createTenantSchema = z.object({
  name: z
    .string()
    .min(2, "İşletme adı en az 2 karakter olmalı.")
    .max(120, "İşletme adı en fazla 120 karakter olabilir."),
});

type CreateTenantForm = z.infer<typeof createTenantSchema>;

export default function NewBusinessPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<CreateTenantForm>({
    resolver: zodResolver(createTenantSchema),
    defaultValues: { name: "" },
  });

  const createTenant = useMutation({
    mutationFn: (values: CreateTenantForm) =>
      api<TenantResponse>("/api/v1/tenants", {
        method: "POST",
        body: JSON.stringify(values),
      }),
    onSuccess: (tenant) => {
      setActiveTenantId(tenant.id);
      queryClient.invalidateQueries({ queryKey: ["me"] });
      toast.success(`${tenant.name} oluşturuldu. Hoş geldiniz!`);
      router.replace("/dashboard");
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : "İşletme oluşturulamadı.");
    },
  });

  const onSubmit = handleSubmit((values) => createTenant.mutateAsync(values).catch(() => undefined));

  return (
    <div className="mx-auto w-full max-w-lg">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Building2 className="size-5 text-muted-foreground" />
            Yeni İşletme
          </CardTitle>
          <CardDescription>
            Her işletmenin kayıtları ayrı tutulur. Dilediğiniz zaman başka bir işletme daha ekleyebilirsiniz.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={onSubmit} className="grid gap-4" noValidate>
            <div className="grid gap-2">
              <Label htmlFor="name">İşletme Adı</Label>
              <Input id="name" placeholder="Yılmaz Kırtasiye" autoFocus {...register("name")} />
              {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
            </div>
            <Button type="submit" disabled={isSubmitting} className="w-full">
              {isSubmitting && <Loader2 className="size-4 animate-spin" />}
              İşletmeyi Oluştur
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
