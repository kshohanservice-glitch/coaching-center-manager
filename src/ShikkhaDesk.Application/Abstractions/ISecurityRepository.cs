using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Application.Abstractions;

public interface ISecurityRepository
{
    Task<User?> GetUserByUserNameAsync(string userName, CancellationToken ct = default);
    Task<User?> GetUserAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken ct = default);
    Task InsertUserAsync(User user, CancellationToken ct = default);
    Task UpdateUserAsync(User user, CancellationToken ct = default);
    Task<int> CountActiveUsersAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken ct = default);
    Task<Role?> GetRoleAsync(Guid id, CancellationToken ct = default);
    Task<Role?> GetRoleByNameAsync(string name, CancellationToken ct = default);
    Task InsertRoleAsync(Role role, CancellationToken ct = default);
    Task<IReadOnlyList<Permission>> GetPermissionsAsync(CancellationToken ct = default);
    Task<Permission?> GetPermissionByCodeAsync(string code, CancellationToken ct = default);
    Task InsertPermissionAsync(Permission permission, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetPermissionsForRoleAsync(Guid roleId, CancellationToken ct = default);
    Task ReplaceRolePermissionsAsync(Guid roleId, IReadOnlyList<Guid> permissionIds,
        CancellationToken ct = default);

    Task InsertAuditLogAsync(AuditLog log, CancellationToken ct = default);
    Task<PagedResult<AuditLog>> SearchAuditLogsAsync(string? search, DateRange? range, int page, int pageSize,
        CancellationToken ct = default);
}
