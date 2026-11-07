# Faz 17 — Yayın doğrulama ve uçtan uca test

- `scripts/verify-environment.ps1`, .NET/Node/npm sürümlerini ve isteğe bağlı production secret/config gereksinimlerini kontrol eder.
- `scripts/smoke-test.ps1`, liveness, readiness, trace header, login ve yetkili dashboard akışını hedef API adresine karşı doğrular.
- Playwright masaüstü ve mobil Chrome projeleri eklendi; Almanca varsayılan giriş, Türkçeye geçiş, erişilebilir form etiketleri ve dar ekran ana aksiyonları test edilir.
- Windows geliştirme ortamı için sunucuyu kontrollü başlatıp her durumda kapatan `scripts/run-e2e.ps1` eklendi.
- Docker API imajı non-root kullanıcıya geçirildi, liveness healthcheck eklendi ve web servisi sağlıklı API'yi bekleyecek şekilde düzenlendi.
- Frontend container kurulumu kilit dosyasına bağlı `npm ci` ile tekrarlanabilir hâle getirildi.

Doğrulama: Playwright 6/6 senaryo geçti. Gerçek production PostgreSQL, SMTP ve webhook uçları yalnızca ilgili erişim bilgileri bulunan hedef ortamda smoke betiği ve runbook ile doğrulanabilir; projede müşteri secret'ı tutulmaz.
