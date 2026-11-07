# Faz 13 — E-posta ve imzalı webhook bildirimleri

Kritik stok ve son kullanma tarihi uyarıları için tenant bazlı harici teslimat kanalları eklendi.

- Yönetici e-posta alıcılarını ve webhook kanalını sistem ayarlarından yönetebilir.
- SMTP sunucusu, kullanıcı adı ve parola uygulama/veritabanı yerine deployment secret'larıyla yapılandırılır.
- Webhook imza secret'ı ASP.NET Core Data Protection ile şifreli saklanır ve API tarafından geri okunamaz.
- Webhook istekleri `X-Almer-Signature: sha256=...` HMAC-SHA256 imzası taşır.
- Production ortamında webhook host'u açık allowlist içinde bulunmalıdır.
- Yalnızca standart porttaki HTTPS adresleri kabul edilir; localhost ve IP literal hedefler reddedilir.
- Yeni veya değişen bildirimler, notification transaction'ı içinde kalıcı teslimat işlerine dönüştürülür.
- E-posta ve webhook teslimatları mevcut background-job kuyruğunda beş kez yeniden denenir.
- Almanca ve Türkçe başlık/mesaj webhook payload'ında birlikte gönderilir.

Gerçek SMTP ve webhook uçları yapılandırılmadığı için dış servis smoke testi yapılmamıştır. URL politikası, allowlist ve imza üretimi otomatik testlerle doğrulanmıştır.
