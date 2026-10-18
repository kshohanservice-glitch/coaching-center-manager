using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Application.Services;

public interface IAuthService
{
    Task<Principal> LoginAsync(string userName, string password, string? deviceId = null,
        CancellationToken ct = default);

    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword,
        CancellationToken ct = default);

    Task<Principal> RefreshAsync(Guid userId, CancellationToken ct = default);
    void Logout();
}

/// <summary>
/// Authentication. Failed attempts are counted and an account is locked for a short period after
/// repeated failures, to make password guessing impractical. Passwords are never logged.
/// </summary>
public sealed class AuthService : IAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    private readonly ISecurityRepository _security;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly PasswordHasher _hasher;

    public AuthService(ISecurityRepository security, IUnitOfWork unitOfWork, ICurrentUserService currentUser,
        IAuditService audit, PasswordHasher hasher)
    {
        _security = security;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _audit = audit;
        _hasher = hasher;
    }

    public async Task<Principal> LoginAsync(string userName, string password, string? deviceId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
        {
            throw new ValidationException("ব্যবহারকারী নাম ও পাসওয়ার্ড প্রয়োজন।");
        }

        Principal principal = await _unitOfWork.ExecuteAsync(async token =>
        {
            User? user = await _security.GetUserByUserNameAsync(userName.Trim(), token).ConfigureAwait(false);

            if (user is null)
            {
                throw new AuthorizationException("ব্যবহারকারী নাম বা পাসওয়ার্ড সঠিক নয়।");
            }

            if (!user.IsActive)
            {
                throw new AuthorizationException("এই অ্যাকাউন্টটি নিষ্ক্রিয়। প্রশাসকের সাথে যোগাযোগ করুন।");
            }

            if (user.LockedUntilUtc.HasValue && user.LockedUntilUtc.Value > DateTime.UtcNow)
            {
                int minutes = (int)Math.Ceiling((user.LockedUntilUtc.Value - DateTime.UtcNow).TotalMinutes);
                throw new AuthorizationException(
                    $"একাধিকবার ভুল পাসওয়ার্ড দেওয়ায় অ্যাকাউন্টটি সাময়িকভাবে বন্ধ আছে। {minutes} মিনিট পর চেষ্টা করুন।");
            }

            if (!_hasher.Verify(password, user.PasswordHash, user.PasswordSalt))
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= MaxFailedAttempts)
                {
                    user.LockedUntilUtc = DateTime.UtcNow.Add(LockDuration);
                    user.FailedLoginAttempts = 0;
                }

                await _security.UpdateUserAsync(user, token).ConfigureAwait(false);
                throw new AuthorizationException("ব্যবহারকারী নাম বা পাসওয়ার্ড সঠিক নয়।");
            }

            user.FailedLoginAttempts = 0;
            user.LockedUntilUtc = null;
            user.LastLoginAtUtc = DateTime.UtcNow;
            await _security.UpdateUserAsync(user, token).ConfigureAwait(false);

            return await BuildPrincipalAsync(user, deviceId, token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        _currentUser.SignIn(principal);
        await _audit.RecordAsync("auth.login", nameof(User), principal.UserId, principal.UserName, ct: ct)
            .ConfigureAwait(false);

        return principal;
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword,
        CancellationToken ct = default)
    {
        IReadOnlyList<string> errors = PasswordHasher.ValidateStrength(newPassword ?? string.Empty);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        await _unitOfWork.ExecuteAsync(async token =>
        {
            User? user = await _security.GetUserAsync(userId, token).ConfigureAwait(false)
                ?? throw new NotFoundException("ব্যবহারকারী পাওয়া যায়নি।");

            if (!_hasher.Verify(currentPassword, user.PasswordHash, user.PasswordSalt))
            {
                throw new AuthorizationException("বর্তমান পাসওয়ার্ড সঠিক নয়।");
            }

            (string hash, string salt) = _hasher.Hash(newPassword);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            user.MustChangePassword = false;
            await _security.UpdateUserAsync(user, token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        await _audit.RecordAsync("auth.password.change", nameof(User), userId, null, ct: ct).ConfigureAwait(false);
    }

    public Task<Principal> RefreshAsync(Guid userId, CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(async token =>
        {
            User? user = await _security.GetUserAsync(userId, token).ConfigureAwait(false)
                ?? throw new NotFoundException("ব্যবহারকারী পাওয়া যায়নি।");

            return await BuildPrincipalAsync(user, _currentUser.Current.DeviceId, token).ConfigureAwait(false);
        }, ct);

    public void Logout()
    {
        _currentUser.SignOut();
    }

    private async Task<Principal> BuildPrincipalAsync(User user, string? deviceId, CancellationToken ct)
    {
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string roleName = string.Empty;

        if (user.RoleId.HasValue)
        {
            Role? role = await _security.GetRoleAsync(user.RoleId.Value, ct).ConfigureAwait(false);
            if (role is not null)
            {
                roleName = role.Name;
                foreach (string code in await _security.GetPermissionsForRoleAsync(role.Id, ct)
                             .ConfigureAwait(false))
                {
                    permissions.Add(code);
                }
            }
        }

        return new Principal
        {
            UserId = user.Id,
            UserName = user.UserName,
            FullName = user.FullName,
            RoleId = user.RoleId,
            RoleName = roleName,
            Permissions = permissions,
            DeviceId = deviceId ?? string.Empty
        };
    }
}
