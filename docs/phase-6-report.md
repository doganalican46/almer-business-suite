# Faz 6 teslimat raporu — Raporlama ve kalite

## Çalışan özellikler

- Tarih ve şube filtreli Analiz ve Rapor Merkezi
- Günlük net satış eğilimi, şube karşılaştırması, en kârlı ve düşük marjlı ürünler
- Satış, satın alma, gider, stok, ürün kârlılığı ve vergi raporları
- CSV, XLSX ve PDF dışa aktarma
- Vergi oranına göre satış matrahı, hesaplanan vergi, alış/gider vergisi ve net vergi özeti
- İade satırlarını ters işaretle değerlendiren ve yalnızca onaylı gider vergisini kullanan backend hesabı
- Yönetimsel vergi özeti uyarısı; resmî beyanname üretildiği iddia edilmez
- Tenant global filtreleri ve kullanıcı şube kapsamını rapor sorgularında uygulama
- Rapor sorguları için PostgreSQL indeksleri
- Almanca/Türkçe literal çeviri anahtarlarını kaynak kod kullanımıyla karşılaştıran test

## Dışa aktarma güvenliği

CSV ve Excel hücreleri `=`, `+`, `-` veya `@` ile başlıyorsa formül olarak çalıştırılmaları engellenir. PDF raporları Unicode font gömer; Almanca ve Türkçe karakterler Windows ve Linux/Docker ortamında desteklenir. Docker imajı gerekli DejaVu fontlarını kurar.

## Kalite doğrulaması

- CSV UTF-8 BOM, özel karakter ve formül enjeksiyonu testi
- Excel dosyasının yeniden açılması ve formül hücresi bulunmadığının doğrulanması
- PDF imzası, dosya boyutu ve yatay sayfa üretimi testi
- Örnek PDF'nin Poppler ile PNG'ye çevrilerek görsel kontrolü
- Vergi hesabında iade ve onaylanmamış gider senaryoları
- Tenant/import izolasyonu ve mevcut finans/stok değişmezlik testleri

## Operasyonel not

Canlı PostgreSQL migration smoke testi çalışan yerel PostgreSQL bulunmadığında otomatik yapılamaz. Migration çalıştırıcısı `006_reporting_indexes.sql` dosyasını API başlangıcında uygular.
