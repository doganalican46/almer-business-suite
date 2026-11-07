# Faz 3 teslimat raporu — Ürün ve stok

## Çalışan özellikler

- İki dilli ürün, kategori ve ölçü birimi yönetimi
- Dinamik kategori hiyerarşisi ve birim dönüşüm katsayıları; döngü koruması
- Tenant içinde benzersiz SKU ve barkod
- Ürün maliyeti, satış fiyatı, minimum stok ve aktif/pasif yaşam döngüsü
- Genel ve şube bazlı fiyat geçmişi
- Şube bazlı, hareket toplamından türetilen stok bakiyesi
- Açılış, alış, satış, müşteri/tedarikçi iadesi, fire ve sayım düzeltmesi
- Transaction içinde çift hareket üreten şubeler arası transfer
- Lot/parti, son kullanma tarihi, referans ve maliyet alanları
- Kritik stok göstergesi, stok değeri ve hareket geçmişi
- Dashboard’da izinli şubelerden hesaplanan stok değeri ve kritik stok KPI’ları

## Tutarlılık ve güvenlik

Stok hareketleri güncellenemez veya silinemez; hata düzeltmek için yeni bir düzeltme hareketi gerekir. Çıkış hareketleri negatif stok oluşturamaz. Transferler serializable transaction içinde kaydedilir. Ürün, kategori, birim, fiyat ve stok tabloları tenant filtreleri ile bileşik tenant foreign key’lerini kullanır. Şube kapsamı hem listeleme hem bakiye hem de hareket API’lerinde uygulanır.

## Sonraki faz

Satış kayıtları, gelir/gider, satın alma, tedarikçi, kampanya, dinamik vergi motoru ve detaylı ürün kârlılığı Faz 4 kapsamındadır.
