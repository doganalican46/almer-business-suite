# ADR 0006 — Veriden türetilen kalıcı bildirimler

## Karar

Kritik stok ve SKT uyarıları yalnızca frontend'de hesaplanmaz. Bildirim listesi yenilenirken backend güncel operasyon verisinden deterministik bir `ExternalKey` üretir, uyarıyı tenant tablosunda kalıcı hale getirir ve artık geçerli olmayan uyarıları çözüldü olarak işaretler.

Okundu bilgisi bildirim kaydında değil, tenant+bildirim+kullanıcı anahtarlı ayrı tabloda tutulur.

## Gerekçe

Aynı operasyonel olay bütün kullanıcılar için ortaktır; okundu durumu ise kişiseldir. Deterministik anahtar, aynı stok sorununun tekrar tekrar yeni bildirim oluşturmasını engeller. Kalıcı kayıt audit ve ileride e-posta/webhook kanalları için temel sağlar.

## Sonuçlar

- Bildirim çözülüp yeniden oluşursa aynı iş anahtarı yeniden etkinleşir.
- Şube kapsamı hem listeleme hem okuma işleminde doğrulanır.
- PostgreSQL advisory lock eşzamanlı üretimde unique-key yarışını önler.
- Bildirim yenileme şu anda uygulama içi liste isteğiyle tetiklenir; kalıcı job kuyruğu eklendiğinde aynı üretim mantığı worker'a taşınabilir.
