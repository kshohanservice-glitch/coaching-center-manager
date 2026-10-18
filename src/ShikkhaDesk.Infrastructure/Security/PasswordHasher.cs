using System.Security.Cryptography;
using System.Text;

namespace ShikkhaDesk.Infrastructure.Security;

/// <summary>
/// Password hashing with PBKDF2-HMAC-SHA256, a per-user random salt and a constant-time comparison.
/// Passwords are never stored or logged in any form, and there is no default or backdoor password.
/// </summary>
public sealed class PasswordHasher
{
    public const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public (string Hash, string Salt) Hash(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("পাসওয়ার্ড খালি হতে পারবে না।", nameof(password));
        }

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        return (Convert.ToBase64String(key), Convert.ToBase64String(salt));
    }

    public bool Verify(string password, string expectedHash, string salt)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(expectedHash) || string.IsNullOrEmpty(salt))
        {
            return false;
        }

        byte[] saltBytes;
        try
        {
            saltBytes = Convert.FromBase64String(salt);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), saltBytes, Iterations, HashAlgorithmName.SHA256, KeySize);

        byte[] expected;
        try
        {
            expected = Convert.FromBase64String(expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>Enforces a minimum strength policy for operator-chosen passwords.</summary>
    public static IReadOnlyList<string> ValidateStrength(string password)
    {
        var errors = new List<string>();
        if (password.Length < 8)
        {
            errors.Add("পাসওয়ার্ড কমপক্ষে ৮ অক্ষরের হতে হবে।");
        }

        if (!password.Any(char.IsDigit))
        {
            errors.Add("পাসওয়ার্ডে অন্তত একটি সংখ্যা থাকতে হবে।");
        }

        if (!password.Any(char.IsLetter))
        {
            errors.Add("পাসওয়ার্ডে অন্তত একটি অক্ষর থাকতে হবে।");
        }

        return errors;
    }
}
