using System.Security.Cryptography;

namespace BusinessLogic.Services;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return $"PBKDF2-SHA256.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        string[] parts = passwordHash.Split('.');

        if (parts.Length != 4 || parts[0] != "PBKDF2-SHA256")
        {
            return false;
        }

        if (!int.TryParse(parts[1], out int iterations))
        {
            return false;
        }

        byte[] salt = Convert.FromBase64String(parts[2]);
        byte[] expectedKey = Convert.FromBase64String(parts[3]);
        byte[] actualKey = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedKey.Length);

        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }
}
