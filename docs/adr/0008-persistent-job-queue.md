# ADR 0008: PostgreSQL tabanlı kalıcı iş kuyruğu

## Karar

İlk aşamada ayrı bir mesaj aracısı eklemek yerine mevcut PostgreSQL veritabanında kalıcı bir `BackgroundJob` tablosu kullanılacaktır. Worker'lar işleri satır kilidi ve `SKIP LOCKED` ile alır.

## Gerekçe

Bu yaklaşım Docker'ı zorunlu kılmaz, mevcut operasyon modeline yeni bir servis eklemez ve tenant bağlamı ile iş sonucunun aynı veritabanında izlenmesini sağlar. Kuyruk hacmi veya farklı servis sayısı belirgin biçimde arttığında harici bir broker yeniden değerlendirilebilir.

## Sonuçlar

Uzun işlemler HTTP zaman aşımından bağımsızdır ve yeniden başlatma sonrasında devam eder. Veritabanının iş kuyruğu yükünü de taşıdığı izlenmelidir; yüksek hacimde ayrı broker ve worker deployment'ı gerekebilir.
