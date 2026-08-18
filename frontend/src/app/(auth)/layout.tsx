import { Calculator } from "lucide-react";

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <main className="flex min-h-svh flex-1 flex-col items-center justify-center gap-6 bg-muted/40 p-6 md:p-10">
      <div className="flex flex-col items-center gap-2 text-center">
        <div className="flex items-center gap-2 font-semibold text-lg">
          <span className="flex size-8 items-center justify-center rounded-lg bg-primary text-primary-foreground">
            <Calculator className="size-4" />
          </span>
          Muhasebe
        </div>
        <p className="text-sm text-muted-foreground">
          Muhasebeci olmadan işletmeni anlayabileceğin ön muhasebe uygulaması
        </p>
      </div>
      <div className="w-full max-w-sm">{children}</div>
    </main>
  );
}
