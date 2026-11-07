# Faz 12 — Dönen refresh token ve aktif oturum yönetimi

JWT oturum modeli kısa ömürlü access token ve veritabanı destekli refresh session yapısına geçirildi.

- Access token ömrü 20 dakikadır.
- Refresh token 30 günlük mutlak süreye sahiptir ve `HttpOnly`, `SameSite=Strict` cookie içinde tutulur.
- Production ortamında refresh cookie yalnızca HTTPS üzerinden gönderilir.
- Veritabanında ham refresh token değil, yalnızca SHA-256 özeti saklanır.
- Her yenilemede refresh token döndürülür; önceki token iptal edilerek yeni session satırına bağlanır.
- Döndürülmüş eski token tekrar kullanılırsa aynı session ailesindeki aktif tokenlar iptal edilir.
- Çok sekmeli tarayıcı yarışları için 10 saniyelik güvenli yarış toleransı ve frontend tekrar denemesi vardır.
- İptal edilen session'a bağlı access token, JWT süresi dolmamış olsa bile bir sonraki API isteğinde reddedilir.
- Kullanıcı hesap güvenliği ekranından cihaz, IP, son kullanım ve bitiş bilgisini görebilir; uzak oturumları sonlandırabilir.
- Kullanıcı başına en fazla 10 aktif session korunur.
- Giriş ve token yenileme endpoint'leri IP başına dakikada 10 istekle sınırlandırılır.

Migration sonrasında eski, `session_id` claim'i içermeyen JWT'ler reddedilir ve kullanıcıların bir kez yeniden giriş yapması gerekir.
