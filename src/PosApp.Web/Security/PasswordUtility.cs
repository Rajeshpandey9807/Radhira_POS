using System;
using System.Security.Cryptography;

namespace PosApp.Web.Security;

public static class PasswordUtility
{
    public sealed record PasswordHash(string PasswordHash, string PasswordSalt);

    /// <summary>
    /// Creates a salted password hash using PBKDF2 (HMAC-SHA256).
    /// Stored format: base64(hash) + base64(salt).
    /// </summary>
    public static PasswordHash HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be empty.", nameof(password));
        }

        var salt = RandomNumberGenerator.GetBytes(32);
        var hash = Pbkdf2(password, salt, iterations: 100_000, numBytesRequested: 32);

        return new PasswordHash(
            PasswordHash: Convert.ToBase64String(hash),
            PasswordSalt: Convert.ToBase64String(salt));
    }

    public static bool VerifyPassword(string password, string hashedPassword, string passwordSalt)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(hashedPassword) || string.IsNullOrWhiteSpace(passwordSalt))
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(passwordSalt);
            expectedHash = Convert.FromBase64String(hashedPassword);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = Pbkdf2(password, salt, iterations: 100_000, numBytesRequested: expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] Pbkdf2(string password, byte[] salt, int iterations, int numBytesRequested)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            numBytesRequested);
    }
}
