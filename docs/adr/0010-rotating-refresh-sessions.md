# ADR 0010: Dönen refresh token ve veritabanı session kaydı

## Karar

Kısa ömürlü JWT access token'ları, hash olarak saklanan ve her kullanımda döndürülen opaque refresh token'larla yenilenecektir. Her access token bir `session_id` taşır ve session aktifliği API isteklerinde doğrulanır.

## Gerekçe

Tek başına uzun ömürlü JWT, kullanıcı veya yönetici tarafından anında iptal edilemez. Kalıcı session kaydı uzaktan çıkış, cihaz görünürlüğü, refresh token tekrar kullanımının tespiti ve hesap kapatıldığında erişimin kesilmesini sağlar.

## Sonuçlar

Her yetkili API isteğinde hafif bir session doğrulama sorgusu yapılır. Büyük ölçekli kurulumlarda aktif session durumu kısa süreli dağıtık cache ile desteklenebilir; veritabanı her zaman nihai doğruluk kaynağı olarak kalır.
