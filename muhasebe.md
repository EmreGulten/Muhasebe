# Ön Muhasebe SaaS — PROJECT_PLAN.md

## 1. Proje Özeti

Bu proje; mikro işletmeler, esnaf, küçük KOBİ'ler, serbest çalışanlar, küçük imalathaneler ve hizmet işletmeleri için geliştirilecek, internet tabanlı, basit kullanımlı ve AI destekli bir **ön muhasebe SaaS platformudur**.

Ürünün temel farkı klasik muhasebe yazılımlarının karmaşık ekranlarını tekrar etmek değil; işletme sahibine **hızlı veri girişi, anlaşılır finansal görünüm ve doğal dille çalışan işletme asistanı** sunmaktır.

Ana slogan / ürün vaadi:

> Muhasebeci olmadan işletmeni anlayabileceğin ön muhasebe uygulaması.

Ürünün temel değer önerileri:

1. Çok basit ve hızlı kullanım
2. Uygun fiyatlı SaaS modeli
3. AI destekli işletme asistanı
4. Mobil uyumlu web deneyimi
5. Cari, stok, gelir-gider ve kasa takibini tek yerde toplama
6. Sonradan e-Fatura, banka, e-ticaret ve POS entegrasyonlarına genişleyebilme
7. Küçük işletmenin günlük operasyonuna odaklanma

---

# 2. Hedef Kullanıcılar

İlk sürümde hedef kitle geniş KOBİ pazarı değil, küçük ve mikro işletmelerdir.

Öncelikli hedefler:

- Oto servisler
- Küçük imalathaneler
- Elektrikçi / tesisatçı / teknik servis işletmeleri
- Bilgisayar ve telefon servisleri
- Mobilya ve atölye işletmeleri
- Küçük mağazalar
- Toptan / perakende küçük işletmeler
- Freelancer ve şahıs şirketleri
- E-ticaret satıcıları
- Ajans ve küçük hizmet firmaları

İlk ürün tamamen sektör bağımsız geliştirilecek ancak altyapı ileride sektörel dikey çözümlere uygun tasarlanacaktır.

---

# 3. MVP Kapsamı

İlk yayınlanabilir sürüm aşağıdaki temel modülleri içerecektir.

## 3.1 Dashboard

Dashboard işletmenin güncel durumunu tek ekranda göstermelidir.

Gösterilecek temel kartlar:

- Günlük satış
- Aylık satış
- Aylık gider
- Tahmini net kazanç
- Toplam alacak
- Toplam borç
- Kasa toplamı
- Banka toplamı
- Kritik stok sayısı
- Gecikmiş alacak sayısı

Grafikler:

- Son 30 günlük gelir / gider
- Son 12 aylık ciro
- En çok satan ürünler
- En kârlı ürünler
- En yüksek borçlu müşteriler

Dashboard sade tutulmalıdır.

---

# 4. Cari Hesap Yönetimi

## 4.1 Müşteri / Tedarikçi

Tek bir `Party` yapısı kullanılabilir.

Party türleri:

- Customer
- Supplier
- Both

Alanlar:

- Id
- TenantId
- Type
- Name
- TaxNumber
- TaxOffice
- Phone
- Email
- Address
- City
- District
- ContactName
- OpeningBalance
- CreditLimit
- Notes
- IsActive
- CreatedAt
- UpdatedAt

## 4.2 Cari Hareketler

Hareket türleri:

- Satış
- Tahsilat
- Alış
- Ödeme
- Borçlandırma
- Alacaklandırma
- Açılış bakiyesi
- Manuel düzeltme

Cari ekranda:

- Güncel bakiye
- Toplam borç
- Toplam alacak
- Son hareket tarihi
- Gecikmiş bakiye
- Hareket geçmişi

olmalıdır.

---

# 5. Ürün ve Stok Yönetimi

## 5.1 Ürün

Alanlar:

- Id
- TenantId
- SKU
- Barcode
- Name
- Description
- CategoryId
- UnitId
- PurchasePrice
- SalePrice
- VatRate
- MinimumStock
- IsService
- IsActive

## 5.2 Stok

Stok hareket türleri:

- Alış
- Satış
- Sayım
- Manuel giriş
- Manuel çıkış
- İade
- Transfer

İlk MVP tek depo ile başlayabilir.

Ancak veri modeli çoklu depo desteğine hazır olmalıdır.

Warehouse tablosu baştan oluşturulmalıdır.

## 5.3 Kritik Stok

Sistem:

`CurrentStock <= MinimumStock`

olan ürünleri kritik stok olarak göstermelidir.

---

# 6. Satış Yönetimi

Satış işlemi kullanıcı açısından mümkün olduğunca hızlı olmalıdır.

Satış ekranı:

- Müşteri seçimi
- Ürün ekleme
- Miktar
- Birim fiyat
- İskonto
- KDV
- Ara toplam
- Genel toplam
- Ödeme durumu
- Vade tarihi
- Açıklama

Satış durumları:

- Draft
- Confirmed
- PartiallyPaid
- Paid
- Cancelled

Satış onaylandığında:

1. Cari hareket oluşur.
2. Stok düşer.
3. Ödeme varsa kasa/banka hareketi oluşur.
4. Audit log yazılır.

---

# 7. Alış Yönetimi

Satış modülüne benzer çalışacaktır.

Alanlar:

- Tedarikçi
- Ürünler
- Miktar
- Alış fiyatı
- KDV
- Vade
- Ödeme durumu

Onaylandığında:

- Stok artar
- Tedarikçi borcu oluşur
- Ödeme varsa kasa/banka hareketi oluşur

---

# 8. Gelir / Gider Yönetimi

Kullanıcı muhasebe hesabı bilmeden gelir veya gider ekleyebilmelidir.

Örnek gider kategorileri:

- Kira
- Elektrik
- Su
- Doğalgaz
- Personel
- Yakıt
- Kargo
- Reklam
- Yemek
- Vergi
- Muhasebeci
- Bakım
- Diğer

Alanlar:

- Type
- CategoryId
- Amount
- Date
- PaymentAccountId
- Description
- DocumentNumber
- AttachmentUrl

Makbuz/fatura görseli ileride eklenebilir.

---

# 9. Kasa ve Banka Yönetimi

Account türleri:

- Cash
- Bank
- CreditCard
- VirtualPOS

Her hesap için:

- Açılış bakiyesi
- Güncel bakiye
- Para birimi
- Hareket geçmişi

Hareket türleri:

- Tahsilat
- Ödeme
- Transfer
- Gelir
- Gider
- Satış tahsilatı
- Alış ödemesi

İki hesap arasında transfer yapılabilmelidir.

---

# 10. Teklif Modülü

İlk MVP'nin ikinci fazında eklenebilir ancak veri modeli planlanmalıdır.

Teklif özellikleri:

- Müşteri
- Teklif numarası
- Tarih
- Geçerlilik tarihi
- Ürün / hizmet satırları
- İskonto
- Vergi
- Not

Durumlar:

- Draft
- Sent
- Accepted
- Rejected
- Expired

Kabul edilen teklif satışa dönüştürülebilmelidir.

---

# 11. AI İşletme Asistanı

Bu ürünün en önemli farklılaştırıcı modülüdür.

AI kullanıcının işletme verileri üzerinden doğal dilde sorularını cevaplamalıdır.

Örnek sorular:

- Bu ay ne kadar kazandım?
- Geçen aya göre giderim neden arttı?
- Bana borcu olan müşterileri göster.
- 30 günden uzun süredir ödeme yapmayan müşteriler kim?
- En çok hangi üründen para kazanıyorum?
- En çok satan 10 ürün nedir?
- Hangi ürünlerin stoğu bitmek üzere?
- Önümüzdeki 7 günde ne kadar ödeme yapmam gerekiyor?
- Bu ay en yüksek gider kategorim nedir?
- Hangi müşteriler düzenli alışveriş yapmayı bıraktı?

## 11.1 AI Güvenlik Kuralı

LLM doğrudan veritabanına SQL çalıştırmamalıdır.

AI mimarisi:

User Prompt
→ Intent Detection
→ Approved Business Tool
→ Backend Query
→ Structured JSON Result
→ LLM Explanation

Örnek tool'lar:

- get_monthly_profit
- get_overdue_receivables
- get_top_products
- get_low_stock_products
- get_customer_balance
- get_expense_breakdown
- compare_months
- get_upcoming_payments

AI sadece backend'in izin verdiği fonksiyonları çağırmalıdır.

## 11.2 AI İçgörüleri

Dashboard otomatik öneriler gösterebilir.

Örnek:

> Elektrik gideriniz geçen aya göre %28 arttı.

> 5 müşterinizin toplam 48.500 TL gecikmiş borcu bulunuyor.

> X ürününün stoğu mevcut satış hızına göre yaklaşık 4 gün içinde bitebilir.

Bu özellik ileriki fazlarda geliştirilebilir.

---

# 12. Hatırlatma Sistemi

İleriki sürümde otomatik ödeme hatırlatmaları olacaktır.

Örneğin:

> Ayşe Tekstil'in 17.500 TL borcu 45 gündür ödenmedi.

Kullanıcı:

- SMS
- e-posta
- WhatsApp

üzerinden hatırlatma gönderebilir.

MVP'de sadece uygulama içi bildirim yeterlidir.

---

# 13. Multi-Tenant SaaS Mimarisi

Sistem mutlaka multi-tenant olmalıdır.

Her müşteri işletmesi bir Tenant olacaktır.

Temel yapı:

Tenant
→ Users
→ Parties
→ Products
→ Sales
→ Purchases
→ Expenses
→ Accounts
→ Transactions

Tüm işletme tablolarında:

`TenantId`

bulunmalıdır.

Backend seviyesinde Tenant izolasyonu zorunlu olmalıdır.

Bir Tenant başka Tenant'ın verisini hiçbir koşulda okuyamamalıdır.

---

# 14. Kullanıcı ve Yetkilendirme

Roller:

## Owner

Tam yetki.

## Admin

İşletme yönetimi.

## Accountant

Muhasebe ve rapor erişimi.

## Employee

Kısıtlı operasyon yetkisi.

## Viewer

Sadece görüntüleme.

İleride custom permission sistemi eklenebilir.

Permission örnekleri:

- Sales.View
- Sales.Create
- Sales.Edit
- Sales.Delete
- Expenses.View
- Expenses.Create
- Products.Edit
- Reports.View
- Users.Manage

---

# 15. Authentication

Backend:

ASP.NET Core Identity kullanılabilir.

Kimlik doğrulama:

- JWT Access Token
- Refresh Token

Desteklenecek giriş yöntemleri:

MVP:

- Email + Password

Sonra:

- Google
- Apple

Parola unutma akışı bulunmalıdır.

---

# 16. Teknoloji Stack

## Backend

- .NET 10 / güncel LTS uyumlu ASP.NET Core Web API
- C#
- Entity Framework Core
- PostgreSQL
- FluentValidation
- Mapster veya AutoMapper
- Serilog
- OpenTelemetry

## Frontend

- Next.js
- TypeScript
- App Router
- Tailwind CSS
- shadcn/ui
- React Hook Form
- Zod
- TanStack Query

## Database

- PostgreSQL

## Cache

- Redis

MVP başında zorunlu değildir ancak altyapı hazır tutulabilir.

## Background Jobs

- Hangfire veya Quartz.NET

Öneri:

Hangfire + PostgreSQL storage.

## Object Storage

- S3 compatible storage

Örnek:

- Cloudflare R2
- AWS S3
- MinIO

## Email

- Resend
- Amazon SES

## SMS

Türkiye entegrasyonu sonraki faz.

## AI

AI provider abstraction oluşturulmalıdır.

Interface:

`IAiProvider`

Implementasyonlar ileride değiştirilebilir olmalıdır.

---

# 17. Mimari Yaklaşım

İlk sürümde Microservice kullanılmayacaktır.

Önerilen mimari:

**Modular Monolith + Clean Architecture**

Solution:

```text
src/
  Accounting.Api
  Accounting.Application
  Accounting.Domain
  Accounting.Infrastructure
  Accounting.Contracts

tests/
  Accounting.UnitTests
  Accounting.IntegrationTests
```

Domain modülleri:

```text
Modules/
  Identity
  Tenants
  Parties
  Products
  Inventory
  Sales
  Purchases
  Expenses
  Accounts
  Reports
  AiAssistant
  Subscriptions
  Notifications
```

İleride ihtiyaç halinde modüller microservice'e ayrılabilir.

---

# 18. Backend Katmanları

## Domain

Entities
Value Objects
Enums
Domain Events

## Application

Use Cases
Commands
Queries
DTO
Validators
Interfaces

## Infrastructure

PostgreSQL
EF Core
External Services
Storage
Email
AI
Payment

## API

Controllers veya Minimal APIs
Authentication
Middleware
OpenAPI

---

# 19. API Standartları

Base URL:

```text
/api/v1
```

Örnek endpointler:

```text
POST   /api/v1/auth/login
POST   /api/v1/auth/register
POST   /api/v1/auth/refresh

GET    /api/v1/parties
POST   /api/v1/parties
GET    /api/v1/parties/{id}
PUT    /api/v1/parties/{id}
DELETE /api/v1/parties/{id}

GET    /api/v1/products
POST   /api/v1/products

GET    /api/v1/sales
POST   /api/v1/sales
GET    /api/v1/sales/{id}
POST   /api/v1/sales/{id}/confirm
POST   /api/v1/sales/{id}/payment

GET    /api/v1/expenses
POST   /api/v1/expenses

GET    /api/v1/dashboard

POST   /api/v1/ai/chat
```

---

# 20. Database Ana Tablolar

Temel tablolar:

```text
Tenants
Users
UserTenants
Roles
Permissions

Parties
PartyTransactions

Products
Categories
Units
Warehouses
InventoryTransactions

Sales
SaleItems

Purchases
PurchaseItems

IncomeExpenses
ExpenseCategories

Accounts
AccountTransactions

Quotes
QuoteItems

Notifications

AiConversations
AiMessages

SubscriptionPlans
Subscriptions

AuditLogs
```

Her temel entity:

```text
Id UUID
TenantId UUID
CreatedAt timestamptz
UpdatedAt timestamptz
CreatedBy UUID nullable
UpdatedBy UUID nullable
```

Soft delete gereken tablolarda:

```text
IsDeleted
DeletedAt
```

kullanılabilir.

---

# 21. Para ve Finans Veri Tipleri

Para alanlarında `float` veya `double` kullanılmayacaktır.

Backend:

```csharp
decimal
```

PostgreSQL:

```sql
numeric(18,2)
```

Miktarlar için gerekirse:

```sql
numeric(18,4)
```

kullanılmalıdır.

---

# 22. Audit Log

Finansal işlemlerde audit zorunludur.

Log:

- UserId
- TenantId
- EntityType
- EntityId
- Action
- OldValue
- NewValue
- IPAddress
- UserAgent
- Timestamp

özelliklerini içermelidir.

Özellikle:

- Satış
- Alış
- Tahsilat
- Ödeme
- Stok düzeltme

kayıtları izlenebilmelidir.

---

# 23. Finansal Veri Silme Politikası

Onaylanmış finansal kayıtların direkt DELETE edilmesi önerilmez.

Örneğin satış iptal edildiğinde:

- satış cancelled olur
- ters cari hareket oluşturulur
- ters stok hareketi oluşturulur
- ters ödeme hareketi gerekirse oluşturulur

Bu işlem transaction içinde yapılmalıdır.

---

# 24. Transaction Yönetimi

Aşağıdaki işlemler DB transaction içinde çalışmalıdır:

Satış onayı:

```text
Sale
+ InventoryTransaction
+ PartyTransaction
+ AccountTransaction
```

Alış onayı:

```text
Purchase
+ InventoryTransaction
+ PartyTransaction
+ AccountTransaction
```

Herhangi biri hata verirse tüm işlem rollback edilmelidir.

---

# 25. Raporlama

MVP raporları:

## Finans

- Gelir gider raporu
- Aylık ciro
- Tahmini kâr
- Kasa/banka bakiyesi

## Cari

- Borçlu müşteriler
- Alacaklı müşteriler
- Gecikmiş alacaklar
- Müşteri ekstresi

## Stok

- Stok durumu
- Kritik stok
- En çok satan ürünler
- Stok hareketleri

## Satış

- Günlük satış
- Aylık satış
- Müşteri bazlı satış
- Ürün bazlı satış

---

# 26. Arama

Global search bulunmalıdır.

Kullanıcı üst bardan şunları arayabilmelidir:

- müşteri
- tedarikçi
- ürün
- satış numarası
- telefon
- vergi numarası

PostgreSQL Full Text Search ilk sürümde şart değildir.

`ILIKE` + doğru index yeterlidir.

---

# 27. UI / UX İlkeleri

Ürün muhasebeciler için değil işletme sahipleri için tasarlanmalıdır.

Bu nedenle teknik muhasebe terimleri mümkün olduğunca azaltılmalıdır.

Örneğin:

Yanlış:

```text
Borç Dekontu
Mahsup
Muhasebe Fişi
```

Tercih:

```text
Müşteriye Borç Ekle
Ödeme Al
Ödeme Yap
Gelir Ekle
Gider Ekle
```

Ana navigasyon:

```text
Dashboard
Satışlar
Alışlar
Müşteriler
Tedarikçiler
Ürünler
Kasa & Banka
Gelir & Gider
Raporlar
AI Asistan
Ayarlar
```

---

# 28. Responsive Tasarım

Mobile-first yaklaşım kullanılmalıdır.

Destek:

- Desktop
- Tablet
- Mobile Browser

İlk aşamada native mobil uygulama yapılmayacaktır.

Next.js web uygulaması PWA desteğine hazırlanabilir.

İleride React Native uygulaması eklenebilir.

---

# 29. SaaS Paketleri

Önerilen ilk fiyatlama:

## Başlangıç

199 TL / ay

Özellikler:

- Cari
- Gelir/Gider
- Kasa
- Temel satış
- Temel raporlar
- 1 kullanıcı

## Pro

349 TL / ay

Ek:

- Stok
- Alış
- Teklif
- 5 kullanıcı
- Gelişmiş raporlar
- AI Asistan

## İşletme

599 TL / ay

Ek:

- Çoklu depo
- Çoklu şube
- API
- E-ticaret entegrasyonları
- Gelişmiş AI
- Öncelikli destek

Not:

İlk beta sürecinde fiyatlandırma daha düşük tutulabilir.

---

# 30. Subscription Sistemi

Tablolar:

```text
SubscriptionPlans
Subscriptions
SubscriptionUsage
```

Kontrol edilecek limitler:

- kullanıcı sayısı
- depo sayısı
- AI kullanım limiti
- API erişimi
- entegrasyon sayısı

Backend'de feature guard sistemi oluşturulmalıdır.

Örnek:

```csharp
[RequiresFeature("AI_ASSISTANT")]
```

veya application seviyesinde kontrol yapılabilir.

---

# 31. Ödeme Sistemi

İlk Türkiye yayını için ödeme sağlayıcısı abstraction üzerinden kullanılmalıdır.

Interface:

```text
IPaymentProvider
```

Örnek entegrasyon seçenekleri:

- iyzico
- PayTR
- Stripe (uluslararası kullanım için)

Provider doğrudan domain katmanına bağlanmamalıdır.

---

# 32. e-Fatura

MVP'nin ilk yayınında e-Fatura zorunlu değildir.

Ancak sistem e-Fatura entegrasyonuna hazır olmalıdır.

Interface:

```text
IEInvoiceProvider
```

İleride özel entegratörlerle entegrasyon yapılabilir.

Örnek ihtiyaçlar:

- e-Fatura
- e-Arşiv
- e-İrsaliye

Bunlar ayrı entegrasyon modülü olarak geliştirilmelidir.

---

# 33. Banka Entegrasyonu

İlk MVP dışında tutulacaktır.

Sonraki faz:

- banka hareketlerini çekme
- otomatik eşleştirme
- tahsilat bulma
- gider kategorilendirme

AI ile otomatik kategori önerisi yapılabilir.

---

# 34. E-Ticaret Entegrasyonları

İleriki fazlar:

- Trendyol
- Hepsiburada
- N11
- Amazon
- Shopify
- WooCommerce

Siparişler otomatik satış kaydına dönüşebilir.

Stock sync ileride eklenebilir.

---

# 35. Güvenlik

Mutlaka uygulanması gerekenler:

- HTTPS
- Secure Cookies
- JWT rotation
- Refresh token revoke
- Rate limiting
- Strong password policy
- Email verification
- Tenant isolation
- SQL injection protection
- CSRF protection gereken akışlarda
- XSS protection
- Input validation
- Audit logging
- Encryption for sensitive fields

Secrets repository'ye yazılmayacaktır.

Environment variables kullanılmalıdır.

---

# 36. KVKK

Sistem Türkiye hedefli olduğu için KVKK dikkate alınmalıdır.

Temel gereksinimler:

- kullanıcı sözleşmesi
- gizlilik politikası
- veri silme süreci
- hesap kapatma
- veri export
- log retention politikası

---

# 37. Backup

PostgreSQL günlük backup alınmalıdır.

Minimum öneri:

- Daily backup
- 7 günlük retention
- Weekly backup
- 4 haftalık retention

Production için point-in-time recovery değerlendirilebilir.

---

# 38. Observability

Loglama:

Serilog

Metrics:

OpenTelemetry

Error tracking:

- Sentry

Health endpoints:

```text
/health
/health/ready
```

---

# 39. Testing

Minimum testler:

## Unit Tests

- Finans hesaplamaları
- Vergi hesaplamaları
- Stok hesaplamaları
- Cari bakiyeler
- Permission kontrolleri

## Integration Tests

Testcontainers + PostgreSQL kullanılabilir.

Özellikle:

- satış oluşturma
- satış onaylama
- stok düşme
- cari hareket
- tahsilat

birlikte test edilmelidir.

## Frontend

- Vitest
- React Testing Library
- Playwright

---

# 40. Docker

Development ortamı Docker Compose ile ayağa kalkmalıdır.

```text
services:
  api
  web
  postgres
  redis
```

Opsiyonel:

```text
minio
mailpit
```

---

# 41. CI/CD

GitHub Actions kullanılabilir.

Pipeline:

```text
Checkout
→ Restore
→ Build
→ Test
→ Frontend Lint
→ Frontend Build
→ Docker Build
→ Deploy
```

Branch modeli:

```text
main
staging
feature/*
```

---

# 42. Deployment

Başlangıç için maliyeti düşük tut.

Öneri seçenekleri:

Frontend:

- Vercel

Backend:

- Railway
- Render
- Hetzner VPS

Database:

- Neon PostgreSQL
- Supabase PostgreSQL
- Railway PostgreSQL

Uzun vadede:

- Docker
- Kubernetes ancak gerekli olduğunda

İlk aşamada Kubernetes kullanılmamalıdır.

---

# 43. Kodlama Kuralları

## Backend

- async/await
- CancellationToken
- Nullable enabled
- File-scoped namespace
- Dependency Injection
- SOLID
- Thin controllers
- Business logic controller içinde olmayacak

Endpoint → Application Use Case → Domain

şeklinde ilerlenmelidir.

## Frontend

- TypeScript strict
- Server Components varsayılan
- Client Component sadece gerektiğinde
- API erişimi tek katmanda
- UI component ile business logic ayrılmalı
- reusable form componentler kullanılmalı

---

# 44. Git Commit Standardı

Conventional Commits kullanılmalıdır.

Örnek:

```text
feat: add customer management
fix: prevent negative stock
refactor: simplify sale service
chore: update dependencies
```

---

# 45. Development Fazları

## PHASE 0 — Proje Kurulumu

- Monorepo/repository oluştur
- Backend solution
- Next.js frontend
- PostgreSQL
- Docker Compose
- CI pipeline
- Environment configuration

---

## PHASE 1 — Authentication + Tenant

- Register
- Login
- Refresh token
- Tenant creation
- User tenant membership
- Role system
- Tenant middleware

Bu faz tamamlanmadan business modüllerine geçme.

---

## PHASE 2 — Cari

- Party CRUD
- Customer / Supplier
- Cari hareket
- Bakiye hesaplama
- Ekstre

---

## PHASE 3 — Product + Inventory

- Product CRUD
- Category
- Unit
- Warehouse
- Inventory transaction
- Current stock
- Critical stock

---

## PHASE 4 — Sales

- Sale CRUD
- Sale items
- Confirm sale
- Inventory deduction
- Party balance
- Payment
- Cancellation

---

## PHASE 5 — Purchases

- Purchase CRUD
- Stock increase
- Supplier debt
- Payment

---

## PHASE 6 — Cash / Bank

- Accounts
- Account transactions
- Transfers
- Balance

---

## PHASE 7 — Income / Expenses

- Income/expense categories
- CRUD
- Reports

---

## PHASE 8 — Dashboard + Reports

- KPIs
- Charts
- Receivable report
- Stock report
- Sales report

---

## PHASE 9 — AI Assistant

- AI provider abstraction
- Tool calling
- Business query tools
- AI chat UI
- Conversation history
- Usage limits

AI hiçbir zaman direkt SQL üretip execution yapmayacak.

---

## PHASE 10 — Subscription

- Plans
- Tenant subscription
- Feature restrictions
- Trial
- Payment integration

---

# 46. MVP Yayın Kriterleri

MVP yayınlanabilir kabul edilmesi için kullanıcı:

1. Üye olabilmeli
2. İşletme oluşturabilmeli
3. Müşteri ekleyebilmeli
4. Tedarikçi ekleyebilmeli
5. Ürün ekleyebilmeli
6. Satış yapabilmeli
7. Tahsilat girebilmeli
8. Alış yapabilmeli
9. Gider ekleyebilmeli
10. Kasa/banka bakiyesi görebilmeli
11. Stok görebilmeli
12. Dashboard kullanabilmeli
13. Rapor alabilmeli
14. AI asistana işletmesi hakkında soru sorabilmeli

---

# 47. MVP Dışında Tutulacaklar

İlk sürümde yapılmayacak:

- Bordro
- Tam muhasebe
- Genel muhasebe fişleri
- Beyanname
- İnsan kaynakları
- CRM automation
- Üretim MRP
- Gelişmiş ERP
- Kubernetes
- Microservices
- Native mobile app

Scope creep engellenmelidir.

---

# 48. AI Coding Agent Talimatları

Bu dosyayı kullanan Codex / GLM / diğer coding agent aşağıdaki kurallara uymalıdır.

1. Projeyi faz faz geliştir.
2. Bir faz tamamlanmadan sonraki faza geçme.
3. Her faz sonunda build ve test çalıştır.
4. Hatalı build bırakma.
5. TODO bırakılması gerekiyorsa nedenini açıkla.
6. Hard-coded secret yazma.
7. TenantId filtrelerini asla unutma.
8. Finansal kayıtlarda transaction kullan.
9. Para hesaplarında decimal kullan.
10. Onaylanmış finansal kayıtları fiziksel silme.
11. API validation olmadan endpoint oluşturma.
12. Frontend ve backend DTO contractlarını tutarlı tut.
13. Database migration her schema değişiminde oluştur.
14. README'yi her önemli fazda güncelle.
15. Kod üretirken önce mevcut projeyi incele; var olan yapıyı gereksiz yere bozma.

---

# 49. İlk Coding Agent Görevi

Ajan ilk olarak yalnızca PHASE 0 ve PHASE 1'i geliştirmelidir.

Beklenen çıktı:

```text
/backend
/frontend
docker-compose.yml
README.md
.env.example
```

Backend:

```text
.NET Web API
Clean Architecture
PostgreSQL
EF Core
Identity
JWT + Refresh Token
Tenant system
Role system
OpenAPI
Health checks
```

Frontend:

```text
Next.js
TypeScript
Tailwind
shadcn/ui
Login
Register
Create Business
Dashboard shell
```

Docker Compose:

```text
PostgreSQL
API
Web
```

İlk faz tamamlandığında:

- register çalışmalı
- login çalışmalı
- tenant oluşturulmalı
- tenant user ile ilişkilendirilmeli
- auth korumalı endpoint çalışmalı
- frontend login/register çalışmalı
- docker compose ile proje açılmalı

Bunlar tamamlandıktan sonra PHASE 2'ye geçilebilir.

---

# 50. Uzun Vadeli Ürün Vizyonu

Ürün ileride sadece ön muhasebe uygulaması değil, küçük işletmeler için bir **AI Business Operating System** haline gelebilir.

Uzun vadeli özellikler:

- AI finans danışmanı
- Otomatik gider kategorilendirme
- Nakit akışı tahmini
- Satış tahmini
- Stok tahmini
- Otomatik ödeme hatırlatma
- WhatsApp satış / tahsilat entegrasyonu
- Banka otomatik eşleştirme
- E-ticaret sipariş sync
- POS entegrasyonu
- Muhasebeci portalı
- Vergi takvimi
- İşletme sağlık skoru

Örneğin sistem işletme sahibine şunu söyleyebilmelidir:

> Önümüzdeki 30 gün için tahmini nakit girişiniz 185.000 TL, beklenen çıkışınız 164.000 TL. Ancak 3 büyük müşterinin ödemesi gecikirse kasanız yaklaşık 42.000 TL açığa düşebilir.

Ürünün uzun vadeli rekabet avantajı burada oluşturulmalıdır.

---

# Son Talimat

Kodlama ajanı bu belgeyi projenin ana teknik ve ürün referansı olarak kabul etmelidir.

Öncelik sırası:

```text
Correctness
Security
Tenant Isolation
Data Integrity
Simplicity
User Experience
Performance
```

Premature optimization yapılmamalıdır.

İlk hedef:

**Gerçek bir küçük işletmenin kullanabileceği basit, güvenilir ve satılabilir MVP çıkarmaktır.**
