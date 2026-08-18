import { PartyList } from "@/components/party-list";

export default function CustomersPage() {
  return <PartyList type="Customer" basePath="/customers" title="Müşteriler" singular="Müşteri" />;
}
