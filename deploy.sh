#!/bin/bash
# deploy.sh — despliegue unificado del cliente de Lineage 2.
# ----------------------------------------------------------------------------
# Reemplaza a deploy-simple.sh (obsoleto: exigía zip+Python locales y NUNCA firmaba).
# Cierra los hallazgos de auditoría H1/H2/H3:
#   - H1: la firma del manifiesto está INTEGRADA en el deploy (no es un paso aparte).
#   - H2: manifest.json y manifest.json.sig se publican JUNTOS al final; el .sig nunca
#         queda desfasado ni se borra.
#   - H3: se respaldan manifest+sig antes de publicar y se hace ROLLBACK si la
#         verificación falla, así los clientes nunca ven un estado inconsistente.
#
# Modelo: aditivo (sube/actualiza archivos; no borra los eliminados). El manifiesto y su
# firma se generan/actualizan SIEMPRE como último paso, de forma conjunta. Usa root@22
# por llave SSH (no necesita el sudo del usuario 'deploy').
#
# Uso:  ./deploy.sh            (despliega C:/Juegos/Lineage2 a producción)
# ----------------------------------------------------------------------------
set -euo pipefail

GAME_PATH="/c/Juegos/Lineage2"
SERVER="212.227.87.65"
SSH_PORT="22"
SSH_USER="root"
REMOTE="/var/www/lineage2"
PRIVATE_KEY="keys/manifest_private.pem"
PUBLIC_KEY="keys/manifest_public.pem"
SSH_OPTS="-p $SSH_PORT -o BatchMode=yes -o ConnectTimeout=20 -o ServerAliveInterval=30"
TS="$(date +%Y%m%d-%H%M%S)"
BACKUP="/var/www/backups/$TS"

say() { echo -e "\n>>> $*"; }

# --- Preflight ---------------------------------------------------------------
[ -d "$GAME_PATH" ]    || { echo "ERROR: no existe el juego en $GAME_PATH" >&2; exit 1; }
[ -f "$PRIVATE_KEY" ]  || { echo "ERROR: falta la clave privada $PRIVATE_KEY" >&2; exit 1; }
command -v tar >/dev/null     || { echo "ERROR: falta 'tar'" >&2; exit 1; }
command -v openssl >/dev/null || { echo "ERROR: falta 'openssl'" >&2; exit 1; }
ssh $SSH_OPTS "$SSH_USER@$SERVER" "command -v python3 >/dev/null" || { echo "ERROR: el server no tiene python3" >&2; exit 1; }

TMP_MANIFEST="$(mktemp)"; TMP_SIG="$(mktemp)"
trap 'rm -f "$TMP_MANIFEST" "$TMP_SIG" "${TMP_SIG}.bin"' EXIT

# --- 1. Backup de los artefactos de confianza (rollback de H3) ----------------
say "Respaldo de manifest+sig actuales en $BACKUP ..."
ssh $SSH_OPTS "$SSH_USER@$SERVER" "
  mkdir -p '$BACKUP'
  [ -f '$REMOTE/manifest.json' ]     && cp '$REMOTE/manifest.json' '$BACKUP/'     || true
  [ -f '$REMOTE/manifest.json.sig' ] && cp '$REMOTE/manifest.json.sig' '$BACKUP/' || true
  echo 'backup OK'"

rollback() {
  echo "!!! Fallo tras tocar producción — ROLLBACK de manifest+sig desde $BACKUP" >&2
  ssh $SSH_OPTS "$SSH_USER@$SERVER" "
    [ -f '$BACKUP/manifest.json' ]     && cp '$BACKUP/manifest.json' '$REMOTE/manifest.json'         || true
    [ -f '$BACKUP/manifest.json.sig' ] && cp '$BACKUP/manifest.json.sig' '$REMOTE/manifest.json.sig' || true
    chown www-data:www-data '$REMOTE/manifest.json' '$REMOTE/manifest.json.sig' 2>/dev/null || true" || true
}

# --- 2. Subir archivos del juego (tar+ssh, aditivo) ---------------------------
say "Subiendo archivos del juego (tar+ssh, con compresión)..."
( cd "$GAME_PATH" && tar --exclude=./logs --exclude=./screenshots --exclude=./temp \
     --exclude='*.log' --exclude='*.part' --exclude='manifest.json' --exclude='manifest.json.sig' -czf - . ) \
  | ssh $SSH_OPTS "$SSH_USER@$SERVER" "tar -xzf - -C '$REMOTE'"

# --- 3. Generar el manifiesto EN el server (a .new) ---------------------------
say "Generando manifest.json en el server..."
ssh $SSH_OPTS "$SSH_USER@$SERVER" "python3 /root/generate_manifest.py '$REMOTE' '$REMOTE/manifest.json.new'" | tail -2

# --- 4. Firmar localmente (clave privada nunca sale de aquí) -------------------
say "Descargando manifest.new y firmándolo localmente..."
scp -P "$SSH_PORT" -o BatchMode=yes "$SSH_USER@$SERVER:$REMOTE/manifest.json.new" "$TMP_MANIFEST"
openssl dgst -sha256 -sign "$PRIVATE_KEY" -out "${TMP_SIG}.bin" "$TMP_MANIFEST"
openssl base64 -A -in "${TMP_SIG}.bin" -out "$TMP_SIG"
openssl rsa -in "$PRIVATE_KEY" -pubout -out "$PUBLIC_KEY" 2>/dev/null
openssl dgst -sha256 -verify "$PUBLIC_KEY" -signature "${TMP_SIG}.bin" "$TMP_MANIFEST" | grep -q "Verified OK" \
  || { echo "ERROR: la firma local no verifica" >&2; exit 1; }
scp -P "$SSH_PORT" -o BatchMode=yes "$TMP_SIG" "$SSH_USER@$SERVER:$REMOTE/manifest.json.sig.new"

# --- 5. Publicar manifest+sig JUNTOS al final (ventana mínima) + permisos -----
say "Publicando manifest+firma y ajustando permisos..."
if ! ssh $SSH_OPTS "$SSH_USER@$SERVER" "
    set -e
    mv '$REMOTE/manifest.json.new' '$REMOTE/manifest.json'
    mv '$REMOTE/manifest.json.sig.new' '$REMOTE/manifest.json.sig'
    chown -R www-data:www-data '$REMOTE'
    find '$REMOTE' -type d -exec chmod 755 {} +
    find '$REMOTE' -type f -exec chmod 644 {} +
    echo publish OK"; then
  rollback; exit 1
fi

# --- 6. Verificación end-to-end por HTTPS -------------------------------------
say "Verificando producción..."
mcode=$(curl -s -o /dev/null -w '%{http_code}' https://downloads.l2-titan.com/manifest.json)
scode=$(curl -s -o /dev/null -w '%{http_code}' https://downloads.l2-titan.com/manifest.json.sig)
if [ "$mcode" != "200" ] || [ "$scode" != "200" ]; then
  echo "ERROR: manifest=$mcode sig=$scode" >&2; rollback; exit 1
fi
# La firma servida debe verificar contra la clave pública
curl -s https://downloads.l2-titan.com/manifest.json -o "$TMP_MANIFEST"
curl -s https://downloads.l2-titan.com/manifest.json.sig | openssl base64 -d -A -out "${TMP_SIG}.bin"
if ! openssl dgst -sha256 -verify "$PUBLIC_KEY" -signature "${TMP_SIG}.bin" "$TMP_MANIFEST" | grep -q "Verified OK"; then
  echo "ERROR: la firma servida NO verifica" >&2; rollback; exit 1
fi

say "DEPLOY OK — manifest+firma publicados y verificados. Backup en $BACKUP"
