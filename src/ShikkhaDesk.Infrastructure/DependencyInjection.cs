using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core.Security;
using ShikkhaDesk.Infrastructure.Backup;
using ShikkhaDesk.Infrastructure.Persistence;
using ShikkhaDesk.Infrastructure.Persistence.Repositories;
using ShikkhaDesk.Infrastructure.Security;

namespace ShikkhaDesk.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Wires the whole application. Everything is registered as a singleton because the desktop app
    /// owns exactly one SQLite connection and one signed-in session at a time; writes are serialised
    /// by the unit of work rather than by object lifetime.
    /// </summary>
    public static IServiceCollection AddShikkhaDesk(this IServiceCollection services,
        ShikkhaDeskEnvironment environment, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);

        Directory.CreateDirectory(environment.DataDirectory);
        Directory.CreateDirectory(environment.BackupDirectory);
        Directory.CreateDirectory(environment.LogDirectory);

        if (loggerFactory is not null)
        {
            services.AddSingleton(loggerFactory);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        }

        services.AddSingleton(new SqliteConnectionOptions { DatabasePath = environment.DatabasePath });
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<DatabaseMigrator>();

        services.AddSingleton<IUnitOfWork, SqliteUnitOfWork>();
        services.AddSingleton<ISequenceAllocator, SequenceAllocator>();

        services.AddSingleton<IAcademicRepository, AcademicRepository>();
        services.AddSingleton<IFeeRepository, FeeRepository>();
        services.AddSingleton<IAccountingRepository, AccountingRepository>();
        services.AddSingleton<ISecurityRepository, SecurityRepository>();
        services.AddSingleton<ISystemRepository, SystemRepository>();

        services.AddSingleton<PasswordHasher>();
        services.AddSingleton<ILicenseVerifier, LicenseVerifierAdapter>();
        services.AddSingleton<IDeviceFingerprint, DeviceFingerprint>();

        services.AddSingleton<ICurrentUserService, SessionContext>();
        services.AddSingleton<IAuditService, AuditService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IAccountingService, AccountingService>();
        services.AddSingleton<IFeeService, FeeService>();
        services.AddSingleton<IPaymentService, PaymentService>();
        services.AddSingleton<IStudentService, StudentService>();
        services.AddSingleton<IAcademicService, AcademicService>();
        services.AddSingleton<IAttendanceService, AttendanceService>();
        services.AddSingleton<IExamService, ExamService>();
        services.AddSingleton<ILedgerOperationsService, LedgerOperationsService>();
        services.AddSingleton<IReportingService, ReportingService>();
        services.AddSingleton<IDashboardService, DashboardService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<ISetupService, SetupService>();
        services.AddSingleton<ILicenseService, LicenseService>();
        services.AddSingleton<IBackupService, DatabaseBackupService>();
        services.AddSingleton<ISupplierService, SupplierService>();

        return services;
    }

    /// <summary>Applies any pending migrations. Called once at startup, before the UI is shown.</summary>
    public static IReadOnlyList<string> Migrate(this IServiceProvider provider)
    {
        DatabaseMigrator migrator = provider.GetRequiredService<DatabaseMigrator>();
        return migrator.ApplyPendingAsync().GetAwaiter().GetResult();
    }
}
