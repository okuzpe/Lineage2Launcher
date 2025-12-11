@echo off
REM Script para publicar el launcher en modo Release con todos los assets
REM Genera un EXE único con todo embebido

echo ========================================
echo   Lineage 2 Launcher - Publish Release
echo ========================================
echo.

echo Limpiando publicaciones anteriores...
if exist "bin\Release\publish" rmdir /s /q "bin\Release\publish"
if exist "bin\Release\publish-clean" rmdir /s /q "bin\Release\publish-clean"

echo.
echo Compilando y publicando...
echo.

dotnet publish -c Release ^
    -p:PublishSingleFile=true ^
    -p:SelfContained=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -r win-x64 ^
    --output bin\Release\publish

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: La compilacion fallo!
    pause
    exit /b 1
)

echo.
echo ========================================
echo   Publicacion completada exitosamente!
echo ========================================
echo.
echo Ubicacion: bin\Release\publish\
echo.
echo Archivos generados:
dir /b bin\Release\publish\Lineage2Launcher.exe
echo.
echo Assets incluidos:
dir /b /s bin\Release\publish\Assets\images\*.png 2>nul | find /c ".png" >nul && echo   - Assets/images: OK || echo   - Assets/images: ERROR
dir /b /s bin\Release\publish\Fonts\*.ttf 2>nul | find /c ".ttf" >nul && echo   - Fonts: OK || echo   - Fonts: ERROR
echo.
echo Para ejecutar:
echo   cd bin\Release\publish
echo   .\Lineage2Launcher.exe
echo.
pause

