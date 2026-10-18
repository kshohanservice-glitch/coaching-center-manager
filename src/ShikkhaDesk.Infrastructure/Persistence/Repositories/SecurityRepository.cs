using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Infrastructure.Persistence.Repositories;

public sealed class SecurityRepository : RepositoryBase, ISecurityRepository
{
    public SecurityRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }

    private const string UserColumns = @"
        id, user_name, full_name, password_hash, password_salt, role_id, is_active, must_change_password,
        last_login_at_utc, failed_login_attempts, locked_until_utc, created_at_utc";

    public Task<User?> GetUserByUserNameAsync(string userName, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<User>($"SELECT {UserColumns} FROM users WHERE user_name = @UserName;",
            new { UserName = userName }, ct);

    public Task<User?> GetUserAsync(Guid id, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<User>($"SELECT {UserColumns} FROM users WHERE id = @Id;", new { Id = id }, ct);

    public async Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken ct = default) =>
        (await QueryAsync<User>($"SELECT {UserColumns} FROM users ORDER BY user_name;", null, ct)
            .ConfigureAwait(false)).ToList();

    public Task InsertUserAsync(User user, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO users (id, user_name, full_name, password_hash, password_salt, role_id, is_active,
                               must_change_password, last_login_at_utc, failed_login_attempts, locked_until_utc,
                               created_at_utc)
            VALUES (@Id, @UserName, @FullName, @PasswordHash, @PasswordSalt, @RoleId, @IsActive,
                    @MustChangePassword, @LastLoginAtUtc, @FailedLoginAttempts, @LockedUntilUtc, @CreatedAtUtc);",
            user, ct);

    public Task UpdateUserAsync(User user, CancellationToken ct = default) =>
        ExecuteAsync(@"
            UPDATE users SET user_name = @UserName, full_name = @FullName, password_hash = @PasswordHash,
                   password_salt = @PasswordSalt, role_id = @RoleId, is_active = @IsActive,
                   must_change_password = @MustChangePassword, last_login_at_utc = @LastLoginAtUtc,
                   failed_login_attempts = @FailedLoginAttempts, locked_until_utc = @LockedUntilUtc
             WHERE id = @Id;", user, ct);

    public Task<int> CountActiveUsersAsync(CancellationToken ct = default) =>
        ExecuteScalarAsync<int>("SELECT COUNT(*) FROM users WHERE is_active = 1;", null, ct)!;

    public async Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken ct = default) =>
        (await QueryAsync<Role>("SELECT id, name, description, is_system, created_at_utc FROM roles ORDER BY name;",
            null, ct).ConfigureAwait(false)).ToList();

    public Task<Role?> GetRoleAsync(Guid id, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<Role>(
            "SELECT id, name, description, is_system, created_at_utc FROM roles WHERE id = @Id;",
            new { Id = id }, ct);

    public Task<Role?> GetRoleByNameAsync(string name, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<Role>(
            "SELECT id, name, description, is_system, created_at_utc FROM roles WHERE name = @Name;",
            new { Name = name }, ct);

    public Task InsertRoleAsync(Role role, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO roles (id, name, description, is_system, created_at_utc)
            VALUES (@Id, @Name, @Description, @IsSystem, @CreatedAtUtc);", role, ct);

    public async Task<IReadOnlyList<Permission>> GetPermissionsAsync(CancellationToken ct = default) =>
        (await QueryAsync<Permission>(
            "SELECT id, code, description, group_name AS \"Group\" FROM permissions ORDER BY code;", null, ct)
            .ConfigureAwait(false)).ToList();

    public Task<Permission?> GetPermissionByCodeAsync(string code, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<Permission>(
            "SELECT id, code, description, group_name AS \"Group\" FROM permissions WHERE code = @Code;",
            new { Code = code }, ct);

    public Task InsertPermissionAsync(Permission permission, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO permissions (id, code, description, group_name)
            VALUES (@Id, @Code, @Description, @Group);", permission, ct);

    public async Task<IReadOnlyList<string>> GetPermissionsForRoleAsync(Guid roleId, CancellationToken ct = default) =>
        (await QueryAsync<string>(@"
            SELECT p.code FROM role_permissions rp
              JOIN permissions p ON p.id = rp.permission_id
             WHERE rp.role_id = @RoleId ORDER BY p.code;", new { RoleId = roleId }, ct)
            .ConfigureAwait(false)).ToList();

    public async Task ReplaceRolePermissionsAsync(Guid roleId, IReadOnlyList<Guid> permissionIds,
        CancellationToken ct = default)
    {
        await ExecuteAsync("DELETE FROM role_permissions WHERE role_id = @RoleId;", new { RoleId = roleId }, ct)
            .ConfigureAwait(false);

        foreach (Guid permissionId in permissionIds)
        {
            await ExecuteAsync(@"
                INSERT INTO role_permissions (id, role_id, permission_id) VALUES (@Id, @RoleId, @PermissionId);",
                new { Id = Guid.NewGuid(), RoleId = roleId, PermissionId = permissionId }, ct)
                .ConfigureAwait(false);
        }
    }

    public Task InsertAuditLogAsync(AuditLog log, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO audit_logs (id, occurred_at_utc, user_id, user_name, action, entity_type, entity_id,
                                    summary, before_json, after_json, source_device)
            VALUES (@Id, @OccurredAtUtc, @UserId, @UserName, @Action, @EntityType, @EntityId, @Summary,
                    @BeforeJson, @AfterJson, @SourceDevice);", log, ct);

    public async Task<PagedResult<AuditLog>> SearchAuditLogsAsync(string? search, DateRange? range, int page,
        int pageSize, CancellationToken ct = default)
    {
        string term = Like(search);
        string where = @"
            WHERE (@Search IS NULL OR user_name LIKE @Term ESCAPE '\'
                        OR action LIKE @Term ESCAPE '\'
                        OR entity_type LIKE @Term ESCAPE '\'
                        OR IFNULL(summary, '') LIKE @Term ESCAPE '\')
              AND (@From IS NULL OR occurred_at_utc >= @From)
              AND (@To IS NULL OR occurred_at_utc <= @To)";

        int total = await ExecuteScalarAsync<int?>($"SELECT COUNT(*) FROM audit_logs {where};",
            new
            {
                Search = search,
                Term = term,
                From = range is null ? (DateTime?)null : StartOfDay(range.From),
                To = range is null ? (DateTime?)null : EndOfDay(range.To)
            }, ct).ConfigureAwait(false) ?? 0;

        int size = Math.Clamp(pageSize, 1, 500);
        int current = Math.Max(1, page);

        var items = (await QueryAsync<AuditLog>($@"
            SELECT id, occurred_at_utc, user_id, user_name, action, entity_type, entity_id, summary,
                   before_json, after_json, source_device
              FROM audit_logs {where}
             ORDER BY occurred_at_utc DESC
             LIMIT @Limit OFFSET @Offset;",
            new
            {
                Search = search,
                Term = term,
                From = range is null ? (DateTime?)null : StartOfDay(range.From),
                To = range is null ? (DateTime?)null : EndOfDay(range.To),
                Limit = size,
                Offset = (current - 1) * size
            }, ct).ConfigureAwait(false)).ToList();

        return new PagedResult<AuditLog>(items, total, current, size);
    }
}
