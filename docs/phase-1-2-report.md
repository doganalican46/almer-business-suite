# Faz 1–2 teslimat raporu

## Çalışan özellikler

Kimlik doğrulama, tenant seçimi, izin tabanlı erişim, responsive layout, tema/dil tercihi ve dinamik dashboard hazırdır. Dashboard tarih ve şube filtreleri API sorgusunu değiştirir; şube kapsamlı kullanıcıların konsolide sonucu yalnızca izinli şubelerden hesaplanır. Firma ayarları, tüzel kişilik ve şube yönetimi; dinamik rol/izin düzenleme; kullanıcı oluşturma, rol ve şube kapsamı atama akışları çalışır. Demo veri açıkça etiketlenir.

## Güvenlik ve doğruluk

Tenant kimliği JWT ve üyelik üzerinden doğrulanır. Global filtreler, bileşik tenant foreign key’leri ve yazma koruması birlikte uygulanır. Her tenant değişikliği kullanıcı, varlık ve zaman bilgisiyle audit tablosuna yazılır. Finansal hesaplar backend'de decimal ile yapılır. Demo seed yalnızca Development ortamında çalışır. SQL migration’ları API başlangıcında transaction içinde ve yalnızca bir kez uygulanır.

## Sonraki faza bırakılanlar

Ürün/stok, satış/gelir-gider, kampanya, dosya aktarımı, background job, MFA sağlayıcısı, PostgreSQL RLS politikaları, production secret vault ve tam tarayıcı e2e paketi Faz 3–6 kapsamındadır.

