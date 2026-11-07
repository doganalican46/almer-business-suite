# ADR 0009: Veritabanı ve Data Protection key ring için tek şifreli paket

## Karar

PostgreSQL custom dump ile Data Protection anahtarları, bütünlük manifesti bulunan AES-256-GCM şifreli `.almerbak` paketinde birlikte taşınacaktır.

## Gerekçe

Yalnızca veritabanını geri yüklemek MFA sırlarını kullanılamaz hâle getirebilir. İki varlığı aynı sürümlü pakette tutmak geri yükleme noktasını tutarlı kılar. Şifreleme, yedeğin uygulama verilerinin ve kimlik doğrulama anahtarlarının açık biçimde depolanmasını engeller.

## Sonuçlar

Yedek parolası ayrı bir secret manager içinde saklanmalı ve kurtarma tatbikatlarında doğrulanmalıdır. API instance'ları geri yükleme sırasında durdurulmalı, işlemden sonra aynı key ring ile yeniden başlatılmalıdır.
