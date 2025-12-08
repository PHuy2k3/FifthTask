// File: Store.Biz/Services/PasswordHasher.cs
using System.Security.Cryptography;
using System.Text;

namespace Store.Biz.Services
{
    public static class PasswordHasher
    {
        // Simple SHA256 hash (not bcrypt). You previously said you don't want bcrypt.
        // For production prefer a stronger hash (PBKDF2/Argon2/bcrypt).
        public static string Hash(string password)
        {
            if (password == null) password = "";
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password + "||store_salt_v1"); // small salt
            var hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        public static bool Verify(string password, string hash)
        {
            if (password == null) password = "";
            var h = Hash(password);
            return h == hash;
        }
    }
}
