# ADR 0002 — Yerelleştirme ve parasal hesaplama

**Durum:** Kabul edildi

Statik metinler `de-DE` ve `tr-TR` JSON kaynaklarında tutulur; anahtar kümeleri CI testinde birebir karşılaştırılır. Varsayılan dil Almancadır ve kullanıcı tercihi backend'de saklanır. Kullanıcı üretimli adlar için `LocalizedText` değer nesnesi (`De`, `Tr`) kullanılır.

Parasal değerler .NET `decimal` ve PostgreSQL `numeric(18,2)` ile saklanır. Dashboard finans formülleri frontend'de değil Application servisinde çalışır. Maliyet eşleşmesi tam değilse sonuçlar “kesin net kâr” olarak sunulmaz; güvenilirlik metriği döner.

