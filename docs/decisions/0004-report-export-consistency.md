# ADR 0004 — Tek rapor veri kümesinden çoklu dışa aktarma

## Karar

Ekran raporları ve CSV/XLSX/PDF dosyaları aynı tenant/şube kapsamlı backend sorgularından üretilir. Format üreticileri yalnızca hazırlanmış `ReportDataset` modelini biçimlendirir; finans hesaplarını yeniden yapmaz.

PDF üretiminde çalışma zamanı ticari lisans anahtarı gerektirmeyen SkiaSharp kullanılır. Unicode font dosyaları Windows'ta Arial, Linux/Docker'da DejaVu Sans üzerinden gömülür.

## Gerekçe

Frontend hesapları veya her formatta ayrı sorgu kullanmak, ekrandaki sonuçla indirilen dosya arasında fark oluşturabilir. Tek veri kümesi finansal tutarlılığı ve test edilebilirliği artırır.

## Sonuçlar

- Yetkisiz tenant veya şube verisi format değiştirerek dışa aktarılamaz.
- CSV, Excel ve PDF aynı satırları içerir.
- CSV/Excel formül enjeksiyonu merkezi olarak engellenir.
- Yeni rapor formatları finans sorgularına dokunmadan eklenebilir.
