using Almer.Application;
using Almer.Domain;
using Almer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Almer.Api;

public static class ManagementEndpoints
{
    public static RouteGroupBuilder MapManagementEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/tenant", GetTenant);
        group.MapPut("/tenant", UpdateTenant).RequirePermission(PermissionCodes.CompaniesManage);

        group.MapPost("/legal-entities", CreateLegalEntity).RequirePermission(PermissionCodes.CompaniesManage);
        group.MapPut("/legal-entities/{id:guid}", UpdateLegalEntity).RequirePermission(PermissionCodes.CompaniesManage);
        group.MapDelete("/legal-entities/{id:guid}", DeactivateLegalEntity).RequirePermission(PermissionCodes.CompaniesManage);

        group.MapPost("/roles", CreateRole).RequirePermission(PermissionCodes.RolesManage);
        group.MapPut("/roles/{id:guid}", UpdateRole).RequirePermission(PermissionCodes.RolesManage);
        group.MapDelete("/roles/{id:guid}", DeleteRole).RequirePermission(PermissionCodes.RolesManage);
        group.MapGet("/permissions", () => Results.Ok(PermissionCodes.All));

        group.MapGet("/users", ListUsers).RequirePermission(PermissionCodes.UsersManage);
        group.MapPost("/users", CreateUser).RequirePermission(PermissionCodes.UsersManage);
        group.MapPut("/users/{id:guid}", UpdateUser).RequirePermission(PermissionCodes.UsersManage);

        group.MapGet("/audit", ListAudit).RequirePermission(PermissionCodes.AuditRead);
        return group;
    }

    private static async Task<IResult> GetTenant(ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        var tenant = await db.TenantSet.SingleAsync(x => x.Id == context.TenantId, ct);
        return Results.Ok(new TenantDto(tenant.Id, tenant.Name, tenant.Slug, tenant.DefaultLocale, tenant.Currency, tenant.IsDemo, tenant.IsActive));
    }

    private static async Task<IResult> UpdateTenant(TenantUpdate input, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.DefaultLocale is not ("de-DE" or "tr-TR") || input.Currency.Length != 3) throw new ArgumentException("Invalid company settings.");
        var tenant = await db.TenantSet.SingleAsync(x => x.Id == context.TenantId, ct);
        tenant.Name = input.Name.Trim(); tenant.DefaultLocale = input.DefaultLocale; tenant.Currency = input.Currency.Trim().ToUpperInvariant();
        db.AuditEntrySet.Add(Audit(context, "Modified", nameof(Tenant), tenant.Id));
        await db.SaveChangesAsync(ct); return Results.Ok(new TenantDto(tenant.Id, tenant.Name, tenant.Slug, tenant.DefaultLocale, tenant.Currency, tenant.IsDemo, tenant.IsActive));
    }

    private static async Task<IResult> CreateLegalEntity(LegalEntityInput input, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        ValidateLegalEntity(input);
        if (await db.LegalEntitySet.AnyAsync(x => x.TaxNumber == input.TaxNumber.Trim(), ct)) throw new ArgumentException("Tax number already exists.");
        var entity = new LegalEntity { Id = Guid.NewGuid(), TenantId = context.TenantId, Name = new(input.NameDe, input.NameTr), TradeName = input.TradeName.Trim(), TaxNumber = input.TaxNumber.Trim(), CountryCode = input.CountryCode.Trim().ToUpperInvariant() };
        db.LegalEntitySet.Add(entity); await db.SaveChangesAsync(ct); return Results.Created($"/api/legal-entities/{entity.Id}", Map(entity));
    }

    private static async Task<IResult> UpdateLegalEntity(Guid id, LegalEntityUpdate input, AlmerDbContext db, CancellationToken ct)
    {
        ValidateLegalEntity(input);
        var entity = await db.LegalEntitySet.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Legal entity not found.");
        if (await db.LegalEntitySet.AnyAsync(x => x.Id != id && x.TaxNumber == input.TaxNumber.Trim(), ct)) throw new ArgumentException("Tax number already exists.");
        entity.Name = new(input.NameDe, input.NameTr); entity.TradeName = input.TradeName.Trim(); entity.TaxNumber = input.TaxNumber.Trim(); entity.CountryCode = input.CountryCode.Trim().ToUpperInvariant(); entity.IsActive = input.IsActive;
        await db.SaveChangesAsync(ct); return Results.Ok(Map(entity));
    }

    private static async Task<IResult> DeactivateLegalEntity(Guid id, AlmerDbContext db, CancellationToken ct)
    {
        var entity = await db.LegalEntitySet.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Legal entity not found.");
        if (await db.BranchSet.AnyAsync(x => x.LegalEntityId == id && x.IsActive, ct)) throw new ArgumentException("Deactivate active branches first.");
        entity.IsActive = false; await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static async Task<IResult> CreateRole(RoleInput input, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        ValidateRole(input); var id = Guid.NewGuid();
        var role = new Role { Id = id, TenantId = context.TenantId, Name = new(input.NameDe, input.NameTr), Permissions = input.Permissions.Distinct().Select(x => new RolePermission { TenantId = context.TenantId, RoleId = id, PermissionCode = x }).ToList() };
        db.RoleSet.Add(role); await db.SaveChangesAsync(ct); return Results.Created($"/api/roles/{id}", Map(role));
    }

    private static async Task<IResult> UpdateRole(Guid id, RoleInput input, AlmerDbContext db, CancellationToken ct)
    {
        ValidateRole(input);
        var role = await db.RoleSet.Include(x => x.Permissions).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Role not found.");
        role.Name = new(input.NameDe, input.NameTr);
        var requested = input.Permissions.Distinct().ToHashSet();
        db.RolePermissionSet.RemoveRange(role.Permissions.Where(x => !requested.Contains(x.PermissionCode)));
        foreach (var code in requested.Except(role.Permissions.Select(x => x.PermissionCode))) role.Permissions.Add(new RolePermission { TenantId = role.TenantId, RoleId = role.Id, PermissionCode = code });
        await db.SaveChangesAsync(ct); return Results.Ok(Map(role));
    }

    private static async Task<IResult> DeleteRole(Guid id, AlmerDbContext db, CancellationToken ct)
    {
        var role = await db.RoleSet.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Role not found.");
        if (role.IsSystem) throw new ArgumentException("System roles cannot be deleted.");
        if (await db.UserTenantSet.AnyAsync(x => x.RoleId == id, ct)) throw new ArgumentException("Role is assigned to users.");
        db.RoleSet.Remove(role); await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static async Task<IResult> ListUsers(ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        var memberships = await db.UserTenantSet.Where(x => x.TenantId == context.TenantId).ToListAsync(ct);
        var userIds = memberships.Select(x => x.UserId).ToArray();
        var users = await db.UserSet.Where(x => userIds.Contains(x.Id)).ToListAsync(ct);
        var branches = await db.UserBranchSet.Where(x => userIds.Contains(x.UserId)).ToListAsync(ct);
        return Results.Ok(users.Select(user => { var m = memberships.Single(x => x.UserId == user.Id); return new UserDto(user.Id, user.DisplayName, user.Email, m.RoleId, branches.Where(x => x.UserId == user.Id).Select(x => x.BranchId).ToArray(), m.IsActive, user.PreferredLocale); }));
    }

    private static async Task<IResult> CreateUser(UserInput input, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        await ValidateUserInput(input, context, db, ct);
        var email = input.Email.Trim().ToLowerInvariant();
        var user = await db.UserSet.SingleOrDefaultAsync(x => x.Email == email, ct);
        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(input.Password) || input.Password.Length < 10) throw new ArgumentException("Password must contain at least 10 characters.");
            PasswordPolicy.Validate(input.Password, email); user = new User { Id = Guid.NewGuid(), Email = email, DisplayName = input.DisplayName.Trim(), PasswordHash = PasswordSecurity.Hash(input.Password), PreferredLocale = input.Locale };
            db.UserSet.Add(user);
        }
        if (await db.UserTenantSet.IgnoreQueryFilters().AnyAsync(x => x.UserId == user.Id && x.TenantId == context.TenantId, ct)) throw new ArgumentException("User already belongs to this company.");
        db.UserTenantSet.Add(new UserTenant { UserId = user.Id, TenantId = context.TenantId, RoleId = input.RoleId });
        foreach (var branchId in input.BranchIds.Distinct()) db.UserBranchSet.Add(new UserBranch { UserId = user.Id, TenantId = context.TenantId, BranchId = branchId });
        await db.SaveChangesAsync(ct); return Results.Created($"/api/users/{user.Id}", new UserDto(user.Id, user.DisplayName, user.Email, input.RoleId, input.BranchIds, true, input.Locale));
    }

    private static async Task<IResult> UpdateUser(Guid id, UserUpdate input, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        await ValidateUserInput(new(input.DisplayName, input.Email, "placeholder-password", input.RoleId, input.BranchIds, input.Locale), context, db, ct);
        var membership = await db.UserTenantSet.SingleOrDefaultAsync(x => x.UserId == id, ct) ?? throw new KeyNotFoundException("User membership not found.");
        var user = await db.UserSet.SingleAsync(x => x.Id == id, ct);
        user.DisplayName = input.DisplayName.Trim(); user.PreferredLocale = input.Locale; membership.RoleId = input.RoleId; membership.IsActive = input.IsActive;
        var current = await db.UserBranchSet.Where(x => x.UserId == id).ToListAsync(ct); db.UserBranchSet.RemoveRange(current);
        foreach (var branchId in input.BranchIds.Distinct()) db.UserBranchSet.Add(new UserBranch { UserId = id, TenantId = context.TenantId, BranchId = branchId });
        await db.SaveChangesAsync(ct); return Results.Ok(new UserDto(user.Id, user.DisplayName, user.Email, input.RoleId, input.BranchIds, input.IsActive, input.Locale));
    }

    private static async Task<IResult> ListAudit(int? page, int? pageSize, AlmerDbContext db, CancellationToken ct)
    {
        var take = Math.Clamp(pageSize ?? 50, 1, 100); var skip = Math.Max((page ?? 1) - 1, 0) * take;
        var rows = await db.AuditEntrySet.OrderByDescending(x => x.OccurredAt).Skip(skip).Take(take).ToListAsync(ct);
        return Results.Ok(rows);
    }

    private static void ValidateLegalEntity(LegalEntityInput input)
    {
        if (string.IsNullOrWhiteSpace(input.NameDe) || string.IsNullOrWhiteSpace(input.NameTr) || string.IsNullOrWhiteSpace(input.TradeName) || string.IsNullOrWhiteSpace(input.TaxNumber) || input.CountryCode.Trim().Length != 2) throw new ArgumentException("All legal entity fields are required.");
    }
    private static void ValidateRole(RoleInput input)
    {
        if (string.IsNullOrWhiteSpace(input.NameDe) || string.IsNullOrWhiteSpace(input.NameTr)) throw new ArgumentException("Role names are required.");
        if (input.Permissions.Except(PermissionCodes.All).Any()) throw new ArgumentException("Unknown permission code.");
    }
    private static async Task ValidateUserInput(UserInput input, ITenantContext context, AlmerDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.DisplayName) || !input.Email.Contains('@') || input.Locale is not ("de-DE" or "tr-TR")) throw new ArgumentException("Invalid user data.");
        if (!await db.RoleSet.AnyAsync(x => x.Id == input.RoleId, ct)) throw new ArgumentException("Role not found in this company.");
        if (await db.BranchSet.CountAsync(x => input.BranchIds.Contains(x.Id), ct) != input.BranchIds.Distinct().Count()) throw new ArgumentException("A branch is outside this company.");
    }
    private static object Map(LegalEntity x) => new { x.Id, nameDe = x.Name.De, nameTr = x.Name.Tr, x.TradeName, x.TaxNumber, x.CountryCode, x.IsActive };
    private static object Map(Role x) => new { x.Id, nameDe = x.Name.De, nameTr = x.Name.Tr, x.IsSystem, permissions = x.Permissions.Select(p => p.PermissionCode).ToArray() };
    private static AuditEntry Audit(ITenantContext context, string action, string type, Guid id) => new() { Id = Guid.NewGuid(), TenantId = context.TenantId, UserId = context.UserId, Action = action, EntityType = type, EntityId = id.ToString(), OccurredAt = DateTimeOffset.UtcNow };
}

public sealed record TenantDto(Guid Id, string Name, string Slug, string DefaultLocale, string Currency, bool IsDemo, bool IsActive);
public sealed record TenantUpdate(string Name, string DefaultLocale, string Currency);
public record LegalEntityInput(string NameDe, string NameTr, string TradeName, string TaxNumber, string CountryCode);
public sealed record LegalEntityUpdate(string NameDe, string NameTr, string TradeName, string TaxNumber, string CountryCode, bool IsActive) : LegalEntityInput(NameDe, NameTr, TradeName, TaxNumber, CountryCode);
public sealed record RoleInput(string NameDe, string NameTr, string[] Permissions);
public record UserInput(string DisplayName, string Email, string Password, Guid RoleId, Guid[] BranchIds, string Locale);
public sealed record UserUpdate(string DisplayName, string Email, Guid RoleId, Guid[] BranchIds, string Locale, bool IsActive);
public sealed record UserDto(Guid Id, string DisplayName, string Email, Guid RoleId, Guid[] BranchIds, bool IsActive, string Locale);
