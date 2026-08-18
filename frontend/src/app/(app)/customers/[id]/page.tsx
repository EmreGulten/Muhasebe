"use client";

import { use } from "react";

import { PartyDetail } from "@/components/party-detail";

export default function CustomerDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return <PartyDetail partyId={id} basePath="/customers" />;
}
