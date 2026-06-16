using System;
using System.Security.Cryptography;

namespace L2TitanLauncher.ViewModels
{
    // Verificación de AUTENTICIDAD del manifiesto.
    //
    // El manifest.json viaja por el MISMO canal que los binarios, así que el SHA-256
    // por sí solo solo prueba "lo descargado == lo que dice el manifiesto", no que el
    // manifiesto sea legítimo. Si comprometen el servidor (VPS) o el canal TLS (CA
    // maliciosa en el equipo del usuario, MITM), podrían servir un manifiesto + .exe
    // falsos con hashes coherentes y el launcher los ejecutaría como administrador.
    //
    // Solución: el manifiesto se acompaña de una firma RSA (manifest.json.sig, en
    // base64) hecha con una clave privada que NUNCA está en el servidor. Aquí va solo
    // la clave PÚBLICA, embebida en el binario. El launcher verifica la firma ANTES de
    // confiar en el manifiesto; sin la clave privada, nadie puede falsificar uno válido.
    //
    // La clave privada vive solo en la máquina de despliegue (carpeta keys/, ignorada
    // por git). Para re-firmar tras regenerar el manifiesto: ./sign-manifest.sh
    internal static class ManifestSecurity
    {
        private const string PublicKeyPem = @"-----BEGIN PUBLIC KEY-----
MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEAtQizNNLo2JOpR/P44Jw/
VmWUkfxpv8ZsWrgkwxrezTO+06ny630fEjg/UI0XbejZFE1cIMZ6T/EfUj6afFoF
OoNP0I+clu4dA5qknR1y2jwhH3CyEuLtlgWXerQ69trLYRlsBqvCntCTsib24xl/
62Kj2gQFhFoi9WldVtJWX7aQKOkKdSBKZW2zKqOtV3CT94ckVpsjrXaf/cSIvGHF
qieykird2YcAQwIZBFLIRLOAHpUpgX4gdO7dURAYgxp0o+W474gZbbVOlRfgNPbv
Ag2m0CKwOxpMnB5H1SDJJlAByEHAEJjp2smsiah8+cgG91na8k2BroNNGEV42qxt
pqBVnJD+aZhwJrJIfNrFYZ7hmfcyQ77EEoaazN/KkZ1jaabvRUum9K/IOVZKATWo
FUX2AtfBc6lTMYTH0POtidHXq/UAo41rW7Fb0REsLqPQSyWcVE66dExykve/zuS5
GA/tPpjzZu7S1t32jego22HlI+Xh0SQ5wCTOi/ReYD1pAgMBAAE=
-----END PUBLIC KEY-----";

        // Verifica una firma RSA PKCS#1 v1.5 sobre SHA-256 (compatible con
        // `openssl dgst -sha256 -sign`). Devuelve false ante cualquier problema.
        public static bool Verify(byte[] manifestBytes, string signatureBase64)
        {
            if (manifestBytes == null || manifestBytes.Length == 0 || string.IsNullOrWhiteSpace(signatureBase64))
                return false;

            byte[] signature;
            try
            {
                signature = Convert.FromBase64String(signatureBase64.Trim());
            }
            catch
            {
                return false;
            }

            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(PublicKeyPem);
                return rsa.VerifyData(manifestBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch
            {
                return false;
            }
        }
    }
}
