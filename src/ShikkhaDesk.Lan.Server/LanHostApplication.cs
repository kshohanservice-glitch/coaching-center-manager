using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Infrastructure;
using ShikkhaDesk.Lan;
using ShikkhaDesk.Lan.Server;

namespace ShikkhaDesk.Lan.Server;

/// <summary>
/// The host process: one machine owns the SQLite file, applies migrations and runs every use case.
/// Clients hold no database connection at all.
/// </summary>
public static class LanHostApplication
{
    public static readonly DateTime StartedAtUtc = DateTime.UtcNow;

    public static WebApplication Build(string[] args, ShikkhaDeskEnvironment environment,
        ILoggerFactory loggerFactory, LanHostOptions options,
        Action<WebApplicationBuilder>? configureBuilder = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args });
        configureBuilder?.Invoke(builder);

        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new ShikkhaDeskLanLoggerProvider(loggerFactory));
        builder.WebHost.UseKestrel(kestrel =>
        {
            if (string.IsNullOrWhiteSpace(options.BindAddress))
            {
                kestrel.ListenAnyIP(options.Port);
            }
            else if (System.Net.IPAddress.TryParse(options.BindAddress, out System.Net.IPAddress? address))
            {
                kestrel.Listen(address, options.Port);
            }
            else
            {
                kestrel.ListenLocalhost(options.Port);
            }
        });

        builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(form =>
            form.MultipartBodyLengthLimit = options.MaxRequestBodyBytes);

        builder.Services.AddShikkhaDesk(environment, loggerFactory);

        // Replace the single-user session holder: the host serves many callers at once.
        builder.Services.Remove(builder.Services.First(s => s.ServiceType == typeof(ICurrentUserService)));
        builder.Services.AddSingleton<ICurrentUserService, LanSessionContext>();

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(new LanSessionStore(options.SessionLifetime));
        builder.Services.AddSingleton<RpcDispatcher>();

        WebApplication app = builder.Build();
        app.UseMiddleware<RequestLoggingMiddleware>();
        RpcEndpoints.Map(app);
        return app;
    }
}
