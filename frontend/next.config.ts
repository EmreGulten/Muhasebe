import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  async rewrites() {
    // Tarayıcı aynı origin'de /api/* çağırır; Next bunu backend'e proxy'ler.
    // Böylece CORS gerekmez ve httpOnly refresh cookie'si doğal akar.
    const backend = process.env.API_PROXY_URL ?? "http://localhost:5000";
    return [
      {
        source: "/api/:path*",
        destination: `${backend}/api/:path*`,
      },
    ];
  },
};

export default nextConfig;
