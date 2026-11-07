# ADR 0013: Platform içi gözlemlenebilirlik ve health endpoint ayrımı

## Karar

API stdout'a yapılandırılmış JSON log yazacak, her yanıt iz kimliği taşıyacak ve canlılık ile hazır olma kontrolleri ayrı endpointlerde sunulacaktır. Tenant operasyon metrikleri yalnızca `settings.manage` veya `audit.read` izniyle görüntülenecektir.

## Gerekçe

Container ve yönetilen platformlar stdout loglarını doğal olarak toplar. Liveness kontrolünün veritabanına bağlı olmaması gereksiz restart döngülerini, readiness kontrolünün bağımlılıkları sınaması ise hazır olmayan instance'a trafik yönlendirilmesini engeller.

## Sonuçlar

Süreç içi sayaçlar instance yeniden başladığında sıfırlanır. Uzun dönem saklama, çoklu instance toplama ve alarm üretimi deployment platformundaki log/metric sistemiyle yapılmalıdır. Loglara request body, token veya secret yazılmaz.
