# Faz 4 teslimat raporu — Satış ve finans

## Çalışan özellikler

- Satış ve iade kaydı; aynı transaction içinde stok çıkışı/girişi
- Satış satırında ürün adı, fiyat, maliyet, indirim ve vergi snapshot’ı
- Dinamik, geçerlilik tarihli ülke/vergi kuralları
- Tedarikçi kartları ve çok satırlı satın alma kaydı
- Satın alma ile transactional stok girişi ve güncel maliyet
- Dinamik gelir/gider kategorileri, vergi hesabı ve onay akışı
- Yüzdesel veya sabit tutarlı ürün/kategori/şube kampanyaları
- Kampanyalarda öncelik ve tek-kampanya kuralı
- Ürün bazlı kârlılık, faaliyet sonucu ve veri güvenilirliği raporu

## Finansal doğruluk

Parasal hesaplar backend'de `decimal` ile yapılır. Satışın geçmiş mali sonucu ürün kartı sonradan değişse bile bozulmaz. İadeler satış ve maliyeti ters yönde etkiler. Stok alımı faaliyet gideri olarak ikinci kez yazılmaz; satılan malın maliyeti satış snapshot’ından hesaplanır. Yalnızca onaylanmış finans kayıtları faaliyet sonucuna dahil edilir.

Vergi ekranı yönetimsel hesaplama içindir; resmî vergi beyannamesi üretildiği iddia edilmez. Production oranları kullanılmadan önce resmî kaynaklardan doğrulanmalıdır.

## Sonraki faz

Excel/CSV aktarım merkezi, background job, kolon eşleştirme, tekrar kontrolü ve aktarım geçmişi Faz 5 kapsamındadır.
