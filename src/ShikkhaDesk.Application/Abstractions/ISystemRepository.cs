using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Application.Abstractions;

public interface ISystemRepository
{
    Task<Institution?> GetInstitutionAsync(CancellationToken ct = default);
    Task UpsertInstitutionAsync(Institution institution, CancellationToken ct = default);

    Task<string?> GetSettingAsync(string key, CancellationToken ct = default);
    Task SetSettingAsync(string key, string value, CancellationToken ct = default);
    Task<IReadOnlyList<Setting>> GetAllSettingsAsync(CancellationToken ct = default);

    // Backup history
    Task InsertBackupRecordAsync(BackupRecord record, CancellationToken ct = default);
    Task UpdateBackupRecordAsync(BackupRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<BackupRecord>> GetBackupHistoryAsync(int limit = 50, CancellationToken ct = default);
    Task<BackupRecord?> GetBackupRecordAsync(Guid id, CancellationToken ct = default);
    Task<BackupRecord?> GetLastSuccessfulBackupAsync(CancellationToken ct = default);

    // Licensing / devices
    Task<LicenseRecord?> GetLicenseAsync(CancellationToken ct = default);
    Task UpsertLicenseAsync(LicenseRecord license, CancellationToken ct = default);
    Task<IReadOnlyList<LicensedDevice>> GetDevicesAsync(CancellationToken ct = default);
    Task<LicensedDevice?> GetDeviceAsync(string deviceId, CancellationToken ct = default);
    Task UpsertDeviceAsync(LicensedDevice device, CancellationToken ct = default);
    Task<int> CountActiveDevicesAsync(CancellationToken ct = default);
}
