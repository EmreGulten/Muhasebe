import { cookies } from "next/headers";
import { redirect } from "next/navigation";

// Oturum varsa panele, yoksa girişe yönlendir.
export default async function Home() {
  const hasSession = (await cookies()).has("muhasebe_refresh");
  redirect(hasSession ? "/dashboard" : "/login");
}
