# Faz 7 teslimat raporu — Dinamik ayarlar ve kişiselleştirme

## Çalışan özellikler

- Tenant bazlı logo URL'si ve ana marka rengi
- Marka renginin navigasyon, buton, odak ve dashboard bileşenlerine anlık uygulanması
- Modülleri kod değişikliği olmadan aktif/pasif yapma
- Pasif modüllerin menüden kaldırılması ve doğrudan rotalarının dashboard'a yönlendirilmesi
- Genel kritik stok eşiği ve son kullanma tarihi uyarı günü ayarları
- Ürün minimum stoğu tanımlı değilse genel kritik stok eşiğinin backend hesabında kullanılması
- Kullanıcı bazlı dashboard KPI kartlarını gösterme/gizleme ve sıralama
- İzin kodu ve şube kapsamına bağlı global arama
- Ürün, şube, tedarikçi ve satış işlemi sonuçları

## Güvenlik ve izolasyon

Firma ayarları ve dashboard tercihleri tenant global filtresine dahildir. Marka/modül/eşik değişiklikleri `settings.manage` izni gerektirir. Dashboard tercihleri oturumdaki kullanıcı kimliğine bağlıdır. Arama endpoint'i her veri türünü ilgili permission koduna göre sorgular; şube kapsamlı kullanıcılar başka şubelerin şube veya satış kayıtlarını göremez.

## Veri modeli

- `TenantSetting`: tenant başına tek ayar kaydı
- `DashboardPreference`: tenant ve kullanıcı başına tek tercih kaydı
- `007_settings_customization.sql`: tablolar, tenant/user foreign key'leri ve unique constraint'ler

## Sonraki geliştirmeler

Kalıcı bildirim merkezi, MFA kayıt/doğrulama akışı, uzun süren aktarımlar için kalıcı background-job kuyruğu ve yedekleme/geri yükleme operasyonları bir sonraki üretim sertleştirme aşamasına bırakılmıştır.
