using System;

namespace L2TitanLauncher.Services
{
    // Configuración persistida en config.json.
    public class LauncherConfig
    {
        public string GamePath { get; set; } = string.Empty;
        public string ServerUrl { get; set; } = string.Empty;
        public string ManifestUrl { get; set; } = string.Empty;
        public string GameExecutable { get; set; } = string.Empty;
        public string GameParameters { get; set; } = string.Empty;
    }

    // Entrada del manifiesto del servidor (Path en estilo Windows, Hash SHA-256 hex).
    public class FileManifest
    {
        public string Path { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public long Size { get; set; }
    }

    // Resultado de resolver la configuración (rutas/URLs ya validadas).
    public sealed class ResolvedConfig
    {
        public string GamePath { get; set; } = string.Empty;
        public string ServerUrl { get; set; } = string.Empty;
        public string ManifestUrl { get; set; } = string.Empty;
    }

    // Error de cara al usuario: su Message está pensado para mostrarse tal cual en
    // la barra de estado, en lugar de colapsarse en el texto genérico de fallo.
    internal class LauncherError : Exception
    {
        public LauncherError(string message) : base(message) { }
    }

    // Distingue un fallo de verificación de contenido/hash de un error de red transitorio.
    internal class HashMismatchException : Exception { }
}
