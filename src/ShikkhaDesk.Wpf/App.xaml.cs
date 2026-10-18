using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Infrastructure;
using ShikkhaDesk.Infrastructure.Logging;
using ShikkhaDesk.Wpf.Infrastructure;
using ShikkhaDesk.Wpf.Shell;
using ShikkhaDesk.Wpf.Views;

namespace ShikkhaDesk.Wpf;

public partial class App : Application
{
    internal static ServiceProvider? Services { get; private set; }
    internal static AppState State { get; } = new();
    internal static string DataLocation { get; private set; } = string.Empty;

    internal static string ApplicationVersion { get; } =
        typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            ReportCrash(args.Exception);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                ReportCrash(exception);
            }
        };

        var environment = new ShikkhaDeskEnvironment();
        Directory.CreateDirectory(environment.LogDirectory);
        DataLocation = environment.DataDirectory;

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new FileLoggerProvider(environment.LogDirectory));
        });

        ILogger<App> logger = loggerFactory.CreateLogger<App>();
        logger.LogInformation("ShikkhaDesk {Version} starting", ApplicationVersion);

        var services = new ServiceCollection();
        services.AddSingleton(loggerFactory);
        services.AddShikkhaDesk(environment, loggerFactory);
        services.AddSingleton<AppState>(State);
        services.AddSingleton<IDialogService, DialogService>();
        services.AddShikkhaDeskPresentation();
        services.AddSingleton<MainWindow>();
        Services = services.BuildServiceProvider();

        try
        {
            IReadOnlyList<string> applied = Services.Migrate();
            if (applied.Count > 0)
            {
                logger.LogInformation("Applied migrations: {Migrations}", string.Join(", ", applied));
            }
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Database migration failed");
            MessageBox.Show(
                "ডেটাবেস প্রস্তুত করা যায়নি:\n" + ex.Message +
                "\n\nসমস্যা সমাধানে সাহায্যের জন্য docs/BACKUP-RESTORE.md দেখুন।",
                "শিক্ষা ডেস্ক", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        ISetupService setup = Services.GetRequiredService<ISetupService>();
        setup.EnsurePermissionsSeededAsync().GetAwaiter().GetResult();

        if (!setup.IsInitializedAsync().GetAwaiter().GetResult())
        {
            var wizard = new SetupWizardWindow();
            if (wizard.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
        }

        if (!ShowLogin())
        {
            Shutdown();
            return;
        }

        MainWindow main = Services.GetRequiredService<MainWindow>();
        main.Closed += (_, _) => Shutdown();
        main.Show();
    }

    private static bool ShowLogin()
    {
        IAuthService? auth = Services?.GetRequiredService<IAuthService>();

        while (true)
        {
            var login = new LoginWindow();
            bool? result = login.ShowDialog();
            if (result != true)
            {
                return false;
            }

            try
            {
                var principal = auth!.LoginAsync(login.UserName, login.Password,
                    Services!.GetRequiredService<IDeviceFingerprint>().DeviceId).GetAwaiter().GetResult();

                State.Begin(principal, Services.GetRequiredService<ISettingsService>()
                    .GetInstitutionAsync().GetAwaiter().GetResult());

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "লগ ইন", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void ReportCrash(Exception exception)
    {
        try
        {
            Services?.GetService<ILogger<App>>()?.LogError(exception, "Unhandled exception");
        }
        catch (Exception)
        {
            // Reporting must never throw.
        }

        MessageBox.Show(
            "একটি অপ্রত্যাশিত সমস্যা হয়েছে। কাজটি সংরক্ষিত না-ও হতে পারে।\n\n" + exception.Message +
            "\n\nবিস্তারিত লগ: %LOCALAPPDATA%\\ShikkhaDesk\\Logs",
            "শিক্ষা ডেস্ক", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
