#!/bin/bash
# Script para ejecutar el launcher en modo desarrollo
# Configura UseNewLauncherUI=true y ejecuta dotnet run

echo "============================================================"
echo "  Lineage2Launcher - Desarrollo"
echo "============================================================"
echo ""

# Verificar que estamos en el directorio correcto
if [ ! -f "Lineage2Launcher.csproj" ]; then
    echo "Error: No se encontró Lineage2Launcher.csproj"
    echo "Ejecuta este script desde la raíz del proyecto."
    exit 1
fi

echo "✓ Proyecto encontrado"
echo ""
echo "Configuración:"
echo "  → UseNewLauncherUI: true (WPF activado)"
echo "  → Target Framework: .NET 10.0"
echo ""

# Ejecutar dotnet run
echo "Iniciando launcher..."
echo ""

dotnet run -c Debug



