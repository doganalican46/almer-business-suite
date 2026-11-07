using System.Security.Claims;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Almer.Application;
using Almer.Domain;
using Almer.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Almer.Api;

public static class AccountEndpoints
{
    private const string MailTokenPurpose = "Almer.AccountMailToken.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static RouteGroupBuilder MapAnonymousAccountEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/auth/password/forgot", ForgotPassword).RequireRateLimiting("auth");
        group.MapPost("/auth/password/reset", ResetPassword).RequireRateLimiting("auth");
        group.MapPost("/auth/email/verify", VerifyEmail).RequireRateLimiting("auth");
        return group;
    }

    public static RouteGroupBuilder MapAccountEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/users/me/password", ChangePassword);
        group.MapPost("/users/me/email-verification", SendVerification);
        group.MapGet("/tenants/available", AvailableTenants);
        group.MapPost("/auth/switch-tenant", SwitchTenant);
        return group;
    }

    private static async Task<IResult> ForgotPassword(EmailRequest request, TenantContext tenantContext, AlmerDbContext db, IDataProtectionProvider protection, IConfiguration configuration, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant(); var user = await db.UserSet.SingleOrDefaultAsync(x => x.Email == email && x.IsActive, ct);
        if (user is not null && !string.IsNullOrWhiteSpace(configuration["Notifications:Smtp:Host"]))
        {
            var membership = await db.UserTenantSet.IgnoreQueryFilters().Where(x => x.UserId == user.Id && x.IsActive).OrderBy(x => x.TenantId).FirstOrDefaultAsync(ct);
            if (membership is not null) { tenantContext.SetSessionScope(membership.TenantId, user.Id); await CreateAndQueue(user, membership.TenantId, AccountTokenType.PasswordReset, protection, db, ct); }
        }
        return Results.Accepted();
    }

    private static async Task<IResult> ResetPassword(ResetPasswordRequest request, AlmerDbContext db, CancellationToken ct)
    {
        var hash = RefreshTokenSecurity.Hash(request.Token); var token = await db.AccountTokenSet.SingleOrDefaultAsync(x => x.TokenHash == hash && x.Type == AccountTokenType.PasswordReset, ct);
        if (token is null || token.ConsumedAt.HasValue || token.ExpiresAt <= DateTimeOffset.UtcNow) return Results.BadRequest(new { code = "token_invalid" });
        var user = await db.UserSet.SingleAsync(x => x.Id == token.UserId, ct); PasswordPolicy.Validate(request.NewPassword, user.Email); await using var transaction = await db.Database.BeginTransactionAsync(ct); user.PasswordHash = PasswordSecurity.Hash(request.NewPassword); token.ConsumedAt = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"UserSessionSet\" SET \"RevokedAt\" = now(), \"RevokeReason\" = 'password-reset' WHERE \"UserId\" = {user.Id} AND \"RevokedAt\" IS NULL", ct); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Results.NoContent();
    }

    private static async Task<IResult> VerifyEmail(TokenRequest request, AlmerDbContext db, CancellationToken ct)
    {
        var hash = RefreshTokenSecurity.Hash(request.Token); var token = await db.AccountTokenSet.SingleOrDefaultAsync(x => x.TokenHash == hash && x.Type == AccountTokenType.EmailVerification, ct);
        if (token is null || token.ConsumedAt.HasValue || token.ExpiresAt <= DateTimeOffset.UtcNow) return Results.BadRequest(new { code = "token_invalid" });
        var user = await db.UserSet.SingleAsync(x => x.Id == token.UserId, ct); user.EmailVerifiedAt = DateTimeOffset.UtcNow; token.ConsumedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static async Task<IResult> ChangePassword(ChangePasswordRequest request, ClaimsPrincipal principal, AlmerDbContext db, CancellationToken ct)
    {
        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!); var user = await db.UserSet.SingleAsync(x => x.Id == userId, ct);
        if (!PasswordSecurity.Verify(request.CurrentPassword, user.PasswordHash)) return Results.Unauthorized(); PasswordPolicy.Validate(request.NewPassword, user.Email); await using var transaction = await db.Database.BeginTransactionAsync(ct); user.PasswordHash = PasswordSecurity.Hash(request.NewPassword);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"UserSessionSet\" SET \"RevokedAt\" = now(), \"RevokeReason\" = 'password-change' WHERE \"UserId\" = {user.Id} AND \"RevokedAt\" IS NULL", ct); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Results.NoContent();
    }

    private static async Task<IResult> SendVerification(ClaimsPrincipal principal, ITenantContext tenant, AlmerDbContext db, IDataProtectionProvider protection, IConfiguration configuration, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(configuration["Notifications:Smtp:Host"])) throw new InvalidOperationException("SMTP is not configured.");
        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!); var user = await db.UserSet.SingleAsync(x => x.Id == userId, ct); if (user.EmailVerifiedAt.HasValue) return Results.NoContent();
        await CreateAndQueue(user, tenant.TenantId, AccountTokenType.EmailVerification, protection, db, ct); return Results.Accepted();
    }

    private static async Task<IResult> AvailableTenants(ClaimsPrincipal principal, AlmerDbContext db, CancellationToken ct)
    {
        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var rows = await db.UserTenantSet.IgnoreQueryFilters().Where(x => x.UserId == userId && x.IsActive).Join(db.TenantSet, membership => membership.TenantId, tenant => tenant.Id, (membership, tenant) => new { tenant.Id, tenant.Name, tenant.Slug, tenant.Currency, tenant.IsDemo, tenant.IsActive }).Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(ct); return Results.Ok(rows);
    }

    private static async Task<IResult> SwitchTenant(SwitchTenantRequest request, ClaimsPrincipal principal, HttpContext http, TenantContext tenantContext, AlmerDbContext db, SessionTokenService tokens, IWebHostEnvironment environment, CancellationToken ct)
    {
        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!); var currentSessionId = Guid.Parse(principal.FindFirstValue("session_id")!);
        var membership = await db.UserTenantSet.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.UserId == userId && x.TenantId == request.TenantId && x.IsActive, ct) ?? throw new UnauthorizedAccessException("Tenant membership not found.");
        var target = await db.TenantSet.SingleOrDefaultAsync(x => x.Id == request.TenantId && x.IsActive, ct) ?? throw new KeyNotFoundException("Tenant not found."); var user = await db.UserSet.SingleAsync(x => x.Id == userId, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct); var current = await db.UserSessionSet.SingleAsync(x => x.Id == currentSessionId, ct); current.RevokedAt = DateTimeOffset.UtcNow; current.RevokeReason = "tenant-switch"; await db.SaveChangesAsync(ct);
        tenantContext.SetTenantSwitchScope(target.Id, userId); var refreshToken = RefreshTokenSecurity.Generate(); var replacement = AuthSessionEndpoints.CreateSession(target.Id, userId, refreshToken, http, current.ExpiresAt, current.FamilyId); db.UserSessionSet.Add(replacement); await db.SaveChangesAsync(ct);
        var response = await tokens.CreateResponseAsync(user, membership, replacement.Id, db, ct); await transaction.CommitAsync(ct); AuthSessionEndpoints.SetRefreshCookie(http, refreshToken, replacement.ExpiresAt, environment); return Results.Ok(response);
    }

    private static async Task CreateAndQueue(User user, Guid tenantId, AccountTokenType type, IDataProtectionProvider protection, AlmerDbContext db, CancellationToken ct)
    {
        var raw = RefreshTokenSecurity.Generate(); var now = DateTimeOffset.UtcNow; var previous = await db.AccountTokenSet.Where(x => x.UserId == user.Id && x.Type == type && x.ConsumedAt == null).ToListAsync(ct); foreach (var old in previous) old.ConsumedAt = now; var token = new AccountToken { Id = Guid.NewGuid(), UserId = user.Id, Type = type, TokenHash = RefreshTokenSecurity.Hash(raw), CreatedAt = now, ExpiresAt = now.Add(type == AccountTokenType.PasswordReset ? TimeSpan.FromHours(1) : TimeSpan.FromHours(24)) }; db.AccountTokenSet.Add(token);
        var protectedToken = protection.CreateProtector(MailTokenPurpose).Protect(raw); var payload = new AccountEmailPayload(user.Email, user.PreferredLocale, type, protectedToken);
        db.BackgroundJobSet.Add(new BackgroundJob { Id = Guid.NewGuid(), TenantId = tenantId, Type = BackgroundJobType.AccountEmailDelivery, Status = BackgroundJobStatus.Queued, PayloadJson = JsonSerializer.Serialize(payload, JsonOptions), BranchIdsJson = "[]", MaxAttempts = 5, CreatedAt = now, AvailableAt = now }); await db.SaveChangesAsync(ct);
    }

    public static string UnprotectMailToken(AccountEmailPayload payload, IDataProtectionProvider protection) => protection.CreateProtector(MailTokenPurpose).Unprotect(payload.ProtectedToken);
}

public sealed record EmailRequest(string Email);
public sealed record TokenRequest(string Token);
public sealed record ResetPasswordRequest(string Token, string NewPassword);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record SwitchTenantRequest(Guid TenantId);
public sealed record AccountEmailPayload(string Email, string Locale, AccountTokenType Type, string ProtectedToken);

public sealed class AccountEmailDeliveryService(IConfiguration configuration, IDataProtectionProvider protection)
{
    public async Task DeliverAsync(AccountEmailPayload payload, CancellationToken ct)
    {
        var token = AccountEndpoints.UnprotectMailToken(payload, protection); var publicUrl = (configuration["App:PublicUrl"] ?? throw new InvalidOperationException("App public URL is missing.")).TrimEnd('/'); var tr = payload.Locale.StartsWith("tr", StringComparison.OrdinalIgnoreCase);
        var path = payload.Type == AccountTokenType.PasswordReset ? "reset-password" : "verify-email"; var link = $"{publicUrl}/{path}?token={Uri.EscapeDataString(token)}";
        var subject = payload.Type == AccountTokenType.PasswordReset ? (tr ? "Almer parola sıfırlama" : "Almer Passwort zurücksetzen") : (tr ? "Almer e-posta doğrulama" : "Almer E-Mail bestätigen");
        var body = payload.Type == AccountTokenType.PasswordReset ? (tr ? $"Parolanızı sıfırlamak için bağlantıyı açın:\n{link}\n\nBu bağlantı 1 saat geçerlidir." : $"Öffnen Sie den Link, um Ihr Passwort zurückzusetzen:\n{link}\n\nDieser Link ist 1 Stunde gültig.") : (tr ? $"E-posta adresinizi doğrulamak için bağlantıyı açın:\n{link}\n\nBu bağlantı 24 saat geçerlidir." : $"Öffnen Sie den Link, um Ihre E-Mail-Adresse zu bestätigen:\n{link}\n\nDieser Link ist 24 Stunden gültig.");
        var host = configuration["Notifications:Smtp:Host"] ?? throw new InvalidOperationException("SMTP host is missing."); using var message = new MailMessage { From = new MailAddress(configuration["Notifications:Smtp:From"] ?? throw new InvalidOperationException("SMTP sender is missing.")), Subject = subject, Body = body };
        message.To.Add(payload.Email); using var smtp = new SmtpClient(host, configuration.GetValue("Notifications:Smtp:Port", 587)) { EnableSsl = configuration.GetValue("Notifications:Smtp:EnableSsl", true) }; var username = configuration["Notifications:Smtp:Username"]; if (!string.IsNullOrWhiteSpace(username)) smtp.Credentials = new NetworkCredential(username, configuration["Notifications:Smtp:Password"]); await smtp.SendMailAsync(message, ct);
    }
}
