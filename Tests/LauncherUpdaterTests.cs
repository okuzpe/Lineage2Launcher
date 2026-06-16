using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using L2TitanLauncher.Services;
using Xunit;

namespace L2TitanLauncher.Tests
{
    public class LauncherUpdaterTests
    {
        private const string InfoUrl = "https://test.local/launcher.json";
        private const string SigUrl = "https://test.local/launcher.json.sig";

        // launcher.json firmado de prueba (versión 1.0.0). Generado con la clave privada real.
        private const string LJ = @"{""Version"":""1.0.0"",""Url"":""https://test.local/launcher/x.exe"",""Sha256"":""aa"",""Size"":1}";
        private const string SIG_LJ =
            "JhXV6CQLFcQ5n4uzxon4kLW90dZryRMJAbpBMrEMV0CwiFKHQ6vfwR0Dd2IYxbppuP0MkBzjObruvpBA4g4a/LWEcGedQHW0JILRiHVCl0uGwasBCqrOkolWfQ3LCfZTemYluj/uoONfBPc5WNlb22EdxWoFxpPlee1lA7QF8xRr1si9Ae1/StUYKlBvP6ddGFERT1hOEsK0T6D/6OcbJTgjhi94P7TwQJvoTeT/m6A5bqoYHxbfcxd9rOC/tnmbmwyGQkaTvJP7Xvj1AdtlybuHUy6GMV/+OCNYcF0pMGe5Y0aVkJHfvyq+z7ZgurYPcSX0GjtCSCfc3JVa3DGJ+eos9ubLhtGRA3LjtpauQ23r+lfoUSZyX810Z88CD66wq+VoJbWkcHQFCcoGkGkFZaWJk7oVwTX7IUB40uTz9I7yzqXXlRokPfKO5aEnc7w73RxzBa2ahUTig3U9MnfuJ416NnOsB9fAmX9j0+JGYgZg1oVWOZrGDq/el74Qd/S7";

        [Theory]
        [InlineData("1.0.1", true)]
        [InlineData("2.0.0", true)]
        [InlineData("1.0.0", false)]   // igual -> no actualizar
        [InlineData("0.9.9", false)]   // menor -> no actualizar
        [InlineData("no-version", false)]
        [InlineData(null, false)]
        public void IsNewer_CompareConVersionActual(string? serverVersion, bool expected)
        {
            Assert.Equal(expected, LauncherUpdater.IsNewer(serverVersion, new Version(1, 0, 0)));
        }

        [Fact]
        public async Task CheckAndUpdate_FirmaValida_MismaVersion_NoActualiza()
        {
            var stub = Stub(new Dictionary<string, byte[]> { [InfoUrl] = U(LJ), [SigUrl] = U(SIG_LJ) });
            var updated = await new LauncherUpdater(new HttpClient(stub))
                .CheckAndUpdateAsync(InfoUrl, new Version(1, 0, 0), _ => { }, CancellationToken.None);
            Assert.False(updated); // versión igual -> no se descarga ni se reemplaza nada
        }

        [Fact]
        public async Task CheckAndUpdate_FirmaInvalida_NoActualiza()
        {
            // Firma que no corresponde al contenido -> Verify falla -> no actualizar.
            var stub = Stub(new Dictionary<string, byte[]> { [InfoUrl] = U(LJ), [SigUrl] = U(Convert.ToBase64String(new byte[384])) });
            var updated = await new LauncherUpdater(new HttpClient(stub))
                .CheckAndUpdateAsync(InfoUrl, new Version(0, 9, 0), _ => { }, CancellationToken.None);
            Assert.False(updated);
        }

        [Fact]
        public async Task CheckAndUpdate_SinLauncherJson_NoActualiza()
        {
            // 404 en launcher.json -> best-effort -> no actualizar (no lanza).
            var stub = Stub(new Dictionary<string, byte[]>());
            var updated = await new LauncherUpdater(new HttpClient(stub))
                .CheckAndUpdateAsync(InfoUrl, new Version(1, 0, 0), _ => { }, CancellationToken.None);
            Assert.False(updated);
        }

        private static byte[] U(string s) => Encoding.UTF8.GetBytes(s);
        private static StubHandler Stub(Dictionary<string, byte[]> routes) => new StubHandler(routes);

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Dictionary<string, byte[]> _routes;
            public StubHandler(Dictionary<string, byte[]> routes) { _routes = routes; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var url = request.RequestUri!.ToString();
                if (_routes.TryGetValue(url, out var body))
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new ByteArrayContent(Array.Empty<byte>()) });
            }
        }
    }
}
