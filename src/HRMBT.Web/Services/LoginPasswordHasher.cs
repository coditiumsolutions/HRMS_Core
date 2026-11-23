using System.Security.Cryptography;
using System.Text;

namespace HRMBT.Web.Services;

/// <summary>
/// PBKDF2 password storage (single field, fits nvarchar(255)). Used for Users.PasswordHash and login verification.
/// </summary>
public static class LoginPasswordHasher
{
    private const int Iterations = 600_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>True when the stored value is not a PBKDF2 hash at the current iteration count (legacy plain text, SHA-256, or fewer iterations).</summary>
    public static bool NeedsRehash(string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return true;
        var parts = storedHash.Split('.', 3);
        return !(parts.Length == 3 && int.TryParse(parts[0], out var iterations) && iterations >= Iterations);
    }

    public static bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return false;

        var parts = storedHash.Split('.', 3);
        if (parts.Length == 3
            && int.TryParse(parts[0], out var iterations)
            && iterations > 0)
        {
            try
            {
                var salt = Convert.FromBase64String(parts[1]);
                var expected = Convert.FromBase64String(parts[2]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(
                    Encoding.UTF8.GetBytes(password),
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256,
                    expected.Length);
                return CryptographicOperations.FixedTimeEquals(expected, actual);
            }
            catch
            {
                return false;
            }
        }

        // Legacy: SHA256 hex (64 chars) from older data
        if (storedHash.Length == 64 && IsHex(storedHash))
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return CryptographicOperations.FixedTimeEquals(bytes, Convert.FromHexString(storedHash));
        }

        // Legacy / SQL seed: password stored as plain text in PasswordHash (e.g. shahid/shahid)
        return PlaintextEquals(password, storedHash);
    }

    private static bool PlaintextEquals(string password, string stored)
    {
        try
        {
            var a = Encoding.UTF8.GetBytes(password);
            var b = Encoding.UTF8.GetBytes(stored);
            if (a.Length != b.Length) return false;
            return CryptographicOperations.FixedTimeEquals(a, b);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsHex(string s)
    {
        foreach (var c in s)
        {
            if (!char.IsAsciiHexDigit(c)) return false;
        }
        return true;
    }
}
