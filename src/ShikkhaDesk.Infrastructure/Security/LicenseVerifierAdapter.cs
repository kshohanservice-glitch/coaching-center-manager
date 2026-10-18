using System.Security.Cryptography;
using System.Text;
using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Core.Services;

namespace ShikkhaDesk.Infrastructure.Security;

public sealed class LicenseVerifierAdapter : ILicenseVerifier
{
    public bool Verify(LicensePayload payload) => LicenseVerifier.Verify(payload);

    public string VendorPublicKeyId
    {
        get
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(LicenseVerifier.VendorPublicKeyPem));
            return Convert.ToHexString(hash)[..12].ToLowerInvariant();
        }
    }
}
