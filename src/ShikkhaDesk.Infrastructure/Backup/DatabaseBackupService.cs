using System.Globalization;
using System.Security.Cryptography;
using ConnectionFactory = ShikkhaDesk.Infrastructure.Persistence.SqliteConnectionFactory;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Infrastructure.Backup;

/// <summary>
/// Backup and restore built on SQLite's online backup API. The database is copied through
/// sqlite3_backup_step rather than by copying the file, so a backup taken while the application is
/// running is a consistent snapshot instead of a torn file.
/// </summary>
public sealed class DatabaseBackupService : IBackupService
{
    private const string BackupFilePattern = "ShikkhaDesk-Backup-{0}.db";

    private readonly ConnectionFactory _factory;
    private readonly ISystemRepository _systemRepository;
    private readonly ILogger<DatabaseBackupService> _logger;

    public DatabaseBackupService(ConnectionFactory factory, ISystemRepository systemRepository,
        ILogger<DatabaseBackupService>? logger = null)
    {
        _factory = factory;
        _systemRepository = systemRepository;
        _logger = logger ?? NullLogger<DatabaseBackupService>.Instance;
    }

    public async Task<BackupRecord> CreateBackupAsync(string? destinationDirectory = null, bool isAutomatic = false,
        CancellationToken ct = default)
    {
        string directory = await ResolveDestinationAsync(destinationDirectory, ct).ConfigureAwait(false);
        Directory.CreateDirectory(directory);

        string fileName = string.Format(CultureInfo.InvariantCulture, BackupFilePattern,
            DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));

        // The stamp is only second-granular, so two backups taken inside one second used to resolve
        // to the same path: two history records, one file, and the first record's integrity hash
        // describing a file that had already been overwritten. Never reuse an existing file.
        string path = Path.Combine(directory, fileName);
        for (int suffix = 2; File.Exists(path); suffix++)
        {
            path = Path.Combine(directory,
                $"{Path.GetFileNameWithoutExtension(fileName)}-{suffix}.db");
        }

        var record = new BackupRecord
        {
            Id = Guid.NewGuid(),
            StartedAtUtc = DateTime.UtcNow,
            FilePath = path,
            Status = BackupStatus.Running,
            IsAutomatic = isAutomatic
        };

        await _systemRepository.InsertBackupRecordAsync(record, ct).ConfigureAwait(false);

        try
        {
            await _factory.WriteLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                using (var source = new SqliteConnection(BuildReadOnlyConnectionString(_factory.Options.DatabasePath)))
                using (var destination = new SqliteConnection(BuildCreateConnectionString(path)))
                {
                    await source.OpenAsync(ct).ConfigureAwait(false);
                    await destination.OpenAsync(ct).ConfigureAwait(false);
                    source.BackupDatabase(destination, "main", "main");
                }
            }
            finally
            {
                _factory.WriteLock.Release();
            }

            var fileInfo = new FileInfo(path);
            record.FileSizeBytes = fileInfo.Length;
            record.FinishedAtUtc = DateTime.UtcNow;
            record.IntegrityHash = await ComputeSha256Async(path, ct).ConfigureAwait(false);
            record.IntegrityVerified = true;
            record.Status = BackupStatus.Succeeded;

            _logger.LogInformation("Backup created at {Path} ({Bytes} bytes)", path, fileInfo.Length);
        }
        catch (Exception ex)
        {
            record.FinishedAtUtc = DateTime.UtcNow;
            record.Status = BackupStatus.Failed;
            record.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Backup failed");
        }

        await _systemRepository.UpdateBackupRecordAsync(record, ct).ConfigureAwait(false);
        return record;
    }

    public async Task<bool> VerifyBackupAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            return await IsConsistentAsync(filePath, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup verification failed for {Path}", filePath);
            return false;
        }
    }

    public async Task<RestoreResult> RestoreAsync(string backupFilePath, bool requireConfirmation = true,
        CancellationToken ct = default)
    {
        if (!File.Exists(backupFilePath))
        {
            return new RestoreResult(false, "নির্বাচিত ব্যাকআপ ফাইলটি পাওয়া যায়নি।", null);
        }

        // A file that is not a SQLite database at all makes the consistency check throw, and a user
        // who picks the wrong file must get a Bengali message, never an unhandled exception.
        bool consistent;
        try
        {
            consistent = await IsConsistentAsync(backupFilePath, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Restore refused: {Path} is not a readable database", backupFilePath);
            consistent = false;
        }

        if (!consistent)
        {
            return new RestoreResult(false, "ব্যাকআপ ফাইলটি ক্ষতিগ্রস্ত বা সামঞ্জস্যপূর্ণ নয়। পুনরুদ্ধার বাতিল করা হয়েছে।", null);
        }

        string? preRestore = null;
        try
        {
            // Never replace the live database without first taking a snapshot of it.
            BackupRecord snapshot = await CreateBackupAsync(null, false, ct).ConfigureAwait(false);
            if (snapshot.Status == BackupStatus.Succeeded)
            {
                preRestore = snapshot.FilePath;
            }
            else
            {
                return new RestoreResult(false,
                    "পুনরুদ্ধারের আগে বর্তমান ডেটার ব্যাকআপ নেওয়া সম্ভব হয়নি, তাই পুনরুদ্ধার বাতিল করা হয়েছে।", null);
            }

            string livePath = _factory.Options.DatabasePath;
            string temporary = livePath + ".restore-tmp";

            File.Copy(backupFilePath, temporary, overwrite: true);

            _factory.Recycle();
            await Task.Delay(150, ct).ConfigureAwait(false);

            File.Copy(temporary, livePath, overwrite: true);
            DeleteSideFiles(livePath);
            File.Delete(temporary);

            _factory.Recycle();
            return new RestoreResult(true, "পুনরুদ্ধার সফল হয়েছে। অ্যাপ্লিকেশন পুনরায় চালু করুন।", preRestore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore failed");
            return new RestoreResult(false, "পুনরুদ্ধার ব্যর্থ হয়েছে: " + ex.Message, preRestore);
        }
    }

    public Task<IReadOnlyList<BackupRecord>> GetHistoryAsync(int limit = 50, CancellationToken ct = default) =>
        _systemRepository.GetBackupHistoryAsync(limit, ct);

    public async Task<int> ApplyRetentionAsync(int keepCount, CancellationToken ct = default)
    {
        if (keepCount < 1)
        {
            // Refuse to be the mechanism that deletes the customer's last backup.
            return 0;
        }

        IReadOnlyList<BackupRecord> history = await _systemRepository.GetBackupHistoryAsync(500, ct)
            .ConfigureAwait(false);

        var successful = history
            .Where(b => b.Status == BackupStatus.Succeeded)
            .OrderByDescending(b => b.StartedAtUtc)
            .ToList();

        if (successful.Count <= keepCount)
        {
            return 0;
        }

        int removed = 0;
        foreach (BackupRecord stale in successful.Skip(keepCount))
        {
            try
            {
                if (File.Exists(stale.FilePath))
                {
                    File.Delete(stale.FilePath);
                    removed++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete stale backup {Path}", stale.FilePath);
            }
        }

        return removed;
    }

    private async Task<string> ResolveDestinationAsync(string? configured, CancellationToken ct)
    {
        string? directory = configured;
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = await _systemRepository.GetSettingAsync("backup.location", ct).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ShikkhaDesk", "Backups");
        }

        return directory;
    }

    private static async Task<bool> IsConsistentAsync(string path, CancellationToken ct)
    {
        using var connection = new SqliteConnection(BuildReadOnlyConnectionString(path));
        await connection.OpenAsync(ct).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        object? result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return string.Equals(result as string, "ok", StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteSideFiles(string databasePath)
    {
        foreach (string suffix in new[] { "-wal", "-shm" })
        {
            string side = databasePath + suffix;
            if (File.Exists(side))
            {
                try
                {
                    File.Delete(side);
                }
                catch (IOException)
                {
                    // A stray WAL file is not fatal: SQLite rebuilds state from the main database.
                }
            }
        }
    }

    private static string BuildReadOnlyConnectionString(string path) =>
        new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ConnectionString;

    private static string BuildCreateConnectionString(string path) =>
        new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate }.ConnectionString;

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
