"use client";

import type { ReactNode } from "react";

import { cn } from "@/lib/utils";

/**
 * CSS-only grafikler (PHASE 8, plan bölüm 3.1): grafik kütüphanesi eklenmez —
 * sütun yükseklikleri yüzde, etiketler flex ile çizilir. Veri yoksa grafik
 * sıfır yükseklikte durur, yer kaplamaz.
 */

interface ChartColumn {
  /** Sütun altındaki kısa etiket (ör. "15 Tem"). */
  label: string;
  /** Hover ipucu metni (title özniteliği). */
  title: string;
  primary: number;
  secondary?: number;
}

/** ~5 etiket seçer: 30 günlük ve 12 aylık eksenlerde kalabalık önlenir. */
function sampledLabels(items: ChartColumn[]): string[] {
  if (items.length <= 6) return items.map((item) => item.label);
  const indices = [0, Math.floor(items.length / 4), Math.floor(items.length / 2), Math.floor((items.length * 3) / 4), items.length - 1];
  return [...new Set(indices)].map((index) => items[index].label);
}

/** Sütun yüksekliği: maksimuma göre yüzde; sıfır değer çizilmez. */
function barHeight(value: number, max: number): string {
  if (value <= 0) return "0%";
  return `${Math.max(3, Math.round((value / max) * 100))}%`;
}

/** İkili sütun grafiği — son 30 gün gelir/gider akışı. Yeşil = birincil seri. */
export function PairedBarChart({
  items,
  primaryLabel,
  secondaryLabel,
}: {
  items: ChartColumn[];
  primaryLabel: string;
  secondaryLabel: string;
}) {
  const max = Math.max(1, ...items.map((item) => Math.max(item.primary, item.secondary ?? 0)));

  return (
    <div className="grid gap-2">
      <div className="flex items-center gap-3 text-xs text-muted-foreground">
        <span className="flex items-center gap-1">
          <span className="size-2 rounded-full bg-emerald-500" />
          {primaryLabel}
        </span>
        <span className="flex items-center gap-1">
          <span className="size-2 rounded-full bg-destructive/70" />
          {secondaryLabel}
        </span>
      </div>
      <div className="flex h-28 items-end gap-0.5 sm:gap-1" role="img" aria-label={`${primaryLabel} / ${secondaryLabel} grafiği`}>
        {items.map((item) => (
          <div key={item.label} className="flex h-full min-w-0 flex-1 items-end justify-center gap-px" title={item.title}>
            <div
              className="w-1/2 rounded-t bg-emerald-500/80 dark:bg-emerald-500/70"
              style={{ height: barHeight(item.primary, max) }}
            />
            <div
              className="w-1/2 rounded-t bg-destructive/60"
              style={{ height: barHeight(item.secondary ?? 0, max) }}
            />
          </div>
        ))}
      </div>
      <div className="flex justify-between text-[10px] text-muted-foreground">
        {sampledLabels(items).map((label) => (
          <span key={label}>{label}</span>
        ))}
      </div>
    </div>
  );
}

/** Tek serili sütun grafiği — aylık ciro gibi değerler. */
export function SingleBarChart({ items }: { items: ChartColumn[] }) {
  const max = Math.max(1, ...items.map((item) => item.primary));

  return (
    <div className="grid gap-2">
      <div className="flex h-28 items-end gap-1" role="img" aria-label="Sütun grafiği">
        {items.map((item) => (
          <div key={item.label} className="flex h-full min-w-0 flex-1 items-end" title={item.title}>
            <div
              className="w-full rounded-t bg-primary/70"
              style={{ height: barHeight(item.primary, max) }}
            />
          </div>
        ))}
      </div>
      <div className="flex justify-between text-[10px] text-muted-foreground">
        {sampledLabels(items).map((label) => (
          <span key={label}>{label}</span>
        ))}
      </div>
    </div>
  );
}

export interface RankedItem {
  id: string;
  label: string;
  value: number;
  /** Etiketin yanında küçük yazıyla gösterilen ek bilgi (ör. adet). */
  sub?: string;
}

/** Sıralı yatay çubuk listesi — en çok satan / en borçlu gibi ilk N listeleri. */
export function RankedList({
  items,
  formatValue,
  tone = "primary",
  emptyText = "Veri yok.",
}: {
  items: RankedItem[];
  formatValue: (value: number) => string;
  tone?: "primary" | "emerald";
  emptyText?: string;
}): ReactNode {
  if (items.length === 0) {
    return <p className="py-6 text-center text-sm text-muted-foreground">{emptyText}</p>;
  }
  const max = Math.max(1, ...items.map((item) => Math.abs(item.value)));

  return (
    <div className="grid gap-2.5">
      {items.map((item) => (
        <div key={item.id} className="grid gap-1">
          <div className="flex items-baseline justify-between gap-2 text-sm">
            <span className="flex min-w-0 items-baseline gap-1.5">
              <span className="truncate">{item.label}</span>
              {item.sub && <span className="shrink-0 text-xs text-muted-foreground">{item.sub}</span>}
            </span>
            <span className="shrink-0 font-medium tabular-nums">{formatValue(item.value)}</span>
          </div>
          <div className="h-1.5 w-full overflow-hidden rounded-full bg-muted">
            <div
              className={cn(
                "h-full rounded-full",
                tone === "emerald" ? "bg-emerald-500/80" : "bg-primary/60",
              )}
              style={{ width: `${Math.max(2, Math.round((Math.abs(item.value) / max) * 100))}%` }}
            />
          </div>
        </div>
      ))}
    </div>
  );
}
