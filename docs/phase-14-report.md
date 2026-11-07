# Faz 14 — Hesap kurtarma ve e-posta doğrulama

- Giriş ekranından keşfedilebilir parola sıfırlama talebi eklendi.
- Hesap varlığı açıklanmadan her talebe aynı kabul yanıtı verilir.
- Parola sıfırlama token'ı veritabanında yalnızca SHA-256 hash olarak tutulur ve bir saat geçerlidir.
- E-posta doğrulama token'ı 24 saat geçerlidir.
- Yeni token üretildiğinde aynı amaçtaki önceki kullanılmamış tokenlar geçersiz olur.
- E-postaya giden ham token, background-job payload'ında Data Protection ile şifrelenir.
- Parola sıfırlama veya değiştirme tüm aktif session'ları transaction içinde iptal eder.
- Güçlü parola politikası sıfırlama, değiştirme ve yönetici kullanıcı oluşturma akışlarında ortaktır.
- Almanca/Türkçe parola sıfırlama ve doğrulama ekranları eklendi.

Gerçek e-posta teslimatı SMTP yapılandırması sağlandığında mevcut kalıcı kuyruğu kullanır.
