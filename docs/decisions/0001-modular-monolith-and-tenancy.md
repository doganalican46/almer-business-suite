# ADR 0001 — Modüler monolit ve tenant izolasyonu

**Durum:** Kabul edildi

Tek deploy edilen API; Domain, Application, Infrastructure ve API katmanlarına ayrılır. İşlemsel varlıklar zorunlu `TenantId` taşır. Tenant bağlamı doğrulanmış JWT claim'inden kurulur; istemciden gelen tenant değeri yalnızca kullanıcının izinli tenant listesiyle eşleşirse kabul edilir. EF global sorgu filtreleri okuma izolasyonu sağlar. `SaveChanges` interceptor benzeri doğrulama, farklı tenant'a ait nesne yazılmasını reddeder. İlişkilerde mümkün olan yerlerde `(TenantId, Id)` bileşik anahtarları kullanılır.

UI filtresi güvenlik sınırı değildir. API yetkilendirmesi ve veritabanı ilişkileri asıl sınırdır. PostgreSQL RLS, production sertleştirme adımı olarak ayrıca planlanmıştır; uygulama katmanı testleri RLS olmadan da izolasyonu zorunlu tutar.

