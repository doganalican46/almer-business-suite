# ADR 0005 — Tenant ayarları ve kullanıcı tercihlerini ayırma

## Karar

Firma genelindeki marka, modül ve operasyon eşikleri `TenantSetting` içinde; kişisel dashboard görünümü ise tenant+kullanıcı kapsamlı `DashboardPreference` içinde saklanır.

## Gerekçe

Firma sahibinin yaptığı marka veya modül değişikliği tüm kullanıcıları etkilemelidir. KPI kartı sırası gibi kişisel tercihler ise başka kullanıcıların ekranını değiştirmemelidir. Ayrı tablolar yetkilendirme ve audit davranışını açık tutar.

## Sonuçlar

- Tenant ayarları yalnızca `settings.manage` izniyle değiştirilebilir.
- Kullanıcılar yalnızca kendi dashboard tercihlerini okuyup yazabilir.
- Her iki tablo da global tenant filtresi ve tenant foreign key'leriyle izole edilir.
- Modül kapatma, yetki sisteminin yerine geçmez; API güvenliği permission kodlarıyla devam eder.
