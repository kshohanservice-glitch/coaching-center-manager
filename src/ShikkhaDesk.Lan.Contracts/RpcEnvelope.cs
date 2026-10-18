using System.Text.Json;
using System.Text.Json.Serialization;
using ShikkhaDesk.Core;

namespace ShikkhaDesk.Lan;

/// <summary>Transport constants shared by the host and its clients.</summary>
public static class RpcProtocol
{
    public const string SessionHeader = "X-ShikkhaDesk-Session";
}

/// <summary>
/// Wire contract between the LAN host and its clients. The transport is deliberately a single
/// invoke endpoint: the host runs the real application services, so every permission check,
/// validation and ledger posting happens exactly once, on the machine that owns the database.
/// </summary>
public sealed class RpcRequest
{
    public string Service { get; set; } = string.Empty;

    public string Method { get; set; } = string.Empty;

    /// <summary>Positional arguments serialised one by one, so parameter types stay explicit.</summary>
    public List<string> Arguments { get; set; } = [];
}

public sealed class RpcResponse
{
    public bool Ok { get; set; }

    public string? Result { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public static RpcResponse Success(string? result) => new() { Ok = true, Result = result };

    public static RpcResponse Failure(string code, string message) =>
        new() { Ok = false, ErrorCode = code, ErrorMessage = message };
}

public sealed class LoginRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
}

public sealed class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public string? MustChangePassword { get; set; }
}

public sealed class HostInfo
{
    public string HostVersion { get; set; } = string.Empty;
    public string InstitutionName { get; set; } = string.Empty;
    public string DatabaseVersion { get; set; } = string.Empty;
    public int ConnectedClients { get; set; }
    public DateTime StartedAtUtc { get; set; }
}

/// <summary>
/// Shared serialiser settings. Money is written as an integer count of minor units so no client can
/// ever introduce a rounding difference.
/// </summary>
public static class RpcJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new MoneyConverter(),
            new JsonStringEnumConverter()
        }
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}
