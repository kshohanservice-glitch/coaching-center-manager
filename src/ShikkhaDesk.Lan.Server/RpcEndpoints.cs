using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Infrastructure.Persistence;
using ShikkhaDesk.Lan;
using ShikkhaDesk.Lan.Server;

namespace ShikkhaDesk.Lan.Server;

public static class RpcEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { ok = true, utc = DateTime.UtcNow }));

        app.MapGet("/api/info", async (HttpContext context) =>
        {
            var info = new HostInfo
            {
                HostVersion = typeof(RpcEndpoints).Assembly.GetName().Version?.ToString(3) ?? "1.0.0",
                StartedAtUtc = LanHostApplication.StartedAtUtc,
                ConnectedClients = app.Services.GetRequiredService<LanSessionStore>().ActiveCount
            };

            try
            {
                Institution? institution = await app.Services.GetRequiredService<ISettingsService>()
                    .GetInstitutionAsync();
                info.InstitutionName = institution?.Name ?? string.Empty;
                info.DatabaseVersion = string.Join(",", app.Services
                    .GetRequiredService<DatabaseMigrator>().GetHistory().Select(h => h.Version));
            }
            catch (Exception)
            {
                // /api/info is reachable before setup; a missing institution is not an error.
            }

            return Results.Ok(info);
        });

        app.MapPost("/api/session/login", async (HttpContext context, LoginRequest request) =>
        {
            ILogger<RpcDispatcher> logger = context.RequestServices.GetRequiredService<ILogger<RpcDispatcher>>();

            try
            {
                Principal principal = await context.RequestServices.GetRequiredService<IAuthService>()
                    .LoginAsync(request.UserName, request.Password, request.DeviceId);

                string token = context.RequestServices.GetRequiredService<LanSessionStore>().Issue(principal);

                logger.LogInformation("LAN session issued for {User} from {Device}",
                    principal.UserName, request.DeviceId);

                return Results.Ok(new LoginResponse
                {
                    Token = token,
                    DisplayName = principal.FullName,
                    RoleName = principal.RoleName,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(8)
                });
            }
            catch (Exception ex)
            {
                // The user name is logged; the password never is.
                logger.LogWarning("LAN login failed for {User}: {Reason}", request.UserName, ex.Message);
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/session/logout", (HttpContext context) =>
        {
            context.RequestServices.GetRequiredService<LanSessionStore>()
                .Revoke(context.Request.Headers[RpcProtocol.SessionHeader]);
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/invoke", async (HttpContext context, RpcRequest request) =>
        {
            LanSessionStore sessions = context.RequestServices.GetRequiredService<LanSessionStore>();
            Principal? principal = sessions.Resolve(context.Request.Headers[RpcProtocol.SessionHeader]);

            if (principal is null)
            {
                return Results.Json(RpcResponse.Failure("Unauthorized",
                        "সেশনের মেয়াদ শেষ হয়েছে। অনুগ্রহ করে আবার লগ ইন করুন।"),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            if (string.Equals(request.Method, nameof(IAuthService.LoginAsync), StringComparison.Ordinal)
                || string.Equals(request.Method, nameof(IAuthService.ChangePasswordAsync), StringComparison.Ordinal))
            {
                return Results.Json(RpcResponse.Failure("NotAllowed",
                    "পাসওয়ার্ড সম্পর্কিত কাজ সরাসরি দূরবর্তীভাবে করা যায় না।"));
            }

            // Identity travels with the request, so the service layer's own permission checks run
            // unchanged on the host.
            context.RequestServices.GetRequiredService<ICurrentUserService>().SignIn(principal);

            RpcResponse response = await context.RequestServices.GetRequiredService<RpcDispatcher>()
                .InvokeAsync(request, context.RequestAborted);

            return Results.Json(response);
        });
    }
}
