# Faz 8 teslimat raporu — Kalıcı bildirim merkezi

## Çalışan özellikler

- Kritik stok seviyesine düşen ürünler için şube bazlı uyarı
- Lot/parti son kullanma tarihi yaklaşan stok girişleri için uyarı
- Tenant ayarlarındaki genel kritik stok ve SKT gün eşiklerinin kullanılması
- Ürün kartındaki minimum stok değerinin genel eşikten öncelikli olması
- Almanca ve Türkçe bildirim başlığı/mesajı
- Uyarıdan ilgili stok ekranına yönlendirme
- Kullanıcı bazlı tekil okundu bilgisi
- Tümünü okundu işaretleme ve okunmamış sayaç
- Çözülen stok durumunda uyarının otomatik kapanması
- Üst navigasyonda 60 saniyelik yenileme

## İzolasyon ve eşzamanlılık

Bildirimler ve okundu kayıtları global tenant filtresine, bileşik tenant foreign key'lerine ve şube kapsamına tabidir. Şube kapsamlı kullanıcılar yalnızca izinli şubelerin uyarılarını görür. Aynı tenant için eşzamanlı yenilemeler PostgreSQL transaction advisory lock ile tekilleştirilerek çift bildirim yarışı engellenir.

## Testler

Kritik stok kuralı ürün minimumu ve tenant varsayılanı için; SKT kuralı uyarı aralığı, geçmiş tarih ve boş tarih için test edilmiştir. Toplam backend test sayısı 28'e yükselmiştir.

## Sonraki geliştirmeler

- TOTP tabanlı MFA kayıt ve doğrulama
- Uzun süren aktarımlar için kalıcı background-job kuyruğu
- E-posta/webhook bildirim kanalları
- Yedekleme ve geri yükleme operasyon ekranı

Genel görsel tasarım yenilemesi kullanıcı tercihi doğrultusunda sonraki aşamaya bırakılmıştır.
