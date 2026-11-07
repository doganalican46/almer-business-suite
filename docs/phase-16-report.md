# Faz 16 — Gözlemlenebilirlik ve sistem sağlığı

- Her API yanıtına `X-Trace-Id` ekleyen ve hassas istek içeriğini kaydetmeyen yapılandırılmış JSON loglama eklendi.
- HTTP durum kodu, istek süresi, 5xx hata ve background-job başarı/başarısızlık sayaçları süreç içinde tutulur.
- `/health/live` yalnızca süreç canlılığını, `/health/ready` ise PostgreSQL bağlantısı ve Data Protection key ring kullanılabilirliğini doğrular.
- `settings.manage` veya `audit.read` izni olan kullanıcılar için tenant kapsamlı kuyruk, oturum, bildirim ve HTTP metriklerini gösteren Sistem Sağlığı ekranı eklendi.
- Worker sonuçları gözlemlenebilirlik sayaçlarına bağlandı; yetkisiz kullanıcılar operasyon ekranını ne menüden ne doğrudan rota üzerinden açabilir.

Doğrulama: backend test paketi 53/53, frontend birim testleri 2/2 ve production build başarılıdır.
