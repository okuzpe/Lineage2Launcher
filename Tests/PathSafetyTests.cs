using System;
using System.IO;
using L2TitanLauncher.Services;
using Xunit;

namespace L2TitanLauncher.Tests
{
    public class PathSafetyTests
    {
        private const string GameDir = @"C:\Games\L2";

        [Fact]
        public void ResolveSafePath_ValidRelative_StaysInsideGameDir()
        {
            var result = PathSafety.ResolveSafePath(GameDir, @"system\L2.exe");
            Assert.StartsWith(@"C:\Games\L2\", result, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(@"system\L2.exe", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ResolveSafePath_NestedSubdir_Ok()
        {
            var result = PathSafety.ResolveSafePath(GameDir, @"textures\pack\t.utx");
            Assert.StartsWith(@"C:\Games\L2\", result, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(@"..\..\Windows\System32\evil.exe")]   // traversal hacia arriba
        [InlineData(@"system\..\..\..\evil.dll")]           // traversal anidado que escapa
        [InlineData(@"C:\Windows\System32\evil.exe")]       // ruta absoluta
        [InlineData(@"\\server\share\evil.exe")]            // UNC
        public void ResolveSafePath_Traversal_OrAbsolute_Throws(string malicious)
        {
            Assert.ThrowsAny<Exception>(() => PathSafety.ResolveSafePath(GameDir, malicious));
        }

        [Fact]
        public void LooksLikeLineageClient_WithSystemL2Exe_True()
        {
            var dir = NewTempDir();
            try
            {
                Directory.CreateDirectory(Path.Combine(dir, "system"));
                File.WriteAllText(Path.Combine(dir, "system", "L2.exe"), "stub");
                Assert.True(PathSafety.LooksLikeLineageClient(dir));
            }
            finally { SafeDelete(dir); }
        }

        [Fact]
        public void LooksLikeLineageClient_WithoutClient_False()
        {
            var dir = NewTempDir();
            try
            {
                Directory.CreateDirectory(dir);
                Assert.False(PathSafety.LooksLikeLineageClient(dir));
            }
            finally { SafeDelete(dir); }
        }

        [Fact]
        public void LooksLikeLineageClient_EmptyOrNull_False()
        {
            Assert.False(PathSafety.LooksLikeLineageClient(""));
            Assert.False(PathSafety.LooksLikeLineageClient("   "));
        }

        [Fact]
        public void IsProtectedSystemPath_ProgramFiles_True()
        {
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            Assert.True(PathSafety.IsProtectedSystemPath(Path.Combine(pf, "L2Titan")));
        }

        [Fact]
        public void IsProtectedSystemPath_NormalFolder_False()
        {
            Assert.False(PathSafety.IsProtectedSystemPath(@"C:\Juegos\Lineage2"));
        }

        private static string NewTempDir() =>
            Path.Combine(Path.GetTempPath(), "l2test_" + Guid.NewGuid().ToString("N"));

        private static void SafeDelete(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }
}
