using ShikkhaDesk.Core;


namespace ShikkhaDesk.Lan.Client;

/// <summary>A failure that came from the network or the host rather than from business rules.</summary>
public sealed class LanTransportException : DomainException
{
    public LanTransportException(string message, string? transportCode = null)
        : base(message) => TransportCode = transportCode;

    public string? TransportCode { get; }
}
