#!/bin/bash
# ⚠️ OBSOLETO — usa ./deploy.sh en su lugar.
# Este script exige zip+Python locales y NO firma el manifiesto (deja manifest.json.sig
# desfasado -> el launcher rechaza la actualización). Se conserva solo como referencia.
# El deploy soportado es ./deploy.sh (firma integrada + backup/rollback + verificación).
#
# Script de Deploy Simple - Paso a Paso
# Genera manifest, comprime, sube y descomprime en el servidor

set -e

# Configuración
GAME_PATH="C:/Juegos/Lineage2"
ZIP_PATH="C:/Juegos/Lineage2.zip"
SERVER_IP="212.227.87.65"  # IP del servidor (para SSH/SCP)
SERVER_URL="https://downloads.l2-titan.com"  # URL pública HTTPS
SERVER_USER="deploy"
SSH_PORT="2222"
REMOTE_TEMP_DIR="/tmp/lineage2-deploy"
REMOTE_ZIP_DIR="/var/www/zip"
DEPLOY_PATH="/var/www/lineage2"
LAUNCHER_EXE_NAME="L2TitanLauncher.exe"
LAUNCHER_EXE_PATH="bin/Release/net10.0-windows/win-x64/publish/${LAUNCHER_EXE_NAME}"

# Archivos/carpetas a preservar durante el deploy (no se borrarán)
# Agrega aquí cualquier archivo o carpeta que quieras mantener
PRESERVE_FILES=(
    ".htaccess"
    "config.php"
    "nginx.conf"
    "logs"
    "backups"
)

# Función para crear ejecutable autoextraíble (SFX)
create_sfx() {
    local launcher_exe="$1"
    local output_sfx="$2"
    
    if [ ! -f "$launcher_exe" ]; then
        return 1
    fi
    
    local launcher_name=$(basename "$launcher_exe")
    
    # Método 1: Usar PowerShell (preferido en Windows)
    if command -v powershell >/dev/null 2>&1 || command -v pwsh >/dev/null 2>&1; then
        print_info "Creando SFX con PowerShell..."
        
        # Crear script PowerShell usando archivo temporal
        local ps_script=$(mktemp)
        cat > "$ps_script" << 'PS_SCRIPT'
param(
    [string]$LauncherExePath,
    [string]$OutputSfxPath,
    [string]$LauncherName
)

$launcherBytes = [System.IO.File]::ReadAllBytes($LauncherExePath)

$batchContent = "@echo off`r`n" +
    "REM Ejecutable autoextraíble L2TitanLauncher`r`n" +
    "echo Extrayendo L2TitanLauncher...`r`n" +
    "echo.`r`n" +
    "set `"SCRIPT_DIR=%~dp0`"`r`n" +
    "set `"EXTRACT_DIR=%SCRIPT_DIR%L2TitanLauncher`"`r`n" +
    "if not exist `"%EXTRACT_DIR%`" mkdir `"%EXTRACT_DIR%`"`r`n" +
    "powershell -ExecutionPolicy Bypass -Command `"`$bytes = [System.IO.File]::ReadAllBytes('%~f0'); `$marker = [System.Text.Encoding]::ASCII.GetBytes('::LAUNCHER_DATA::'); `$markerPos = 0; for (`$i = 0; `$i -lt `$bytes.Length - `$marker.Length; `$i++) { `$found = `$true; for (`$j = 0; `$j -lt `$marker.Length; `$j++) { if (`$bytes[`$i + `$j] -ne `$marker[`$j]) { `$found = `$false; break; } } if (`$found) { `$markerPos = `$i + `$marker.Length; break; } } if (`$markerPos -gt 0) { `$launcherBytes = `$bytes[`$markerPos..(`$bytes.Length-1)]; [System.IO.File]::WriteAllBytes('%EXTRACT_DIR%\\' + '$LauncherName', `$launcherBytes); }`"`r`n" +
    "if exist `"%EXTRACT_DIR%\\$LauncherName`" (`r`n" +
    "    echo Extraccion completada!`r`n" +
    "    echo.`r`n" +
    "    echo Iniciando L2TitanLauncher...`r`n" +
    "    start `"`" `"%EXTRACT_DIR%\\$LauncherName`"`r`n" +
    ") else (`r`n" +
    "    echo ERROR: No se pudo extraer el launcher`r`n" +
    "    pause`r`n" +
    ")`r`n" +
    "exit`r`n" +
    "::LAUNCHER_DATA::`r`n"

$batchBytes = [System.Text.Encoding]::ASCII.GetBytes($batchContent)
$markerBytes = [System.Text.Encoding]::ASCII.GetBytes("::LAUNCHER_DATA::`r`n")

$sfxBytes = New-Object System.Collections.ArrayList
$sfxBytes.AddRange($batchBytes) | Out-Null
$sfxBytes.AddRange($markerBytes) | Out-Null
$sfxBytes.AddRange($launcherBytes) | Out-Null

[System.IO.File]::WriteAllBytes($OutputSfxPath, $sfxBytes)
PS_SCRIPT
        
        powershell -ExecutionPolicy Bypass -File "$ps_script" -LauncherExePath "$launcher_exe" -OutputSfxPath "$output_sfx" -LauncherName "$launcher_name" 2>/dev/null
        local ps_result=$?
        rm -f "$ps_script"
        
        if [ -f "$output_sfx" ]; then
            return 0
        fi
    fi
    
    # Método 2: Usar 7-Zip como alternativa
    if command -v 7z >/dev/null 2>&1; then
        print_info "Creando SFX con 7-Zip..."
        
        local temp_dir=$(mktemp -d)
        trap "rm -rf $temp_dir" RETURN 2>/dev/null || true
        
        cp "$launcher_exe" "$temp_dir/"
        
        cat > "$temp_dir/extract.bat" << 'EXTRACT_BAT'
@echo off
echo Extrayendo L2TitanLauncher...
echo.
set SCRIPT_DIR=%~dp0
set EXTRACT_DIR=%SCRIPT_DIR%L2TitanLauncher
if not exist "%EXTRACT_DIR%" mkdir "%EXTRACT_DIR%"
copy /Y "%~dpnx0" "%EXTRACT_DIR%\L2TitanLauncher.exe" >nul 2>&1
echo Extraccion completada!
echo.
echo Iniciando L2TitanLauncher...
start "" "%EXTRACT_DIR%\L2TitanLauncher.exe"
exit
EXTRACT_BAT
        
        cat > "$temp_dir/config.txt" << 'CONFIG'
;!@Install@!UTF-8!
Title="L2TitanLauncher Installer"
BeginPrompt="¿Desea extraer e instalar L2TitanLauncher?"
ExtractDialogText="Extrayendo L2TitanLauncher..."
ExtractPathText="Extraer a:"
ExtractTitle="Extrayendo..."
GUIMode="1"
;!@InstallEnd@!
CONFIG
        
        cd "$temp_dir"
        7z a -tzip launcher.zip extract.bat "$launcher_name" >/dev/null 2>&1
        
        local sfx_module=""
        if [ -f "/usr/lib/p7zip/7z.sfx" ]; then
            sfx_module="/usr/lib/p7zip/7z.sfx"
        elif [ -f "/usr/lib/7z/7z.sfx" ]; then
            sfx_module="/usr/lib/7z/7z.sfx"
        elif [ -f "C:/Program Files/7-Zip/7z.sfx" ]; then
            sfx_module="C:/Program Files/7-Zip/7z.sfx"
        fi
        
        if [ -n "$sfx_module" ] && [ -f "$sfx_module" ]; then
            cat "$sfx_module" "$temp_dir/config.txt" "$temp_dir/launcher.zip" > "$output_sfx"
            chmod +x "$output_sfx" 2>/dev/null || true
            cd - >/dev/null 2>&1
            rm -rf "$temp_dir"
            return 0
        fi
        
        cd - >/dev/null 2>&1
        rm -rf "$temp_dir"
    fi
    
    return 1
}

# Flags para saltar pasos
SKIP_BUILD=false
SKIP_MANIFEST=false
SKIP_ZIP=false
SKIP_UPLOAD=false
SKIP_EXTRACT=false
TEST_CONNECTION=false
BUILD_ONLY=false

# Parsear argumentos
while [[ $# -gt 0 ]]; do
    case $1 in
        --no-build|--skip-build)
            SKIP_BUILD=true
            shift
            ;;
        --no-manifest|--skip-manifest)
            SKIP_MANIFEST=true
            shift
            ;;
        --no-zip|--skip-zip)
            SKIP_ZIP=true
            shift
            ;;
        --no-upload|--skip-upload)
            SKIP_UPLOAD=true
            shift
            ;;
        --no-extract|--skip-extract)
            SKIP_EXTRACT=true
            shift
            ;;
        --test-connection)
            TEST_CONNECTION=true
            shift
            ;;
        --build-only|--build)
            BUILD_ONLY=true
            shift
            ;;
        -h|--help)
            echo "Uso: $0 [opciones]"
            echo ""
            echo "Opciones:"
            echo "  --no-build, --skip-build          Saltar compilación del launcher"
            echo "  --no-manifest, --skip-manifest    Saltar generación de manifest.json"
            echo "  --no-zip, --skip-zip              Saltar compresión del juego"
            echo "  --no-upload, --skip-upload         Saltar subida al servidor"
            echo "  --no-extract, --skip-extract      Saltar descompresión en el servidor"
            echo "  --test-connection                  Solo probar conexión SSH y estado del servidor"
            echo "  --build-only, --build              Solo compilar el launcher y abrir la carpeta"
            echo "  -h, --help                        Mostrar esta ayuda"
            echo ""
            echo "Ejemplos:"
            echo "  $0                                Ejecutar todos los pasos (incluye build)"
            echo "  $0 --build-only                   Solo compilar y abrir carpeta del EXE"
            echo "  $0 --no-build                     Saltar compilación del launcher"
            echo "  $0 --no-zip                       Saltar compresión (usar ZIP existente)"
            echo "  $0 --no-manifest --no-zip         Saltar manifest y compresión"
            echo "  $0 --test-connection               Verificar conexión y estado del servidor"
            exit 0
            ;;
        *)
            echo "Opción desconocida: $1"
            echo "Usa --help para ver las opciones disponibles"
            exit 1
            ;;
    esac
done

# Colores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

function print_step() {
    echo -e "\n${CYAN}========================================${NC}"
    echo -e "${CYAN}  $1${NC}"
    echo -e "${CYAN}========================================${NC}\n"
}

function print_info() {
    echo -e "${BLUE}ℹ $1${NC}"
}

function print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

function print_error() {
    echo -e "${RED}✗ $1${NC}"
}

function print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

# Convertir ruta de Windows a formato Git Bash
convert_path() {
    local path="$1"
    # Convertir C:\ a /c/
    path=$(echo "$path" | sed 's|^C:|/c|' | sed 's|\\|/|g')
    echo "$path"
}

GAME_PATH_BASH=$(convert_path "$GAME_PATH")
ZIP_PATH_BASH=$(convert_path "$ZIP_PATH")

# Convertir ruta del launcher a absoluta si es relativa
if [[ "$LAUNCHER_EXE_PATH" != /* ]] && [[ "$LAUNCHER_EXE_PATH" != [a-zA-Z]:* ]]; then
    # Es una ruta relativa, convertir a absoluta desde el directorio actual
    SCRIPT_CURRENT_DIR=$(pwd)
    LAUNCHER_EXE_PATH="${SCRIPT_CURRENT_DIR}/${LAUNCHER_EXE_PATH}"
fi
# Convertir a formato bash si es ruta de Windows
LAUNCHER_EXE_PATH=$(convert_path "$LAUNCHER_EXE_PATH")

# Verificar que existe la carpeta del juego
if [ ! -d "$GAME_PATH_BASH" ]; then
    print_error "La carpeta del juego no existe: $GAME_PATH_BASH"
    exit 1
fi

echo -e "\n${CYAN}========================================${NC}"
echo -e "${CYAN}  Script de Deploy Simple${NC}"
echo -e "${CYAN}========================================${NC}\n"
print_info "Ruta del juego: $GAME_PATH_BASH"
print_info "Servidor: $SERVER_USER@$SERVER_IP (puerto SSH: $SSH_PORT)"
print_info "URL pública: $SERVER_URL"
print_info "Ruta de deploy: $DEPLOY_PATH\n"

# --- SSH Configuration ---
SSH_OPTS="-p $SSH_PORT -o ConnectTimeout=10 -o BatchMode=yes"
SCP_OPTS="-P $SSH_PORT -o ConnectTimeout=10 -o BatchMode=yes"

# --- Test Connection Mode ---
if [ "$TEST_CONNECTION" = true ]; then
    print_step "Test de Conexión al Servidor"

    print_info "Probando SSH a $SERVER_USER@$SERVER_IP:$SSH_PORT..."
    if ssh $SSH_OPTS "$SERVER_USER@$SERVER_IP" "echo 'SSH OK'" 2>/dev/null; then
        print_success "Conexión SSH establecida"
    else
        print_error "No se puede conectar por SSH"
        print_info "Verifica:"
        print_info "  - Clave SSH configurada para usuario '$SERVER_USER'"
        print_info "  - Puerto $SSH_PORT accesible"
        print_info "  - Prueba manual: ssh -p $SSH_PORT $SERVER_USER@$SERVER_IP"
        exit 1
    fi

    print_info "Verificando permisos sudo..."
    if ssh $SSH_OPTS "$SERVER_USER@$SERVER_IP" "sudo /bin/mkdir -p /tmp/deploy-test && sudo /bin/rm -rf /tmp/deploy-test && echo 'Sudo OK'" 2>/dev/null; then
        print_success "Permisos sudo correctos"
    else
        print_error "Permisos sudo insuficientes"
        print_info "Ejecuta setup-server-security.sh en el servidor primero"
        exit 1
    fi

    print_info "Verificando nginx..."
    if ssh $SSH_OPTS "$SERVER_USER@$SERVER_IP" "systemctl is-active --quiet nginx && echo 'Nginx OK'" 2>/dev/null; then
        print_success "Nginx está corriendo"
    else
        print_warning "No se pudo verificar nginx (puede requerir permisos adicionales)"
    fi

    print_info "Verificando fail2ban..."
    if ssh $SSH_OPTS "$SERVER_USER@$SERVER_IP" "systemctl is-active --quiet fail2ban && echo 'fail2ban OK'" 2>/dev/null; then
        print_success "fail2ban está corriendo"
    else
        print_warning "fail2ban no está corriendo o no instalado"
    fi

    print_info "Verificando directorios de deploy..."
    ssh $SSH_OPTS "$SERVER_USER@$SERVER_IP" "
        echo 'Directorios:'
        [ -d $DEPLOY_PATH ] && echo '  $DEPLOY_PATH: OK' || echo '  $DEPLOY_PATH: NO EXISTE'
        [ -d $REMOTE_ZIP_DIR ] && echo '  $REMOTE_ZIP_DIR: OK' || echo '  $REMOTE_ZIP_DIR: NO EXISTE'
        echo ''
        echo 'Espacio en disco:'
        df -h / | tail -1 | awk '{print \"  Usado: \" \$3 \" / \" \$2 \" (\" \$5 \" usado)\"}'" 2>/dev/null

    print_info "Verificando URLs públicas..."
    HTTP_STATUS=$(curl -sI --connect-timeout 5 "$SERVER_URL/manifest.json" 2>/dev/null | head -1)
    if echo "$HTTP_STATUS" | grep -q "200"; then
        print_success "manifest.json accesible: $HTTP_STATUS"
    else
        print_warning "manifest.json: $HTTP_STATUS"
    fi

    print_success "Test de conexión completado"
    exit 0
fi

# Paso 0: Compilar Launcher
if [ "$SKIP_BUILD" = false ]; then
    print_step "Paso 0: Compilando Launcher"
    
    print_info "Compilando launcher en modo Release..."
    
    if ! dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true; then
        print_error "Error al compilar el launcher"
        exit 1
    fi
    
    # Verificar que el ejecutable se generó
    if [ ! -f "$LAUNCHER_EXE_PATH" ]; then
        print_error "El ejecutable no se generó en: $LAUNCHER_EXE_PATH"
        exit 1
    fi
    
    LAUNCHER_SIZE=$(du -h "$LAUNCHER_EXE_PATH" | cut -f1)
    print_success "Launcher compilado: $LAUNCHER_EXE_PATH ($LAUNCHER_SIZE)"
    
    # Si es --build-only, limpiar carpeta, abrir y salir
    if [ "$BUILD_ONLY" = true ]; then
        PUBLISH_DIR=$(dirname "$LAUNCHER_EXE_PATH")
        
        # Limpiar archivos que no son el launcher (PDB, ZIP, carpetas del juego)
        rm -f "$PUBLISH_DIR"/*.pdb 2>/dev/null
        rm -f "$PUBLISH_DIR"/*.zip 2>/dev/null
        # Eliminar carpetas del juego que se crearon si el launcher se ejecuto desde aqui
        for dir in "$PUBLISH_DIR"/animations "$PUBLISH_DIR"/system "$PUBLISH_DIR"/textures "$PUBLISH_DIR"/maps "$PUBLISH_DIR"/sounds "$PUBLISH_DIR"/music "$PUBLISH_DIR"/SysTextures "$PUBLISH_DIR"/StaticMeshes; do
            if [ -d "$dir" ]; then
                rm -rf "$dir"
                print_info "Limpiado: $(basename "$dir")/"
            fi
        done
        
        FINAL_SIZE=$(du -h "$LAUNCHER_EXE_PATH" | cut -f1)
        print_success "Build completado! Solo tienes: L2TitanLauncher.exe ($FINAL_SIZE)"
        print_info "Abriendo carpeta..."
        
        # Abrir carpeta en el explorador (Windows/Git Bash)
        if command -v explorer.exe &>/dev/null; then
            explorer.exe "$(cygpath -w "$PUBLISH_DIR")" 2>/dev/null || true
        elif command -v start &>/dev/null; then
            start "" "$PUBLISH_DIR" 2>/dev/null || true
        else
            print_info "Carpeta: $(cd "$PUBLISH_DIR" && pwd)"
        fi
        exit 0
    fi
    
    # Crear ejecutable autoextraíble (reemplaza al EXE original)
    print_info "Creando ejecutable autoextraíble..."
    
    # Guardar el EXE original con un nombre temporal
    LAUNCHER_EXE_ORIGINAL="${LAUNCHER_EXE_PATH}.original"
    cp "$LAUNCHER_EXE_PATH" "$LAUNCHER_EXE_ORIGINAL"
    
    # Intentar crear SFX
    if create_sfx "$LAUNCHER_EXE_ORIGINAL" "$LAUNCHER_EXE_PATH"; then
        SFX_SIZE=$(du -h "$LAUNCHER_EXE_PATH" | cut -f1)
        print_success "Ejecutable autoextraíble creado: $LAUNCHER_EXE_PATH ($SFX_SIZE)"
        rm -f "$LAUNCHER_EXE_ORIGINAL"
    else
        print_warning "No se pudo crear ejecutable autoextraíble, usando EXE normal"
        mv "$LAUNCHER_EXE_ORIGINAL" "$LAUNCHER_EXE_PATH"
    fi
else
    print_info "Saltando compilación del launcher (--no-build)"
    if [ ! -f "$LAUNCHER_EXE_PATH" ]; then
        print_error "El ejecutable no existe: $LAUNCHER_EXE_PATH"
        print_info "Compila primero con: dotnet publish -c Release"
        exit 1
    else
        LAUNCHER_SIZE=$(du -h "$LAUNCHER_EXE_PATH" | cut -f1)
        print_success "Usando launcher existente: $LAUNCHER_EXE_PATH ($LAUNCHER_SIZE)"
        
        # Intentar crear SFX si no existe ya
        print_info "Verificando ejecutable autoextraíble..."
        LAUNCHER_EXE_ORIGINAL="${LAUNCHER_EXE_PATH}.original"
        if [ ! -f "$LAUNCHER_EXE_ORIGINAL" ]; then
            cp "$LAUNCHER_EXE_PATH" "$LAUNCHER_EXE_ORIGINAL"
            if create_sfx "$LAUNCHER_EXE_ORIGINAL" "$LAUNCHER_EXE_PATH"; then
                SFX_SIZE=$(du -h "$LAUNCHER_EXE_PATH" | cut -f1)
                print_success "Ejecutable autoextraíble creado: $LAUNCHER_EXE_PATH ($SFX_SIZE)"
                rm -f "$LAUNCHER_EXE_ORIGINAL"
            else
                print_warning "No se pudo crear ejecutable autoextraíble, usando EXE normal"
                mv "$LAUNCHER_EXE_ORIGINAL" "$LAUNCHER_EXE_PATH"
            fi
        fi
    fi
fi

# Paso 1: Generar manifest.json
if [ "$SKIP_MANIFEST" = false ]; then
    print_step "Paso 1: Generando manifest.json"

    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    MANIFEST_PATH="$SCRIPT_DIR/manifest.json"
    GENERATE_MANIFEST_SCRIPT="$SCRIPT_DIR/generate_manifest.py"

    if [ ! -f "$GENERATE_MANIFEST_SCRIPT" ]; then
        print_error "No se encontró generate_manifest.py en $SCRIPT_DIR"
        exit 1
    fi

    # Detectar Python - intentar varias opciones (probar py primero ya que funciona)
PYTHON_CMD=""

# Función para verificar si un comando de Python funciona
test_python() {
    local cmd="$1"
    if command -v "$cmd" >/dev/null 2>&1; then
        if $cmd --version >/dev/null 2>&1; then
            return 0
        fi
    fi
    return 1
}

# Intentar py primero (Python Launcher de Windows - sabemos que funciona)
if test_python "py"; then
    PYTHON_CMD="py"
# Intentar ruta directa de py en Windows
elif [ -f "/c/WINDOWS/py.exe" ] || [ -f "/c/WINDOWS/py" ]; then
    if /c/WINDOWS/py --version >/dev/null 2>&1; then
        PYTHON_CMD="/c/WINDOWS/py"
    fi
# Intentar python3 (solo si funciona)
elif test_python "python3"; then
    PYTHON_CMD="python3"
# Intentar python (solo si funciona)
elif test_python "python"; then
    PYTHON_CMD="python"
# Intentar python.exe (Windows)
elif test_python "python.exe"; then
    PYTHON_CMD="python.exe"
# Intentar rutas comunes de Windows usando find
elif [ -n "$(find /c/Program\ Files/Python* -name python.exe 2>/dev/null | head -1)" ]; then
    PYTHON_CANDIDATE=$(find /c/Program\ Files/Python* -name python.exe 2>/dev/null | head -1)
    if [ -f "$PYTHON_CANDIDATE" ] && "$PYTHON_CANDIDATE" --version >/dev/null 2>&1; then
        PYTHON_CMD="$PYTHON_CANDIDATE"
    fi
elif [ -n "$(find /c/Users/$USER/AppData/Local/Programs/Python -name python.exe 2>/dev/null | head -1)" ]; then
    PYTHON_CANDIDATE=$(find /c/Users/$USER/AppData/Local/Programs/Python -name python.exe 2>/dev/null | head -1)
    if [ -f "$PYTHON_CANDIDATE" ] && "$PYTHON_CANDIDATE" --version >/dev/null 2>&1; then
        PYTHON_CMD="$PYTHON_CANDIDATE"
    fi
# Intentar con cmd.exe desde Windows
elif cmd.exe /c "where python" >/dev/null 2>&1; then
    if cmd.exe /c "python --version" >/dev/null 2>&1; then
        PYTHON_CMD="cmd.exe /c python"
    fi
fi

if [ -z "$PYTHON_CMD" ]; then
    print_error "Python no está disponible. Necesario para generar manifest.json"
    print_info ""
    print_info "Opciones:"
    print_info "  1. Instala Python desde https://www.python.org/downloads/"
    print_info "  2. Asegúrate de agregar Python al PATH durante la instalación"
    print_info "  3. Verifica que 'py --version' funciona en Git Bash"
    exit 1
fi

    PYTHON_VERSION=$($PYTHON_CMD --version 2>&1)
    print_info "Python encontrado: $PYTHON_CMD ($PYTHON_VERSION)"

    print_info "Generando manifest.json desde $GAME_PATH_BASH..."
    $PYTHON_CMD "$GENERATE_MANIFEST_SCRIPT" "$GAME_PATH_BASH" "$MANIFEST_PATH"

    if [ ! -f "$MANIFEST_PATH" ]; then
        print_error "Error al generar manifest.json"
        exit 1
    fi

    print_success "manifest.json generado: $MANIFEST_PATH"
else
    print_info "Saltando generación de manifest.json (--no-manifest)"
    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    MANIFEST_PATH="$SCRIPT_DIR/manifest.json"
    if [ ! -f "$MANIFEST_PATH" ]; then
        print_warning "manifest.json no existe en el proyecto. Genera uno primero sin --no-manifest."
    else
        print_success "Usando manifest.json existente: $MANIFEST_PATH"
    fi
fi

# Paso 2: Comprimir el juego
if [ "$SKIP_ZIP" = false ]; then
    print_step "Paso 2: Comprimiendo el juego"

    # Eliminar ZIP anterior si existe
    if [ -f "$ZIP_PATH_BASH" ]; then
        print_info "Eliminando ZIP anterior..."
        rm -f "$ZIP_PATH_BASH"
    fi

    print_info "Comprimiendo $GAME_PATH_BASH en $ZIP_PATH_BASH..."
    print_info "Esto puede tardar varios minutos..."

    # Verificar que tenemos zip
    if ! command -v zip >/dev/null 2>&1; then
        print_error "El comando 'zip' no está disponible. Instálalo o usa otro método."
        exit 1
    fi

    # Comprimir excluyendo carpetas innecesarias
    cd "$GAME_PATH_BASH"
    zip -r "$ZIP_PATH_BASH" . \
        -x "logs/*" \
        -x "screenshots/*" \
        -x "temp/*" \
        -x "__pycache__/*" \
        -x "*.log" \
        -x "manifest.json" > /dev/null 2>&1

    if [ ! -f "$ZIP_PATH_BASH" ]; then
        print_error "Error al crear el archivo ZIP"
        exit 1
    fi

    ZIP_SIZE=$(du -h "$ZIP_PATH_BASH" | cut -f1)
    print_success "ZIP creado: $ZIP_PATH_BASH ($ZIP_SIZE)"
else
    print_info "Saltando compresión (--no-zip)"
    if [ ! -f "$ZIP_PATH_BASH" ]; then
        print_error "El archivo ZIP no existe: $ZIP_PATH_BASH"
        print_info "Elimina --no-zip o crea el ZIP manualmente"
        exit 1
    else
        ZIP_SIZE=$(du -h "$ZIP_PATH_BASH" | cut -f1)
        print_success "Usando ZIP existente: $ZIP_PATH_BASH ($ZIP_SIZE)"
    fi
fi

# Paso 3: Subir ZIP al servidor
if [ "$SKIP_UPLOAD" = false ]; then
    print_step "Paso 3: Subiendo ZIP al servidor"

    print_info "Conectando a $SERVER_USER@$SERVER_IP (puerto $SSH_PORT)..."
    print_info "Usando autenticación por clave SSH (sin contraseña)"
    
    # Intentar crear directorio (esto probará la conexión SSH)
    print_info "Probando conexión SSH..."
    
    if ! ssh $SSH_OPTS "$SERVER_USER@$SERVER_IP" "mkdir -p $REMOTE_TEMP_DIR" 2>&1; then
        print_error "No se puede conectar al servidor $SERVER_IP en el puerto $SSH_PORT"
        print_info ""
        print_info "El script intentó conectarse pero falló."
        print_info "Verifica:"
        print_info "  - Que las claves SSH estén configuradas para '$SERVER_USER'"
        print_info "  - Que puedas conectarte: ssh -p $SSH_PORT $SERVER_USER@$SERVER_IP"
        print_info "  - Que el firewall permita conexiones SSH (puerto $SSH_PORT)"
        print_info "  - Que hayas ejecutado setup-server-security.sh en el servidor"
        print_info ""
        print_warning "Puedes saltar la subida con --no-upload y hacerla manualmente después"
        exit 1
    fi
    
    print_success "Conexión SSH establecida correctamente"

    # Preparar carpeta /var/www/zip en el servidor
    print_info "Preparando carpeta /var/www/zip en el servidor..."
    ssh $SSH_OPTS "$SERVER_USER@$SERVER_IP" <<ZIP_SETUP
# Abortar el bloque remoto ante cualquier fallo (set -e local no se propaga al shell remoto)
set -e
# Eliminar carpeta si existe (limpiar completamente)
if [ -d "$REMOTE_ZIP_DIR" ]; then
    echo "Eliminando carpeta ZIP anterior..."
    sudo rm -rf "$REMOTE_ZIP_DIR"
fi

# Crear carpeta nueva
sudo mkdir -p "$REMOTE_ZIP_DIR"

# Ajustar permisos
sudo chown -R www-data:www-data "$REMOTE_ZIP_DIR" 2>/dev/null || sudo chown -R nginx:nginx "$REMOTE_ZIP_DIR" 2>/dev/null || true
sudo chmod 755 "$REMOTE_ZIP_DIR"
echo "Carpeta $REMOTE_ZIP_DIR preparada correctamente"
ZIP_SETUP
    rc=$?

    if [ $rc -ne 0 ]; then
        print_error "Error al preparar carpeta ZIP en el servidor"
        exit 1
    fi

    # Subir todo a /tmp primero (deploy user tiene permisos), luego mover con sudo
    print_info "Subiendo archivo ZIP al servidor (esto puede tardar varios minutos)..."
    ZIP_NAME=$(basename "$ZIP_PATH_BASH")
    scp $SCP_OPTS "$ZIP_PATH_BASH" "${SERVER_USER}@${SERVER_IP}:${REMOTE_TEMP_DIR}/Lineage2.zip"

    if [ $? -ne 0 ]; then
        print_error "Error al subir el archivo ZIP"
        exit 1
    fi

    # Mover ZIP a destino final con sudo
    ssh $SSH_OPTS "$SERVER_USER@$SERVER_IP" "sudo mv ${REMOTE_TEMP_DIR}/Lineage2.zip ${REMOTE_ZIP_DIR}/Lineage2.zip && sudo chmod 644 ${REMOTE_ZIP_DIR}/Lineage2.zip && sudo chown www-data:www-data ${REMOTE_ZIP_DIR}/Lineage2.zip 2>/dev/null || sudo chown nginx:nginx ${REMOTE_ZIP_DIR}/Lineage2.zip 2>/dev/null || true"

    print_success "ZIP subido exitosamente a ${REMOTE_ZIP_DIR}/Lineage2.zip"

    # Subir Launcher al servidor
    print_info "Subiendo Launcher al servidor..."
    
    if [ ! -f "$LAUNCHER_EXE_PATH" ]; then
        print_error "El launcher no existe en: $LAUNCHER_EXE_PATH"
        exit 1
    fi
    
    # Crear ZIP del launcher localmente
    LAUNCHER_DIR=$(dirname "$LAUNCHER_EXE_PATH")
    LAUNCHER_FILE=$(basename "$LAUNCHER_EXE_PATH")
    LAUNCHER_ZIP_NAME="L2TitanLauncher.zip"
    LAUNCHER_ZIP_PATH="${LAUNCHER_DIR}/${LAUNCHER_ZIP_NAME}"
    print_info "Creando ZIP del launcher..."
    
    if [ -f "$LAUNCHER_ZIP_PATH" ]; then
        rm -f "$LAUNCHER_ZIP_PATH"
    fi
    
    cd "$LAUNCHER_DIR"
    zip "$LAUNCHER_ZIP_PATH" "$LAUNCHER_FILE" > /dev/null 2>&1
    cd - > /dev/null 2>&1
    
    if [ ! -f "$LAUNCHER_ZIP_PATH" ]; then
        print_error "Error al crear el ZIP del launcher"
        exit 1
    fi
    
    LAUNCHER_ZIP_SIZE=$(du -h "$LAUNCHER_ZIP_PATH" | cut -f1)
    print_success "ZIP del launcher creado: $LAUNCHER_ZIP_PATH ($LAUNCHER_ZIP_SIZE)"
    
    # Subir a /tmp y mover con sudo
    print_info "Subiendo ZIP del launcher al servidor..."
    scp $SCP_OPTS "$LAUNCHER_ZIP_PATH" "${SERVER_USER}@${SERVER_IP}:${REMOTE_TEMP_DIR}/${LAUNCHER_ZIP_NAME}"

    if [ $? -ne 0 ]; then
        print_error "Error al subir el ZIP del Launcher"
        exit 1
    fi

    ssh $SSH_OPTS "$SERVER_USER@$SERVER_IP" "sudo mv ${REMOTE_TEMP_DIR}/${LAUNCHER_ZIP_NAME} ${REMOTE_ZIP_DIR}/${LAUNCHER_ZIP_NAME} && sudo chmod 644 ${REMOTE_ZIP_DIR}/${LAUNCHER_ZIP_NAME} && sudo chown www-data:www-data ${REMOTE_ZIP_DIR}/${LAUNCHER_ZIP_NAME} 2>/dev/null || sudo chown nginx:nginx ${REMOTE_ZIP_DIR}/${LAUNCHER_ZIP_NAME} 2>/dev/null || true"

    print_success "Launcher (ZIP) subido exitosamente a ${REMOTE_ZIP_DIR}/${LAUNCHER_ZIP_NAME}"

    # Subir manifest.json a /tmp
    print_info "Subiendo manifest.json..."
    scp $SCP_OPTS "$MANIFEST_PATH" "${SERVER_USER}@${SERVER_IP}:${REMOTE_TEMP_DIR}/manifest.json"

    if [ $? -ne 0 ]; then
        print_error "Error al subir manifest.json"
        exit 1
    fi

    print_success "manifest.json subido exitosamente"
else
    print_info "Saltando subida al servidor (--no-upload)"
    ZIP_NAME=$(basename "$ZIP_PATH_BASH")
fi

# Paso 4: Descomprimir en el servidor
if [ "$SKIP_EXTRACT" = false ]; then
    print_step "Paso 4: Descomprimiendo en el servidor"

    print_info "Conectándose al servidor y descomprimiendo..."
    print_info "Esto puede tardar varios minutos..."

    # Ejecutar comandos en el servidor para descomprimir
    ssh $SSH_OPTS "$SERVER_USER@$SERVER_IP" <<EOF
# Abortar el bloque remoto ante cualquier fallo (set -e local no se propaga al shell remoto)
set -e
# Crear directorio si no existe
sudo mkdir -p $DEPLOY_PATH

# Archivos/carpetas a preservar (no se borrarán)
PRESERVE=(".htaccess" "config.php" "nginx.conf" "logs" "backups")

# Limpiar el directorio antes de descomprimir
echo "Limpiando directorio $DEPLOY_PATH..."
cd $DEPLOY_PATH || exit 1

# Construir lista de exclusiones para find
FIND_EXCLUDE=""
for item in "\${PRESERVE[@]}"; do
    FIND_EXCLUDE="\$FIND_EXCLUDE ! -name '\$item'"
done

# Eliminar todo excepto archivos/carpetas a preservar
eval "sudo find . -mindepth 1 -maxdepth 1 \$FIND_EXCLUDE -exec rm -rf {} +" 2>/dev/null || true

# Descomprimir el nuevo ZIP en el directorio correcto (desde /var/www/zip)
echo "Descomprimiendo nuevo contenido en $DEPLOY_PATH..."
sudo unzip -o ${REMOTE_ZIP_DIR}/Lineage2.zip -d $DEPLOY_PATH || [ \$? -eq 1 ]

# Si el ZIP creó una subcarpeta Lineage2, mover archivos un nivel arriba
if [ -d "Lineage2" ] && [ ! -f "system/Lineage2.exe" ] && [ ! -f "system/L2.exe" ]; then
    echo "Detectada subcarpeta Lineage2, moviendo archivos a la raíz..."
    sudo mv Lineage2/* . 2>/dev/null || true
    sudo mv Lineage2/.* . 2>/dev/null || true
    sudo rmdir Lineage2 2>/dev/null || true
    echo "Archivos movidos correctamente"
fi

# Copiar manifest.json
sudo cp ${REMOTE_TEMP_DIR}/manifest.json $DEPLOY_PATH/manifest.json

# Ajustar permisos: directorios 755 (traversables), archivos 644 (sin bit de ejecución)
sudo chown -R www-data:www-data $DEPLOY_PATH 2>/dev/null || sudo chown -R nginx:nginx $DEPLOY_PATH 2>/dev/null || true
sudo find $DEPLOY_PATH -type d -exec chmod 755 {} +
sudo find $DEPLOY_PATH -type f -exec chmod 644 {} +

echo "Archivos descomprimidos exitosamente"
ls -la manifest.json
if [ -f system/Lineage2.exe ]; then
    ls -la system/Lineage2.exe
else
    ls -la system/*.exe 2>/dev/null | head -1
fi
EOF
    rc=$?

    if [ $rc -ne 0 ]; then
        print_error "Error al descomprimir en el servidor"
        exit 1
    fi

    print_success "Archivos descomprimidos en $DEPLOY_PATH"
else
    print_info "Saltando descompresión en el servidor (--no-extract)"
fi

# Resumen final
print_step "Deploy Completado"

print_success "Resumen:"
print_info "  Manifest generado: $MANIFEST_PATH"
print_info "  ZIP creado: $ZIP_PATH_BASH"
print_info "  ZIP subido a: ${REMOTE_ZIP_DIR}/Lineage2.zip"
print_info "  Archivos descomprimidos en: $DEPLOY_PATH"
print_info "  URL del servidor: $SERVER_URL"
print_info "  URL del manifest: $SERVER_URL/manifest.json"
print_info "  URL del ZIP: $SERVER_URL/zip/Lineage2.zip"
print_info "  URL del Launcher: $SERVER_URL/zip/L2TitanLauncher.zip"

print_info ""
print_info "Próximos pasos:"
print_info "  1. Verifica que el servidor responde: curl $SERVER_URL/manifest.json"
print_info "  2. Verifica archivos: curl -I $SERVER_URL/system/L2.exe"
print_info "  3. Verifica ZIP: curl -I $SERVER_URL/zip/Lineage2.zip"
print_info "  4. Verifica Launcher: curl -I $SERVER_URL/zip/L2TitanLauncher.zip"
print_info "  5. Test conexión: $0 --test-connection"

print_success ""
print_success "Deploy completado exitosamente!"

