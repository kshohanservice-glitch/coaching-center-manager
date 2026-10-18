namespace ShikkhaDesk.Lan.Client;

public sealed class LanClientOptions
{
    public required string HostAddress { get; init; }

    public int Port { get; init; } = 8787;

    public bool UseHttps { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    public string BaseAddress =>
        $"{(UseHttps ? "https" : "http")}://{HostAddress.Trim().TrimEnd('/')}:{Port}".TrimEnd('/');
}
