# Muhasebe

Mikro işletmeler için AI destekli ön muhasebe uygulaması. "Muhasebeci olmadan işletmeni anla" —
sıfır muhasebe bilgisi olan esnafın günlük işlemlerini kaydedip anlaşılır bir döküm almasını hedefler.

Bu depo, ürün planının **PHASE 0 + PHASE 1 + PHASE 2 + PHASE 3 + PHASE 4 + PHASE 5 + PHASE 6 + PHASE 7 + PHASE 8 + PHASE 9** kapsamını içerir: proje kurulumu, kimlik doğrulama,
çok kiracılı (multi-tenant) işletme sistemi, cari hesaplar (müşteri/tedarikçi), ürün/stok yönetimi, satış ve alış belgeleri, kasa/banka hesapları,
gelir/gider yönetimi, dashboard ve raporlar, doğal dilde iş sorguları yapan AI asistan.

## Bu fazda çalışanlar

- Kayıt olma (isteğe bağlı işletme adıyla; verilmezse "`<Ad Soyad>` İşletmesi" oluşturulur)
- Giriş / çıkış, JWT access token + httpOnly cookie'de dönen refresh token (rotasyon + yeniden kullanım tespiti)
- Parola sıfırlama akışı (geliştirmede kod log'a yazılır)
- İşletme (tenant) oluşturma, listeleme, aktif işletme seçimi (`X-Tenant-Id`)
- Roller ve izin altyapısı (Owner / Muhasebeci / Çalışan); uç nokta bazlı `perm:<izin>` politikaları
- Cari kartları: müşteri/tedarikçi/ikisi oluşturma, düzenleme, pasifleştirme, silme (hareketi varsa reddedilir)
- Açılış bakiyesi: kartla atomik tek seferlik hareket (pozitif = taraf bize borçlu)
- Manuel cari hareketleri (borçlandırma/alacaklandırma/düzeltme/açılış); satış/tahsilat/alış/ödeme
  hareketleri ilgili modüllerden oluşur
- Bakiye = Σborç − Σalacak; cari ekstresi: tarih sıralı hareketler, çalışan bakiye, sayfalama
- Müşteri/Tedarikçi listeleri: arama, tür filtresi, pasifleri göster, sayfalama
- Ürün/hizmet kartları: SKU (tenant içinde benzersiz), barkod, kategori/birim, alış/satış fiyatı,
  KDV oranı, kritik stok eşiği; hizmet kartlarında stok takibi yapılmaz
- Kategori, birim ve depo tanımları; ilk depo listelemede otomatik "Ana Depo" olarak oluşur,
  varsayılan depo silinemez
- Manuel stok hareketleri: sayım (fark hareketi otomatik), manuel giriş/çıkış, iade; alış/satış
  hareketleri ilgili modüllerden oluşur
- Depolar arası transfer: tek işlemde çıkış + giriş çifti (atomik)
- Stok = Σişaretli miktar; kritik stok uyarısı (eşik > 0 ve stok ≤ eşik), depo bazlı stok dökümü
- Ürün listesi: ad/SKU/barkod arama, kategori ve kritik stok filtreleri, pasifleri göster, sayfalama
- Hareketli ürün silinemez (kayıt zinciri korunur); pasifleştirme önerilir
- Satış belgeleri: S-000001... numara serisi (tenant içinde benzersiz), müşteri (opsiyonel — nakit
  satış), depo (varsayılan "Ana Depo" lazy oluşur), vade tarihi, açıklama
- Kalem hesaplamaları: brüt = Round(miktar × fiyat); iskonto oranı; KDV zinciri — tüm tutarlar
  kalem bazında yuvarlanır (2 basamak), belge toplamları kalemlerden toplanır
- Taslak → Onay: onay tek transaction'da stok düşümü (hizmet kalemleri hariç) + cari borç +
  (istenirse) anlık tahsilat yazar; yetersiz stokta belge onaylanmaz ve hiçbir hareket yazılmaz
- Tahsilatlar: kasa hareketi (ilk tahsilatta "Kasa" hesabı oluşur) + cari alacak; kısmi tahsilat
  (PartiallyPaid) → tam tahsilat (Paid); kalan borcu aşan tahsilat reddedilir
- İptal: ters hareketlerle dengeleme — stok geri eklenir, cari borç ve tahsilatların cari alacağı
  ters işaretle kapanır, kasa hareketleri iade edilir; kayıtlar silinmez, terminal durumdur
- Onaylı belge değiştirilemez/silinemez (409); taslak serbestçe düzenlenir ya da silinir
- Satış listesi: durum filtresi, belge no/müşteri araması, sayfalama; frontend'de belge formu
  (dinamik kalemler, canlı toplamlar), detay (onay/iptal/tahsilat diyaloğu)
- Alış belgeleri: P-000001... numara serisi, tedarikçi (opsiyonel — nakit alış), depo, vade;
  kalem hesapları satışla aynı LineMath ile yuvarlanır
- Alış onayı: tek transaction'da stok girişi (hizmet kalemleri hariç) + tedarikçi borcu
  (cariye alacak) + (istenirse) anlık ödeme; müşteri carisinden alış reddedilir
- Ödemeler: kasa çıkışı (ilk ödemede "Kasa" hesabı oluşur) + cari borç; kısmi ödeme
  (PartiallyPaid) → tam ödeme (Paid); kalan borcu aşan ödeme reddedilir
- Alış iptali: stok geri düşer, tedarikçi borcu ve ödemelerin cari borcu ters işaretle kapanır,
  kasa ödemeleri kasaya iade edilir; kayıtlar silinmez
- Alış listesi: durum filtresi, belge no/tedarikçi araması, sayfalama; frontend'de belge formu
  (ürün seçiminde alış fiyatı varsayılanı) ve detay (onay/iptal/ödeme diyaloğu)
- Yetkiler: Çalışan satış yapar ama alış giremez; alış Muhasebeci/Owner yetkisindedir
- Kasa/banka hesapları: Kasa / Banka / Kredi Kartı / Sanal POS türleri, para birimi (MVP: TRY),
  açılış bakiyesi (pozitifse tek seferlik açılış hareketi deftere girer)
- Bakiye = Σişaretli hareketler (giriş pozitif, çıkış negatif) — tek gerçek kaynak hareket tablosudur;
  satış tahsilatları ve alış ödemeleri varsayılan "Kasa" hesabına akar (lazy oluşur)
- Manuel hareketler: giriş (tahsilat) / çıkış (ödeme); hareketler değiştirilemez, düzeltme ters
  hareketle yapılır (bölüm 23)
- Hesaplar arası transfer: tek işlemde zıt işaretli çıkış + giriş çifti, paylaşılan ReferenceId ile
  bağlı; aynı hesaba transfer reddedilir
- Hesap ekstresi: tarih sırası hareketler, çalışan bakiye, sayfa öncesi toplam, sayfalama
- Koruma kuralları: varsayılan kasa pasifleştirilemez/silinemez (satış/alış ödemelerinin hedefi),
  hareketli hesap silinemez (pasifleştirin), pasif hesaba hareket yazılamaz
- Yetkiler: Çalışan yalnızca hesapları görüntüler; hesap yönetimi Muhasebeci/Owner yetkisindedir
- Gelir/gider kategorileri: plandaki 13 gider + 4 gelir varsayılanı eksiksiz tamamlanır (kullanıcının
  sildiği geri gelmez); ad tenant ve tür içinde benzersiz, "Diğer" iki tarafta yaşar
- Kategori yönetimi: ekleme, ad/aktiflik düzenleme (tür sabit); kaydı olan kategori silinemez
- Gelir/gider kaydı: kategori + tutar + tarih + kasa/banka hesabı (verilmezse varsayılan "Kasa" lazy
  oluşur) + açıklama + belge no; kayıt ve hesap hareketi tek transaction'da yazılır (gelir +, gider −)
- Kayıtlar değiştirilemez/silinmez; iptal kasa hareketinin tersini yazar ve terminal durumdur (bölüm 23);
  kategori türü kayıt türüyle uyuşmalıdır
- Kayıt listesi: tür, kategori ve dönem filtreleri, sayfalama; iptal edilenler soluk + İptal etiketiyle
- Dönem özeti (bölüm 25 MVP): toplam gelir/gider/net kartları, aylık döküm (boş aylar sıfırla) ve
  kategori bazlı toplamlar; iptal edilenler özete girmez
- Yetkiler: Çalışan yalnızca görüntüler; gelir/gider girişi Muhasebeci/Owner yetkisindedir
- Frontend: hesap kart listesi (toplam bakiye), hesap detayı + ekstre, yeni hesap / hareket /
  transfer / düzenleme / silme diyalogları
- Dashboard (bölüm 3.1): 10 KPI kartı — günlük satış, aylık ciro ve gider, tahmini net kazanç,
  toplam alacak/borç, kasa ve banka toplamları, kritik stok ile gecikmiş alacak sayıları
- Dashboard grafikleri (CSS ile, kütüphanesiz): son 30 gün gelir/gider akışı, son 12 ay ciro,
  en çok satan / en kârlı 5 ürün, en yüksek borçlu 5 müşteri
- Alacaklar raporu (bölüm 25): pozitif bakiyeli müşteriler; gecikmiş tutar vadesi geçmiş
  ödenmemiş onaylı satışlardan müşteri bazlı hesaplanır
- Stok raporu: stoklu ürünlerin (pasifler dahil, hizmetler hariç) eldeki miktarı, maliyet değeri
  (eldeki × alış fiyatı) ve kritik stok durumu
- Satış raporu: dönem toplamları (adet / tutar / KDV / ortalama) ve gün / müşteri / ürün bazlı
  döküm; taslak ve iptal edilen belgeler hiçbir rapora girmez
- Yetkiler: raporlar salt okunur — tüm roller görüntüler (Reports.View)
- Frontend: gerçek veriyle dashboard; /reports sayfası (alacaklar, stok, satış raporu)
- AI asistan (bölüm 11): doğal dilde iş soruları — "Bu ay ne kadar kazandım?", "Bana borcu olan
  müşterileri göster" gibi. AI **hiçbir koşulda SQL üretip çalıştırmaz**; akış plan bölüm 11.1'deki
  gibidir: Soru → Niyet → Onaylı İş Aracı → Sorgu → Yapılandırılmış Veri → Açıklama
- Onaylı iş araçları (8 adet, hepsi salt okunur ve `TenantId` filtreli): aylık kâr, gecikmiş
  alacaklar, en çok satan ürünler, kritik stok, müşteri bakiyesi (adla arama), gider kategori
  dökümü, ay kıyası, yaklaşan tedarikçi ödemeleri; bilinmeyen araç çağrıları reddedilir
- Sağlayıcı soyutlaması (`IAiProvider`): `Ai__ApiKey` tanımlıysa OpenAI uyumlu API (çok turlu
  tool calling), tanımsızsa **offline asistan** — dış ağa istek çıkmaz, anahtar kelime eşleştirme
  ile aynı araçlar gerçek veriyle yanıtlanır
- Sohbet geçmişi: soru + yanıt çiftleri işletme ve kullanıcı bazında saklanır; son 10 mesaj bağlam
  olarak sağlayıcıya verilir
- Kullanım limiti: işletme başına aylık soru sayısı (`Ai__MonthlyQuestionLimit`, varsayılan 100);
  aşımında 429
- Yetkiler: asistan yalnızca Owner / Admin / Muhasebeci'de (`AiAssistant.Use`)
- Frontend: /assistant sohbet arayüzü — baloncuklu geçmiş, örnek soru çipleri, sağlayıcı rozeti,
  çevrimdışı mod belirtileri
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
  src/app/(app)/                  # korumalı alan: dashboard, business/new, customers, suppliers, products, sales, purchases, settings
  src/components/                 # app-shell, cari ve ürün/stok bileşenleri, tanımlar, ui/
  src/lib/                        # api istemcisi (401→sessiz refresh), tipler, cari/ürün yardımcıları, gezinme
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
| `AI_API_KEY` | AI asistan sağlayıcı anahtarı (OpenAI uyumlu). **Boş bırakılırsa asistan offline moda geçer** — dış ağa istek çıkmaz |
| `AI_BASE_URL`, `AI_MODEL` | Sağlayıcı adresi ve modeli (varsayılan `https://api.openai.com/v1` / `gpt-4o-mini`) |
| `AI_MONTHLY_QUESTION_LIMIT` | İşletme başına aylık soru limiti (varsayılan 100) |
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
- [x] PHASE 2 — Cari hesaplar (müşteri/tedarikçi kartları, hareketler, bakiye, ekstre)
- [x] PHASE 3 — Ürün + Stok (kategori, birim, depo, stok hareketi, kritik stok)
- [x] PHASE 4 — Satışlar (belge, kalemler, onay, stok düşümü, tahsilat, iptal)
- [x] PHASE 5 — Alışlar (belge, stok artışı, tedarikçi borcu, ödeme)
- [x] PHASE 6 — Kasa/Banka (hesaplar, hareketler, transfer, bakiye)
- [x] PHASE 7 — Gelir/Gider (kategoriler, kayıtlar, raporlar)
- [x] PHASE 8 — Dashboard + Raporlar (KPI, alacak/stok/satış raporları)
- [x] PHASE 9 — AI Asistan (iş sorguları, onaylı araçlar, offline mod; doğrudan SQL çalıştırmaz)
- [ ] PHASE 10 — Abonelik (planlar, deneme, ödeme entegrasyonu)
