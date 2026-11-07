# Faz 10 — Kalıcı background-job kuyruğu

PostgreSQL üzerinde çalışan, uygulama yeniden başlatmalarına dayanıklı bir iş kuyruğu eklendi.

- Excel/CSV aktarım commit işlemleri HTTP isteğinden ayrılarak kuyruğa taşındı.
- Kritik stok ve son kullanma tarihi bildirimleri kullanıcı isteğinden ayrılarak beş dakikalık kalıcı sistem işlerine taşındı.
- İş alma işlemi `FOR UPDATE SKIP LOCKED` kullanır; birden fazla API instance aynı işi eşzamanlı çalıştırmaz.
- İşler tenant, kullanıcı ve izin verilen şube bağlamıyla çalışır; mevcut EF tenant filtreleri ve yazma korumaları worker içinde de geçerlidir.
- Başarısız işler üstel gecikmeyle üç kez denenir; son hata ve durum kalıcı olarak saklanır.
- Yarım kalmış `Running` işleri 15 dakika sonra yeniden kuyruğa alınır.
- Uzun çalışan işler iki dakikada bir lease yeniler; aktif işler yanlışlıkla sahipsiz sayılmaz.
- Aktarım geçmişi `Queued`, `Processing`, `Completed` ve `Failed` durumlarını otomatik yeniler.
- Başarısız işler için kontrollü tekrar endpoint'i eklendi.

Gerçek PostgreSQL üzerinde migration ve worker smoke testi, çalışan bir PostgreSQL servisi sağlandığında ayrıca yapılmalıdır.
