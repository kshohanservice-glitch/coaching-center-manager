using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShikkhaDesk.Infrastructure;
using ShikkhaDesk.Infrastructure.Logging;
using ShikkhaDesk.Lan.Server;

namespace ShikkhaDesk.Lan.Host;

/// <summary>
/// Runs ShikkhaDesk as the LAN host. In interactive mode it prints the addresses clients should
/// use; installed as a Windows service it starts with the machine and needs no desktop session.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        bool asService = !(args.Contains("--console") || Environment.UserInteractive);

        var environment = new ShikkhaDeskEnvironment();
        Directory.CreateDirectory(environment.LogDirectory);

        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new FileLoggerProvider(environment.LogDirectory));
            if (!asService)
            {
                builder.AddSimpleConsole();
            }
        });

        ILogger logger = loggerFactory.CreateLogger("ShikkhaDesk.Lan.Host");

        LanHostConfiguration configuration = LanHostConfiguration.Load(environment.DataDirectory);

        if (TryReadPort(args, out int portFromArgs))
        {
            configuration.Port = portFromArgs;
        }

        LanHostOptions options = configuration.ToOptions();

        WebApplication app = LanHostApplication.Build(args, environment, loggerFactory, options,
            builder =>
            {
                if (asService)
                {
                    builder.Host.UseWindowsService(service => service.ServiceName = "ShikkhaDeskLanHost");
                }
            });

        // The host is the single owner of the database, so migration happens here and only here.
        IReadOnlyList<string> applied = app.Services.Migrate();
        if (applied.Count > 0)
        {
            logger.LogInformation("Host applied migrations: {Migrations}", string.Join(", ", applied));
        }

        if (!asService)
        {
            PrintAddresses(options.Port, logger);
        }
        else
        {
            logger.LogInformation("ShikkhaDesk LAN host service listening on port {Port}", options.Port);
        }

        await app.RunAsync();
        return 0;
    }

    private static bool TryReadPort(string[] args, out int port)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--port", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[i + 1], out int fromArgs))
            {
                port = fromArgs;
                return true;
            }
        }

        port = 0;
        return false;
    }

    private static void PrintAddresses(int port, ILogger logger)
    {
        logger.LogInformation("ShikkhaDesk LAN host চালু হয়েছে। ক্লায়েন্টদের নিচের ঠিকানা দিন:");

        foreach (System.Net.IPAddress address in LocalAddresses())
        {
            logger.LogInformation("  http://{Address}:{Port}", address, port);
        }

        if (!Console.IsOutputRedirected)
        {
            Console.WriteLine();
            Console.WriteLine("বন্ধ করতে Ctrl+C চাপুন।");
        }
    }

    internal static IEnumerable<System.Net.IPAddress> LocalAddresses()
    {
        foreach (System.Net.NetworkInformation.NetworkInterface nic in
                 System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
            {
                continue;
            }

            foreach (System.Net.NetworkInformation.UnicastIPAddressInformation info in
                     nic.GetIPProperties().UnicastAddresses)
            {
                if (info.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                    && !System.Net.IPAddress.IsLoopback(info.Address))
                {
                    yield return info.Address;
                }
            }
        }
    }
}
