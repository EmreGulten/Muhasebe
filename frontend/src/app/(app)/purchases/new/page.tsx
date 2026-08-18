import { PurchaseForm } from "@/components/purchase-form";

export default function NewPurchasePage() {
  return (
    <div className="mx-auto grid w-full max-w-5xl gap-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Yeni Alış</h1>
        <p className="text-sm text-muted-foreground">
          Belge taslak olarak oluşur; onaylanana kadar stok ve cari etkisi yoktur.
        </p>
      </div>
      <PurchaseForm />
    </div>
  );
}
