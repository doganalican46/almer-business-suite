# ADR 0011: Bildirim teslimatlarını kalıcı job kuyruğuna yazma

## Karar

E-posta ve webhook teslimatları, bildirim değişikliğiyle aynı veritabanı transaction'ında `BackgroundJob` kayıtları olarak oluşturulacaktır. Ayrı bir in-memory kanal veya doğrudan HTTP/SMTP çağrısı kullanılmayacaktır.

## Gerekçe

Bu model API isteğini dış servis gecikmesinden ayırır, uygulama yeniden başlatmalarında teslimatı kaybetmez ve mevcut retry/lease/çoklu worker güvenliğini yeniden kullanır.

## Sonuçlar

Harici sağlayıcı kesintileri bildirim üretimini engellemez. Kesin bir kez teslim garantisi yerine en az bir kez teslim modeli kullanılır; webhook tüketicileri `X-Almer-Delivery` değerini idempotency anahtarı olarak değerlendirmelidir.
