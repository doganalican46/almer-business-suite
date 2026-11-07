using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Almer.Domain;
using Almer.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Almer.Api;

public sealed class SessionTokenService(IConfiguration configuration)
{
    private readonly byte[] key = Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required."));

    public async Task<LoginResponse> CreateResponseAsync(User user, UserTenant membership, Guid sessionId, AlmerDbContext db, CancellationToken ct)
    {
        var permissions = await db.RolePermissionSet.IgnoreQueryFilters().Where(x => x.TenantId == membership.TenantId && x.RoleId == membership.RoleId).Select(x => x.PermissionCode).ToListAsync(ct);
        var tenant = await db.TenantSet.SingleAsync(x => x.Id == membership.TenantId, ct);
        var branchIds = await db.UserBranchSet.IgnoreQueryFilters().Where(x => x.TenantId == membership.TenantId && x.UserId == user.Id).Select(x => x.BranchId).ToListAsync(ct);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.DisplayName), new(ClaimTypes.Email, user.Email), new("tenant_id", tenant.Id.ToString()), new("locale", user.PreferredLocale), new("session_id", sessionId.ToString()) };
        claims.AddRange(permissions.Select(x => new Claim("permission", x))); claims.AddRange(branchIds.Select(x => new Claim("branch_id", x.ToString())));
        var token = new JwtSecurityToken("almer-api", "almer-web", claims, expires: DateTime.UtcNow.AddMinutes(20), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), new(user.Id, user.DisplayName, user.Email, user.PreferredLocale, user.PreferredTheme, user.EmailVerifiedAt.HasValue), new(tenant.Id, tenant.Name, tenant.IsDemo, tenant.Currency), permissions);
    }
}

public static class AuthSessionEndpoints
{
    private const string CookieName = "almer_refresh";

    public static RouteGroupBuilder MapAnonymousSessionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/auth/refresh", Refresh).RequireRateLimiting("auth");
        group.MapPost("/auth/logout", Logout);
        return group;
    }

    public static RouteGroupBuilder MapUserSessionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/sessions", List);
        group.MapDelete("/sessions/{id:guid}", Revoke);
        return group;
    }

    public static UserSession CreateSession(Guid tenantId, Guid userId, string rawToken, HttpContext http, DateTimeOffset? familyExpiry = null, Guid? familyId = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new UserSession { Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId, FamilyId = familyId ?? Guid.NewGuid(), RefreshTokenHash = RefreshTokenSecurity.Hash(rawToken), UserAgent = Clean(http.Request.Headers.UserAgent.ToString(), 300), IpAddress = Clean(http.Connection.RemoteIpAddress?.ToString(), 64), CreatedAt = now, LastUsedAt = now, ExpiresAt = familyExpiry ?? now.AddDays(30) };
    }

    public static void SetRefreshCookie(HttpContext http, string token, DateTimeOffset expires, IWebHostEnvironment environment) => http.Response.Cookies.Append(CookieName, token, new CookieOptions { HttpOnly = true, Secure = !environment.IsDevelopment(), SameSite = SameSiteMode.Strict, Path = "/api/auth", Expires = expires, IsEssential = true });
    public static void ClearRefreshCookie(HttpContext http) => http.Response.Cookies.Delete(CookieName, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/api/auth" });

    private static async Task<IResult> Refresh(HttpContext http, TenantContext tenantContext, AlmerDbContext db, SessionTokenService tokens, IWebHostEnvironment environment, CancellationToken ct)
    {
        if (!http.Request.Cookies.TryGetValue(CookieName, out var rawToken) || string.IsNullOrWhiteSpace(rawToken)) return Results.Unauthorized();
        var hash = RefreshTokenSecurity.Hash(rawToken);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var matches = await db.UserSessionSet.FromSqlInterpolated($"SELECT * FROM \"UserSessionSet\" WHERE \"RefreshTokenHash\" = {hash} FOR UPDATE").IgnoreQueryFilters().ToListAsync(ct);
        var current = matches.SingleOrDefault();
        if (current is null) { ClearRefreshCookie(http); return Results.Unauthorized(); }
        tenantContext.SetSessionScope(current.TenantId, current.UserId);
        var now = DateTimeOffset.UtcNow;
        if (current.RevokedAt.HasValue)
        {
            if (current.RevokeReason == "rotated" && current.RevokedAt > now.AddSeconds(-10)) return Results.Conflict(new { code = "refresh_race" });
            if (current.RevokeReason == "rotated")
            {
                var family = await db.UserSessionSet.Where(x => x.FamilyId == current.FamilyId && x.RevokedAt == null).ToListAsync(ct);
                foreach (var item in family) { item.RevokedAt = now; item.RevokeReason = "refresh-token-reuse"; }
                await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            }
            ClearRefreshCookie(http); return Results.Unauthorized();
        }
        var user = await db.UserSet.SingleOrDefaultAsync(x => x.Id == current.UserId && x.IsActive, ct);
        var membership = await db.UserTenantSet.SingleOrDefaultAsync(x => x.UserId == current.UserId && x.TenantId == current.TenantId && x.IsActive, ct);
        if (current.ExpiresAt <= now || user is null || membership is null)
        {
            current.RevokedAt = now; current.RevokeReason = current.ExpiresAt <= now ? "expired" : "account-disabled";
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); ClearRefreshCookie(http); return Results.Unauthorized();
        }
        var replacementToken = RefreshTokenSecurity.Generate();
        var replacement = CreateSession(current.TenantId, current.UserId, replacementToken, http, current.ExpiresAt, current.FamilyId);
        current.RevokedAt = now; current.RevokeReason = "rotated"; current.ReplacedBySessionId = replacement.Id; current.LastUsedAt = now;
        db.UserSessionSet.Add(replacement); await db.SaveChangesAsync(ct);
        var response = await tokens.CreateResponseAsync(user, membership, replacement.Id, db, ct);
        await transaction.CommitAsync(ct); SetRefreshCookie(http, replacementToken, replacement.ExpiresAt, environment);
        return Results.Ok(response);
    }

    private static async Task<IResult> Logout(HttpContext http, TenantContext tenantContext, AlmerDbContext db, CancellationToken ct)
    {
        if (http.Request.Cookies.TryGetValue(CookieName, out var rawToken) && !string.IsNullOrWhiteSpace(rawToken))
        {
            var hash = RefreshTokenSecurity.Hash(rawToken); var session = await db.UserSessionSet.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.RefreshTokenHash == hash, ct);
            if (session is not null && !session.RevokedAt.HasValue) { tenantContext.SetSessionScope(session.TenantId, session.UserId); session.RevokedAt = DateTimeOffset.UtcNow; session.RevokeReason = "logout"; await db.SaveChangesAsync(ct); }
        }
        ClearRefreshCookie(http); return Results.NoContent();
    }

    private static async Task<IResult> List(ClaimsPrincipal principal, AlmerDbContext db, CancellationToken ct)
    {
        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!); var currentId = Guid.Parse(principal.FindFirstValue("session_id")!); var now = DateTimeOffset.UtcNow;
        var sessions = await db.UserSessionSet.AsNoTracking().Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > now).OrderByDescending(x => x.LastUsedAt).Select(x => new { x.Id, x.UserAgent, x.IpAddress, x.CreatedAt, x.LastUsedAt, x.ExpiresAt, isCurrent = x.Id == currentId }).ToListAsync(ct);
        return Results.Ok(sessions);
    }

    private static async Task<IResult> Revoke(Guid id, ClaimsPrincipal principal, HttpContext http, AlmerDbContext db, CancellationToken ct)
    {
        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!); var currentId = Guid.Parse(principal.FindFirstValue("session_id")!);
        var session = await db.UserSessionSet.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct) ?? throw new KeyNotFoundException("Session not found.");
        if (!session.RevokedAt.HasValue) { session.RevokedAt = DateTimeOffset.UtcNow; session.RevokeReason = "user-revoked"; await db.SaveChangesAsync(ct); }
        if (id == currentId) ClearRefreshCookie(http);
        return Results.NoContent();
    }

    private static string? Clean(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
