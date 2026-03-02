using System;
using System.Security.Cryptography;
using System.Text;

namespace ShoppingWebApi.Services.Security
{
    // Format: "v1.{base64Salt}.{base64Hash}"
    public static class PasswordHasher
    {
        private const int SaltSize = 16;    // 128 bits
        private const int KeySize = 32;     // 256 bits
        private const int Iterations = 100_000;

        public static string Hash(string password)
        {
            using var rng = RandomNumberGenerator.Create();
            var salt = new byte[SaltSize];
            rng.GetBytes(salt);

            var hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                KeySize);

            return $"v1.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        public static bool Verify(string password, string encoded)
        {
            if (string.IsNullOrWhiteSpace(encoded)) return false;

            var parts = encoded.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3 || parts[0] != "v1") return false;

            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);

            var hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                KeySize);

            return CryptographicOperations.FixedTimeEquals(hash, expected);
        }
    }
}