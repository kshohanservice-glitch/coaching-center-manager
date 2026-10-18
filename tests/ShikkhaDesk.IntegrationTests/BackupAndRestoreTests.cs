using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Dto;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Core.Services;
using Xunit;

namespace ShikkhaDesk.IntegrationTests;

/// <summary>
/// Backup and restore against a real database. These are the tests that stand between a centre and
/// the loss of its accounts: a backup that is not a consistent copy, or a restore that silently
/// produces a half-migrated database, is worse than no backup at all.
/// </summary>
public sealed class BackupAndRestoreTests
{
    private static readonly DateRange All = new(new DateTime(2000, 1, 1), new DateTime(2100, 1, 1));

    [Fact]
    public void A_backup_is_a_verified_consistent_copy_of_the_live_database()
    {
        using TestHost host = TestHost.Create().Setup();
        (Guid batchId, Guid studentId) = host.CreateBatchWithStudent();
        host.CreateFeePlan(Money.FromDecimal(2000), batchId);
        host.Get<IFeeService>().AssessMonthlyFeesAsync(new AssessMonthlyFeesRequest(batchId, "2026-03"))
            .GetAwaiter().GetResult();
        host.Get<IPaymentService>().CollectAsync(new CollectPaymentRequest(
            studentId, Money.FromDecimal(1200), PaymentMethod.Cash)).GetAwaiter().GetResult();

        IBackupService backups = host.Get<IBackupService>();
        BackupRecord backup = CreateVerifiedBackup(host);

        Assert.True(File.Exists(backup.FilePath), $"ব্যাকআপ ফাইল তৈরি হয়নি: {backup.FilePath}");
        Assert.True(backup.FileSizeBytes > 0);
        Assert.True(backup.IntegrityVerified);
        Assert.False(string.IsNullOrWhiteSpace(backup.IntegrityHash));
        Assert.True(backups.VerifyBackupAsync(backup.FilePath).GetAwaiter().GetResult());

        // The copy must hold the data, not merely exist.
        Assert.Equal(Money.FromDecimal(1300), ReadOutstanding(host, studentId));
    }

    [Fact]
    public void Restoring_a_backup_returns_the_ledger_to_exactly_what_it_was()
    {
        using TestHost host = TestHost.Create().Setup();
        (Guid batchId, Guid studentId) = host.CreateBatchWithStudent();
        host.CreateFeePlan(Money.FromDecimal(2000), batchId);
        host.Get<IFeeService>().AssessMonthlyFeesAsync(new AssessMonthlyFeesRequest(batchId, "2026-03"))
            .GetAwaiter().GetResult();
        host.Get<IPaymentService>().CollectAsync(new CollectPaymentRequest(
            studentId, Money.FromDecimal(1200), PaymentMethod.Cash)).GetAwaiter().GetResult();

        LedgerSnapshot before = Snapshot(host, studentId);
        Assert.Equal(before.Debits, before.Credits);

        BackupRecord backup = CreateVerifiedBackup(host);

        // More business happens after the backup, so the ledger is genuinely different.
        host.Get<IPaymentService>().CollectAsync(new CollectPaymentRequest(
            studentId, Money.FromDecimal(1000), PaymentMethod.Cash)).GetAwaiter().GetResult();

        Account electricity = host.Get<IAccountingRepository>()
            .GetAccountByCodeAsync(SystemAccountCodes.Electricity).GetAwaiter().GetResult()!;
        host.Get<ILedgerOperationsService>().RecordExpenseAsync(new ExpenseRequest(
            DateTime.Today, electricity.Id, Money.FromDecimal(750), "বিদ্যুৎ বিল",
            FinancialAccountId: host.CurrentCashAccountId())).GetAwaiter().GetResult();

        LedgerSnapshot after = Snapshot(host, studentId);

        // The two sides of a trial balance always sum to the same figure, so that total is not what
        // tells you the ledger moved: money flowing in and out of cash is.
        Assert.NotEqual(before.Outstanding, after.Outstanding);
        Assert.NotEqual(before.Cash, after.Cash);

        RestoreResult result = host.Get<IBackupService>().RestoreAsync(backup.FilePath).GetAwaiter().GetResult();

        Assert.True(result.Succeeded, result.Message);
        Assert.False(string.IsNullOrEmpty(result.PreRestoreBackupPath),
            "পুনরুদ্ধারের আগে বর্তমান ডেটার ব্যাকআপ নেওয়া হয়নি।");
        Assert.True(File.Exists(result.PreRestoreBackupPath!));

        LedgerSnapshot restored = Snapshot(host, studentId);

        // To the poisa: the same as it was when the backup was taken.
        Assert.Equal(before.Debits, restored.Debits);
        Assert.Equal(before.Credits, restored.Credits);
        Assert.Equal(before.Outstanding, restored.Outstanding);
        Assert.Equal(before.Cash, restored.Cash);
        Assert.Equal(restored.Debits, restored.Credits);
    }

    [Fact]
    public void A_corrupt_backup_is_refused_and_the_live_data_is_untouched()
    {
        using TestHost host = TestHost.Create().Setup();
        (Guid batchId, Guid studentId) = host.CreateBatchWithStudent();
        host.CreateFeePlan(Money.FromDecimal(2000), batchId);
        host.Get<IFeeService>().AssessMonthlyFeesAsync(new AssessMonthlyFeesRequest(batchId, "2026-03"))
            .GetAwaiter().GetResult();

        Money outstandingBefore = ReadOutstanding(host, studentId);

        string corrupt = Path.Combine(host.Root, "corrupt-backup.db");
        File.WriteAllText(corrupt, "এটি কোনো ডেটাবেস নয় — এটি একটি ক্ষতিগ্রস্ত ফাইলের পরীক্ষা।");

        IBackupService backups = host.Get<IBackupService>();
        Assert.False(backups.VerifyBackupAsync(corrupt).GetAwaiter().GetResult());

        RestoreResult result = backups.RestoreAsync(corrupt).GetAwaiter().GetResult();

        Assert.False(result.Succeeded);
        Assert.Equal(outstandingBefore, ReadOutstanding(host, studentId));
    }

    [Fact]
    public void A_missing_backup_file_is_refused_outright()
    {
        using TestHost host = TestHost.Create().Setup();

        RestoreResult result = host.Get<IBackupService>()
            .RestoreAsync(Path.Combine(host.Root, "does-not-exist.db")).GetAwaiter().GetResult();

        Assert.False(result.Succeeded);
        Assert.Null(result.PreRestoreBackupPath);
    }

    [Fact]
    public void Two_backups_taken_in_the_same_second_are_two_files()
    {
        using TestHost host = TestHost.Create().Setup();

        IBackupService backups = host.Get<IBackupService>();
        BackupRecord first = CreateVerifiedBackup(host);
        BackupRecord second = CreateVerifiedBackup(host);

        // The file name is stamped to the second, so two backups taken inside one second used to
        // land on the same path: two history records, one file, and the first record's integrity
        // hash describing a file that had already been replaced.
        Assert.NotEqual(first.FilePath, second.FilePath);
        Assert.True(File.Exists(first.FilePath));
        Assert.True(File.Exists(second.FilePath));
        Assert.True(backups.VerifyBackupAsync(first.FilePath).GetAwaiter().GetResult());
        Assert.True(backups.VerifyBackupAsync(second.FilePath).GetAwaiter().GetResult());
    }

    [Fact]
    public void Retention_keeps_the_newest_backups_and_never_deletes_the_last_one()
    {
        using TestHost host = TestHost.Create().Setup();

        IBackupService backups = host.Get<IBackupService>();
        for (int i = 0; i < 3; i++)
        {
            CreateVerifiedBackup(host);
            System.Threading.Thread.Sleep(1100);
        }

        IReadOnlyList<BackupRecord> history = backups.GetHistoryAsync(50).GetAwaiter().GetResult();
        Assert.Equal(3, history.Count(b => b.Status == BackupStatus.Succeeded));

        // Keeping zero would mean deleting everything, so the service refuses.
        Assert.Equal(0, backups.ApplyRetentionAsync(0).GetAwaiter().GetResult());

        Assert.Equal(1, backups.ApplyRetentionAsync(2).GetAwaiter().GetResult());

        IReadOnlyList<BackupRecord> kept = backups.GetHistoryAsync(50).GetAwaiter().GetResult()
            .Where(b => b.Status == BackupStatus.Succeeded)
            .OrderByDescending(b => b.StartedAtUtc)
            .ToList();

        Assert.Equal(2, kept.Count(b => File.Exists(b.FilePath)));
    }

    /// <summary>Creates a backup and fails with the service's own reason if it did not succeed.</summary>
    private static BackupRecord CreateVerifiedBackup(TestHost host)
    {
        BackupRecord record = host.Get<IBackupService>().CreateBackupAsync().GetAwaiter().GetResult();
        Assert.True(record.Status == BackupStatus.Succeeded, $"ব্যাকআপ ব্যর্থ: {record.ErrorMessage}");
        return record;
    }

    private static Money ReadOutstanding(TestHost host, Guid studentId) =>
        host.Get<IStudentService>().GetBalanceAsync(studentId).GetAwaiter().GetResult().Outstanding;

    /// <summary>Enough of the ledger to prove a restore put every figure back where it was.</summary>
    private sealed record LedgerSnapshot(Money Debits, Money Credits, Money Outstanding, Money Cash);

    private static LedgerSnapshot Snapshot(TestHost host, Guid studentId)
    {
        IReadOnlyList<TrialBalanceRow> trial = host.Get<IAccountingService>()
            .GetTrialBalanceAsync(All).GetAwaiter().GetResult();

        Money debits = trial.Aggregate(Money.Zero, (sum, row) => sum + row.Debit);
        Money credits = trial.Aggregate(Money.Zero, (sum, row) => sum + row.Credit);
        Money cash = host.Get<IAccountingRepository>()
            .GetFinancialAccountBalanceAsync(host.CurrentCashAccountId(), null).GetAwaiter().GetResult();

        return new LedgerSnapshot(debits, credits, ReadOutstanding(host, studentId), cash);
    }
}
