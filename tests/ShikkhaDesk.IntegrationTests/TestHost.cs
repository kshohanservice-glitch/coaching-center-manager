using Microsoft.Extensions.DependencyInjection;
using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Core.Services;
using ShikkhaDesk.Infrastructure;
using ShikkhaDesk.Infrastructure.Persistence;
using Xunit;

namespace ShikkhaDesk.IntegrationTests;

/// <summary>
/// Builds a complete, real ShikkhaDesk installation in a temporary folder: migrations, setup wizard,
/// admin login. Nothing is mocked, so these exercises run against genuine SQLite databases.
/// </summary>
public sealed class TestHost : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly string _root;

    private TestHost(string root, ServiceProvider provider)
    {
        _root = root;
        _provider = provider;
    }

    public string Root => _root;
    public string DatabasePath => _provider.GetRequiredService<SqliteConnectionFactory>().Options.DatabasePath;

    public T Get<T>() where T : notnull => _provider.GetRequiredService<T>();

    public static TestHost Create()
    {
        string root = Path.Combine(Path.GetTempPath(), "shikkhadesk-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var environment = ShikkhaDeskEnvironment.ForTests(root);
        var services = new ServiceCollection();
        services.AddShikkhaDesk(environment);
        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<DatabaseMigrator>().ApplyPendingAsync().GetAwaiter().GetResult();

        return new TestHost(root, provider);
    }

    /// <summary>Runs the real first-run setup wizard and signs the administrator in.</summary>
    public TestHost Setup(string adminPassword = "Shikkha@2026", Money? openingCash = null)
    {
        ISetupService setup = Get<ISetupService>();
        var request = new SetupRequest(
            InstitutionName: "নূর কোচিং সেন্টার",
            InstitutionAddress: "টাঙ্গাইল, ঢাকা",
            Phone: "01700000000",
            Email: "info@nurcoaching.test",
            CurrencyCode: "BDT",
            CurrencySymbol: "৳",
            DigitPreference: DigitPreference.Bengali,
            ReceiptPrefix: "RCP",
            BackupLocation: Path.Combine(Root, "backups"),
            AdminUserName: "admin",
            AdminFullName: "প্রধান প্রশাসক",
            AdminPassword: adminPassword,
            HostMode: HostMode.SingleComputer,
            SessionName: "২০২৬ শিক্ষাবর্ষ",
            SessionStart: new DateTime(2026, 1, 1),
            SessionEnd: new DateTime(2026, 12, 31),
            OpeningCashBalance: openingCash ?? Money.FromDecimal(10000));

        setup.InitializeAsync(request).GetAwaiter().GetResult();
        Get<IAuthService>().LoginAsync("admin", adminPassword, "test-device").GetAwaiter().GetResult();
        return this;
    }

    /// <summary>The cash account created by the setup wizard.</summary>
    public Guid CurrentCashAccountId() => Get<IAccountingRepository>()
        .GetFinancialAccountsAsync(true).GetAwaiter().GetResult()
        .First(a => a.Kind == FinancialAccountKind.Cash).Id;

    public Guid SessionId => Guid.Parse(Get<ISettingsService>().GetAsync("academic.sessionId", string.Empty)
        .GetAwaiter().GetResult());

    /// <summary>Creates a batch with one enrolled student, ready for fee and attendance tests.</summary>
    public (Guid BatchId, Guid StudentId) CreateBatchWithStudent(string studentName = "রাহিম আহমেদ",
        string batchName = "ব্যাচ-এ")
    {
        IAcademicService academic = Get<IAcademicService>();
        IStudentService students = Get<IStudentService>();

        Guid courseId = academic.SaveCourseAsync(new Course
        {
            Name = "এইচএসসি বিজ্ঞান",
            DurationMonths = 12,
            IsActive = true
        }).GetAwaiter().GetResult();

        Guid batchId = academic.SaveBatchAsync(new Batch
        {
            Name = batchName,
            SessionId = SessionId,
            CourseId = courseId,
            StartDate = new DateTime(2026, 1, 1),
            IsActive = true
        }, []).GetAwaiter().GetResult();

        AdmissionResult admission = students.AdmitAsync(new AdmissionRequest(
            FullName: studentName,
            GuardianName: "আব্দুল করিম",
            GuardianPhone: "01711111111",
            StudentPhone: null,
            Address: "টাঙ্গাইল",
            DateOfBirth: new DateTime(2010, 5, 1),
            Gender: "ছাত্র",
            SchoolOrCollege: "বীর মুক্তিযোদ্ধা উচ্চ বিদ্যালয়",
            SchoolClass: "নবম",
            SessionId: SessionId,
            BatchId: batchId,
            CourseId: courseId,
            ClassGradeId: null,
            AdmissionFee: Money.FromDecimal(500),
            FeePlanId: null,
            AdmissionDate: new DateTime(2026, 1, 5))).GetAwaiter().GetResult();

        return (batchId, admission.StudentId);
    }

    public Guid CreateFeePlan(Money monthlyAmount, Guid? batchId = null)
    {
        IFeeService fees = Get<IFeeService>();
        var plan = new FeePlan
        {
            Name = "মাসিক ফি প্ল্যান",
            BatchId = batchId,
            EffectiveFrom = new DateTime(2026, 1, 1),
            IsActive = true
        };

        return fees.CreateFeePlanAsync(plan,
        [
            new FeePlanItem
            {
                Kind = FeeChargeKind.MonthlyTuition,
                Description = "মাসিক টিউশন ফি",
                Amount = monthlyAmount,
                RecurrenceMonths = 1,
                SortOrder = 1
            }
        ]).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _provider.Dispose();

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Temporary files left behind by an in-flight connection are not fatal for the test run.
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore: Windows may still hold a handle briefly after dispose.
        }
    }
}
