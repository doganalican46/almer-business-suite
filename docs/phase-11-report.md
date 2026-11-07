# Faz 11 — Şifreli yedekleme ve kontrollü geri yükleme

PostgreSQL verisi ile MFA sırlarını açabilen Data Protection anahtarlarını birlikte koruyan operasyon araçları eklendi.

- Yedek paketi PostgreSQL custom dump, Data Protection key ring ve sürümlü manifest içerir.
- Paket PBKDF2-SHA256 ile türetilen anahtar ve AES-256-GCM kullanılarak şifrelenir.
- Her dosyanın SHA-256 özeti geri yüklemeden önce doğrulanır.
- Yanlış parola, bozuk paket, değiştirilmiş dosya, beklenmeyen manifest ve path traversal girişimleri reddedilir.
- Restore komutu varsayılan olarak yalnızca doğrulama yapar.
- Gerçek geri yükleme için hem `-Apply` hem de hedef veritabanı adının `-ConfirmDatabase` ile birebir verilmesi gerekir.
- Veritabanı geri yüklemesi başarısız olursa Data Protection anahtarları değiştirilmez.
- Mevcut key ring, geri yükleme öncesinde tarih damgalı güvenlik kopyasına alınır.

## Kullanım

PostgreSQL client tools (`pg_dump`, `pg_restore`) PATH üzerinde olmalıdır.

```powershell
./tools/backup-almer.ps1
./tools/restore-almer.ps1 -BackupFile ./backups/almer-almer-20261106-120000.almerbak
./tools/restore-almer.ps1 -BackupFile ./backups/almer-almer-20261106-120000.almerbak -Apply -ConfirmDatabase almer
```

Şifre kaybolursa yedek açılamaz. Yedek parolası, `.almerbak` dosyası ve altyapı erişim bilgileri ayrı güvenli alanlarda tutulmalıdır.

Bu geliştirme ortamında PostgreSQL istemci araçları bulunmadığı için gerçek dump/restore smoke testi yapılmamıştır. AES-GCM roundtrip ve yanlış parola reddi doğrulanmıştır.
