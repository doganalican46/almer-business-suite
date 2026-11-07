using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Almer.Api;
using Almer.Application;
using Almer.Domain;
using Almer.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders(); builder.Logging.AddJsonConsole(options => options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ");
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32) throw new InvalidOperationException("Jwt:Key must be at least 32 bytes.");

builder.Services.AddHttpContextAccessor();
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("AlmerBusinessSuite");
var keyPath = builder.Configuration["DataProtection:KeyPath"];
if (!string.IsNullOrWhiteSpace(keyPath)) dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddScoped<IAlmerData>(sp => sp.GetRequiredService<AlmerDbContext>());
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<BranchService>();
builder.Services.AddScoped<SessionTokenService>();
builder.Services.AddScoped<NotificationDeliveryService>();
builder.Services.AddScoped<AccountEmailDeliveryService>();
builder.Services.AddHttpClient("notification-webhook", client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddDbContext<AlmerDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => { o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = "almer-api", ValidateAudience = true, ValidAudience = "almer-web", ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), ClockSkew = TimeSpan.FromMinutes(1) }; o.Events = new JwtBearerEvents { OnTokenValidated = async context => { var sessionClaim = context.Principal?.FindFirstValue("session_id"); var userClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier); var tenantClaim = context.Principal?.FindFirstValue("tenant_id"); if (!Guid.TryParse(sessionClaim, out var sessionId) || !Guid.TryParse(userClaim, out var userId) || !Guid.TryParse(tenantClaim, out var tenantId)) { context.Fail("Session claim is missing."); return; } var db = context.HttpContext.RequestServices.GetRequiredService<AlmerDbContext>(); var active = await db.UserSessionSet.IgnoreQueryFilters().AnyAsync(x => x.Id == sessionId && x.UserId == userId && x.TenantId == tenantId && x.RevokedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow); if (!active) context.Fail("Session is no longer active."); } }; });
builder.Services.AddAuthorization();
builder.Services.AddOpenApi();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173", "http://localhost:4173"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddRateLimiter(options => options.AddPolicy("api", context => RateLimitPartition.GetFixedWindowLimiter(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous", _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
builder.Services.AddRateLimiter(options => options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
builder.Services.AddHostedService<PersistentJobWorker>();
builder.Services.AddSingleton<OperationalMetrics>();

var app = builder.Build();
app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto });
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});
app.UseMiddleware<RequestTelemetryMiddleware>();
app.UseExceptionHandler(exceptionApp => exceptionApp.Run(async context =>
{
    var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    context.Response.StatusCode = error switch { UnauthorizedAccessException => 403, KeyNotFoundException => 404, InvalidOperationException => 409, ArgumentException => 400, _ => 500 };
    await Results.Problem(statusCode: context.Response.StatusCode, title: error is ArgumentException ? error.Message : "The request could not be completed.", extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
}));
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapPlatformHealth(builder.Configuration);

var api = app.MapGroup("/api").RequireRateLimiting("api");
api.MapGet("/health", () => Results.Ok(new { status = "ok" }));

api.MapPost("/auth/login", async (LoginRequest request, HttpContext http, TenantContext tenantContext, AlmerDbContext db, IDataProtectionProvider protection, SessionTokenService tokens, IWebHostEnvironment environment, CancellationToken ct) =>
{
    var email = request.Email.Trim().ToLowerInvariant();
    var user = await db.UserSet.SingleOrDefaultAsync(x => x.Email == email, ct);
    if (user is null || !user.IsActive || !PasswordSecurity.Verify(request.Password, user.PasswordHash)) return Results.Unauthorized();
    var memberships = await db.UserTenantSet.IgnoreQueryFilters().Where(x => x.UserId == user.Id).ToListAsync(ct);
    var membership = request.TenantId.HasValue ? memberships.SingleOrDefault(x => x.TenantId == request.TenantId) : memberships.FirstOrDefault();
    if (membership is null || !membership.IsActive) return Results.Forbid();
    if (user.MfaEnabled)
    {
        if (string.IsNullOrWhiteSpace(request.MfaCode)) return Results.Json(new { code = "mfa_required" }, statusCode: StatusCodes.Status428PreconditionRequired);
        if (!MfaEndpointExtensions.VerifyAndConsume(user, request.MfaCode, protection)) return Results.Unauthorized();
        await db.SaveChangesAsync(ct);
    }
    tenantContext.SetSessionScope(membership.TenantId, user.Id);
    var refreshToken = RefreshTokenSecurity.Generate(); var session = AuthSessionEndpoints.CreateSession(membership.TenantId, user.Id, refreshToken, http); db.UserSessionSet.Add(session);
    var older = await db.UserSessionSet.Where(x => x.UserId == user.Id && x.RevokedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow).OrderByDescending(x => x.CreatedAt).Skip(9).ToListAsync(ct); foreach (var item in older) { item.RevokedAt = DateTimeOffset.UtcNow; item.RevokeReason = "session-limit"; }
    await db.SaveChangesAsync(ct); AuthSessionEndpoints.SetRefreshCookie(http, refreshToken, session.ExpiresAt, environment);
    return Results.Ok(await tokens.CreateResponseAsync(user, membership, session.Id, db, ct));
}).RequireRateLimiting("auth");
api.MapAnonymousSessionEndpoints();
api.MapAnonymousAccountEndpoints();

var secured = api.MapGroup("").RequireAuthorization();
secured.MapGet("/dashboard", async (DateOnly? from, DateOnly? to, Guid? branchId, HttpContext http, DashboardService service, CancellationToken ct) =>
{
    var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
    var start = from ?? end.AddDays(-29);
    var locale = http.User.FindFirstValue("locale") ?? "de-DE";
    return Results.Ok(await service.GetAsync(new(start, end, branchId), locale, ct));
}).RequirePermission(PermissionCodes.DashboardRead);

secured.MapGet("/legal-entities", async (IAlmerData db, CancellationToken ct) => Results.Ok(await Task.Run(() => db.LegalEntities.Select(x => new { x.Id, nameDe = x.Name.De, nameTr = x.Name.Tr, x.TradeName, x.TaxNumber, x.CountryCode, x.IsActive }).ToList(), ct)));
secured.MapGet("/branches", async (BranchService service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct)));
secured.MapPost("/branches", async (BranchInput input, BranchService service, CancellationToken ct) => Results.Created("/api/branches", await service.CreateAsync(input, ct))).RequirePermission(PermissionCodes.BranchesManage);
secured.MapPut("/branches/{id:guid}", async (Guid id, BranchUpdate input, BranchService service, CancellationToken ct) => Results.Ok(await service.UpdateAsync(id, input, ct))).RequirePermission(PermissionCodes.BranchesManage);
secured.MapDelete("/branches/{id:guid}", async (Guid id, BranchService service, CancellationToken ct) => { await service.DeactivateAsync(id, ct); return Results.NoContent(); }).RequirePermission(PermissionCodes.BranchesManage);
secured.MapGet("/roles", async (IAlmerData db, CancellationToken ct) => Results.Ok(await Task.Run(() => db.Roles.Select(x => new { x.Id, nameDe = x.Name.De, nameTr = x.Name.Tr, x.IsSystem, permissions = x.Permissions.Select(p => p.PermissionCode) }).ToList(), ct))).RequirePermission(PermissionCodes.RolesManage);
secured.MapPut("/users/me/preferences", async (PreferenceRequest request, ClaimsPrincipal principal, AlmerDbContext db, CancellationToken ct) =>
{
    var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var user = await db.UserSet.SingleAsync(x => x.Id == userId, ct);
    if (request.Locale is not ("de-DE" or "tr-TR") || request.Theme is not ("light" or "dark")) return Results.BadRequest();
    user.PreferredLocale = request.Locale; user.PreferredTheme = request.Theme;
    await db.SaveChangesAsync(ct); return Results.NoContent();
});
secured.MapManagementEndpoints();
secured.MapCatalogInventoryEndpoints();
secured.MapCommerceFinanceEndpoints();
secured.MapImportEndpoints();
secured.MapReportEndpoints();
secured.MapSettingsSearchEndpoints();
secured.MapNotificationEndpoints();
secured.MapMfaEndpoints();
secured.MapBackgroundJobEndpoints();
secured.MapUserSessionEndpoints();
secured.MapNotificationChannelEndpoints();
secured.MapAccountEndpoints();
secured.MapObservabilityEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AlmerDbContext>();
    await SqlMigrationRunner.RunAsync(builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection is required."), typeof(Program).Assembly);
    if (app.Environment.IsDevelopment()) await DevelopmentSeeder.SeedAsync(db);
}
app.Run();

public sealed record LoginRequest(string Email, string Password, Guid? TenantId, string? MfaCode);
public sealed record UserInfo(Guid Id, string DisplayName, string Email, string Locale, string Theme, bool EmailVerified);
public sealed record TenantInfo(Guid Id, string Name, bool IsDemo, string Currency);
public sealed record LoginResponse(string AccessToken, UserInfo User, TenantInfo Tenant, IReadOnlyList<string> Permissions);
public sealed record PreferenceRequest(string Locale, string Theme);

static class PermissionEndpointExtensions
{
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permission) => builder.RequireAuthorization(policy => policy.RequireClaim("permission", permission));
    public static RouteHandlerBuilder RequireAnyPermission(this RouteHandlerBuilder builder, params string[] permissions) => builder.RequireAuthorization(policy => policy.RequireAssertion(context => permissions.Any(permission => context.User.HasClaim("permission", permission))));
}

public partial class Program { }

