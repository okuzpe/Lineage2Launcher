using System;
using System.IO;
using L2TitanLauncher.Services;
using Xunit;

namespace L2TitanLauncher.Tests
{
    public class ConfigServiceTests
    {
        [Theory]
        [InlineData("http://evil.example")]
        [InlineData("ftp://x")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void EnforceHttps_NonHttps_ReturnsDefault(string? url)
        {
            const string def = "https://downloads.l2-titan.com";
            Assert.Equal(def, ConfigService.EnforceHttps(url, def));
        }

        [Fact]
        public void EnforceHttps_Https_IsKept()
        {
            Assert.Equal("https://ok.com/m.json",
                ConfigService.EnforceHttps("https://ok.com/m.json", "https://def"));
        }

        [Fact]
        public void EnforceHttps_SchemeIsCaseInsensitive()
        {
            Assert.Equal("HTTPS://OK.com", ConfigService.EnforceHttps("HTTPS://OK.com", "https://def"));
        }

        [Fact]
        public void ResolveGamePath_ExeDirIsClient_ReturnsExeDir()
        {
            var exe = NewClientDir();
            try { Assert.Equal(exe, ConfigService.ResolveGamePath(@"C:\whatever", exe)); }
            finally { SafeDelete(exe); }
        }

        [Fact]
        public void ResolveGamePath_ConfiguredIsClient_ExeIsNot_ReturnsConfigured()
        {
            var configured = NewClientDir();
            var exe = NewTempDir();
            Directory.CreateDirectory(exe);
            try { Assert.Equal(configured, ConfigService.ResolveGamePath(configured, exe)); }
            finally { SafeDelete(configured); SafeDelete(exe); }
        }

        [Fact]
        public void ResolveGamePath_NeitherIsClient_ReturnsDefaultInstallPath()
        {
            var exe = NewTempDir();
            Directory.CreateDirectory(exe);
            try { Assert.Equal(ConfigService.DefaultInstallPath, ConfigService.ResolveGamePath(@"C:\nope", exe)); }
            finally { SafeDelete(exe); }
        }

        private static string NewTempDir() =>
            Path.Combine(Path.GetTempPath(), "l2cfg_" + Guid.NewGuid().ToString("N"));

        private static string NewClientDir()
        {
            var dir = NewTempDir();
            Directory.CreateDirectory(Path.Combine(dir, "system"));
            File.WriteAllText(Path.Combine(dir, "system", "L2.exe"), "stub");
            return dir;
        }

        private static void SafeDelete(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }
}
