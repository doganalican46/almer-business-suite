# Faz 15 — Çoklu firma çalışma alanı geçişi

- Kullanıcının aktif üyeliği bulunan firmalar hesap ekranında listelenir.
- Tenant değiştirme işlemi yalnızca aktif üyelik bulunan hedeflere izin verir.
- Geçiş sırasında mevcut tenant session'ı iptal edilir.
- Hedef tenant için yeni refresh cookie, session kaydı ve tenant/rol/şube claim'lerine sahip JWT oluşturulur.
- Eski tenant JWT'si session doğrulamasında anında reddedilir.
- Session iptali ve hedef session oluşturma aynı veritabanı transaction'ında gerçekleşir.
- Firma geçişi menüdeki `Konto & Unternehmen / Hesap ve Firmalar` ekranından yapılır.

Tenant sorguları ve yazmaları mevcut global filtre ve tenant write-guard kurallarını kullanmaya devam eder.
