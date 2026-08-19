"use client";

import { use } from "react";

import { CashDetail } from "@/components/cash-detail";

export default function CashAccountPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return <CashDetail accountId={id} />;
}
