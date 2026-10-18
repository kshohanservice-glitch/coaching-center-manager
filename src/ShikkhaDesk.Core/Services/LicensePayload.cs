using System.Globalization;
using System.Text;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShikkhaDesk.Core.Services;

/// <summary>The licence document as issued: the payload plus the vendor signature over it.</summary>
public sealed class LicenseDocument
{
    public LicensePayload Payload { get; set; } = new();

    /// <summary>RSA-SHA256-PKCS#1 signature over <see cref="LicensePayload.CanonicalString"/>, base64.</summary>
    public string Signature { get; set; } = string.Empty;

    public string PublicKeyId { get; set; } = "default";
}

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

    /// <summary>
    /// Reads a licence document supplied by the operator. The document is JSON; the signature travels
    /// with it and is verified offline by the licence verifier, never by calling a server.
    /// </summary>
    public static LicensePayload Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ValidationException("লাইসেন্স কী খালি।");
        }

        string trimmed = text.Trim();
        LicenseDocument? document;

        try
        {
            document = JsonSerializer.Deserialize<LicenseDocument>(trimmed, ParseOptions);
        }
        catch (JsonException)
        {
            throw new ValidationException(
                "লাইসেন্স কী পড়া যায়নি। অনুগ্রহ করে প্রদত্ত লাইসেন্স ফাইলের সম্পূর্ণ বিষয়বস্তু কপি করে পেস্ট করুন।");
        }

        if (document?.Payload is null)
        {
            throw new ValidationException("লাইসেন্স কী-এ কোনো তথ্য পাওয়া যায়নি।");
        }

        if (string.IsNullOrWhiteSpace(document.Signature))
        {
            throw new ValidationException("লাইসেন্স কী-এ স্বাক্ষর নেই — এটি গ্রহণযোগ্য নয়।");
        }

        document.Payload.Signature = document.Signature.Trim();
        document.Payload.PublicKeyId = document.PublicKeyId;
        document.Payload.ValidateFields();
        return document.Payload;
    }

    private static readonly JsonSerializerOptions ParseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
