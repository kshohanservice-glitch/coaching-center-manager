using System.Net;
using System.Net.Http.Json;
using ShikkhaDesk.Core;
using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Lan;

namespace ShikkhaDesk.Lan.Client;

/// <summary>
/// HTTP transport for a client workstation. It holds no database connection: every call is executed
/// by the host, which owns the data. The session token is held in memory only.
/// </summary>
public sealed class LanConnection : ILanConnection, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _deviceId;

    public LanConnection(LanClientOptions options, string deviceId)
    {
        _deviceId = deviceId;
        BaseAddress = options.BaseAddress;

        _http = new HttpClient
        {
            BaseAddress = new Uri(options.BaseAddress + "/api/", UriKind.Absolute),
            Timeout = options.Timeout
        };
    }

    public string BaseAddress { get; }

    public Principal? CurrentUser { get; private set; }

    public bool IsConnected { get; private set; }

    private string? _token;

    public async Task<Principal> LoginAsync(string userName, string password, string deviceId,
        CancellationToken ct = default)
    {
        var request = new LoginRequest { UserName = userName, Password = password, DeviceId = deviceId };

        HttpResponseMessage response = await _http.PostAsJsonAsync("session/login", request,
            RpcJson.Options, ct);

        if (!response.IsSuccessStatusCode)
        {
            string? detail = null;
            try
            {
                var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(
                    RpcJson.Options, ct);
                detail = body is not null && body.TryGetValue("error", out string? value) ? value : null;
            }
            catch (Exception)
            {
                detail = null;
            }

            throw new AuthorizationException(detail ?? "হোস্টের সাথে সংযোগ হয়নি বা লগ ইন ব্যর্থ হয়েছে।");
        }

        LoginResponse? result = await response.Content.ReadFromJsonAsync<LoginResponse>(RpcJson.Options, ct);
        _token = result?.Token;
        IsConnected = _token is not null;

        // The principal's permissions stay on the host: the client keeps identity for display only.
        CurrentUser = new Principal
        {
            UserName = userName,
            FullName = result?.DisplayName ?? userName,
            RoleName = result?.RoleName ?? string.Empty,
            DeviceId = deviceId
        };

        return CurrentUser;
    }

    public void Logout()
    {
        if (_token is not null)
        {
            try
            {
                _http.PostAsync("session/logout", null).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // A host that is already gone cannot be logged out of; the local state still clears.
            }
        }

        _token = null;
        CurrentUser = null;
        IsConnected = false;
    }

    public async Task<HostInfo> GetInfoAsync(CancellationToken ct = default)
    {
        HostInfo? info = await _http.GetFromJsonAsync<HostInfo>("info", RpcJson.Options, ct);
        IsConnected = info is not null;
        return info ?? new HostInfo();
    }

    public async Task<bool> TestAsync(CancellationToken ct = default)
    {
        try
        {
            HttpResponseMessage response = await _http.GetAsync("health", ct);
            IsConnected = response.IsSuccessStatusCode;
            return IsConnected;
        }
        catch (Exception)
        {
            IsConnected = false;
            return false;
        }
    }

    public async Task<RpcResponse> InvokeAsync(RpcRequest request, CancellationToken ct = default)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "invoke")
            {
                Content = JsonContent.Create(request, options: RpcJson.Options)
            };

            if (_token is not null)
            {
                message.Headers.TryAddWithoutValidation(RpcEndpoints.SessionHeader, _token);
            }

            HttpResponseMessage response = await _http.SendAsync(message, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                IsConnected = false;
                return RpcResponse.Failure("Unauthorized",
                    "সেশনের মেয়াদ শেষ হয়েছে। অনুগ্রহ করে আবার লগ ইন করুন।");
            }

            RpcResponse? result = await response.Content.ReadFromJsonAsync<RpcResponse>(RpcJson.Options, ct);
            IsConnected = result is not null;
            return result ?? RpcResponse.Failure("EmptyResponse", "হোস্ট থেকে কোনো উত্তর আসেনি।");
        }
        catch (TaskCanceledException)
        {
            IsConnected = false;
            return RpcResponse.Failure("Timeout", "হোস্ট কম্পিউটারের সাথে যোগাযোগ করা যাচ্ছে না।");
        }
        catch (HttpRequestException ex)
        {
            IsConnected = false;
            return RpcResponse.Failure("Offline",
                "হোস্ট কম্পিউটারের সাথে সংযোগ করা যাচ্ছে না। নেটওয়ার্ক ও হোস্ট চালু আছে কিনা দেখুন। "
                + ex.Message);
        }
    }

    public void Dispose() => _http.Dispose();
}
