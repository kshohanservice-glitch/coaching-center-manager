namespace ShikkhaDesk.Core.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public Guid? RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class Role
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class Permission
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Group { get; set; }
}

public sealed class RolePermission
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
}

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? Summary { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? SourceDevice { get; set; }
}

public sealed class BackupRecord
{
    public Guid Id { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public BackupStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsAutomatic { get; set; }
    public string? IntegrityHash { get; set; }
    public bool IntegrityVerified { get; set; }
}

public sealed class LicensedDevice
{
    public Guid Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string? WindowsUserName { get; set; }
    public string? Notes { get; set; }
    public DateTime RegisteredAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
    public DeviceStatus Status { get; set; } = DeviceStatus.Registered;
    public string? ReplacedByDeviceId { get; set; }
}

public sealed class LicenseRecord
{
    public Guid Id { get; set; }
    public string LicenseId { get; set; } = string.Empty;
    public string InstitutionId { get; set; } = string.Empty;
    public string ProductCode { get; set; } = "SDESK";
    public string LicenseType { get; set; } = "PerpetualSingleInstitution";
    public int DeviceLimit { get; set; } = 5;
    public string? VersionEntitlement { get; set; }
    public string? IssuedTo { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string? Signature { get; set; }
    public string? PublicKeyId { get; set; }
    public DateTime ActivatedAtUtc { get; set; }
}

public sealed class Setting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class MigrationHistory
{
    public long Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime AppliedAtUtc { get; set; }
    public string Checksum { get; set; } = string.Empty;
}
