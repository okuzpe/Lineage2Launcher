#!/bin/bash
# publish-launcher.sh — publica una NUEVA versión del launcher para el auto-update.
# ----------------------------------------------------------------------------
# El launcher comprueba al arrancar https://downloads.l2-titan.com/launcher.json (firmado),
# y si la versión es mayor que la suya, descarga el exe (verificando SHA-256 + firma) y se
# auto-reemplaza. Este script publica esos artefactos.
#
# ANTES de correrlo:
#   1. Sube la <Version> en Lineage2Launcher.csproj (p.ej. 1.0.1).
#   2. dotnet publish -c Release
#
# Uso:  ./publish-launcher.sh 1.0.1     (la versión DEBE coincidir con la del csproj)
# ----------------------------------------------------------------------------
set -euo pipefail

VERSION="${1:-}"
[ -n "$VERSION" ] || { echo "Uso: $0 <version>  (p.ej. 1.0.1, igual que la <Version> del csproj)" >&2; exit 1; }

SERVER="212.227.87.65"
SSH_PORT="22"
SSH_USER="root"
REMOTE="/var/www/lineage2"
EXE="bin/Release/net10.0-windows/win-x64/publish/L2TitanLauncher.exe"
PRIVATE_KEY="keys/manifest_private.pem"
PUBLIC_KEY="keys/manifest_public.pem"
BASE_URL="https://downloads.l2-titan.com"
SSH_OPTS="-p $SSH_PORT -o BatchMode=yes -o ConnectTimeout=20"

[ -f "$EXE" ]         || { echo "ERROR: no existe $EXE — corre 'dotnet publish -c Release' primero." >&2; exit 1; }
[ -f "$PRIVATE_KEY" ] || { echo "ERROR: falta la clave privada $PRIVATE_KEY" >&2; exit 1; }

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
