# Almer Business Suite

Faz 1–18 tamamlanmış, Almanca varsayılan, Türkçe destekli ve çok kiracılı işletme yönetimi uygulaması.

## Hızlı başlangıç

Docker kurulu bir makinede:

```bash
cp .env.example .env
docker compose up --build
```

Web: `http://localhost:4173` · API/OpenAPI: `http://localhost:8080/openapi/v1.json`

Docker isteğe bağlıdır. Yerel PostgreSQL (`almer` veritabanı), .NET 9 ve Node.js 22+ ile geliştirme:

```bash
dotnet run --project src/backend/Almer.Api
cd src/frontend && npm install && npm run dev
```

Demo giriş: `admin@almer.demo` / `AlmerDemo!2026`. Bu kullanıcı ve ekrandaki rakamlar yalnızca geliştirme/demo ortamında üretilir. Production ortamında demo seed çalışmaz.

## Tamamlanan kapsam

- JWT giriş, izin kodları ve tenant/şube bağlamı
- Almanca/Türkçe arayüz, kullanıcı bazlı dil tercihi
- Açık/koyu tema ve responsive uygulama kabuğu
- Backend tarafından hesaplanan dashboard KPI’ları
- Firma ayarları; tüzel kişilik ve şube CRUD işlemleri
- Dinamik rol/izin yönetimi, kullanıcı oluşturma ve şube kapsamı atama
- Otomatik audit trail ve işlem geçmişi ekranı
- Sürümlü PostgreSQL migration çalıştırıcısı, bileşik tenant foreign key’leri ve global tenant filtresi
- Unit testler, çeviri anahtarı eşitlik testi ve tenant izolasyon testi
- İki dilli ürün/kategori/birim CRUD işlemleri ve barkod yönetimi
- Şube fiyatları ve fiyat geçmişi
- Hareket bazlı stok, sayım düzeltmesi, kritik stok ve şube transferi
- Değiştirilemez stok günlüğü ve negatif stok koruması
- Satış/iade, tedarikçi ve satın alma akışları
- Dinamik vergi kuralları, gelir/gider ve onay sistemi
- Kampanya motoru ve snapshot tabanlı kârlılık analizi
- XLSX, XLS ve CSV destekli Veri Aktarım Merkezi
- Otomatik/manuel kolon eşleştirme, önizleme ve satır bazlı doğrulama
- Ürün, satış, stok, gider, tedarikçi ve satın alma toplu aktarımı
- Dosya/kayıt tekrar kontrolü, transaction ile kayıt, aktarım geçmişi ve eşleştirme şablonları
- İndirilebilir Excel/CSV boş şablonları ve yerel sayı/tarih/encoding seçenekleri
- Şube, kategori ve ürün bazlı Analiz ve Rapor Merkezi
- Yönetimsel KDV/vergi özeti ve oran bazlı giriş/çıkış vergisi görünümü
- Satış, satın alma, gider, stok, kârlılık ve vergi raporlarını CSV/XLSX/PDF dışa aktarma
- Formül enjeksiyonuna dayanıklı CSV/Excel üretimi ve Unicode PDF raporları
- Kullanılan literal çeviri anahtarlarını doğrulayan DE/TR testleri
- Tenant bazlı logo, ana renk, aktif modüller ve operasyonel uyarı eşikleri
- Kullanıcı bazlı dashboard KPI gizleme ve sıralama tercihleri
- İzin ve şube kapsamlı ürün, şube, tedarikçi ve satış global araması
- Kalıcı kritik stok ve son kullanma tarihi bildirimleri
- Kullanıcı bazlı okundu bilgisi, okunmamış sayacı ve tenant/şube kapsamı
- PostgreSQL ve MFA anahtarlarını birlikte koruyan AES-256-GCM şifreli yedek/restore araçları
- Dönen refresh token, tekrar kullanım tespiti ve kullanıcı kontrollü aktif oturum yönetimi
- Kalıcı kuyruk üzerinden yeniden denenen SMTP e-posta ve HMAC imzalı webhook bildirimleri
- Tek kullanımlık parola sıfırlama, e-posta doğrulama ve ortak güçlü parola politikası
- Session döndürme ile anında izole edilen çoklu firma çalışma alanı geçişi
- JSON operasyon logları, trace ID, liveness/readiness kontrolleri ve yetkili Sistem Sağlığı ekranı
- Production ortam ön-kontrolü, API smoke testi ve yayın/geri yükleme runbook'u
- Masaüstü ve mobil Chrome Playwright E2E testleri
- Klavye erişilebilirliği, reduced-motion, iki dilli hata sınırı ve dinamik belge dili
- Vendor chunk ayrımı, gzip/immutable cache, CSP ve HTTP güvenlik başlıkları
- Çalışan ESLint kalite kapısı ve non-root healthcheck'li API container'ı

Faz 1–18 tamamlanmıştır. Ayrıntılar `docs/phase-1-2-report.md` ile `docs/phase-18-report.md` arasındaki teslimat raporlarındadır. Canlı ortam prosedürü `docs/production-runbook.md` içindedir.

## Kontroller

```bash
dotnet test tests/Almer.Tests/Almer.Tests.csproj -m:1
cd src/frontend && npm run lint && npm run test && npm run build
cd ../.. && ./scripts/run-e2e.ps1
```

Hedef ortam smoke kontrolü:

```powershell
./scripts/verify-environment.ps1 -Production
./scripts/smoke-test.ps1 -ApiBaseUrl https://api.example.com -Email release-check@example.com -Password '<secret>'
```

