using System;
using System.Text;
using L2TitanLauncher.Services;
using Xunit;

namespace L2TitanLauncher.Tests
{
    public class ManifestSecurityTests
    {
        // Mensaje fijo y su firma RSA válida (hecha con la clave privada que corresponde
        // a la pública embebida en ManifestSecurity). Generado con:
        //   printf '%s' 'L2TITAN-TEST-VECTOR-v1' | openssl dgst -sha256 -sign keys/manifest_private.pem | openssl base64 -A
        private const string Msg = "L2TITAN-TEST-VECTOR-v1";
        private const string ValidSig =
            "IVV7Lnkwfflbybazb37Dwx9hHvhnYXu60jTOmkMWuMqBq9+aVPX2GhhzdKqg9r4n8nrPRK4OGW3nUwLjixNwQ41ySqoaSP4tKSEFIhyg32mWVeYlPX/fxSgjKm3Y74E+Og0kPI5Gzn+xOUZ1zbdJrLulSgFSDI+iZUIbCd9wGOV4KK8t5ilSPM0rW39A83Zl8agCv6Qnzn2pRHGt1mSmp1WB7IaKRKRzKReG3WZ91YALf+rNR/xLK7T26To4PjWFdF8i69vU4swUnWBVoC73BMZhKoltmGkVjNoq0GfW8cVwxtLAZSBqDuiQFuYgd7hV1mwQOI09H6PMrSQ8/EuP/ttuSGNYTGU7fsaas+5Zl2QYoDR4+l/zOJNSsZmgLwTKdIu4eqfB8Dr5fMMLHxy5/HNppBHB2X/0xaIro4vXMxLZOOeJRympmjskj+DlT3Z9S7EJ2YxeifhtKQhOswGj7n520EnrV6cj+K1Zer98/PGZiz4xAl3LjEejzGdy8t8E";

        [Fact]
        public void Verify_ValidSignature_True()
        {
            Assert.True(ManifestSecurity.Verify(Encoding.UTF8.GetBytes(Msg), ValidSig));
        }

        [Fact]
        public void Verify_TamperedContent_False()
        {
            // Un solo byte distinto debe invalidar la firma.
            Assert.False(ManifestSecurity.Verify(Encoding.UTF8.GetBytes(Msg + "X"), ValidSig));
        }

        [Fact]
        public void Verify_GarbageSignature_False()
        {
            Assert.False(ManifestSecurity.Verify(Encoding.UTF8.GetBytes(Msg), "no-es-base64-!!!"));
        }

        [Fact]
        public void Verify_ValidBase64ButWrongSignature_False()
        {
            // 384 bytes (longitud de firma RSA-3072) pero todo ceros: base64 válido, firma falsa.
            Assert.False(ManifestSecurity.Verify(Encoding.UTF8.GetBytes(Msg), Convert.ToBase64String(new byte[384])));
        }

        [Fact]
        public void Verify_EmptyOrNullInputs_False()
        {
            Assert.False(ManifestSecurity.Verify(Array.Empty<byte>(), ValidSig));
            Assert.False(ManifestSecurity.Verify(Encoding.UTF8.GetBytes(Msg), ""));
            Assert.False(ManifestSecurity.Verify(null!, ValidSig));
        }
    }
}
