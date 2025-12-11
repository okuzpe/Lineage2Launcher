# Script para ejecutar el launcher en modo desarrollo
# Configura UseNewLauncherUI=true y ejecuta dotnet run

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Lineage2Launcher - Desarrollo" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# Verificar que estamos en el directorio correcto
if (-not (Test-Path "Lineage2Launcher.csproj")) {
    Write-Host "Error: No se encontró Lineage2Launcher.csproj" -ForegroundColor Red
    Write-Host "Ejecuta este script desde la raíz del proyecto." -ForegroundColor Yellow
    exit 1
}

Write-Host "✓ Proyecto encontrado" -ForegroundColor Green
Write-Host ""
Write-Host "Configuración:" -ForegroundColor Yellow
Write-Host "  → UseNewLauncherUI: true (WPF activado)" -ForegroundColor White
Write-Host "  → Target Framework: .NET 10.0" -ForegroundColor White
Write-Host ""

# Ejecutar dotnet run
Write-Host "Iniciando launcher..." -ForegroundColor Cyan
Write-Host ""

dotnet run -c Debug


