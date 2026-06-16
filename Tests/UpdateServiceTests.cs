using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using L2TitanLauncher.Services;
using Xunit;

namespace L2TitanLauncher.Tests
{
    // Tests de la lógica más crítica (verificación + descarga). Usan vectores de manifiesto
    // FIRMADOS con la clave privada real (keys/manifest_private.pem) para ejercitar el gate
    // de firma de ManifestSecurity de extremo a extremo, sin red (HttpMessageHandler stub).
    // Las rutas usan '/' a propósito para que los bytes del JSON coincidan exactamente con
    // lo firturado por openssl (sin escapes de backslash).
    public class UpdateServiceTests
    {
        private const string ServerUrl = "https://test.local";
        private const string ManifestUrl = "https://test.local/manifest.json";
        private const string SigUrl = "https://test.local/manifest.json.sig";
        private const string FileUrl = "https://test.local/system/L2.exe";

        // Manifiesto válido: un archivo system/L2.exe con SHA-256 de "abc" (3 bytes).
        private const string M_VALID = @"[{""Path"":""system/L2.exe"",""Hash"":""ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"",""Size"":3}]";
        private const string SIG_VALID = "btLoLO/JQDbrXYY38A26nnrPpTLSsRRQaIUMEG1tsv74BUxEFkdkomEKo5h5QcXPhHVc3SGgDJ4AamdkWvCQcGA0aNlijfvlf8IbY3+FWVGYlGpUZdwp31zeRlBC0PfBo6UHcKRaBMaDUdrbQNAd2YX7ueTLftU1cHhIM2j9qH4zhoOecRlJPAbH9mfQp8rwTmdCH+DnJhdxGT0bz2MKSN5ltc15LVoC0ADWgKAreSSf+IFhkRomE0KcITHXtXAoUxTO60JrLCTfzhcYHQEYfzSe84oVMqi+k/GNJXp3aJv8+Er1DXCXjZy5KkhgySCTy2rbw+voxoUhhyeVYn/3WAx4mS0pHRHy2vFdHRQUF9vIm1P7ypdvvDo81fQVRwlO29r7II8xspor/kGRftgkO65P5WBn3DCalg0dzxjuGRHRAEhE0Y4f1Zeacso17kSn1JFUj7G8rqIYXab04GbPxdZPSze35783/rVAjB4zx1w7IxAxfoYF8Sd9W1/aYDx8";

        private const string M_EMPTY = "[]";
        private const string SIG_EMPTY = "hb6G/pD7FUM3K50hAGLeJJLHl5rXgxymbLRiBL1ZK9kAjDR8e39110Y8zhJxSONsmLcu+XS3NP6pFw1WD6U4WmwccypYqVtstEjgrcbrbFr5KvuJef9e3Bd4QmAkDLkwvylGpB6uqvjwW+W/QugzjB//E+x0IOFBOJDz8Lge4tvY4oaZTZwgWkZnqQNhbnvIfExl7/luXZoEi73dDuiUY4SJooPuWJ7CUXWVrJytbiNeTobPA69A2WBEDtkS0eKL/uO02soK6A4M/5ECa0DRWnFsC6goOQB2cC7krnsRhyxaPoMNqu+N6Us02yh9bPZ3o5Mwzcb4VOh1nkKhVeWvXsBhqugE7PqvqFbeiWc+tx0KKv4dzf+h/vnqZhIQNRJL1jrHVf76yVeGpAmNhgNQSGa7B6oREACR+6B7j7F7KomrKd4DvxhTvNbGH/YngE8m53DC0/rHfqWtyU4AellF6PgYioQhebw2DdZqDaehrc+ipLv3eweQSjd1KhLUa3N9";

        private const string M_CORRUPT = "not-json";
        private const string SIG_CORRUPT = "aitcFtY6Na2fviJBteqzftnJraVaHLvsaFQpjahq6ah7h2lqZm/+W0dmhKNYxwFH8xhw62F4wF+diCKlDLlIc6KzvtwEIIa+UP8JNTKTmSTLm2/zvEVrQIUDTQKAtUaEyeqx2vwTCETV/IMMWDnTqbxhq8T5D6aJSEIjj6hLE0Voxb5u4R+ohgOP1Oe4Fgr1srBoeNGMM8QeFSVkyZUaNO8KW+kkE7PKyKCXF0sOQzol7gGl2I37iS2Sk6/kbk+j9yvUYU1DEIhkQnMjOUBtPAtbkxo1wQKTbexDjftYcjtc31YWCEktXQHuDYrhLFGlb5BKA55E6Lz2f5VNzlVBSwpVMilxz7nlOGxiPrmt/sVtbDLgprXCUqvsyzwvlsQ8YWjPsY07U5x7MwJ1GX7ow0EpuqdGcD4TAC2EydlKUKkRE5pSXAkZcbUfDMLWviIOccq1gY5u2y/rTPAHoVAW0dNZBarJ0Y1Rmn1D2wbRgwmVHGDkedLbodMfMfh4Rydn";

        // --- happy path: manifiesto válido y el archivo local ya coincide -> sin descarga ---
        [Fact]
        public async Task RunAsync_ManifiestoValido_ArchivoYaCoincide_NoDescarga()
        {
            var dir = NewDir();
            try
            {
                Directory.CreateDirectory(Path.Combine(dir, "system"));
                File.WriteAllText(Path.Combine(dir, "system", "L2.exe"), "abc");
                var stub = Stub(new Dictionary<string, byte[]> { [ManifestUrl] = U(M_VALID), [SigUrl] = U(SIG_VALID) });
                var host = new FakeHost();

                await new UpdateService(new HttpClient(stub)).RunAsync(dir, ServerUrl, ManifestUrl, host);

                Assert.Equal(100, host.Progress);
                Assert.DoesNotContain(FileUrl, stub.Requests); // no se intentó descargar el archivo
            }
            finally { Del(dir); }
        }

        // --- firma inválida -> LauncherError y no se descarga nada ---
        [Fact]
        public async Task RunAsync_FirmaInvalida_LanzaLauncherError_SinDescarga()
        {
            var dir = NewDir();
            try
            {
                // SIG_EMPTY es base64 válido pero NO firma a M_VALID -> Verify devuelve false.
                var stub = Stub(new Dictionary<string, byte[]> { [ManifestUrl] = U(M_VALID), [SigUrl] = U(SIG_EMPTY) });
                var host = new FakeHost();

                await Assert.ThrowsAsync<LauncherError>(() =>
                    new UpdateService(new HttpClient(stub)).RunAsync(dir, ServerUrl, ManifestUrl, host));

                Assert.DoesNotContain(FileUrl, stub.Requests);
            }
            finally { Del(dir); }
        }

        // --- JSON corrupto (pero correctamente firmado) -> LauncherError ---
        [Fact]
        public async Task RunAsync_JsonCorrupto_LanzaLauncherError()
        {
            var dir = NewDir();
            try
            {
                var stub = Stub(new Dictionary<string, byte[]> { [ManifestUrl] = U(M_CORRUPT), [SigUrl] = U(SIG_CORRUPT) });
                await Assert.ThrowsAsync<LauncherError>(() =>
                    new UpdateService(new HttpClient(stub)).RunAsync(dir, ServerUrl, ManifestUrl, new FakeHost()));
            }
            finally { Del(dir); }
        }

        // --- manifiesto vacío [] (firmado) -> LauncherError ---
        [Fact]
        public async Task RunAsync_ManifiestoVacio_LanzaLauncherError()
        {
            var dir = NewDir();
            try
            {
                var stub = Stub(new Dictionary<string, byte[]> { [ManifestUrl] = U(M_EMPTY), [SigUrl] = U(SIG_EMPTY) });
                await Assert.ThrowsAsync<LauncherError>(() =>
                    new UpdateService(new HttpClient(stub)).RunAsync(dir, ServerUrl, ManifestUrl, new FakeHost()));
            }
            finally { Del(dir); }
        }

        // --- archivo faltante -> lo descarga, verifica el hash y lo mueve a su sitio ---
        [Fact]
        public async Task RunAsync_ArchivoFaltante_DescargaVerificaYMueve()
        {
            var dir = NewDir();
            try
            {
                var stub = Stub(new Dictionary<string, byte[]> { [ManifestUrl] = U(M_VALID), [SigUrl] = U(SIG_VALID), [FileUrl] = U("abc") });
                var host = new FakeHost();

                await new UpdateService(new HttpClient(stub)).RunAsync(dir, ServerUrl, ManifestUrl, host);

                var dest = Path.Combine(dir, "system", "L2.exe");
                Assert.True(host.DownloadingStarted);
                Assert.True(File.Exists(dest));
                Assert.Equal("abc", File.ReadAllText(dest));
                Assert.False(File.Exists(dest + ".part")); // .part movido/limpiado
                Assert.Equal(100, host.Progress);
            }
            finally { Del(dir); }
        }

        // --- el servidor entrega bytes con hash equivocado -> LauncherError y .part limpiado ---
        [Fact]
        public async Task RunAsync_HashDescargadoNoCoincide_LanzaLauncherError_LimpiaPart()
        {
            var dir = NewDir();
            try
            {
                var stub = Stub(new Dictionary<string, byte[]> { [ManifestUrl] = U(M_VALID), [SigUrl] = U(SIG_VALID), [FileUrl] = U("xyz") });

                await Assert.ThrowsAsync<LauncherError>(() =>
                    new UpdateService(new HttpClient(stub)).RunAsync(dir, ServerUrl, ManifestUrl, new FakeHost()));

                var dest = Path.Combine(dir, "system", "L2.exe");
                Assert.False(File.Exists(dest));            // no se movió contenido corrupto
                Assert.False(File.Exists(dest + ".part"));  // .part limpiado en el fallo
            }
            finally { Del(dir); }
        }

        // === helpers ===
        private static byte[] U(string s) => Encoding.UTF8.GetBytes(s);
        private static StubHandler Stub(Dictionary<string, byte[]> routes) => new StubHandler(routes);
        private static string NewDir() => Path.Combine(Path.GetTempPath(), "l2upd_" + Guid.NewGuid().ToString("N"));
        private static void Del(string dir) { try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { } }

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Dictionary<string, byte[]> _routes;
            public readonly List<string> Requests = new();
            public StubHandler(Dictionary<string, byte[]> routes) { _routes = routes; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var url = request.RequestUri!.ToString();
                Requests.Add(url);
                if (_routes.TryGetValue(url, out var body))
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new ByteArrayContent(Array.Empty<byte>()) });
            }
        }

        private sealed class FakeHost : IUpdateHost
        {
            public int Progress;
            public string Status = "";
            public bool DownloadingStarted;
            public readonly List<string> Logs = new();
            private readonly CancellationTokenSource _cts = new();
            public void Log(string message) => Logs.Add(message);
            public void SetProgress(int percent) => Progress = percent;
            public void SetStatus(string status) => Status = status;
            public void OnDownloadingStarted() => DownloadingStarted = true;
            public bool IsPaused => false;
            public CancellationToken Token => _cts.Token;
        }
    }
}
