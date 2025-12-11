using System;
using System.Security.Cryptography;

namespace PosApp.Web.Security;

public static class PasswordUtility
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public static PasswordHashResult CreatePasswordHash(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be empty.", nameof(password));
        }

        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = HashPasswordInternal(password, saltBytes);
        return new PasswordHashResult(hash, Convert.ToHexString(saltBytes));
    }

    public static string HashPassword(string password, string saltHex)
    {
        if (string.IsNullOrWhiteSpace(saltHex))
        {
            throw new ArgumentException("Salt cannot be empty.", nameof(saltHex));
        }

        var saltBytes = Convert.FromHexString(saltHex);
        return HashPasswordInternal(password, saltBytes);
    }

    private static string HashPasswordInternal(string password, byte[] saltBytes)
    {
        using var deriveBytes = new Rfc2898DeriveBytes(password, saltBytes, Iterations, HashAlgorithmName.SHA256);
        return Convert.ToHexString(deriveBytes.GetBytes(HashSize));
    }

    public readonly record struct PasswordHashResult(string Hash, string Salt);
}
