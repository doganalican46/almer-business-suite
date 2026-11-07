# Faz 18 — Kurumsal son düzenleme, erişilebilirlik ve performans

- Klavye kullanıcıları için ana içeriğe geçiş bağlantısı, belirgin `:focus-visible` durumu ve semantik navigasyon etiketleri eklendi.
- Hareket azaltma tercihi desteklenir; mobil menü, dil, tema ve bildirim kontrolleri erişilebilir ad ve açık/kapalı durumları taşır.
- Belgenin `lang` değeri DE/TR değişiminde güncellenir; giriş ekranındaki sabit pazarlama metinleri dahil tüm yeni metinler i18next kaynaklarına taşındı.
- Uygulama seviyesinde iki dilli hata sınırı eklendi; beklenmeyen render hataları kontrollü yeniden yükleme ekranına düşer.
- Vite vendor chunk ayrımı ile React, Query, grafik, form ve i18n paketleri uzun süreli cache için ayrıştırıldı.
- Nginx gzip, immutable asset cache, CSP, frame/referrer/content-type/permissions güvenlik başlıklarıyla sertleştirildi.
- API CORS origin listesi konfigürasyona taşındı; forwarded-header, HSTS ve temel güvenlik başlıkları eklendi.
- Oxc tabanlı TypeScript/React lint kalite kapısı çalışır hâle getirildi.

Son doğrulama: backend 53/53, frontend 2/2, E2E 6/6, DE/TR 452/452 anahtar, lint ve production build başarılıdır.
