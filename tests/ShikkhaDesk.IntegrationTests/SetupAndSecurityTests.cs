using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Dto;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Core.Services;
using ShikkhaDesk.Infrastructure.Persistence;
using Xunit;

namespace ShikkhaDesk.IntegrationTests;

public sealed class SetupAndSecurityTests
{
    [Fact]
    public void Migrations_apply_and_are_recorded()
    {
        using TestHost host = TestHost.Create();
        IReadOnlyList<MigrationRecord> history = host.Get<DatabaseMigrator>().GetHistory();

        Assert.NotEmpty(history);
        Assert.Contains(history, h => h.Version == "001");
        Assert.Contains(history, h => h.Version == "002");
        Assert.All(history, h => Assert.False(string.IsNullOrWhiteSpace(h.Checksum)));
    }

    [Fact]
    public void Migrations_are_idempotent_across_restarts()
    {
        using TestHost host = TestHost.Create();
        DatabaseMigrator migrator = host.Get<DatabaseMigrator>();

        IReadOnlyList<string> second = migrator.ApplyPendingAsync().GetAwaiter().GetResult();

        Assert.Empty(second);
    }

    [Fact]
    public void Starter_chart_of_accounts_is_present()
    {
        using TestHost host = TestHost.Create();

        var accounts = host.Get<IAccountingService>();
        AccountMap map = accounts.GetAccountMapAsync().GetAwaiter().GetResult();

        Assert.NotEqual(Guid.Empty, map.StudentReceivables);
        Assert.NotEqual(Guid.Empty, map.StudentAdvanceCredit);
        Assert.NotEqual(Guid.Empty, map.TuitionIncome);
        Assert.NotEqual(Guid.Empty, map.AccountsPayable);
    }

    [Fact]
    public void Setup_creates_institution_admin_and_opening_cash()
    {
        using TestHost host = TestHost.Create().Setup();

        Institution? institution = host.Get<ISettingsService>().GetInstitutionAsync().GetAwaiter().GetResult();
        Assert.NotNull(institution);
        Assert.Equal("নূর কোচিং সেন্টার", institution!.Name);

        IReadOnlyList<User> users = host.Get<IUserService>().GetUsersAsync().GetAwaiter().GetResult();
        Assert.Single(users);
        Assert.Equal("admin", users[0].UserName);

        IReadOnlyList<FinancialAccountBalance> balances = host.Get<IAccountingService>()
            .GetFinancialAccountBalancesAsync().GetAwaiter().GetResult();

        Assert.Contains(balances, b => b.IsCash && b.Balance == Money.FromDecimal(10000));
    }

    [Fact]
    public void Setup_refuses_to_run_twice()
    {
        using TestHost host = TestHost.Create().Setup();

        Assert.Throws<DomainException>(() =>
            host.Get<ISetupService>().InitializeAsync(new SetupRequest(
                "দ্বিতীয়", null, null, null, "BDT", "৳", DigitPreference.Bengali, "RCP", null,
                "admin2", "দ্বিতীয় প্রশাসক", "Shikkha@2026", HostMode.SingleComputer,
                "২০২৭", new DateTime(2027, 1, 1), new DateTime(2027, 12, 31), Money.Zero))
                .GetAwaiter().GetResult());
    }

    [Fact]
    public void Setup_rejects_a_weak_administrator_password()
    {
        using TestHost host = TestHost.Create();

        Assert.Throws<ValidationException>(() =>
            host.Get<ISetupService>().InitializeAsync(new SetupRequest(
                "টেস্ট", null, null, null, "BDT", "৳", DigitPreference.Bengali, "RCP", null,
                "admin", "প্রশাসক", "123", HostMode.SingleComputer,
                "২০২৬", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), Money.Zero))
                .GetAwaiter().GetResult());
    }

    [Fact]
    public void Login_works_and_rejects_a_wrong_password()
    {
        using TestHost host = TestHost.Create().Setup();
        IAuthService auth = host.Get<IAuthService>();

        Assert.Throws<AuthorizationException>(() => auth.LoginAsync("admin", "wrong-password")
            .GetAwaiter().GetResult());

        var principal = auth.LoginAsync("admin", "Shikkha@2026").GetAwaiter().GetResult();
        Assert.Equal("admin", principal.UserName);
        Assert.True(principal.IsAdministrator);
        Assert.Contains("fees.collect", principal.Permissions);
    }

    [Fact]
    public void Account_locks_after_repeated_failures()
    {
        using TestHost host = TestHost.Create().Setup();
        IAuthService auth = host.Get<IAuthService>();

        for (int i = 0; i < 5; i++)
        {
            Assert.Throws<AuthorizationException>(() => auth.LoginAsync("admin", "nope").GetAwaiter().GetResult());
        }

        AuthorizationException exception = Assert.Throws<AuthorizationException>(
            () => auth.LoginAsync("admin", "Shikkha@2026").GetAwaiter().GetResult());

        Assert.Contains("বন্ধ", exception.Message);
    }

    [Fact]
    public void Permissions_are_enforced_in_the_service_layer()
    {
        using TestHost host = TestHost.Create().Setup();
        IUserService users = host.Get<IUserService>();

        Guid teacherRoleId = host.Get<ISecurityRepository>().GetRoleByNameAsync("Teacher")
            .GetAwaiter().GetResult()!.Id;

        Guid teacherId = users.CreateUserAsync("sir", "আব্দুল করিম শিক্ষক", "Teacher@2026", teacherRoleId)
            .GetAwaiter().GetResult();

        host.Get<IAuthService>().LoginAsync("sir", "Teacher@2026").GetAwaiter().GetResult();

        // A teacher may view students but must not be able to collect fees or post accounting entries.
        Assert.Throws<AuthorizationException>(() =>
            host.Get<IPaymentService>().SearchAsync(new PaymentQuery()).GetAwaiter().GetResult());

        Assert.Throws<AuthorizationException>(() =>
            host.Get<IReportingService>().RunReconciliationAsync().GetAwaiter().GetResult());

        host.Get<IStudentService>().SearchAsync(new StudentQuery()).GetAwaiter().GetResult();
        Assert.NotNull(host.Get<ISecurityRepository>().GetUserAsync(teacherId).GetAwaiter().GetResult());
    }

    [Fact]
    public void Audit_log_records_financial_actions()
    {
        using TestHost host = TestHost.Create().Setup();
        (Guid batchId, Guid studentId) = host.CreateBatchWithStudent();

        var auditLogs = host.Get<IUserService>()
            .GetAuditLogsAsync(null, null, 1, 100).GetAwaiter().GetResult();

        Assert.Contains(auditLogs.Items, l => l.Action == "admissions.create");
        Assert.Contains(auditLogs.Items, l => l.EntityId == studentId || l.EntityType == "Admission");
    }

    [Fact]
    public void Duplicate_students_are_detected()
    {
        using TestHost host = TestHost.Create().Setup();
        IStudentService students = host.Get<IStudentService>();

        students.AdmitAsync(new AdmissionRequest(
            "রাহিম আহমেদ", "আব্দুল করিম", "01711111111", null, null, null, null, null, null,
            host.SessionId, null, null, null, Money.Zero, null)).GetAwaiter().GetResult();

        IReadOnlyList<Student> duplicates = students
            .FindDuplicatesAsync("রাহিম আহমেদ", "01711111111", null).GetAwaiter().GetResult();

        Assert.NotEmpty(duplicates);
    }
}
