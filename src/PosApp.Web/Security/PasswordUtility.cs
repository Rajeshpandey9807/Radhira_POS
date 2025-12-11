using System;
using System.Security.Cryptography;

namespace PosApp.Web.Security;

public static class PasswordUtility
{
    public readonly record struct PasswordHashResult(string Hash, string Salt);

    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public static PasswordHashResult CreateHash(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be empty.", nameof(password));
        }

        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var hashBytes = DeriveBytes(password, saltBytes);

        return new PasswordHashResult(
            Hash: Convert.ToHexString(hashBytes),
            Salt: Convert.ToHexString(saltBytes));
    }

    public static string HashPassword(string password, string saltHex)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be empty.", nameof(password));
        }

        if (string.IsNullOrWhiteSpace(saltHex))
        {
            throw new ArgumentException("Salt cannot be empty.", nameof(saltHex));
        }

        var saltBytes = Convert.FromHexString(saltHex);
        var hashBytes = DeriveBytes(password, saltBytes);
        return Convert.ToHexString(hashBytes);
    }

    private static byte[] DeriveBytes(string password, byte[] saltBytes)
    {
        using var deriveBytes = new Rfc2898DeriveBytes(password, saltBytes, Iterations, HashAlgorithmName.SHA256);
        return deriveBytes.GetBytes(HashSize);
    }
}
