using System;
using System.IO;
using System.Text;
using L2TitanLauncher.Services;
using Xunit;

namespace L2TitanLauncher.Tests
{
    public class FileIntegrityTests
    {
        [Fact]
        public void ComputeSha256_KnownVector_Matches()
        {
            // SHA-256("abc") = ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(path, Encoding.ASCII.GetBytes("abc"));
                var hash = FileIntegrity.ComputeSha256(path);
                Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
            }
            finally { try { File.Delete(path); } catch { } }
        }

        [Fact]
        public void ComputeSha256_EmptyFile_KnownVector()
        {
            // SHA-256("") = e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(path, Array.Empty<byte>());
                var hash = FileIntegrity.ComputeSha256(path);
                Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", hash);
            }
            finally { try { File.Delete(path); } catch { } }
        }

        [Fact]
        public void ComputeSha256_IsLowercaseHex()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "contenido cualquiera");
                var hash = FileIntegrity.ComputeSha256(path);
                Assert.Equal(64, hash.Length);
                Assert.Equal(hash.ToLowerInvariant(), hash);
            }
            finally { try { File.Delete(path); } catch { } }
        }
    }
}
