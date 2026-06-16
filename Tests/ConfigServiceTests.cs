using System;
using System.Collections.Generic;
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

        [Fact]
        public void ResolveFrom_UrlHttp_SeRechazaADefault_YSeLoguea()
        {
            var dir = NewTempDir();
            Directory.CreateDirectory(dir);
            var cfg = Path.Combine(dir, "config.json");
            File.WriteAllText(cfg, @"{""ServerUrl"":""http://evil.example"",""ManifestUrl"":""http://evil.example/m.json""}");
            var logs = new List<string>();
            try
            {
                var r = new ConfigService().ResolveFrom(cfg, dir, logs.Add);
                Assert.Equal(ConfigService.DefaultServerUrl, r.ServerUrl);
                Assert.StartsWith("https://", r.ManifestUrl);
                Assert.Contains(logs, l => l.Contains("Insecure ServerUrl"));
            }
            finally { SafeDelete(dir); }
        }

        [Fact]
        public void ResolveFrom_JsonCorrupto_NoLanza_UsaDefaults()
        {
            var dir = NewTempDir();
            Directory.CreateDirectory(dir);
            var cfg = Path.Combine(dir, "config.json");
            File.WriteAllText(cfg, "{ esto no es json válido ");
            var logs = new List<string>();
            try
            {
                var r = new ConfigService().ResolveFrom(cfg, dir, logs.Add);
                Assert.Equal(ConfigService.DefaultServerUrl, r.ServerUrl);
                Assert.Equal(ConfigService.DefaultInstallPath, r.GamePath);
            }
            finally { SafeDelete(dir); }
        }

        [Fact]
        public void ResolveFrom_SinConfig_DevuelveDefaults()
        {
            var dir = NewTempDir();
            Directory.CreateDirectory(dir);
            var logs = new List<string>();
            try
            {
                var r = new ConfigService().ResolveFrom(null, dir, logs.Add);
                Assert.Equal(ConfigService.DefaultServerUrl, r.ServerUrl);
                Assert.Equal(ConfigService.DefaultInstallPath, r.GamePath);
            }
            finally { SafeDelete(dir); }
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
