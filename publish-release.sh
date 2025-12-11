#!/bin/bash
# Script para publicar el launcher en modo Release con todos los assets
# Genera un EXE único con todo embebido

echo "========================================"
echo "  Lineage 2 Launcher - Publish Release"
echo "========================================"
echo ""

echo "Limpiando publicaciones anteriores..."
rm -rf bin/Release/publish
rm -rf bin/Release/publish-clean

echo ""
echo "Compilando y publicando..."
echo ""

dotnet publish -c Release \
    -p:PublishSingleFile=true \
    -p:SelfContained=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -r win-x64 \
    --output bin/Release/publish

if [ $? -ne 0 ]; then
    echo ""
    echo "ERROR: La compilación falló!"
    exit 1
fi

echo ""
echo "========================================"
echo "  Publicación completada exitosamente!"
echo "========================================"
echo ""
echo "Ubicación: bin/Release/publish/"
echo ""
echo "Archivos generados:"
ls -lh bin/Release/publish/Lineage2Launcher.exe
echo ""
echo "Assets incluidos:"
echo "  - Assets/images: $(ls -1 bin/Release/publish/Assets/images/*.png 2>/dev/null | wc -l) PNG files"
echo "  - Assets/ui: $(ls -1 bin/Release/publish/Assets/ui/*.png 2>/dev/null | wc -l) PNG files"
echo "  - Fonts: $(ls -1 bin/Release/publish/Fonts/*.ttf 2>/dev/null | wc -l) TTF files"
echo ""
echo "Para ejecutar:"
echo "  cd bin/Release/publish"
echo "  ./Lineage2Launcher.exe"
echo ""

