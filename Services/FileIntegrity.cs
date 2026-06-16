using System;
using System.IO;
using System.Security.Cryptography;

namespace L2TitanLauncher.Services
{
    internal static class FileIntegrity
    {
        // SHA-256 en hex minúscula. DEBE coincidir con generate_manifest.py
        // (calculate_sha256 → hexdigest().lower()); usar MD5 aquí rompería el 100% de la verificación.
        public static string ComputeSha256(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
