"use client";

import { use } from "react";

import { PurchaseDetail } from "@/components/purchase-detail";

export default function PurchaseDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return <PurchaseDetail purchaseId={id} />;
}
