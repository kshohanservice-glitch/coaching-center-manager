using System.Data;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ShikkhaDesk.Infrastructure.Persistence;

public sealed class MigrationRecord
{
    public long Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AppliedAtUtc { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
}

/// <summary>
/// Applies versioned SQL migrations embedded in this assembly. Each migration is checksummed and the
/// checksum is re-verified on every start, so an edited migration is detected instead of silently
/// rewriting history. Migrations never drop or rewrite historical data tables.
/// </summary>
public sealed class DatabaseMigrator
{
    private const string ResourcePrefix = "ShikkhaDesk.Infrastructure.Persistence.Schema.";

    private readonly SqliteConnectionFactory _factory;
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(SqliteConnectionFactory factory, ILogger<DatabaseMigrator>? logger = null)
    {
        _factory = factory;
        _logger = logger ?? NullLogger<DatabaseMigrator>.Instance;
    }

    public IReadOnlyList<(string Version, string Name, string Sql)> DiscoverMigrations()
    {
        Assembly assembly = typeof(DatabaseMigrator).Assembly;
        var migrations = new List<(string Version, string Name, string Sql)>();

        foreach (string name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal) || !name.EndsWith(".sql", StringComparison.Ordinal))
            {
                continue;
            }

            string fileName = name[ResourcePrefix.Length..];
            string[] parts = fileName.Split('_', 2);
            if (parts.Length != 2)
            {
                throw new InvalidOperationException($"মাইগ্রেশন ফাইলের নাম সঠিক নয়: {fileName}");
            }

            using Stream? stream = assembly.GetManifestResourceStream(name);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            migrations.Add((parts[0], parts[1].Replace(".sql", string.Empty), reader.ReadToEnd()));
        }

        return migrations
            .OrderBy(m => m.Version, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> ApplyPendingAsync(CancellationToken ct = default)
    {
        var applied = new List<string>();
        IReadOnlyList<(string Version, string Name, string Sql)> migrations = DiscoverMigrations();

        using IDbCommand createHistory = _factory.Connection.CreateCommand();
        createHistory.CommandText = @"CREATE TABLE IF NOT EXISTS migration_history (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            version TEXT NOT NULL UNIQUE,
            description TEXT NOT NULL,
            applied_at_utc TEXT NOT NULL,
            checksum TEXT NOT NULL);";
        createHistory.ExecuteNonQuery();

        List<MigrationRecord> history = _factory.Connection
            .Query<MigrationRecord>("SELECT id, version, description, applied_at_utc AS AppliedAtUtc, checksum FROM migration_history")
            .ToList();

        foreach ((string version, string name, string sql) in migrations)
        {
            ct.ThrowIfCancellationRequested();
            string checksum = ComputeChecksum(sql);

            MigrationRecord? existing = history.FirstOrDefault(h => h.Version == version);
            if (existing is not null)
            {
                if (!string.Equals(existing.Checksum, checksum, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"মাইগ্রেশন {version} ({name}) পরিবর্তিত হয়েছে কিন্তু ইতিমধ্যে প্রয়োগ করা হয়েছে। " +
                        "বিদ্যমান ডেটা রক্ষা করতে নতুন মাইগ্রেশন যোগ করুন; পূর্ববর্তী মাইগ্রেশন সম্পাদনা করবেন না।");
                }

                continue;
            }

            _logger.LogInformation("Applying migration {Version} ({Name})", version, name);
            using IDbTransaction transaction = _factory.Connection.BeginTransaction();
            try
            {
                using IDbCommand command = _factory.Connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                command.ExecuteNonQuery();

                using IDbCommand record = _factory.Connection.CreateCommand();
                record.Transaction = transaction;
                record.CommandText =
                    "INSERT INTO migration_history (version, description, applied_at_utc, checksum) VALUES (@version, @description, @appliedAt, @checksum);";
                AddParameter(record, "@version", version);
                AddParameter(record, "@description", name);
                AddParameter(record, "@appliedAt", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
                AddParameter(record, "@checksum", checksum);
                record.ExecuteNonQuery();

                transaction.Commit();
            }
            catch
            {
                try
                {
                    transaction.Rollback();
                }
                catch (Exception rollbackError)
                {
                    _logger.LogError(rollbackError, "Rollback failed for migration {Version}", version);
                }

                throw;
            }

            applied.Add($"{version}_{name}");
            _logger.LogInformation("Migration {Version} applied", version);
        }

        return applied;
    }

    public IReadOnlyList<MigrationRecord> GetHistory() =>
        _factory.Connection
            .Query<MigrationRecord>("SELECT id, version, description, applied_at_utc AS AppliedAtUtc, checksum FROM migration_history ORDER BY id")
            .ToList();

    private static string ComputeChecksum(string content)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content.Replace("\r\n", "\n")));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void AddParameter(IDbCommand command, string name, object? value)
    {
        IDbDataParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
