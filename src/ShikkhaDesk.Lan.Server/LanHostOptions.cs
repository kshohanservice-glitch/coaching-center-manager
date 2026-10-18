namespace ShikkhaDesk.Lan.Server;

/// <summary>
/// Host configuration. The host binds to a specific LAN address rather than every interface, and
/// refuses requests that do not present a session token issued by this host.
/// </summary>
public sealed class LanHostOptions
{
    /// <summary>TCP port the host listens on. Default 8787.</summary>
    public int Port { get; init; } = 8787;

    /// <summary>Address to bind. Empty means every local address; a host machine should set a fixed IP.</summary>
    public string BindAddress { get; init; } = string.Empty;

    /// <summary>Sliding session lifetime for a connected client.</summary>
    public TimeSpan SessionLifetime { get; init; } = TimeSpan.FromHours(8);

    /// <summary>Maximum request body size, in bytes. Payloads are small; a tight cap limits abuse.</summary>
    public int MaxRequestBodyBytes { get; init; } = 4 * 1024 * 1024;

    public int MaxConcurrentRequests { get; init; } = 32;
}
