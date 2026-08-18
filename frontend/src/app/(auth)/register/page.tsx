"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useQueryClient } from "@tanstack/react-query";
import { Loader2 } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { api } from "@/lib/api";
import { authStore, setActiveTenantId } from "@/lib/auth-store";
import type { AuthResponse } from "@/lib/types";

const registerSchema = z.object({
  fullName: z.string().min(2, "Ad soyad en az 2 karakter olmalı.").max(100, "Ad soyad en fazla 100 karakter olabilir."),
  businessName: z.string().max(120, "İşletme adı en fazla 120 karakter olabilir.").optional(),
  email: z.string().min(1, "E-posta gereklidir.").email("Geçerli bir e-posta adresi girin."),
  password: z
    .string()
    .min(8, "Parola en az 8 karakter olmalı.")
    .max(128, "Parola en fazla 128 karakter olabilir.")
    .regex(/[a-z]/, "Parola en az bir küçük harf içermeli.")
    .regex(/[A-Z]/, "Parola en az bir büyük harf içermeli.")
    .regex(/[0-9]/, "Parola en az bir rakam içermeli.")
    .regex(/[^a-zA-Z0-9]/, "Parola en az bir özel karakter içermeli."),
  confirmPassword: z.string().min(1, "Parola tekrarı gereklidir."),
}).refine((data) => data.password === data.confirmPassword, {
  message: "Parolalar eşleşmiyor.",
  path: ["confirmPassword"],
});

type RegisterForm = z.infer<typeof registerSchema>;

export default function RegisterPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RegisterForm>({
    resolver: zodResolver(registerSchema),
    defaultValues: { fullName: "", businessName: "", email: "", password: "", confirmPassword: "" },
  });

  const onSubmit = handleSubmit(async (values) => {
    try {
      const result = await api<AuthResponse>("/api/v1/auth/register", {
        method: "POST",
        body: JSON.stringify({
          fullName: values.fullName,
          businessName: values.businessName?.trim() || undefined,
          email: values.email,
          password: values.password,
        }),
      });

      authStore.setToken(result.accessToken);
      if (result.tenants.length > 0) {
        setActiveTenantId(result.tenants[0].tenantId);
      }
      queryClient.clear();

      toast.success("Hesabınız oluşturuldu. Hoş geldiniz!");
      router.replace(result.tenants.length > 0 ? "/dashboard" : "/business/new");
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Kayıt tamamlanamadı.");
    }
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle>Ücretsiz kayıt ol</CardTitle>
        <CardDescription>İşletmenizin ön muhasebesini dakikalar içinde yönetmeye başlayın.</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={onSubmit} className="grid gap-4" noValidate>
          <div className="grid gap-2">
            <Label htmlFor="fullName">Ad Soyad</Label>
            <Input id="fullName" placeholder="Ayşe Yılmaz" autoComplete="name" autoFocus {...register("fullName")} />
            {errors.fullName && <p className="text-sm text-destructive">{errors.fullName.message}</p>}
          </div>
          <div className="grid gap-2">
            <Label htmlFor="businessName">
              İşletme Adı <span className="text-muted-foreground">(isteğe bağlı)</span>
            </Label>
            <Input id="businessName" placeholder="Yılmaz Kırtasiye" autoComplete="organization" {...register("businessName")} />
            {errors.businessName && <p className="text-sm text-destructive">{errors.businessName.message}</p>}
            <p className="text-xs text-muted-foreground">
              Boş bırakırsanız &quot;&lt;Ad Soyad&gt; İşletmesi&quot; olarak oluşturulur.
            </p>
          </div>
          <div className="grid gap-2">
            <Label htmlFor="email">E-posta</Label>
            <Input id="email" type="email" placeholder="ornek@isletmem.com" autoComplete="email" {...register("email")} />
            {errors.email && <p className="text-sm text-destructive">{errors.email.message}</p>}
          </div>
          <div className="grid gap-2">
            <Label htmlFor="password">Parola</Label>
            <Input id="password" type="password" autoComplete="new-password" {...register("password")} />
            {errors.password && <p className="text-sm text-destructive">{errors.password.message}</p>}
            <p className="text-xs text-muted-foreground">
              En az 8 karakter; büyük harf, küçük harf, rakam ve özel karakter içermeli.
            </p>
          </div>
          <div className="grid gap-2">
            <Label htmlFor="confirmPassword">Parola Tekrarı</Label>
            <Input id="confirmPassword" type="password" autoComplete="new-password" {...register("confirmPassword")} />
            {errors.confirmPassword && <p className="text-sm text-destructive">{errors.confirmPassword.message}</p>}
          </div>
          <Button type="submit" disabled={isSubmitting} className="w-full">
            {isSubmitting && <Loader2 className="size-4 animate-spin" />}
            Hesap Oluştur
          </Button>
        </form>
      </CardContent>
      <CardFooter>
        <p className="text-sm text-muted-foreground">
          Zaten hesabın var mı?{" "}
          <Link href="/login" className="font-medium text-primary hover:underline">
            Giriş yap
          </Link>
        </p>
      </CardFooter>
    </Card>
  );
}
