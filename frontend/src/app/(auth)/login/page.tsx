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

const loginSchema = z.object({
  email: z.string().min(1, "E-posta gereklidir.").email("Geçerli bir e-posta adresi girin."),
  password: z.string().min(1, "Parola gereklidir."),
});

type LoginForm = z.infer<typeof loginSchema>;

export default function LoginPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginForm>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: "", password: "" },
  });

  const onSubmit = handleSubmit(async (values) => {
    try {
      const result = await api<AuthResponse>("/api/v1/auth/login", {
        method: "POST",
        body: JSON.stringify(values),
      });

      authStore.setToken(result.accessToken);
      if (result.tenants.length > 0) {
        setActiveTenantId(result.tenants[0].tenantId);
      }
      queryClient.clear();

      toast.success(`Hoş geldin, ${result.user.fullName.split(" ")[0]}!`);
      router.replace(result.tenants.length > 0 ? "/dashboard" : "/business/new");
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Giriş yapılamadı.");
    }
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle>Giriş yap</CardTitle>
        <CardDescription>İşletmenize erişmek için hesabınıza giriş yapın.</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={onSubmit} className="grid gap-4" noValidate>
          <div className="grid gap-2">
            <Label htmlFor="email">E-posta</Label>
            <Input
              id="email"
              type="email"
              placeholder="ornek@isletmem.com"
              autoComplete="email"
              autoFocus
              {...register("email")}
            />
            {errors.email && <p className="text-sm text-destructive">{errors.email.message}</p>}
          </div>
          <div className="grid gap-2">
            <div className="flex items-center justify-between">
              <Label htmlFor="password">Parola</Label>
              <Link href="/forgot-password" className="text-sm text-muted-foreground hover:text-foreground">
                Parolamı unuttum
              </Link>
            </div>
            <Input id="password" type="password" autoComplete="current-password" {...register("password")} />
            {errors.password && <p className="text-sm text-destructive">{errors.password.message}</p>}
          </div>
          <Button type="submit" disabled={isSubmitting} className="w-full">
            {isSubmitting && <Loader2 className="size-4 animate-spin" />}
            Giriş yap
          </Button>
        </form>
      </CardContent>
      <CardFooter>
        <p className="text-sm text-muted-foreground">
          Hesabın yok mu?{" "}
          <Link href="/register" className="font-medium text-primary hover:underline">
            Ücretsiz kayıt ol
          </Link>
        </p>
      </CardFooter>
    </Card>
  );
}
