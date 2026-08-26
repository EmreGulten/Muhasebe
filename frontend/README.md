# Muhasebe Web Uygulaması

Ön muhasebe platformunun Next.js tabanlı web arayüzüdür. Uygulama; cari hesaplar,
ürün ve stok, satış ve alış belgeleri, kasa/banka, gelir-gider, raporlar ve işletme
asistanı ekranlarını içerir.

## Geliştirme

Node.js 20.9 veya daha yeni bir sürüm gereklidir.

```bash
npm ci
npm run dev
```

Geliştirme sunucusu varsayılan olarak `http://localhost:3000` adresinde çalışır.
`/api/*` istekleri `next.config.ts` üzerinden backend servisine yönlendirilir.

## Kontroller

```bash
npm run lint
npm run build
```

Uygulamanın tamamını Docker ile çalıştırmak ve ortam değişkenlerini yapılandırmak
için depo kökündeki README dosyasına bakın.
