using System.Security.Claims;
using Almer.Application;

namespace Almer.Api;

public sealed class TenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    private Guid? backgroundTenantId;
    private Guid? backgroundUserId;
    private IReadOnlySet<Guid>? backgroundBranchIds;
    private ClaimsPrincipal User => accessor.HttpContext?.User ?? new ClaimsPrincipal();
    public Guid TenantId => backgroundTenantId ?? (Guid.TryParse(User.FindFirstValue("tenant_id"), out var id) ? id : Guid.Empty);
    public Guid UserId => backgroundUserId ?? (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty);
    public IReadOnlySet<Guid> AllowedBranchIds => backgroundBranchIds ?? User.FindAll("branch_id").Select(x => Guid.TryParse(x.Value, out var id) ? id : Guid.Empty).Where(x => x != Guid.Empty).ToHashSet();

    public void SetBackgroundScope(Guid tenantId, Guid userId, IEnumerable<Guid> branchIds)
    {
        if (accessor.HttpContext is not null) throw new InvalidOperationException("Background tenant scope cannot replace an HTTP request scope.");
        backgroundTenantId = tenantId; backgroundUserId = userId; backgroundBranchIds = branchIds.ToHashSet();
    }

    public void SetSessionScope(Guid tenantId, Guid userId)
    {
        if (Guid.TryParse(User.FindFirstValue("tenant_id"), out _)) throw new InvalidOperationException("Session scope cannot replace an authenticated tenant scope.");
        backgroundTenantId = tenantId; backgroundUserId = userId; backgroundBranchIds = new HashSet<Guid>();
    }

    public void SetTenantSwitchScope(Guid tenantId, Guid userId)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var authenticatedUserId) || authenticatedUserId != userId) throw new UnauthorizedAccessException("Tenant switch user mismatch.");
        backgroundTenantId = tenantId; backgroundUserId = userId; backgroundBranchIds = new HashSet<Guid>();
    }
}

