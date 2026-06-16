using System;

namespace L2TitanLauncher.Services
{
    internal enum UpdateErrorKind { Connection, Permission, Generic }

    // Clasifica errores de actualización por el contenido del mensaje. La heurística por
    // substrings es frágil ante cambios de redacción del runtime, pero solo decide el TEXTO
    // de estado mostrado al usuario en el catch genérico (tras los LauncherError tipados),
    // no el flujo de control. Aislado aquí para poder testearlo.
    internal static class ErrorClassifier
    {
        public static UpdateErrorKind Classify(Exception ex)
        {
            var m = ex.Message.ToLowerInvariant();
            if (m.Contains("could not connect") || m.Contains("connection"))
                return UpdateErrorKind.Connection;
            if (m.Contains("access to the path") || m.Contains("access is denied") || m.Contains("no write permission"))
                return UpdateErrorKind.Permission;
            return UpdateErrorKind.Generic;
        }
    }
}
