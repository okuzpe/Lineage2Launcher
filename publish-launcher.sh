#!/bin/bash
# publish-launcher.sh — publica una NUEVA versión del launcher para el auto-update.
# ----------------------------------------------------------------------------
# El launcher comprueba al arrancar https://downloads.l2-titan.com/launcher.json (firmado),
# y si la versión es mayor que la suya, descarga el exe (verificando SHA-256 + firma) y se
# auto-reemplaza. Este script publica esos artefactos.
#
# ANTES de correrlo:
#   1. Sube la <Version> en Lineage2Launcher.csproj (p.ej. 1.0.1).
#   2. Consigue el exe FIRMADO con Authenticode (SignPath):
#        git tag vX.Y.Z && git push --tags   → CI lo firma y crea un GitHub Release.
#        Descarga ese L2TitanLauncher.exe firmado (p.ej. a ./signed/).
#      Un exe SIN firmar dispara SmartScreen/antivirus en el cliente; por eso este
#      script lo RECHAZA salvo que pases --allow-unsigned (solo para pruebas internas).
#
# Uso:  ./publish-launcher.sh <version> [--exe <ruta-al-exe-firmado>] [--allow-unsigned]
#   ./publish-launcher.sh 1.0.1 --exe ./signed/L2TitanLauncher.exe
# ----------------------------------------------------------------------------
set -euo pipefail

SERVER="212.227.87.65"
SSH_PORT="22"
SSH_USER="root"
REMOTE="/var/www/lineage2"
EXE="bin/Release/net10.0-windows/win-x64/publish/L2TitanLauncher.exe"
PRIVATE_KEY="keys/manifest_private.pem"
PUBLIC_KEY="keys/manifest_public.pem"
BASE_URL="https://downloads.l2-titan.com"
SSH_OPTS="-p $SSH_PORT -o BatchMode=yes -o ConnectTimeout=20"

VERSION=""
ALLOW_UNSIGNED=0
while [ $# -gt 0 ]; do
    case "$1" in
        --exe)            case "${2:-}" in ""|-*) echo "ERROR: --exe requiere una ruta a un .exe" >&2; exit 1 ;; esac; EXE="$2"; shift ;;
        --allow-unsigned) ALLOW_UNSIGNED=1 ;;
        -*)               echo "Opción desconocida: $1" >&2; exit 1 ;;
        *)                if [ -z "$VERSION" ]; then VERSION="$1"; else echo "ERROR: argumento extra '$1'" >&2; exit 1; fi ;;
    esac
    shift
done
[ -n "$VERSION" ] || { echo "Uso: $0 <version> [--exe <ruta-al-exe-firmado>] [--allow-unsigned]" >&2; exit 1; }

[ -f "$EXE" ]         || { echo "ERROR: no existe $EXE — pásalo con --exe o corre 'dotnet publish -c Release'." >&2; exit 1; }
[ -f "$PRIVATE_KEY" ] || { echo "ERROR: falta la clave privada $PRIVATE_KEY" >&2; exit 1; }

# --- No publicar un exe sin firmar (Authenticode): SmartScreen/AV lo marcarían ---
# NOTA de seguridad: esta verificación es para UX (SmartScreen/AV). La seguridad del canal
# de auto-update NO depende de ella: el cliente valida la firma RSA de launcher.json.
# Pin opcional del firmante esperado: EXPECTED_SIGNER="SignPath Foundation" (recomendado).
EXPECTED_SIGNER="${EXPECTED_SIGNER:-}"

verify_authenticode() {
    local f="$1"
    if command -v powershell.exe >/dev/null 2>&1; then
        local winpath ps out status signer
        winpath=$(cygpath -w "$f" 2>/dev/null || wslpath -w "$f" 2>/dev/null || echo "$f")
        ps='$s = Get-AuthenticodeSignature -LiteralPath "__P__"; Write-Output $s.Status; if ($s.SignerCertificate) { Write-Output $s.SignerCertificate.Subject } else { Write-Output "(ninguno)" }'
        ps="${ps/__P__/$winpath}"
        out=$(powershell.exe -NoProfile -NonInteractive -Command "$ps" 2>/dev/null | tr -d '\r')
        status=$(printf '%s\n' "$out" | sed -n '1p')
        signer=$(printf '%s\n' "$out" | sed -n '2p')
        echo "    Authenticode: status=${status:-?} signer=${signer:-?}"
        # Allow-list estricta: SOLO 'Valid' (firma presente + cadena de confianza OK).
        # Cualquier otro estado (NotSigned, NotTrusted, UnknownError, HashMismatch...) se rechaza.
        [ "$status" = "Valid" ] || return 1
        if [ -n "$EXPECTED_SIGNER" ]; then
            case "$signer" in
                *"$EXPECTED_SIGNER"*) : ;;
                *) echo "    ERROR: firmante '$signer' no coincide con EXPECTED_SIGNER='$EXPECTED_SIGNER'" >&2; return 1 ;;
            esac
        fi
        return 0
    elif command -v osslsigncode >/dev/null 2>&1; then
        # 'osslsigncode verify' sin -CAfile NO valida la cadena de confianza (acepta self-signed).
        # Por eso exigimos pin EXPECTED_SIGNER; sin él, fail-closed.
        if ! osslsigncode verify "$f" >/dev/null 2>&1; then
            echo "    Authenticode: SIN FIRMAR / firma inválida (osslsigncode)"; return 1
        fi
        if [ -z "$EXPECTED_SIGNER" ]; then
            echo "    ERROR: osslsigncode no valida confianza sin pin. Define EXPECTED_SIGNER='SignPath Foundation' o usa una máquina con powershell.exe." >&2
            return 1
        fi
        if osslsigncode verify "$f" 2>/dev/null | grep -qF "$EXPECTED_SIGNER"; then
            echo "    Authenticode: firmado, firmante coincide ($EXPECTED_SIGNER)"; return 0
        fi
        echo "    ERROR: firmante no coincide con EXPECTED_SIGNER='$EXPECTED_SIGNER'" >&2; return 1
    else
        echo "    AVISO: sin powershell.exe ni osslsigncode; no puedo verificar la firma." >&2
        return 1
    fi
}

echo ">>> Verificando firma Authenticode de $EXE..."
if ! verify_authenticode "$EXE"; then
    if [ "$ALLOW_UNSIGNED" -eq 1 ]; then
        echo ">>> ADVERTENCIA: exe SIN FIRMAR; publico igualmente por --allow-unsigned (SmartScreen/AV avisarán)." >&2
    else
        echo "ERROR: el exe NO está firmado con Authenticode → SmartScreen/antivirus lo marcarán." >&2
        echo "       Publica el exe firmado por SignPath (GitHub Release del tag v*):" >&2
        echo "         $0 $VERSION --exe ./signed/L2TitanLauncher.exe" >&2
        echo "       (Solo para pruebas internas: añade --allow-unsigned.)" >&2
        exit 1
    fi
fi

SHA=$(sha256sum "$EXE" | awk '{print $1}')
SIZE=$(stat -c%s "$EXE" 2>/dev/null || wc -c < "$EXE")

TMP_JSON="$(mktemp)"; TMP_SIG="$(mktemp)"
trap 'rm -f "$TMP_JSON" "$TMP_SIG" "${TMP_SIG}.bin"' EXIT

printf '{"Version":"%s","Url":"%s/launcher/L2TitanLauncher.exe","Sha256":"%s","Size":%s}' \
    "$VERSION" "$BASE_URL" "$SHA" "$SIZE" > "$TMP_JSON"

echo ">>> Firmando launcher.json (RSA / SHA-256)..."
openssl dgst -sha256 -sign "$PRIVATE_KEY" -out "${TMP_SIG}.bin" "$TMP_JSON"
openssl base64 -A -in "${TMP_SIG}.bin" -out "$TMP_SIG"
openssl rsa -in "$PRIVATE_KEY" -pubout -out "$PUBLIC_KEY" 2>/dev/null
openssl dgst -sha256 -verify "$PUBLIC_KEY" -signature "${TMP_SIG}.bin" "$TMP_JSON" | grep -q "Verified OK" \
    || { echo "ERROR: la firma local no verifica" >&2; exit 1; }

echo ">>> Subiendo exe + launcher.json + firma..."
ssh $SSH_OPTS "$SSH_USER@$SERVER" "mkdir -p '$REMOTE/launcher'"
scp -P "$SSH_PORT" -o BatchMode=yes "$EXE"      "$SSH_USER@$SERVER:$REMOTE/launcher/L2TitanLauncher.exe"
scp -P "$SSH_PORT" -o BatchMode=yes "$TMP_JSON" "$SSH_USER@$SERVER:$REMOTE/launcher.json"
scp -P "$SSH_PORT" -o BatchMode=yes "$TMP_SIG"  "$SSH_USER@$SERVER:$REMOTE/launcher.json.sig"
ssh $SSH_OPTS "$SSH_USER@$SERVER" "
    chown -R www-data:www-data '$REMOTE/launcher' '$REMOTE/launcher.json' '$REMOTE/launcher.json.sig'
    chmod 644 '$REMOTE/launcher.json' '$REMOTE/launcher.json.sig' '$REMOTE/launcher/L2TitanLauncher.exe'"

echo ">>> Verificando..."
code=$(curl -s -o /dev/null -w '%{http_code}' "$BASE_URL/launcher.json")
[ "$code" = "200" ] || { echo "ERROR: launcher.json responde $code" >&2; exit 1; }
ecode=$(curl -s -o /dev/null -w '%{http_code}' "$BASE_URL/launcher/L2TitanLauncher.exe")
[ "$ecode" = "200" ] || { echo "ERROR: el exe responde $ecode" >&2; exit 1; }

echo ">>> LISTO: launcher v$VERSION publicado (sha256 $SHA, $SIZE bytes)."
echo "    Los launchers con versión menor se actualizarán solos en el próximo arranque."
