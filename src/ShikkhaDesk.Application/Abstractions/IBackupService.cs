using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Application.Abstractions;

public sealed record RestoreResult(bool Succeeded, string Message, string? PreRestoreBackupPath);

public interface IBackupService
{
    Task<BackupRecord> CreateBackupAsync(string? destinationDirectory = null, bool isAutomatic = false,
        CancellationToken ct = default);

    Task<bool> VerifyBackupAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Restores a backup. Always takes a pre-restore backup of the live database first, and validates
    /// the incoming file before replacing anything, so a corrupt file can never destroy live data.
    /// </summary>
    Task<RestoreResult> RestoreAsync(string backupFilePath, bool requireConfirmation = true,
        CancellationToken ct = default);

    Task<IReadOnlyList<BackupRecord>> GetHistoryAsync(int limit = 50, CancellationToken ct = default);

    Task<int> ApplyRetentionAsync(int keepCount, CancellationToken ct = default);
}
