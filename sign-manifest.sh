#!/bin/bash
# sign-manifest.sh
# ----------------------------------------------------------------------------
# Firma el manifest.json del servidor con la clave PRIVADA local y sube la
# firma (manifest.json.sig). Ejecutar SIEMPRE tras regenerar el manifiesto en
# un deploy: si el manifest.json cambia y la firma no, el launcher lo rechazará.
#
# La clave privada (keys/manifest_private.pem) NUNCA sale de esta máquina ni se
# sube a git. La pública correspondiente está embebida en el launcher
# (Services/ManifestSecurity.cs). Si pierdes la privada, hay que generar un
# par nuevo y re-publicar el launcher con la nueva pública.
#
# Uso:  ./sign-manifest.sh
# ----------------------------------------------------------------------------

set -euo pipefail

SERVER_IP="212.227.87.65"
SSH_PORT="22"
SSH_USER="root"
REMOTE_MANIFEST="/var/www/lineage2/manifest.json"
PRIVATE_KEY="keys/manifest_private.pem"
SSH_OPTS="-p $SSH_PORT -o BatchMode=yes -o ConnectTimeout=15"

if [ ! -f "$PRIVATE_KEY" ]; then
    echo "ERROR: no existe $PRIVATE_KEY (la clave privada de firma)." >&2
    echo "Si la perdiste: genera un par nuevo y actualiza la pública en ManifestSecurity.cs." >&2
    exit 1
fi

TMP_MANIFEST="$(mktemp)"
TMP_SIG="$(mktemp)"
trap 'rm -f "$TMP_MANIFEST" "$TMP_SIG"' EXIT

echo ">>> Descargando manifest.json del servidor..."
scp -P "$SSH_PORT" -o BatchMode=yes "${SSH_USER}@${SERVER_IP}:${REMOTE_MANIFEST}" "$TMP_MANIFEST"

echo ">>> Firmando (RSA PKCS#1 v1.5 / SHA-256)..."
openssl dgst -sha256 -sign "$PRIVATE_KEY" -out "${TMP_SIG}.bin" "$TMP_MANIFEST"
openssl base64 -A -in "${TMP_SIG}.bin" -out "$TMP_SIG"

echo ">>> Verificando la firma localmente con la clave pública..."
openssl rsa -in "$PRIVATE_KEY" -pubout -out keys/manifest_public.pem 2>/dev/null
if openssl dgst -sha256 -verify keys/manifest_public.pem -signature "${TMP_SIG}.bin" "$TMP_MANIFEST" | grep -q "Verified OK"; then
    echo "    Firma válida."
else
    echo "ERROR: la verificación local falló." >&2
    exit 1
fi

echo ">>> Subiendo manifest.json.sig al servidor..."
scp -P "$SSH_PORT" -o BatchMode=yes "$TMP_SIG" "${SSH_USER}@${SERVER_IP}:/tmp/manifest.json.sig"
ssh $SSH_OPTS "${SSH_USER}@${SERVER_IP}" "mv /tmp/manifest.json.sig ${REMOTE_MANIFEST}.sig && chown www-data:www-data ${REMOTE_MANIFEST}.sig && chmod 644 ${REMOTE_MANIFEST}.sig"

rm -f "${TMP_SIG}.bin"
echo ">>> LISTO: ${REMOTE_MANIFEST}.sig publicado."
