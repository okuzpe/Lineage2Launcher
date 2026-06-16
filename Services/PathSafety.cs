using System;
using System.IO;

namespace L2TitanLauncher.Services
{
    // Utilidades de seguridad de rutas. Aisladas y SIN dependencias de WPF para poder
    // testearse (la verificación anti path-traversal es crítica para la seguridad).
    internal static class PathSafety
    {
        // Resuelve una ruta relativa del manifiesto a una ruta absoluta DENTRO de gamePath.
        // Rechaza rutas rooteadas/absolutas y traversal ".." para que un manifiesto malicioso
        // no pueda escribir fuera del directorio de instalación (escritura arbitraria de archivos).
        public static string ResolveSafePath(string gamePath, string relativePath)
        {
            var baseDir = Path.GetFullPath(gamePath);
            if (!baseDir.EndsWith(Path.DirectorySeparatorChar))
                baseDir += Path.DirectorySeparatorChar;

            var full = Path.GetFullPath(Path.Combine(baseDir, relativePath));
            if (Path.IsPathRooted(relativePath) ||
                !full.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new PathTraversalException($"Manifest path escapes install directory: {relativePath}");
            }
            return full;
        }

        // Heurística: una carpeta "parece" un cliente de Lineage 2 si tiene system\L2.exe.
        public static bool LooksLikeLineageClient(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                string fullPath = Path.GetFullPath(path);
                string systemDir = Path.Combine(fullPath, "system");
                string l2Exe = Path.Combine(systemDir, "L2.exe");
                return Directory.Exists(systemDir) && File.Exists(l2Exe);
            }
            catch
            {
                return false;
            }
        }

        // True si la ruta cae dentro de Program Files / Windows (descargas pueden fallar sin admin).
        public static bool IsProtectedSystemPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch
            {
                return false;
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            return fullPath.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase) ||
                   fullPath.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase) ||
                   fullPath.StartsWith(windowsDir, StringComparison.OrdinalIgnoreCase);
        }
    }

    // Lanzada cuando una ruta del manifiesto intenta escapar del directorio del juego
    // (traversal, absoluta o hermano-con-prefijo-común). Tipo dedicado para tests claros.
    internal sealed class PathTraversalException : Exception
    {
        public PathTraversalException(string message) : base(message) { }
    }
}
