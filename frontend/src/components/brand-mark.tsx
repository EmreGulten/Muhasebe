import { cn } from "@/lib/utils";

export function BrandMark({ className }: { className?: string }) {
  return (
    <span
      aria-hidden="true"
      className={cn(
        "relative flex size-8 items-center justify-center overflow-hidden rounded-[0.7rem] bg-primary text-sm font-bold text-primary-foreground shadow-sm",
        "after:absolute after:inset-x-1.5 after:bottom-1 after:h-0.5 after:rounded-full after:bg-emerald-300",
        className,
      )}
    >
      M
    </span>
  );
}
