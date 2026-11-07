# Production yayın ve doğrulama runbook'u

## Yayın öncesi

```powershell
./scripts/verify-environment.ps1 -Production
dotnet test tests/Almer.Tests/Almer.Tests.csproj -m:1
cd src/frontend
npm ci
npm run lint
npm test
npm run build
cd ../..
./scripts/run-e2e.ps1
```

`Jwt__Key` en az 32 rastgele byte olmalı; PostgreSQL, SMTP, webhook secret ve yedek parolaları secret manager üzerinden verilmelidir. `ASPNETCORE_ENVIRONMENT=Development` production'da kullanılmamalıdır. CORS origin listesi yalnızca gerçek web originlerini içermelidir.

## Yayın sonrası

```powershell
./scripts/smoke-test.ps1 -ApiBaseUrl https://api.example.com -Email release-check@example.com -Password '<secret>'
```

Ardından `/operations` ekranında kuyruk, 5xx ve yanıt süresi göstergeleri incelenir. SMTP ve webhook kanalları ayrı test tenant'ında kontrollü bildirimle doğrulanır.

## Yedek ve geri yükleme tatbikatı

```powershell
./tools/backup-almer.ps1
./tools/restore-almer.ps1 -BackupFile ./backups/<file>.almerbak
```

İlk restore komutu yalnızca şifre, manifest ve hash bütünlüğünü doğrular. Gerçek tatbikat izole PostgreSQL hedefinde `-Apply -ConfirmDatabase <database>` ile yapılır. API instance'ları restore boyunca durdurulur; database ve Data Protection key ring birlikte geri getirilir. Tatbikat sonrası login, MFA, tenant geçişi ve rapor smoke akışları tekrarlanır.

## Geri alma

- Önceki uygulama imajı yeniden dağıtılır.
- Geriye uyumsuz veri değişikliği varsa onaylanmış `.almerbak` paketi izole olarak doğrulanır ve kontrollü restore prosedürü uygulanır.
- Readiness başarılı olmadan instance trafiğe açılmaz.
