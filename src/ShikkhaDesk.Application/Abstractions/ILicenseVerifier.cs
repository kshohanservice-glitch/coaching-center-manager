using ShikkhaDesk.Core.Services;

namespace ShikkhaDesk.Application.Abstractions;

public interface ILicenseVerifier
{
    /// <summary>Verifies the RSA signature of a licence offline. Returns false for any tampering.</summary>
    bool Verify(LicensePayload payload);

    string VendorPublicKeyId { get; }
}

/// <summary>Stable identifier for this computer, used to count registered devices.</summary>
public interface IDeviceFingerprint
{
    string DeviceId { get; }
    string MachineName { get; }
    string? WindowsUserName { get; }
}
