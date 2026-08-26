import type { Metadata } from "next";
import "./globals.css";
import { Providers } from "./providers";

export const metadata: Metadata = {
  title: {
    default: "Muhasebe",
    template: "%s | Muhasebe",
  },
  description:
    "Muhasebeci olmadan işletmeni anlayabileceğin ön muhasebe uygulaması.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="tr" className="h-full font-sans antialiased">
      <body className="min-h-full flex flex-col">
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
