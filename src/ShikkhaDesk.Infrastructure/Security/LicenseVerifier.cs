using System.Security.Cryptography;
using System.Text;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Services;

namespace ShikkhaDesk.Infrastructure.Security;

/// <summary>
/// Offline licence verification. The licence is an RSA-SHA256 signature over a canonical string; the
/// private signing key never ships with the product, only this public key. Verification requires no
/// internet connection and never touches customer data.
/// </summary>
public static class LicenseVerifier
{
    /// <summary>
    /// Vendor public key. This is NOT a secret — it can only verify, never issue. Replace it with the
    /// key produced by scripts/Generate-LicenseKeys.ps1 before commercial distribution.
    /// </summary>
    public const string VendorPublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAmefOAu2eADTkW4qYnBQV
        Qy/+PCElP/Nczjo2HykXxcwp+nEdF59u+PCV3Wnak7JoUBnYWnjaUjuQH5xYAVEl
        TzbgTSS10QXoRVxFLSal31wqnRyMtya4Bw09qqY8AdKJxuLB46EBQX0JGg+BC+6c
        im5NAQ1CW9dbFTY6XYIV/jFh4TW+oIbIKG0vi8Pf3Yi/hFyrY8DVvZwh7VgsmtQS
        aFnPVFKpT18/E4gaqP5I9aLWil207Cu6WgUl8AkHPxvmnqvwYWaau6/ZKsedHPTd
        5FssVnVEoPGC+RuA3PuE8YObMrzl1zplEEZUoufPigoYbzGhvS7XmXerHLDyomuI
        wQIDAQAB
        -----END PUBLIC KEY-----
        """;

    public static byte[] Sign(LicensePayload payload, RSA privateKey)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(privateKey);
        payload.ValidateFields();
        return privateKey.SignData(
            Encoding.UTF8.GetBytes(payload.CanonicalString()),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }

    public static bool Verify(LicensePayload payload, string? publicKeyPem = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        payload.ValidateFields();

        if (string.IsNullOrWhiteSpace(payload.Signature))
        {
            return false;
        }

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(payload.Signature);
        }
        catch (FormatException)
        {
            return false;
        }

        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem ?? VendorPublicKeyPem);
        return rsa.VerifyData(
            Encoding.UTF8.GetBytes(payload.CanonicalString()),
            signature,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }
}
