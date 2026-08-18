"use client";

import { use } from "react";

import { PartyDetail } from "@/components/party-detail";

export default function SupplierDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return <PartyDetail partyId={id} basePath="/suppliers" />;
}
