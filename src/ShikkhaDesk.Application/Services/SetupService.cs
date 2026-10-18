using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Core.Services;

namespace ShikkhaDesk.Application.Services;

public sealed record SetupRequest(
    string InstitutionName,
    string? InstitutionAddress,
    string? Phone,
    string? Email,
    string CurrencyCode,
    string CurrencySymbol,
    DigitPreference DigitPreference,
    string ReceiptPrefix,
    string? BackupLocation,
    string AdminUserName,
    string AdminFullName,
    string AdminPassword,
    HostMode HostMode,
    string SessionName,
    DateTime SessionStart,
    DateTime SessionEnd,
    Money OpeningCashBalance,
    string? LanHostAddress = null,
    int? LanPort = null);

public interface ISetupService
{
    Task<bool> IsInitializedAsync(CancellationToken ct = default);
    Task InitializeAsync(SetupRequest request, CancellationToken ct = default);
    Task EnsurePermissionsSeededAsync(CancellationToken ct = default);
}

/// <summary>
/// First-run setup. Creates the institution, the administrator account, the permission catalogue,
/// the opening cash account and the first academic session. It refuses to run twice so an existing
/// institution can never be silently overwritten.
/// </summary>
public sealed class SetupService : ISetupService
{
    private readonly ISystemRepository _system;
    private readonly ISecurityRepository _security;
    private readonly IAccountingRepository _accounting;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;
    private readonly PasswordHasher _hasher;

    public SetupService(ISystemRepository system, ISecurityRepository security,
        IAccountingRepository accounting, IUnitOfWork unitOfWork, IAuditService audit, PasswordHasher hasher)
    {
        _system = system;
        _security = security;
        _accounting = accounting;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _hasher = hasher;
    }

    public async Task<bool> IsInitializedAsync(CancellationToken ct = default)
    {
        Institution? institution = await _unitOfWork.ExecuteAsync(
            token => _system.GetInstitutionAsync(token), ct).ConfigureAwait(false);

        string? completed = await _unitOfWork.ExecuteAsync(
            token => _system.GetSettingAsync("app.firstRunCompleted", token), ct).ConfigureAwait(false);

        return institution is not null && string.Equals(completed, "1", StringComparison.Ordinal);
    }

    public async Task InitializeAsync(SetupRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await IsInitializedAsync(ct).ConfigureAwait(false))
        {
            throw new DomainException("প্রতিষ্ঠান ইতিমধ্যে সেটআপ করা হয়েছে।");
        }

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.InstitutionName))
        {
            errors.Add("প্রতিষ্ঠানের নাম প্রয়োজন।");
        }

        if (string.IsNullOrWhiteSpace(request.AdminUserName))
        {
            errors.Add("প্রশাসকের ব্যবহারকারী নাম প্রয়োজন।");
        }

        if (string.IsNullOrWhiteSpace(request.AdminFullName))
        {
            errors.Add("প্রশাসকের পুরো নাম প্রয়োজন।");
        }

        errors.AddRange(PasswordHasher.ValidateStrength(request.AdminPassword ?? string.Empty));

        if (request.SessionEnd <= request.SessionStart)
        {
            errors.Add("শিক্ষাবর্ষের শেষ তারিখ শুরুর তারিখের পরে হতে হবে।");
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var institution = new Institution
            {
                Id = Guid.NewGuid(),
                Name = request.InstitutionName.Trim(),
                Address = request.InstitutionAddress,
                Phone = request.Phone,
                Email = request.Email,
                CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "BDT" : request.CurrencyCode,
                CurrencySymbol = string.IsNullOrWhiteSpace(request.CurrencySymbol) ? "৳" : request.CurrencySymbol,
                DigitPreference = request.DigitPreference,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _system.UpsertInstitutionAsync(institution, token).ConfigureAwait(false);

            await EnsurePermissionsSeededAsync(token).ConfigureAwait(false);

            Role? adminRole = await _security.GetRoleByNameAsync("Admin", token).ConfigureAwait(false)
                ?? throw new DomainException("প্রশাসক ভূমিকা তৈরি করা যায়নি।");

            (string hash, string salt) = _hasher.Hash(request.AdminPassword);

            var admin = new User
            {
                Id = Guid.NewGuid(),
                UserName = request.AdminUserName.Trim(),
                FullName = request.AdminFullName.Trim(),
                PasswordHash = hash,
                PasswordSalt = salt,
                RoleId = adminRole.Id,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _security.InsertUserAsync(admin, token).ConfigureAwait(false);

            var session = new AcademicSession
            {
                Id = Guid.NewGuid(),
                Name = request.SessionName.Trim(),
                StartDate = request.SessionStart.Date,
                EndDate = request.SessionEnd.Date,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _system.SetSettingAsync("academic.sessionId", session.Id.ToString(), token)
                .ConfigureAwait(false);

            Account? cashLedger = await _accounting
                .GetAccountByCodeAsync(SystemAccountCodes.CashInHand, token).ConfigureAwait(false)
                ?? throw new DomainException("নগদ হিসাব (কোড ১১০০) পাওয়া যায়নি।");

            var cashAccount = new FinancialAccount
            {
                Id = Guid.NewGuid(),
                Name = "প্রধান নগদ হিসাব",
                Kind = FinancialAccountKind.Cash,
                AccountId = cashLedger.Id,
                OpeningBalance = request.OpeningCashBalance,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _accounting.InsertFinancialAccountAsync(cashAccount, token).ConfigureAwait(false);

            await _system.SetSettingAsync("receipt.prefix",
                string.IsNullOrWhiteSpace(request.ReceiptPrefix) ? "RCP" : request.ReceiptPrefix.Trim(), token)
                .ConfigureAwait(false);
            await _system.SetSettingAsync("backup.location", request.BackupLocation ?? string.Empty, token)
                .ConfigureAwait(false);
            await _system.SetSettingAsync("app.hostMode", ((int)request.HostMode).ToString(), token)
                .ConfigureAwait(false);
            await _system.SetSettingAsync("lan.hostAddress", request.LanHostAddress ?? string.Empty, token)
                .ConfigureAwait(false);
            await _system.SetSettingAsync("lan.port", (request.LanPort ?? 5188).ToString(), token)
                .ConfigureAwait(false);
            await _system.SetSettingAsync("app.firstRunCompleted", "1", token).ConfigureAwait(false);

            await _audit.RecordAsync("setup.complete", nameof(Institution), institution.Id,
                $"{institution.Name} সেটআপ সম্পন্ন", ct: token).ConfigureAwait(false);

            _ = session;
            return institution.Id;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Keeps the permission catalogue in step with the code. New permissions added by an upgrade are
    /// inserted; existing ones and their role assignments are never removed.
    /// </summary>
    public async Task EnsurePermissionsSeededAsync(CancellationToken ct = default)
    {
        await _unitOfWork.ExecuteAsync(async token =>
        {
            IReadOnlyList<Permission> existing = await _security.GetPermissionsAsync(token).ConfigureAwait(false);
            var byCode = existing.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);

            foreach ((string code, string group, string nameBn) in PermissionCodes.All)
            {
                if (byCode.ContainsKey(code))
                {
                    continue;
                }

                await _security.InsertPermissionAsync(new Permission
                {
                    Id = Guid.NewGuid(),
                    Code = code,
                    Description = nameBn,
                    Group = group
                }, token).ConfigureAwait(false);
            }

            IReadOnlyList<Permission> all = await _security.GetPermissionsAsync(token).ConfigureAwait(false);
            var lookup = all.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);

            await EnsureRoleAsync("Admin", "পূর্ণ নিয়ন্ত্রণ", true, PermissionCodes.AdministratorPermissions,
                lookup, token).ConfigureAwait(false);
            await EnsureRoleAsync("Accountant", "হিসাব ও ফি ব্যবস্থাপনা", true,
                PermissionCodes.AccountantPermissions, lookup, token).ConfigureAwait(false);
            await EnsureRoleAsync("Teacher", "শিক্ষক", true, PermissionCodes.TeacherPermissions, lookup, token)
                .ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    private async Task EnsureRoleAsync(string name, string description, bool isSystem, string[] permissions,
        IReadOnlyDictionary<string, Permission> lookup, CancellationToken ct)
    {
        Role? role = await _security.GetRoleByNameAsync(name, ct).ConfigureAwait(false);
        if (role is null)
        {
            role = new Role
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = description,
                IsSystem = isSystem,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _security.InsertRoleAsync(role, ct).ConfigureAwait(false);
        }

        IReadOnlyList<string> granted = await _security.GetPermissionsForRoleAsync(role.Id, ct)
            .ConfigureAwait(false);

        if (granted.Count == permissions.Length)
        {
            return;
        }

        var ids = new List<Guid>(permissions.Length);
        foreach (string code in permissions)
        {
            if (lookup.TryGetValue(code, out Permission? permission))
            {
                ids.Add(permission.Id);
            }
        }

        await _security.ReplaceRolePermissionsAsync(role.Id, ids, ct).ConfigureAwait(false);
    }
}
