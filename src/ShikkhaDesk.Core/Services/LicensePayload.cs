using System.Globalization;
using System.Text;

namespace ShikkhaDesk.Core.Services;

/// <summary>
/// The signed licence document. The canonical string is what gets signed, so field order and
/// formatting are part of the contract and must never change without bumping the licence format.
/// </summary>
public sealed class LicensePayload
{
    public const string FormatVersion = "1";
    public const string DefaultProductCode = "SDESK";

    public string LicenseId { get; set; } = string.Empty;
    public string InstitutionId { get; set; } = string.Empty;
    public string ProductCode { get; set; } = DefaultProductCode;
    public string LicenseType { get; set; } = "PerpetualSingleInstitution";
    public int DeviceLimit { get; set; } = 5;
    public string VersionEntitlement { get; set; } = "1.x";
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string? IssuedTo { get; set; }
    public string? Signature { get; set; }
    public string? PublicKeyId { get; set; }

    /// <summary>The exact byte sequence covered by the signature.</summary>
    public string CanonicalString()
    {
        var builder = new StringBuilder();
        builder.Append("SDESK-LIC").Append('|').Append(FormatVersion).Append('|');
        builder.Append(Trim(LicenseId)).Append('|');
        builder.Append(Trim(InstitutionId)).Append('|');
        builder.Append(Trim(ProductCode)).Append('|');
        builder.Append(Trim(LicenseType)).Append('|');
        builder.Append(DeviceLimit.ToString(CultureInfo.InvariantCulture)).Append('|');
        builder.Append(Trim(VersionEntitlement)).Append('|');
        builder.Append(IssuedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)).Append('|');
        builder.Append(ExpiresAtUtc.HasValue
            ? ExpiresAtUtc.Value.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
            : string.Empty).Append('|');
        builder.Append(Trim(IssuedTo));
        return builder.ToString();
    }

    public void ValidateFields()
    {
        if (string.IsNullOrWhiteSpace(LicenseId))
        {
            throw new ValidationException("লাইসেন্স আইডি প্রয়োজন।");
        }

        if (string.IsNullOrWhiteSpace(InstitutionId))
        {
            throw new ValidationException("প্রতিষ্ঠান আইডি প্রয়োজন।");
        }

        if (DeviceLimit is < 1 or > 1000)
        {
            throw new ValidationException("ডিভাইস সীমা ১ থেকে ১০০০ এর মধ্যে হতে হবে।");
        }

        if (IssuedAtUtc == default)
        {
            throw new ValidationException("ইস্যুর তারিখ প্রয়োজন।");
        }
    }

    private static string Trim(string? value) => (value ?? string.Empty).Trim();
}
