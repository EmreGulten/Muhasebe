# Muhasebe

Mikro işletmeler için AI destekli ön muhasebe uygulaması. "Muhasebeci olmadan işletmeni anla" —
sıfır muhasebe bilgisi olan esnafın günlük işlemlerini kaydedip anlaşılır bir döküm almasını hedefler.

Bu depo, ürün planının **PHASE 0 + PHASE 1** kapsamını içerir: proje kurulumu, kimlik doğrulama
ve çok kiracılı (multi-tenant) işletme sistemi.

## Bu fazda çalışanlar

- Kayıt olma (isteğe bağlı işletme adıyla; verilmezse "`<Ad Soyad>` İşletmesi" oluşturulur)
- Giriş / çıkış, JWT access token + httpOnly cookie'de dönen refresh token (rotasyon + yeniden kullanım tespiti)
- Parola sıfırlama akışı (geliştirmede kod log'a yazılır)
- İşletme (tenant) oluşturma, listeleme, aktif işletme seçimi (`X-Tenant-Id`)
- Roller ve izin altyapısı (Owner / Muhasebeci / Çalışan)
- Dashboard kabuğu: plan bölüm 27'deki gezinme, sonraki faz modülleri "Yakında" etiketiyle
- Health check uçları, OpenAPI (Scalar), rate limiting, denetim kaydı (audit log) altyapısı

## Teknolojiler

| Katman | Teknoloji |
| --- | --- |
| Backend | .NET 10, ASP.NET Core Minimal API, Clean Architecture (Domain / Contracts / Application / Infrastructure / Api) |
| Veri | PostgreSQL 18, EF Core 10, ASP.NET Core Identity |
| Kimlik doğrulama | JWT Bearer + refresh token rotasyonu (SHA-256 hash, httpOnly cookie) |
| Frontend | Next.js 16 (App Router), React 19, TypeScript, Tailwind CSS 4, shadcn/ui, TanStack Query, RHF + Zod |
| Test | xUnit (SQLite in-memory üzerinde gerçek Identity + EF ile) |
| Altyapı | Docker Compose (api, web, postgres, redis), Serilog, GitHub Actions |

## Klasör yapısı

```text
backend/
  Accounting.slnx
  src/Accounting.Domain/          # Varlıklar, enum'lar, izinler (bağımlılıksız)
  src/Accounting.Contracts/       # İstek/yanıt DTO'ları
  src/Accounting.Application/     # Use case'ler, validatorlar, servis soyutları
  src/Accounting.Infrastructure/  # EF Core, Identity, JWT, refresh token, e-posta
  src/Accounting.Api/             # Uç noktalar, middleware, DI başlatma
  tests/Accounting.UnitTests/
frontend/
  src/app/(auth)/                 # login, register, forgot/reset password
  src/app/(app)/                  # korumalı alan: dashboard, business/new
  src/components/                 # app-shell (sidebar + tenant/user menüleri), ui/
  src/lib/                        # api istemcisi (401→sessiz refresh), tipler, gezinme
docker-compose.yml
.env.example
```

## Hızlı başlangıç (Docker)

Gereksinim: Docker.

```bash
cp .env.example .env
# .env içinde JWT_SECRET ve POSTGRES_PASSWORD doldurun:
#   openssl rand -base64 48   # JWT_SECRET için

docker compose up --build
```

- Web: http://localhost:3000
- API: http://localhost:5000 (health: `/health`, readiness: `/health/ready`)
- OpenAPI (geliştirme ortamında): http://localhost:5000/scalar

PostgreSQL ve Redis yalnızca `127.0.0.1`'e yayınlanır. API, konteyner açılışında migration'ları
otomatik uygular (`ApplyMigrations=true`).

## Geliştirme ortamı (Docker'sız)

Backend (.NET 10 SDK):

```bash
cd backend
dotnet tool restore                 # dotnet-ef (local tool)
docker run -d --name muhasebe-pg -p 5432:5432 \
  -e POSTGRES_DB=accounting -e POSTGRES_USER=accounting -e POSTGRES_PASSWORD=change-me \
  postgres:18-alpine
export Jwt__Secret="$(openssl rand -base64 48)"   # en az 32 karakter
dotnet run --project src/Accounting.Api           # ApplyMigrations=Development'ta açık
```

Migration üretimi (şema değişikliğinde):

```bash
cd backend
dotnet tool restore
dotnet ef migrations add <Ad> --project src/Accounting.Infrastructure --startup-project src/Accounting.Api
```

Frontend (Node 20.9+):

```bash
cd frontend
npm ci
npm run dev        # http://localhost:3000, /api/* istekleri localhost:5000'e proxy'lenir
```

## Testler

```bash
cd backend
dotnet test Accounting.slnx
```

## Ortam değişkenleri

| Değişken | Açıklama |
| --- | --- |
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | PostgreSQL kimlik/bağlantı bilgileri |
| `JWT_SECRET` | JWT imzalama anahtarı (≥32 karakter; üretimde zorunlu, yoksa API açılmaz) |
| `JWT_ISSUER`, `JWT_AUDIENCE` | Token talep değerleri |
| `API_PORT`, `WEB_PORT` | Compose'ta yayınlanan portlar (varsayılan 5000 / 3000) |
| `API_PROXY_URL` | Next.js rewrite proxy'sinin API adresi (Compose içinde `http://api:8080`) |
| `ApplyMigrations` | `true` ise açılışta migration'lar uygulanır |

## Güvenlik notları

- Access token yalnızca tarayıcı belleğinde tutulur (localStorage'a yazılmaz).
- Refresh token `httpOnly` + `SameSite=Lax` cookie'dedir; her yenilemede rotasyon yapılır,
  iptal edilmiş bir token yeniden kullanılırsa kullanıcının tüm oturumları düşürülür.
- Tarayıcı ↔ API aynı origin üzerinden çalışır (Next.js rewrite proxy): CORS genişletme ihtiyacı yoktur.
- Kiracı yalıtımı: `X-Tenant-Id` başlığı middleware'de kullanıcının üyeliğiyle doğrulanır;
  korumalı uç noktalar `RequireTenant()` ile işaretlenir.
- Kimlik uç noktaları IP başına sabit pencere rate limit'e tabidir.

## Yol haritası

- [x] PHASE 0 — kurulum, Docker, CI iskeleti
- [x] PHASE 1 — Authentication + Tenant
- [ ] PHASE 2 — Cari kartlar (müşteri/tedarikçi) ve ürünler
- [ ] PHASE 3 — Satış ve alış belgeleri
- [ ] PHASE 4 — Kasa/banka ve gelir-gider
- [ ] PHASE 5 — Raporlar ve KDV/beyan desteği
- [ ] PHASE 6 — AI asistan (fatura okuma, kategorileme, özetler)
