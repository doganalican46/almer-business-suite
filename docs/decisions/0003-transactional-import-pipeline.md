# ADR 0003 — Transactional import pipeline

## Karar

Aktarım iki aşamalıdır: yükleme/önizleme sırasında dosya tenant kapsamlı `ImportBatch` kaydında tutulur; kullanıcı kolon eşleştirmesini onayladıktan sonra tüm satırlar yeniden doğrulanır ve tek veritabanı transaction'ında kaydedilir.

Tekrar kontrolü hem dosya seviyesinde SHA-256 özetiyle hem de aktarım türüne özgü iş anahtarlarıyla yapılır. Satış işlem numarası ve satın alma belge numarası gibi zaten benzersiz olan alanlar mevcut operasyon tablolarında da kontrol edilir.

## Gerekçe

Önizleme ile kesin kayıt arasındaki veri değişimini engellemek, kısmi finans/stok kayıtlarını önlemek ve aynı dosya ya da iş kaydının tekrar işlenmesini durdurmak için sunucu tarafında ikinci doğrulama gerekir. Eşleştirme ve yerel format seçeneklerinin aktarım kaydında saklanması sonucu denetlenebilir kılar.

## Sonuçlar

- Hatalı bir satır varsa hiçbir operasyon kaydı yazılmaz.
- Başarılı satış ve satın alma aktarımında belge ile stok hareketi atomiktir.
- Ham dosya başarılı kayıttan sonra temizlenir; geçmiş ve sonuç metrikleri korunur.
- 50.000 satırdan büyük dosyalar ileride kalıcı background-job altyapısına yönlendirilecektir.
