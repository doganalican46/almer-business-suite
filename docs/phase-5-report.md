# Faz 5 teslimat raporu — Veri Aktarım Merkezi

## Çalışan özellikler

- XLSX, eski XLS ve CSV dosya okuma
- Ürün, satış, stok, gider, tedarikçi ve satın alma aktarım türleri
- Şirket ve izin verilen şube bağlamına bağlı aktarım kayıtları
- Almanca, Türkçe ve İngilizce kolon adlarından otomatik eşleştirme önerisi
- Manuel kolon eşleştirme ve tekrar kullanılabilir eşleştirme şablonları
- İlk 10 satır önizleme, tüm dosya doğrulaması ve satır/alan bazlı hata raporu
- CSV delimiter, UTF-8/Windows-1252/ISO-8859-9 encoding, ondalık ayırıcı ve tarih formatı seçenekleri
- Dosya SHA-256 özeti, iş anahtarı ve veritabanı unique constraint ile tekrar kontrolü
- Doğrulanan dosyanın tek transaction içinde kaydedilmesi
- Satış ve satın alma satırlarının belge numarasına göre gruplanması
- Satış/satın alma ile stok hareketlerinin aynı transaction içinde oluşması
- Aktarım geçmişi ve XLSX/CSV boş şablon indirme

## Güvenlik ve sınırlar

Dosya uzantısı beyaz listeyle sınırlıdır, azami dosya boyutu 10 MB ve azami satır sayısı 50.000'dir. Aktarım kayıtları global tenant filtresinden geçer; şube kapsamlı kullanıcılar yalnızca izinli şubedeki aktarımları görebilir ve çalıştırabilir. Yüklenen ham dosya başarıyla işlendiğinde veritabanından temizlenir.

Mevcut toplu kayıt işlemi 50.000 satır sınırı içinde senkron çalışır. Daha büyük dosyalar için kalıcı job kuyruğu ve ilerleme bildirimi Faz 6 performans çalışmasına bırakılmıştır.

## Sonraki faz

Faz 6; rapor merkezi, CSV/XLSX/PDF dışa aktarma, genişletilmiş entegrasyon ve izolasyon testleri, responsive kontroller ve performans iyileştirmelerini kapsar.
