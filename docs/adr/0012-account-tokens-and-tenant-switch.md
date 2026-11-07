# ADR 0012: Hash'li hesap tokenları ve session döndürerek tenant değiştirme

Hesap kurtarma tokenları opaque ve tek kullanımlıdır; yalnızca hash'leri saklanır. E-posta worker'ının ihtiyaç duyduğu ham değer Data Protection ile korunur. Tenant geçişi aynı session üzerinde claim değiştirmek yerine eski session'ı iptal edip hedef tenant için yeni session üretir. Böylece eski JWT anında geçersizleşir ve tenant claim'leri üyelik verisinden yeniden oluşturulur.
