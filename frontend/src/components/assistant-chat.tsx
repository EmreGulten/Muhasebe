"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { Bot, Loader2, Send, Sparkles, User } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { toast } from "sonner";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { api } from "@/lib/api";
import { getActiveTenantId } from "@/lib/auth-store";
import type { AiMessageDto, AskAssistantResponse, MeResponse, PagedResponse } from "@/lib/types";

/** Plan bölüm 11.1'deki onaylı soru tiplerinden örnekler. */
const EXAMPLE_QUESTIONS = [
  "Bu ay ne kadar kazandım?",
  "Bana borcu olan müşterileri göster.",
  "En çok satan ürünlerim hangileri?",
  "Hangi ürünlerin stoğu bitmek üzere?",
  "Önümüzdeki 7 günde ne kadar ödeme yapmam gerekir?",
  "Geçen aya göre giderim nasıl değişti?",
];

/** Asistan yalnızca bu rollerde kullanılabilir (AiAssistant.Use izni). */
const ASSISTANT_ROLES = new Set(["Owner", "Admin", "Accountant"]);

function providerLabel(provider: string | null): string | null {
  if (provider === "offline") return "Çevrimdışı asistan";
  if (provider === "openai") return "OpenAI";
  return provider;
}

/** Saat etiketi — sohbet baloncukları için "14:32" gibi. */
function timeLabel(iso: string): string {
  return new Date(iso).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" });
}

/** AI Asistan: işletme verileri üzerinden doğal dilde soru-cevap (PHASE 9). */
export function AssistantChat() {
  const [question, setQuestion] = useState("");
  // Bu oturumda gönderilen soru + yanıtlar; geçmiş sorgudan türetilerek birleşir.
  const [sessionMessages, setSessionMessages] = useState<AiMessageDto[]>([]);
  const [provider, setProvider] = useState<string | null>(null);
  const bottomRef = useRef<HTMLDivElement>(null);

  // Aktif işletme + rol: izin kontrolü ve tenant gating.
  const me = useQuery({
    queryKey: ["me"],
    queryFn: () => api<MeResponse>("/api/v1/auth/me"),
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
  const activeTenant =
    me.data?.tenants.find((tenant) => tenant.tenantId === getActiveTenantId()) ?? me.data?.tenants[0] ?? null;
  const canUse = activeTenant !== null && ASSISTANT_ROLES.has(activeTenant.role);
  const loading = me.isPending;

  // Geçmiş: en yeni önce gelir → kronolojik sıraya çevrilir.
  const history = useQuery({
    queryKey: ["assistant-history"],
    queryFn: () => api<PagedResponse<AiMessageDto>>("/api/v1/assistant/history?page=1&pageSize=50"),
    enabled: canUse,
  });

  // Geçmiş en yeni önce gelir → kronolojik sıraya çevrilip oturum mesajlarıyla birleşir.
  const messages = useMemo(
    () => [...[...(history.data?.items ?? [])].reverse(), ...sessionMessages],
    [history.data, sessionMessages],
  );

  // Soru gönderimi: kullanıcı baloncuğu anında eklenir, yanıt gelince tamamlanır.
  const ask = useMutation({
    mutationFn: (text: string) =>
      api<AskAssistantResponse>("/api/v1/assistant/ask", {
        method: "POST",
        body: JSON.stringify({ question: text }),
      }),
    onMutate: (text) => {
      setSessionMessages((previous) => [
        ...previous,
        { id: `question-${Date.now()}`, role: "User", content: text, createdAtUtc: new Date().toISOString() },
      ]);
    },
    onSuccess: (response) => {
      setProvider(response.provider);
      setSessionMessages((previous) => [
        ...previous,
        { id: `answer-${Date.now()}`, role: "Assistant", content: response.answer, createdAtUtc: new Date().toISOString() },
      ]);
    },
    onError: (mutationError) => {
      toast.error(mutationError instanceof Error ? mutationError.message : "Soru gönderilemedi.");
    },
  });

  // Yeni mesajda görünümü en alta kaydır.
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages, ask.isPending]);

  function send(text: string) {
    const trimmed = text.trim();
    if (!trimmed || ask.isPending) return;
    setQuestion("");
    ask.mutate(trimmed);
  }

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="text-2xl font-semibold tracking-tight">AI Asistan</h1>
        {providerLabel(provider) && (
          <Badge variant="secondary">{providerLabel(provider)}</Badge>
        )}
        <p className="w-full text-sm text-muted-foreground">
          İşletme verileriniz üzerinden doğal dilde soru sorun. Asistan yalnızca onaylı iş
          araçlarından okur; hiçbir koşulda doğrudan veritabanına erişmez.
        </p>
      </div>

      {loading ? (
        <div className="flex justify-center py-16">
          <Loader2 className="size-6 animate-spin text-muted-foreground" />
        </div>
      ) : !canUse ? (
        <Card>
          <CardContent className="grid gap-2 py-6 text-center">
            <Sparkles className="mx-auto size-8 text-muted-foreground" />
            <p className="text-sm text-muted-foreground">
              AI asistanı yalnızca Sahip, Yönetici ve Muhasebeci rolleri kullanabilir.
            </p>
          </CardContent>
        </Card>
      ) : (
        <>
          <Card>
            <CardContent className="flex max-h-[60vh] min-h-80 flex-col gap-4 overflow-y-auto py-4">
              {messages.length === 0 && !ask.isPending ? (
                <div className="grid gap-2 text-center">
                  <Bot className="mx-auto size-8 text-muted-foreground" />
                  <p className="text-sm text-muted-foreground">
                    Merhaba! Aşağıdaki örnek sorulardan biriyle başlayabilirsiniz.
                  </p>
                </div>
              ) : (
                messages.map((message) => (
                  <div
                    key={message.id}
                    className={
                      message.role === "User"
                        ? "ml-auto max-w-[85%] rounded-2xl rounded-br-sm bg-primary px-4 py-2 text-primary-foreground"
                        : "mr-auto max-w-[85%] whitespace-pre-line rounded-2xl rounded-bl-sm bg-muted px-4 py-2"
                    }
                  >
                    <p className="text-sm">{message.content}</p>
                    <p className="mt-1 flex items-center justify-end gap-1 text-[11px] opacity-70">
                      {message.role === "User" ? <User className="size-3" /> : <Bot className="size-3" />}
                      {timeLabel(message.createdAtUtc)}
                    </p>
                  </div>
                ))
              )}
              {ask.isPending && (
                <div className="mr-auto flex items-center gap-2 rounded-2xl bg-muted px-4 py-2 text-sm text-muted-foreground">
                  <Loader2 className="size-4 animate-spin" />
                  Asistan verilerinizi inceliyor…
                </div>
              )}
              <div ref={bottomRef} />
            </CardContent>
          </Card>

          {messages.length === 0 && (
            <div className="flex flex-wrap gap-2">
              {EXAMPLE_QUESTIONS.map((example) => (
                <Button
                  key={example}
                  variant="outline"
                  size="sm"
                  className="h-auto whitespace-normal py-1.5 text-left text-xs"
                  onClick={() => send(example)}
                  disabled={ask.isPending}
                >
                  {example}
                </Button>
              ))}
            </div>
          )}

          <form
            className="flex gap-2"
            onSubmit={(event) => {
              event.preventDefault();
              send(question);
            }}
          >
            <Input
              value={question}
              onChange={(event) => setQuestion(event.target.value)}
              placeholder="Örn: Bu ay ne kadar kazandım?"
              maxLength={500}
              disabled={ask.isPending}
            />
            <Button type="submit" size="icon" disabled={ask.isPending || !question.trim()}>
              {ask.isPending ? <Loader2 className="size-4 animate-spin" /> : <Send className="size-4" />}
            </Button>
          </form>
        </>
      )}
    </div>
  );
}
