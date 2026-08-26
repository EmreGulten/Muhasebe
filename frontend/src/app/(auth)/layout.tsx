import { BrandMark } from "@/components/brand-mark";

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <main className="relative flex min-h-svh flex-1 flex-col items-center justify-center gap-8 overflow-hidden bg-background p-6 md:p-10">
      <div className="pointer-events-none absolute -left-40 -top-40 size-96 rounded-full bg-primary/8 blur-3xl" />
      <div className="pointer-events-none absolute -bottom-48 -right-32 size-[28rem] rounded-full bg-emerald-400/10 blur-3xl" />
      <div className="flex flex-col items-center gap-2 text-center">
        <div className="flex items-center gap-2 font-semibold text-lg">
          <BrandMark />
          Muhasebe
        </div>
        <p className="text-sm text-muted-foreground">
          Muhasebeci olmadan işletmeni anlayabileceğin ön muhasebe uygulaması
        </p>
      </div>
      <div className="relative w-full max-w-sm">{children}</div>
    </main>
  );
}
