using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Almer.Application;
using Almer.Domain;
using Almer.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Almer.Api;

public static class NotificationChannelEndpoints
{
    private const string SecretPurpose = "Almer.WebhookSecret.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static RouteGroupBuilder MapNotificationChannelEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/notification-channels", Get).RequirePermission(PermissionCodes.SettingsManage);
        group.MapPut("/notification-channels", Update).RequirePermission(PermissionCodes.SettingsManage);
        return group;
    }

    private static async Task<IResult> Get(AlmerDbContext db, CancellationToken ct)
    {
        var value = await db.NotificationChannelSettingSet.AsNoTracking().SingleOrDefaultAsync(ct);
        return Results.Ok(value is null ? new NotificationChannelDto(false, [], false, null, false) : Map(value));
    }

    private static async Task<IResult> Update(NotificationChannelInput input, AlmerDbContext db, ITenantContext tenant, IDataProtectionProvider protection, IConfiguration configuration, IWebHostEnvironment environment, CancellationToken ct)
    {
        var recipients = input.EmailRecipients.Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).Distinct().ToArray();
        if (recipients.Length > 20 || recipients.Any(x => !IsEmail(x))) throw new ArgumentException("Invalid email recipient list.");
        if (input.EmailEnabled && (recipients.Length == 0 || string.IsNullOrWhiteSpace(configuration["Notifications:Smtp:Host"]))) throw new ArgumentException("SMTP must be configured and at least one recipient is required.");
        if (input.WebhookEnabled) NotificationChannelSecurity.ValidateWebhook(input.WebhookUrl, configuration, environment);
        var value = await db.NotificationChannelSettingSet.SingleOrDefaultAsync(ct);
        if (value is null) { value = new NotificationChannelSetting { Id = Guid.NewGuid(), TenantId = tenant.TenantId }; db.NotificationChannelSettingSet.Add(value); }
        if (input.WebhookEnabled && string.IsNullOrWhiteSpace(input.WebhookSecret) && string.IsNullOrWhiteSpace(value.WebhookSecretProtected)) throw new ArgumentException("A webhook signing secret of at least 32 characters is required.");
        if (!string.IsNullOrWhiteSpace(input.WebhookSecret))
        {
            if (input.WebhookSecret.Length is < 32 or > 256) throw new ArgumentException("Webhook secret must be between 32 and 256 characters.");
            value.WebhookSecretProtected = protection.CreateProtector(SecretPurpose).Protect(input.WebhookSecret);
        }
        value.EmailEnabled = input.EmailEnabled; value.EmailRecipientsJson = JsonSerializer.Serialize(recipients, JsonOptions); value.WebhookEnabled = input.WebhookEnabled; value.WebhookUrl = input.WebhookEnabled ? input.WebhookUrl?.Trim() : null;
        await db.SaveChangesAsync(ct); return Results.Ok(Map(value));
    }

    public static string UnprotectSecret(NotificationChannelSetting setting, IDataProtectionProvider protection) => protection.CreateProtector(SecretPurpose).Unprotect(setting.WebhookSecretProtected ?? throw new InvalidOperationException("Webhook secret is missing."));
    private static NotificationChannelDto Map(NotificationChannelSetting value) => new(value.EmailEnabled, JsonSerializer.Deserialize<string[]>(value.EmailRecipientsJson, JsonOptions) ?? [], value.WebhookEnabled, value.WebhookUrl, !string.IsNullOrWhiteSpace(value.WebhookSecretProtected));
    private static bool IsEmail(string value) { try { return new MailAddress(value).Address.Equals(value, StringComparison.OrdinalIgnoreCase); } catch { return false; } }
}

public static class NotificationChannelSecurity
{
    public static Uri ValidateWebhook(string? value, IConfiguration configuration, IWebHostEnvironment environment)
        => ValidateWebhook(value, configuration.GetSection("Notifications:WebhookAllowedHosts").Get<string[]>() ?? [], environment.IsDevelopment());

    public static Uri ValidateWebhook(string? value, IReadOnlyCollection<string> allowed, bool isDevelopment)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length > 0 || uri.Port is not (443 or -1)) throw new ArgumentException("Webhook URL must use HTTPS on the standard port.");
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || IPAddress.TryParse(uri.Host, out _)) throw new ArgumentException("Webhook IP literals and localhost are not allowed.");
        if (!isDevelopment && allowed.Count == 0) throw new ArgumentException("WebhookAllowedHosts must be configured in production.");
        if (allowed.Count > 0 && !allowed.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("Webhook host is not allowed.");
        return uri;
    }

    public static string ComputeSignature(string secret, string payload) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
}

public sealed class NotificationDeliveryService(IHttpClientFactory clients, IConfiguration configuration, IWebHostEnvironment environment, IDataProtectionProvider protection)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task DeliverAsync(BackgroundJobType type, NotificationDeliveryPayload delivery, AlmerDbContext db, CancellationToken ct)
    {
        var notification = await db.NotificationSet.AsNoTracking().SingleAsync(x => x.Id == delivery.NotificationId, ct);
        var setting = await db.NotificationChannelSettingSet.AsNoTracking().SingleOrDefaultAsync(ct) ?? throw new InvalidOperationException("Notification channels are not configured.");
        if (type == BackgroundJobType.NotificationEmailDelivery) await SendEmail(notification, setting, ct);
        else if (type == BackgroundJobType.NotificationWebhookDelivery) await SendWebhook(notification, delivery.EventId, setting, ct);
        else throw new InvalidOperationException("Unknown notification delivery type.");
    }

    private async Task SendEmail(Notification notification, NotificationChannelSetting setting, CancellationToken ct)
    {
        if (!setting.EmailEnabled) return;
        var recipients = JsonSerializer.Deserialize<string[]>(setting.EmailRecipientsJson, JsonOptions) ?? [];
        var host = configuration["Notifications:Smtp:Host"] ?? throw new InvalidOperationException("SMTP host is missing.");
        using var message = new MailMessage { From = new MailAddress(configuration["Notifications:Smtp:From"] ?? throw new InvalidOperationException("SMTP sender is missing.")), Subject = $"[Almer] {notification.Title.De}", Body = $"{notification.Message.De}\n\n{notification.Title.Tr}\n{notification.Message.Tr}", IsBodyHtml = false };
        foreach (var recipient in recipients) message.To.Add(recipient);
        using var smtp = new SmtpClient(host, configuration.GetValue("Notifications:Smtp:Port", 587)) { EnableSsl = configuration.GetValue("Notifications:Smtp:EnableSsl", true) };
        var username = configuration["Notifications:Smtp:Username"]; if (!string.IsNullOrWhiteSpace(username)) smtp.Credentials = new NetworkCredential(username, configuration["Notifications:Smtp:Password"]);
        await smtp.SendMailAsync(message, ct);
    }

    private async Task SendWebhook(Notification notification, Guid eventId, NotificationChannelSetting setting, CancellationToken ct)
    {
        if (!setting.WebhookEnabled) return;
        var uri = NotificationChannelSecurity.ValidateWebhook(setting.WebhookUrl, configuration, environment);
        var payload = JsonSerializer.Serialize(new { id = notification.Id, type = notification.Type, severity = notification.Severity, title = new { de = notification.Title.De, tr = notification.Title.Tr }, message = new { de = notification.Message.De, tr = notification.Message.Tr }, notification.BranchId, notification.ProductId, notification.TargetUrl, occurredAt = notification.UpdatedAt }, JsonOptions);
        var secret = NotificationChannelEndpoints.UnprotectSecret(setting, protection); var signature = NotificationChannelSecurity.ComputeSignature(secret, payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Almer-Event", "notification.updated"); request.Headers.Add("X-Almer-Delivery", eventId.ToString()); request.Headers.Add("X-Almer-Signature", $"sha256={signature}");
        using var response = await clients.CreateClient("notification-webhook").SendAsync(request, ct); response.EnsureSuccessStatusCode();
    }
}

public sealed record NotificationChannelDto(bool EmailEnabled, IReadOnlyList<string> EmailRecipients, bool WebhookEnabled, string? WebhookUrl, bool HasWebhookSecret);
public sealed record NotificationChannelInput(bool EmailEnabled, IReadOnlyList<string> EmailRecipients, bool WebhookEnabled, string? WebhookUrl, string? WebhookSecret);
public sealed record NotificationDeliveryPayload(Guid NotificationId, Guid EventId);
