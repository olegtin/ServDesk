using System;
using System.Linq;
using System.Security.Cryptography;

namespace ServiceDesk.Security
{
    public static class PasswordHasher
    {
        private const int Iterations = 100000;
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const string Prefix = "PBKDF2-SHA1";

        public static string HashPassword(string password)
        {
            if (password == null)
                throw new ArgumentNullException(nameof(password));

            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            byte[] hash = GetHash(password, salt, Iterations);

            return string.Join("$",
                Prefix,
                Iterations.ToString(),
                Convert.ToBase64String(salt),
                Convert.ToBase64String(hash));
        }

        public static bool VerifyPassword(string password, string storedPassword)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedPassword))
                return false;

            if (!IsHashedPassword(storedPassword))
                return password == storedPassword;

            string[] parts = storedPassword.Split('$');
            if (parts.Length != 4 || !int.TryParse(parts[1], out int iterations))
                return false;
            try
            {
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] expectedHash = Convert.FromBase64String(parts[3]);
                byte[] actualHash = GetHash(password, salt, iterations);

                return SlowEquals(actualHash, expectedHash);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static bool IsHashedPassword(string storedPassword)
        {
            return storedPassword != null && storedPassword.StartsWith(Prefix + "$", StringComparison.Ordinal);
        }

        private static byte[] GetHash(string password, byte[] salt, int iterations)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations))
            {
                return pbkdf2.GetBytes(HashSize);
            }
        }

        private static bool SlowEquals(byte[] first, byte[] second)
        {
            if (first == null || second == null)
                return false;

            int diff = first.Length ^ second.Length;
            for (int i = 0; i < first.Length && i < second.Length; i++)
            {
                diff |= first[i] ^ second[i];
            }

            return diff == 0;
        }
    }
}
