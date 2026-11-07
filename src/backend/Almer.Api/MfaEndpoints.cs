using System.Security.Claims;
using System.Text.Json;
using Almer.Domain;
using Almer.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Almer.Api;

public static class MfaEndpointExtensions
{
    private const string Purpose = "Almer.MfaSecret.v1";

    public static RouteGroupBuilder MapMfaEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/mfa/status", async (ClaimsPrincipal principal, AlmerDbContext db, CancellationToken ct) =>
        {
            var user = await CurrentUser(principal, db, ct);
            return Results.Ok(new { enabled = user.MfaEnabled, setupPending = user.MfaPendingSecretProtected is not null, recoveryCodesRemaining = ReadHashes(user).Count });
        });

        group.MapPost("/mfa/setup", async (ClaimsPrincipal principal, AlmerDbContext db, IDataProtectionProvider protection, CancellationToken ct) =>
        {
            var user = await CurrentUser(principal, db, ct);
            if (user.MfaEnabled) return Results.Conflict(new { code = "mfa_already_enabled" });
            var secret = MfaSecurity.GenerateSecret();
            user.MfaPendingSecretProtected = Protector(protection).Protect(secret);
            await db.SaveChangesAsync(ct);
            var issuer = Uri.EscapeDataString("Almer Business Suite");
            var label = Uri.EscapeDataString($"Almer:{user.Email}");
            return Results.Ok(new { secret, otpauthUri = $"otpauth://totp/{label}?secret={secret}&issuer={issuer}&digits=6&period=30" });
        });

        group.MapPost("/mfa/enable", async (MfaCodeRequest request, ClaimsPrincipal principal, AlmerDbContext db, IDataProtectionProvider protection, CancellationToken ct) =>
        {
            var user = await CurrentUser(principal, db, ct);
            if (user.MfaEnabled) return Results.Conflict(new { code = "mfa_already_enabled" });
            if (user.MfaPendingSecretProtected is null) return Results.BadRequest(new { code = "mfa_setup_required" });
            string secret;
            try { secret = Protector(protection).Unprotect(user.MfaPendingSecretProtected); }
            catch { return Results.Problem(statusCode: 409, title: "MFA setup must be restarted."); }
            if (!MfaSecurity.VerifyTotp(secret, request.Code)) return Results.BadRequest(new { code = "mfa_code_invalid" });
            var recoveryCodes = MfaSecurity.GenerateRecoveryCodes();
            user.MfaSecretProtected = Protector(protection).Protect(secret);
            user.MfaPendingSecretProtected = null;
            user.MfaEnabled = true;
            user.RecoveryCodesJson = JsonSerializer.Serialize(recoveryCodes.Select(x => MfaSecurity.HashRecoveryCode(user.Id, x)));
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { recoveryCodes });
        });

        group.MapPost("/mfa/disable", async (MfaDisableRequest request, ClaimsPrincipal principal, AlmerDbContext db, IDataProtectionProvider protection, CancellationToken ct) =>
        {
            var user = await CurrentUser(principal, db, ct);
            if (!PasswordSecurity.Verify(request.Password, user.PasswordHash)) return Results.Unauthorized();
            if (!user.MfaEnabled || !VerifyAndConsume(user, request.Code, protection)) return Results.BadRequest(new { code = "mfa_code_invalid" });
            user.MfaEnabled = false; user.MfaSecretProtected = null; user.MfaPendingSecretProtected = null; user.RecoveryCodesJson = "[]";
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapPost("/mfa/recovery-codes", async (MfaDisableRequest request, ClaimsPrincipal principal, AlmerDbContext db, IDataProtectionProvider protection, CancellationToken ct) =>
        {
            var user = await CurrentUser(principal, db, ct);
            if (!PasswordSecurity.Verify(request.Password, user.PasswordHash)) return Results.Unauthorized();
            if (!user.MfaEnabled || !VerifyAndConsume(user, request.Code, protection)) return Results.BadRequest(new { code = "mfa_code_invalid" });
            var recoveryCodes = MfaSecurity.GenerateRecoveryCodes();
            user.RecoveryCodesJson = JsonSerializer.Serialize(recoveryCodes.Select(x => MfaSecurity.HashRecoveryCode(user.Id, x)));
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { recoveryCodes });
        });
        return group;
    }

    public static bool VerifyAndConsume(User user, string code, IDataProtectionProvider protection)
    {
        if (user.MfaSecretProtected is null) return false;
        try
        {
            var secret = Protector(protection).Unprotect(user.MfaSecretProtected);
            if (MfaSecurity.VerifyTotp(secret, code)) return true;
        }
        catch { return false; }
        var hashes = ReadHashes(user);
        if (!MfaSecurity.TryUseRecoveryCode(user.Id, code, hashes)) return false;
        user.RecoveryCodesJson = JsonSerializer.Serialize(hashes);
        return true;
    }

    private static List<string> ReadHashes(User user)
    {
        try { return JsonSerializer.Deserialize<List<string>>(user.RecoveryCodesJson) ?? []; }
        catch (JsonException) { return []; }
    }

    private static IDataProtector Protector(IDataProtectionProvider provider) => provider.CreateProtector(Purpose);
    private static Task<User> CurrentUser(ClaimsPrincipal principal, AlmerDbContext db, CancellationToken ct) => db.UserSet.SingleAsync(x => x.Id == Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), ct);
}

public sealed record MfaCodeRequest(string Code);
public sealed record MfaDisableRequest(string Password, string Code);
