# Muhasebe

Mikro işletmeler için geliştirilen, çok işletmeli ve AI destekli bir ön muhasebe
uygulamasıdır. Günlük finansal işlemleri tek yerde toplar; işletme sahibine cari,
stok, nakit akışı ve satış durumunu muhasebe bilgisi gerektirmeden izleyebileceği
sade bir çalışma alanı sunar.

## Özellikler

- Müşteri ve tedarikçi kartları, hareketler, bakiye ve ekstre
- Ürün, hizmet, kategori, birim, depo ve stok takibi
- Taslak, onay, tahsilat/ödeme ve iptal akışlarıyla satış ve alış belgeleri
- Kasa, banka, kredi kartı ve sanal POS hesapları; hesaplar arası transfer
- Gelir-gider kayıtları, dönem özetleri ve finansal raporlar
- Dashboard üzerinde satış, gider, alacak, borç ve stok göstergeleri
- İşletme verilerini yalnızca onaylı salt-okunur araçlarla sorgulayan AI asistan
- İşletme bazlı üyelik, rol ve izin yönetimi
- Abonelik planları, deneme süresi, özellik ve kullanım kotaları

Finansal hareketler silinmek yerine ters kayıtlarla dengelenir. Stok, cari ve kasa
bakiyeleri hareket defterlerinden hesaplanır. İşletme verileri tüm sorgularda tenant
kimliğiyle ayrılır.

## Teknoloji

| Katman | Teknoloji |
| --- | --- |
| Backend | .NET 10, ASP.NET Core Minimal API, EF Core 10 |
| Veri | PostgreSQL 18, ASP.NET Core Identity |
| Frontend | Next.js 16, React 19, TypeScript, Tailwind CSS 4 |
| Test | xUnit, SQLite in-memory |
| Çalıştırma | Docker Compose |

Backend; Domain, Contracts, Application, Infrastructure ve API projelerine ayrılır.
Frontend, Next.js App Router kullanır. API erişimi aynı origin üzerinden proxy edilir.

## Docker ile çalıştırma

Docker kurulu olmalıdır. Önce örnek ortam dosyasını kopyalayıp güvenli değerleri
tanımlayın:

```bash
cp .env.example .env
openssl rand -base64 48
```

Üretilen değeri `.env` içindeki `JWT_SECRET` alanına yazın; ayrıca
`POSTGRES_PASSWORD` değerini değiştirin. Ardından servisleri başlatın:

```bash
docker compose up --build
```

- Web: `http://localhost:3000`
- API: `http://localhost:5000`
- Sağlık kontrolü: `http://localhost:5000/health`
- OpenAPI arayüzü (development): `http://localhost:5000/scalar`

## Yerel geliştirme

Backend için .NET 10 SDK ve çalışan bir PostgreSQL sunucusu gerekir:

```bash
cd backend
dotnet tool restore
dotnet run --project src/Accounting.Api
```

Frontend için Node.js 20.9 veya daha yeni bir sürüm kullanın:

```bash
cd frontend
npm ci
npm run dev
```

Varsayılan geliştirme ayarında frontend, API isteklerini
`http://localhost:5000` adresine yönlendirir.

## Test ve kalite kontrolleri

```bash
dotnet test backend/Accounting.slnx
npm --prefix frontend run lint
npm --prefix frontend run build
```

## Yapılandırma

| Değişken | Açıklama |
| --- | --- |
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | PostgreSQL bağlantı bilgileri |
| `JWT_SECRET` | JWT imzalama anahtarı; en az 32 karakter olmalıdır |
| `JWT_ISSUER`, `JWT_AUDIENCE` | Token issuer ve audience değerleri |
| `API_PORT`, `WEB_PORT` | Docker Compose dış portları |
| `API_PROXY_URL` | Frontend'in backend proxy adresi |
| `AI_API_KEY` | OpenAI uyumlu sağlayıcı anahtarı; boşsa offline asistan kullanılır |
| `AI_BASE_URL`, `AI_MODEL` | AI sağlayıcısının adresi ve model adı |
| `AI_MONTHLY_QUESTION_LIMIT` | İşletme başına aylık üst kullanım sınırı |
| `ApplyMigrations` | Açılışta veritabanı migration'larının uygulanmasını sağlar |

Secret değerleri repoya eklemeyin. Access token tarayıcı belleğinde, refresh token
ise `httpOnly` cookie içinde tutulur.
