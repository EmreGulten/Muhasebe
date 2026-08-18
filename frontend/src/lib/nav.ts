import {
  ArrowLeftRight,
  BarChart3,
  Landmark,
  LayoutDashboard,
  Package,
  ReceiptText,
  Settings,
  ShoppingCart,
  Sparkles,
  Truck,
  Users,
  type LucideIcon,
} from "lucide-react";

export interface NavItem {
  label: string;
  href: string;
  icon: LucideIcon;
  enabled: boolean;
}

/**
 * Plan bölüm 27'deki ana gezinme. Phase 1'de yalnızca Dashboard aktif;
 * diğer modüller sonraki fazlarla geldiğinde açılır.
 */
export const NAV_ITEMS: NavItem[] = [
  { label: "Dashboard", href: "/dashboard", icon: LayoutDashboard, enabled: true },
  { label: "Satışlar", href: "/sales", icon: ShoppingCart, enabled: true },
  { label: "Alışlar", href: "/purchases", icon: ReceiptText, enabled: false },
  { label: "Müşteriler", href: "/customers", icon: Users, enabled: true },
  { label: "Tedarikçiler", href: "/suppliers", icon: Truck, enabled: true },
  { label: "Ürünler", href: "/products", icon: Package, enabled: true },
  { label: "Kasa & Banka", href: "/cash", icon: Landmark, enabled: false },
  { label: "Gelir & Gider", href: "/income-expense", icon: ArrowLeftRight, enabled: false },
  { label: "Raporlar", href: "/reports", icon: BarChart3, enabled: false },
  { label: "AI Asistan", href: "/assistant", icon: Sparkles, enabled: false },
  { label: "Ayarlar", href: "/settings", icon: Settings, enabled: true },
];
