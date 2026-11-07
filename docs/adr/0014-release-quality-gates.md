# ADR 0014: Yayın kalite kapıları

## Karar

Bir yayın adayı backend unit/integration testleri, frontend çeviri testleri, Oxc lint, TypeScript production build ve masaüstü/mobil Playwright smoke senaryolarını geçmelidir. Canlı ortama geçişte ayrıca readiness, kimlik doğrulama ve dashboard smoke akışı çalıştırılır.

## Gerekçe

Derleme ve birim testleri tarayıcı yerleşimi, dil değiştirme ve gerçek HTTP bağımlılıklarını tek başına doğrulamaz. Katmanlı kapılar hatayı en düşük maliyetli seviyede yakalarken production secret'larının repository'ye girmesini gerektirmez.

## Sonuçlar

Playwright yerel Windows'ta kurulu Chrome kanalını kullanır; CI ortamı isterse kanal ayarını kendi browser imajına uyarlamalıdır. Production smoke testi hedef ortam kimlik bilgisi gerektirdiğinden otomatik kaynak kod testlerinden ayrıdır.
