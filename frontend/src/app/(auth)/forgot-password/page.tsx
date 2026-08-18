"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2, MailCheck } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { api } from "@/lib/api";
import type { MessageResponse } from "@/lib/types";

const forgotSchema = z.object({
  email: z.string().min(1, "E-posta gereklidir.").email("Geçerli bir e-posta adresi girin."),
});

type ForgotForm = z.infer<typeof forgotSchema>;

export default function ForgotPasswordPage() {
  const [sent, setSent] = useState(false);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ForgotForm>({
    resolver: zodResolver(forgotSchema),
    defaultValues: { email: "" },
  });

  const onSubmit = handleSubmit(async (values) => {
    try {
      await api<MessageResponse>("/api/v1/auth/forgot-password", {
        method: "POST",
        body: JSON.stringify(values),
      });
      setSent(true);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "İstek başarısız oldu.");
    }
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle>Parolamı sıfırla</CardTitle>
        <CardDescription>
          Hesabınıza bağlı e-posta adresini girin, sıfırlama bağlantısını oraya gönderelim.
        </CardDescription>
      </CardHeader>
      <CardContent>
        {sent ? (
          <div className="grid gap-4 text-center">
            <MailCheck className="mx-auto size-10 text-muted-foreground" />
            <p className="text-sm text-muted-foreground">
              Bağlantı gönderildi (gevise girmemiş olabilir — spam klasörünü de kontrol edin). E-postanız geçerliyse
              birkaç dakika içinde sıfırlama bağlantısı alacaksınız.
            </p>
          </div>
        ) : (
          <form onSubmit={onSubmit} className="grid gap-4" noValidate>
            <div className="grid gap-2">
              <Label htmlFor="email">E-posta</Label>
              <Input id="email" type="email" placeholder="ornek@isletmem.com" autoComplete="email" autoFocus {...register("email")} />
              {errors.email && <p className="text-sm text-destructive">{errors.email.message}</p>}
            </div>
            <Button type="submit" disabled={isSubmitting} className="w-full">
              {isSubmitting && <Loader2 className="size-4 animate-spin" />}
              Sıfırlama bağlantısı gönder
            </Button>
          </form>
        )}
      </CardContent>
      <CardFooter className="justify-center">
        <Link href="/login" className="text-sm text-muted-foreground hover:text-foreground">
          Girişe geri dön
        </Link>
      </CardFooter>
    </Card>
  );
}
