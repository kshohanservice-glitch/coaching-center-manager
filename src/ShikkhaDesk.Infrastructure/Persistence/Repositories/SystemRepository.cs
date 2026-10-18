using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Infrastructure.Persistence.Repositories;

public sealed class SystemRepository : RepositoryBase, ISystemRepository
{
    public SystemRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }

    private const string InstitutionColumns = @"
        id, name, name_bn, address, phone, email, logo_path, registration_number, currency_code,
        currency_symbol, digit_preference, created_at_utc, updated_at_utc";

    public Task<Institution?> GetInstitutionAsync(CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<Institution>($"SELECT {InstitutionColumns} FROM institutions LIMIT 1;", null, ct);

    public async Task UpsertInstitutionAsync(Institution institution, CancellationToken ct = default)
    {
        int updated = await ExecuteAsync(@"
            UPDATE institutions SET name = @Name, name_bn = @NameBn, address = @Address, phone = @Phone,
                   email = @Email, logo_path = @LogoPath, registration_number = @RegistrationNumber,
                   currency_code = @CurrencyCode, currency_symbol = @CurrencySymbol,
                   digit_preference = @DigitPreference, updated_at_utc = @UpdatedAtUtc
             WHERE id = @Id;", institution, ct).ConfigureAwait(false);

        if (updated == 0)
        {
            await ExecuteAsync(@"
                INSERT INTO institutions (id, name, name_bn, address, phone, email, logo_path,
                                          registration_number, currency_code, currency_symbol, digit_preference,
                                          created_at_utc, updated_at_utc)
                VALUES (@Id, @Name, @NameBn, @Address, @Phone, @Email, @LogoPath, @RegistrationNumber,
                        @CurrencyCode, @CurrencySymbol, @DigitPreference, @CreatedAtUtc, @UpdatedAtUtc);",
                institution, ct).ConfigureAwait(false);
        }
    }

    public Task<string?> GetSettingAsync(string key, CancellationToken ct = default) =>
        ExecuteScalarAsync<string>("SELECT value FROM settings WHERE key = @Key;", new { Key = key }, ct);

    public async Task SetSettingAsync(string key, string value, CancellationToken ct = default)
    {
        int updated = await ExecuteAsync(
            "UPDATE settings SET value = @Value, updated_at_utc = @UpdatedAt WHERE key = @Key;",
            new { Key = key, Value = value, UpdatedAt = DateTime.UtcNow }, ct).ConfigureAwait(false);

        if (updated == 0)
        {
            await ExecuteAsync(
                "INSERT INTO settings (key, value, updated_at_utc) VALUES (@Key, @Value, @UpdatedAt);",
                new { Key = key, Value = value, UpdatedAt = DateTime.UtcNow }, ct).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<Setting>> GetAllSettingsAsync(CancellationToken ct = default) =>
        (await QueryAsync<Setting>("SELECT key, value, updated_at_utc FROM settings ORDER BY key;", null, ct)
            .ConfigureAwait(false)).ToList();

    private const string BackupColumns = @"
        id, started_at_utc, finished_at_utc, file_path, file_size_bytes, status, error_message, is_automatic,
        integrity_hash, integrity_verified";

    public Task InsertBackupRecordAsync(BackupRecord record, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO backup_records (id, started_at_utc, finished_at_utc, file_path, file_size_bytes, status,
                                        error_message, is_automatic, integrity_hash, integrity_verified)
            VALUES (@Id, @StartedAtUtc, @FinishedAtUtc, @FilePath, @FileSizeBytes, @Status, @ErrorMessage,
                    @IsAutomatic, @IntegrityHash, @IntegrityVerified);", record, ct);

    public Task UpdateBackupRecordAsync(BackupRecord record, CancellationToken ct = default) =>
        ExecuteAsync(@"
            UPDATE backup_records SET finished_at_utc = @FinishedAtUtc, file_size_bytes = @FileSizeBytes,
                   status = @Status, error_message = @ErrorMessage, integrity_hash = @IntegrityHash,
                   integrity_verified = @IntegrityVerified
             WHERE id = @Id;", record, ct);

    public async Task<IReadOnlyList<BackupRecord>> GetBackupHistoryAsync(int limit = 50,
        CancellationToken ct = default) =>
        (await QueryAsync<BackupRecord>(
            $"SELECT {BackupColumns} FROM backup_records ORDER BY started_at_utc DESC LIMIT @Limit;",
            new { Limit = Math.Clamp(limit, 1, 500) }, ct).ConfigureAwait(false)).ToList();

    public Task<BackupRecord?> GetBackupRecordAsync(Guid id, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<BackupRecord>(
            $"SELECT {BackupColumns} FROM backup_records WHERE id = @Id;", new { Id = id }, ct);

    public Task<BackupRecord?> GetLastSuccessfulBackupAsync(CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<BackupRecord>(
            $"SELECT {BackupColumns} FROM backup_records WHERE status = 0 ORDER BY started_at_utc DESC LIMIT 1;",
            null, ct);

    private const string LicenseColumns = @"
        id, license_id, institution_id, product_code, license_type, device_limit, version_entitlement,
        issued_to, issued_at_utc, expires_at_utc, signature, public_key_id, activated_at_utc";

    public Task<LicenseRecord?> GetLicenseAsync(CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<LicenseRecord>($"SELECT {LicenseColumns} FROM licenses LIMIT 1;", null, ct);

    public async Task UpsertLicenseAsync(LicenseRecord license, CancellationToken ct = default)
    {
        int updated = await ExecuteAsync(@"
            UPDATE licenses SET license_id = @LicenseId, institution_id = @InstitutionId,
                   product_code = @ProductCode, license_type = @LicenseType, device_limit = @DeviceLimit,
                   version_entitlement = @VersionEntitlement, issued_to = @IssuedTo, issued_at_utc = @IssuedAtUtc,
                   expires_at_utc = @ExpiresAtUtc, signature = @Signature, public_key_id = @PublicKeyId,
                   activated_at_utc = @ActivatedAtUtc;", license, ct).ConfigureAwait(false);

        if (updated == 0)
        {
            await ExecuteAsync(@"
                INSERT INTO licenses (id, license_id, institution_id, product_code, license_type, device_limit,
                                      version_entitlement, issued_to, issued_at_utc, expires_at_utc, signature,
                                      public_key_id, activated_at_utc)
                VALUES (@Id, @LicenseId, @InstitutionId, @ProductCode, @LicenseType, @DeviceLimit,
                        @VersionEntitlement, @IssuedTo, @IssuedAtUtc, @ExpiresAtUtc, @Signature, @PublicKeyId,
                        @ActivatedAtUtc);", license, ct).ConfigureAwait(false);
        }
    }

    private const string DeviceColumns = @"
        id, device_id, machine_name, windows_user_name, notes, registered_at_utc, last_seen_at_utc, status,
        replaced_by_device_id";

    public async Task<IReadOnlyList<LicensedDevice>> GetDevicesAsync(CancellationToken ct = default) =>
        (await QueryAsync<LicensedDevice>(
            $"SELECT {DeviceColumns} FROM licensed_devices ORDER BY registered_at_utc;", null, ct)
            .ConfigureAwait(false)).ToList();

    public Task<LicensedDevice?> GetDeviceAsync(string deviceId, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<LicensedDevice>(
            $"SELECT {DeviceColumns} FROM licensed_devices WHERE device_id = @DeviceId;",
            new { DeviceId = deviceId }, ct);

    public async Task UpsertDeviceAsync(LicensedDevice device, CancellationToken ct = default)
    {
        int updated = await ExecuteAsync(@"
            UPDATE licensed_devices SET machine_name = @MachineName, windows_user_name = @WindowsUserName,
                   notes = @Notes, last_seen_at_utc = @LastSeenAtUtc, status = @Status,
                   replaced_by_device_id = @ReplacedByDeviceId
             WHERE device_id = @DeviceId;", device, ct).ConfigureAwait(false);

        if (updated == 0)
        {
            await ExecuteAsync(@"
                INSERT INTO licensed_devices (id, device_id, machine_name, windows_user_name, notes,
                                              registered_at_utc, last_seen_at_utc, status, replaced_by_device_id)
                VALUES (@Id, @DeviceId, @MachineName, @WindowsUserName, @Notes, @RegisteredAtUtc,
                        @LastSeenAtUtc, @Status, @ReplacedByDeviceId);", device, ct).ConfigureAwait(false);
        }
    }

    public Task<int> CountActiveDevicesAsync(CancellationToken ct = default) =>
        ExecuteScalarAsync<int>("SELECT COUNT(*) FROM licensed_devices WHERE status = 0;", null, ct)!;
}
