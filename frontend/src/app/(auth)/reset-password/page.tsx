"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2 } from "lucide-react";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense } from "react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { api } from "@/lib/api";
import type { MessageResponse } from "@/lib/types";

const resetSchema = z
  .object({
    token: z.string().min(1, "Sıfırlama kodu gereklidir."),
    password: z
      .string()
      .min(8, "Parola en az 8 karakter olmalı.")
      .max(128, "Parola en fazla 128 karakter olabilir.")
      .regex(/[a-z]/, "Parola en az bir küçük harf içermeli.")
      .regex(/[A-Z]/, "Parola en az bir büyük harf içermeli.")
      .regex(/[0-9]/, "Parola en az bir rakam içermeli.")
      .regex(/[^a-zA-Z0-9]/, "Parola en az bir özel karakter içermeli."),
    confirmPassword: z.string().min(1, "Parola tekrarı gereklidir."),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: "Parolalar eşleşmiyor.",
    path: ["confirmPassword"],
  });

type ResetForm = z.infer<typeof resetSchema>;

function ResetPasswordForm() {
  const router = useRouter();
  const searchParams = useSearchParams();

  // Bağlantı /reset-password?email=...&token=... formatında gelir.
  const email = searchParams.get("email") ?? "";
  const tokenFromLink = searchParams.get("token") ?? "";

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ResetForm>({
    resolver: zodResolver(resetSchema),
    defaultValues: { token: tokenFromLink, password: "", confirmPassword: "" },
  });

  const onSubmit = handleSubmit(async (values) => {
    try {
      await api<MessageResponse>("/api/v1/auth/reset-password", {
        method: "POST",
        body: JSON.stringify({ email, token: values.token, newPassword: values.password }),
      });
      toast.success("Parolanız güncellendi. Yeni parolanızla giriş yapabilirsiniz.");
      router.replace("/login");
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Parola sıfırlanamadı.");
    }
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle>Yeni parola belirle</CardTitle>
        <CardDescription>{email ? `Hesap: ${email}` : "Sıfırlama kodunuzu ve yeni parolanızı girin."}</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={onSubmit} className="grid gap-4" noValidate>
          <div className="grid gap-2">
            <Label htmlFor="token">Sıfırlama Kodu</Label>
            <Input id="token" placeholder="E-postanızdaki kod" className="font-mono" autoFocus={!tokenFromLink} {...register("token")} />
            {errors.token && <p className="text-sm text-destructive">{errors.token.message}</p>}
            <p className="text-xs text-muted-foreground">
              Kod e-postanıza gönderildi; bağlantıya tıkladıysanız otomatik dolmalıdır.
            </p>
          </div>
          <div className="grid gap-2">
            <Label htmlFor="password">Yeni Parola</Label>
            <Input id="password" type="password" autoComplete="new-password" {...register("password")} />
            {errors.password && <p className="text-sm text-destructive">{errors.password.message}</p>}
          </div>
          <div className="grid gap-2">
            <Label htmlFor="confirmPassword">Parola Tekrarı</Label>
            <Input id="confirmPassword" type="password" autoComplete="new-password" {...register("confirmPassword")} />
            {errors.confirmPassword && <p className="text-sm text-destructive">{errors.confirmPassword.message}</p>}
          </div>
          <Button type="submit" disabled={isSubmitting || !email} className="w-full">
            {isSubmitting && <Loader2 className="size-4 animate-spin" />}
            Parolayı Güncelle
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

export default function ResetPasswordPage() {
  return (
    <Suspense fallback={null}>
      <ResetPasswordForm />
    </Suspense>
  );
}
