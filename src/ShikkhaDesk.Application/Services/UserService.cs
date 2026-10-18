using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Core.Security;
using ShikkhaDesk.Core.Services;

namespace ShikkhaDesk.Application.Services;

public interface IUserService
{
    Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken ct = default);
    Task<Guid> CreateUserAsync(string userName, string fullName, string password, Guid? roleId,
        CancellationToken ct = default);
    Task SetUserActiveAsync(Guid userId, bool isActive, CancellationToken ct = default);
    Task ResetPasswordAsync(Guid userId, string newPassword, bool mustChange, CancellationToken ct = default);
    Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken ct = default);
    Task<Guid> CreateRoleAsync(string name, string? description, CancellationToken ct = default);
    Task SetRolePermissionsAsync(Guid roleId, IReadOnlyList<string> permissionCodes,
        CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetRolePermissionsAsync(Guid roleId, CancellationToken ct = default);
    Task<IReadOnlyList<Permission>> GetPermissionsAsync(CancellationToken ct = default);
    Task<PagedResult<AuditLog>> GetAuditLogsAsync(string? search, DateRange? range, int page = 1,
        int pageSize = 100, CancellationToken ct = default);
}

public sealed class UserService : IUserService
{
    private readonly ISecurityRepository _security;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;
    private readonly PasswordHasher _hasher;

    public UserService(ISecurityRepository security, IUnitOfWork unitOfWork, IAuditService audit,
        ICurrentUserService currentUser, PasswordHasher hasher)
    {
        _security = security;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _currentUser = currentUser;
        _hasher = hasher;
    }

    public Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken ct = default)
    {
        _currentUser.Current.Require("users.manage");
        return _unitOfWork.ExecuteAsync(token => _security.GetUsersAsync(token), ct);
    }

    public async Task<Guid> CreateUserAsync(string userName, string fullName, string password, Guid? roleId,
        CancellationToken ct = default)
    {
        _currentUser.Current.Require("users.manage");

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(userName))
        {
            errors.Add("ব্যবহারকারী নাম প্রয়োজন।");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            errors.Add("পুরো নাম প্রয়োজন।");
        }

        errors.AddRange(PasswordHasher.ValidateStrength(password ?? string.Empty));

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return await _unitOfWork.ExecuteAsync(async token =>
        {
            User? existing = await _security.GetUserByUserNameAsync(userName.Trim(), token).ConfigureAwait(false);
            if (existing is not null)
            {
                throw new DomainException("এই ব্যবহারকারী নাম ইতিমধ্যে ব্যবহৃত হয়েছে।");
            }

            (string hash, string salt) = _hasher.Hash(password ?? string.Empty);

            var user = new User
            {
                Id = Guid.NewGuid(),
                UserName = userName.Trim(),
                FullName = fullName.Trim(),
                PasswordHash = hash,
                PasswordSalt = salt,
                RoleId = roleId,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _security.InsertUserAsync(user, token).ConfigureAwait(false);
            await _audit.RecordAsync("users.create", nameof(User), user.Id, user.UserName, ct: token)
                .ConfigureAwait(false);
            return user.Id;
        }, ct).ConfigureAwait(false);
    }

    public async Task SetUserActiveAsync(Guid userId, bool isActive, CancellationToken ct = default)
    {
        _currentUser.Current.Require("users.manage");

        if (userId == _currentUser.Current.UserId && !isActive)
        {
            throw new DomainException("নিজের অ্যাকাউন্ট নিষ্ক্রিয় করা যাবে না।");
        }

        await _unitOfWork.ExecuteAsync(async token =>
        {
            User? user = await _security.GetUserAsync(userId, token).ConfigureAwait(false)
                ?? throw new NotFoundException("ব্যবহারকারী পাওয়া যায়নি।");

            if (user.IsActive && !isActive)
            {
                int active = await _security.CountActiveUsersAsync(token).ConfigureAwait(false);
                if (active <= 1)
                {
                    throw new DomainException("অন্তত একজন সক্রিয় ব্যবহারকারী থাকতে হবে।");
                }
            }

            user.IsActive = isActive;
            await _security.UpdateUserAsync(user, token).ConfigureAwait(false);
            await _audit.RecordAsync(isActive ? "users.activate" : "users.deactivate", nameof(User), userId,
                user.UserName, ct: token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    public async Task ResetPasswordAsync(Guid userId, string newPassword, bool mustChange,
        CancellationToken ct = default)
    {
        _currentUser.Current.Require("users.manage");

        IReadOnlyList<string> errors = PasswordHasher.ValidateStrength(newPassword ?? string.Empty);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        await _unitOfWork.ExecuteAsync(async token =>
        {
            User? user = await _security.GetUserAsync(userId, token).ConfigureAwait(false)
                ?? throw new NotFoundException("ব্যবহারকারী পাওয়া যায়নি।");

            (string hash, string salt) = _hasher.Hash(newPassword ?? string.Empty);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            user.MustChangePassword = mustChange;
            user.FailedLoginAttempts = 0;
            user.LockedUntilUtc = null;
            await _security.UpdateUserAsync(user, token).ConfigureAwait(false);
            await _audit.RecordAsync("users.password.reset", nameof(User), userId, user.UserName, ct: token)
                .ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _security.GetRolesAsync(token), ct);

    public async Task<Guid> CreateRoleAsync(string name, string? description, CancellationToken ct = default)
    {
        _currentUser.Current.Require("roles.manage");

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("ভূমিকার নাম প্রয়োজন।");
        }

        return await _unitOfWork.ExecuteAsync(async token =>
        {
            Role? existing = await _security.GetRoleByNameAsync(name.Trim(), token).ConfigureAwait(false);
            if (existing is not null)
            {
                throw new DomainException("এই নামের ভূমিকা ইতিমধ্যে আছে।");
            }

            var role = new Role
            {
                Id = Guid.NewGuid(),
                Name = name.Trim(),
                Description = description,
                IsSystem = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _security.InsertRoleAsync(role, token).ConfigureAwait(false);
            await _audit.RecordAsync("roles.create", nameof(Role), role.Id, role.Name, ct: token)
                .ConfigureAwait(false);
            return role.Id;
        }, ct).ConfigureAwait(false);
    }

    public async Task SetRolePermissionsAsync(Guid roleId, IReadOnlyList<string> permissionCodes,
        CancellationToken ct = default)
    {
        _currentUser.Current.Require("roles.manage");

        await _unitOfWork.ExecuteAsync(async token =>
        {
            Role? role = await _security.GetRoleAsync(roleId, token).ConfigureAwait(false)
                ?? throw new NotFoundException("ভূমিকা পাওয়া যায়নি।");

            IReadOnlyList<Permission> all = await _security.GetPermissionsAsync(token).ConfigureAwait(false);
            var lookup = all.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);

            var ids = new List<Guid>(permissionCodes.Count);
            foreach (string code in permissionCodes)
            {
                if (!PermissionCodes.IsKnown(code))
                {
                    throw new ValidationException($"অজানা অনুমতি: {code}");
                }

                if (lookup.TryGetValue(code, out Permission? permission))
                {
                    ids.Add(permission.Id);
                }
            }

            await _security.ReplaceRolePermissionsAsync(roleId, ids, token).ConfigureAwait(false);
            await _audit.RecordAsync("roles.permissions.update", nameof(Role), roleId,
                $"{role.Name}: {ids.Count} টি অনুমতি", ct: token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<string>> GetRolePermissionsAsync(Guid roleId, CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _security.GetPermissionsForRoleAsync(roleId, token), ct);

    public Task<IReadOnlyList<Permission>> GetPermissionsAsync(CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _security.GetPermissionsAsync(token), ct);

    public Task<PagedResult<AuditLog>> GetAuditLogsAsync(string? search, DateRange? range, int page = 1,
        int pageSize = 100, CancellationToken ct = default)
    {
        _currentUser.Current.Require("audit.view");
        return _unitOfWork.ExecuteAsync(
            token => _security.SearchAuditLogsAsync(search, range, page, pageSize, token), ct);
    }
}
