using System.Security.Cryptography;
using System.Text;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Security;
using ShikkhaDesk.Core.Services;
using ShikkhaDesk.Infrastructure.Security;
using Xunit;

namespace ShikkhaDesk.Tests;

public sealed class SupportTests
{
    [Fact]
    public void Bengali_digits_round_trip()
    {
        Assert.Equal("১২৩৪৫", Digits.ToBengaliDigits("12345"));
        Assert.Equal("12345", Digits.ToAsciiDigits("১২৩৪৫"));
        Assert.Equal("২০২৬-০৩-১০", Digits.ToBengaliDigits("2026-03-10"));
    }

    [Fact]
    public void Digit_preference_does_not_change_stored_values()
    {
        Money amount = Money.FromDecimal(1234.56m);
        Assert.Equal(123456L, amount.MinorUnits);
        Assert.Equal("১,২৩৪.৫৬", Digits.Format(amount, true));
        Assert.Equal("1,234.56", Digits.Format(amount, false));
        Assert.Equal(123456L, amount.MinorUnits);
    }

    [Fact]
    public void Password_hashing_uses_a_unique_salt_and_verifies()
    {
        var hasher = new PasswordHasher();
        (string firstHash, string firstSalt) = hasher.Hash("Shikkha@2026");
        (string secondHash, string secondSalt) = hasher.Hash("Shikkha@2026");

        Assert.NotEqual(firstSalt, secondSalt);
        Assert.NotEqual(firstHash, secondHash);
        Assert.True(hasher.Verify("Shikkha@2026", firstHash, firstSalt));
        Assert.False(hasher.Verify("wrong-password", firstHash, firstSalt));
    }

    [Fact]
    public void Password_policy_rejects_weak_passwords()
    {
        Assert.NotEmpty(PasswordHasher.ValidateStrength("abc"));
        Assert.NotEmpty(PasswordHasher.ValidateStrength("12345678"));
        Assert.Empty(PasswordHasher.ValidateStrength("Shikkha1"));
    }

    [Fact]
    public void Verifier_accepts_a_genuinely_signed_licence()
    {
        using RSA rsa = RSA.Create(2048);
        string publicPem = rsa.ExportSubjectPublicKeyInfoPem();

        var payload = new LicensePayload
        {
            LicenseId = "LIC-0001",
            InstitutionId = "INST-0001",
            ProductCode = "SDESK",
            LicenseType = "PerpetualSingleInstitution",
            DeviceLimit = 5,
            VersionEntitlement = "1.x",
            IssuedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ExpiresAtUtc = null,
            IssuedTo = "Test Coaching Centre"
        };

        payload.Signature = Convert.ToBase64String(LicenseVerifier.Sign(payload, rsa));

        Assert.True(LicenseVerifier.Verify(payload, publicPem));
    }

    [Fact]
    public void Verifier_rejects_a_tampered_licence()
    {
        using RSA rsa = RSA.Create(2048);
        string publicPem = rsa.ExportSubjectPublicKeyInfoPem();

        var payload = new LicensePayload
        {
            LicenseId = "LIC-0001",
            InstitutionId = "INST-0001",
            IssuedAtUtc = DateTime.UtcNow,
            DeviceLimit = 5
        };

        payload.Signature = Convert.ToBase64String(LicenseVerifier.Sign(payload, rsa));
        payload.DeviceLimit = 500;

        Assert.False(LicenseVerifier.Verify(payload, publicPem));
    }

    [Fact]
    public void Verifier_rejects_a_licence_signed_by_another_key()
    {
        using RSA legit = RSA.Create(2048);
        using RSA attacker = RSA.Create(2048);

        var payload = new LicensePayload
        {
            LicenseId = "LIC-0002",
            InstitutionId = "INST-0001",
            IssuedAtUtc = DateTime.UtcNow,
            DeviceLimit = 5
        };

        payload.Signature = Convert.ToBase64String(LicenseVerifier.Sign(payload, attacker));

        Assert.False(LicenseVerifier.Verify(payload, legit.ExportSubjectPublicKeyInfoPem()));
    }

    [Fact]
    public void Verifier_rejects_a_licence_with_no_signature()
    {
        var payload = new LicensePayload
        {
            LicenseId = "LIC-0003",
            InstitutionId = "INST-0001",
            IssuedAtUtc = DateTime.UtcNow
        };

        Assert.False(LicenseVerifier.Verify(payload));
    }

    [Fact]
    public void Receipt_numbers_are_padded_and_scoped()
    {
        var date = new DateTime(2026, 3, 10);
        Assert.Equal("RCP-2026-000123", ReceiptNumbering.Format("RCP", 123, 6, true, date));
        Assert.Equal("RCP-000123", ReceiptNumbering.Format("RCP", 123, 6, false, date));
        Assert.Equal("RCPT|RCP|2026", ReceiptNumbering.Scope("RCPT", "RCP", true, date));
        Assert.Equal("RCPT|RCP|ALL", ReceiptNumbering.Scope("RCPT", "RCP", false, date));
    }

    [Fact]
    public void Period_keys_round_trip()
    {
        Assert.Equal("2026-03", PeriodKeys.FromDate(new DateTime(2026, 3, 31)));
        Assert.Equal(new DateTime(2026, 3, 1), PeriodKeys.PeriodStart("2026-03"));
        Assert.Equal(new DateTime(2026, 3, 31), PeriodKeys.PeriodEnd("2026-03"));
        Assert.Equal("2027-01", PeriodKeys.AddMonths("2026-12", 1));
    }

    [Fact]
    public void Period_keys_reject_nonsense()
    {
        Assert.Throws<ArgumentException>(() => PeriodKeys.PeriodStart("not-a-period"));
        Assert.Throws<ArgumentException>(() => PeriodKeys.PeriodStart("2026-13"));
    }

    [Fact]
    public void Grade_boundaries_are_inclusive_of_the_minimum()
    {
        var scale = new List<GradeScaleItem>
        {
            new() { Grade = "A+", MinimumPercentage = 80, GradePoint = 5.0m },
            new() { Grade = "A", MinimumPercentage = 70, GradePoint = 4.0m },
            new() { Grade = "F", MinimumPercentage = 0, GradePoint = 0m }
        };

        Assert.Equal("A+", GradeCalculator.GradeFor(80, scale));
        Assert.Equal("A", GradeCalculator.GradeFor(79.99m, scale));
        Assert.Equal(80.00m, GradeCalculator.Percentage(80, 100));
        Assert.True(GradeCalculator.IsPassed(40, 33, false));
        Assert.False(GradeCalculator.IsPassed(90, 33, true));
    }

    [Fact]
    public void Credit_balance_is_recomputed_from_movements()
    {
        var movements = new List<(CreditTransactionKind Kind, Money Amount)>
        {
            (CreditTransactionKind.Received, Money.FromDecimal(5000)),
            (CreditTransactionKind.Allocated, Money.FromDecimal(2000)),
            (CreditTransactionKind.Refunded, Money.FromDecimal(500))
        };

        Assert.Equal(Money.FromDecimal(2500), StudentCreditAccount.Balance(movements));
    }

    [Fact]
    public void Credit_can_never_be_spent_twice()
    {
        var movements = new List<(CreditTransactionKind Kind, Money Amount)>
        {
            (CreditTransactionKind.Received, Money.FromDecimal(1000)),
            (CreditTransactionKind.Allocated, Money.FromDecimal(1000)),
            (CreditTransactionKind.Allocated, Money.FromDecimal(1000))
        };

        Assert.Equal(Money.FromDecimal(-1000), StudentCreditAccount.Balance(movements));
    }
}
