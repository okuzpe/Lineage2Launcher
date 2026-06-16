#!/bin/bash
# setup-server-security.sh
# ----------------------------------------------------------------------------
# Prepara un VPS Ubuntu 24.04 (IONOS) para recibir despliegues de
# deploy-simple.sh de forma segura. Es IDEMPOTENTE: se puede correr varias veces.
#
# Qué hace:
#   1. Crea el usuario 'deploy' (sin contraseña, solo llave SSH).
#   2. Instala TU llave pública SSH para ese usuario.
#   3. Da sudo SIN contraseña pero SOLO para los comandos que el deploy usa.
#   4. Mueve SSH al puerto 2222 (manteniendo el 22 como red de seguridad).
#   5. Configura el firewall local (ufw): 22, 2222, 80, 443.
#   6. Prepara /var/www/zip y /var/www/lineage2 con permisos correctos.
#
# NO toca todavía: PasswordAuthentication ni PermitRootLogin (para que no te
# quedes fuera). El endurecimiento final se hace a mano DESPUÉS de confirmar
# que entras con la llave (ver el bloque "PASOS SIGUIENTES" al final).
#
# Uso (como root, en el servidor):
#   ./setup-server-security.sh "ssh-ed25519 AAAA... tu_comentario"
#
# La llave pública es el contenido de ~/.ssh/id_ed25519.pub de tu PC.
# ----------------------------------------------------------------------------

set -euo pipefail

DEPLOY_USER="deploy"
SSH_PORT="2222"
ZIP_DIR="/var/www/zip"
DEPLOY_DIR="/var/www/lineage2"
SSHD_DROPIN="/etc/ssh/sshd_config.d/99-l2titan.conf"
SUDOERS_FILE="/etc/sudoers.d/deploy"

# --- Pre-requisitos ----------------------------------------------------------
if [ "$(id -u)" -ne 0 ]; then
    echo "ERROR: ejecuta este script como root (sudo)." >&2
    exit 1
fi

PUBKEY="${1:-}"
if [ -z "$PUBKEY" ]; then
    echo "ERROR: falta la llave pública." >&2
    echo "Uso: $0 \"ssh-ed25519 AAAA... comentario\"" >&2
    exit 1
fi
if ! printf '%s' "$PUBKEY" | grep -Eq '^(ssh-ed25519|ssh-rsa|ecdsa-) '; then
    echo "ERROR: el argumento no parece una llave pública SSH válida." >&2
    exit 1
fi

echo ">>> [1/6] Usuario '$DEPLOY_USER'..."
if ! id "$DEPLOY_USER" &>/dev/null; then
    adduser --disabled-password --gecos "" "$DEPLOY_USER"
    echo "    Usuario creado."
else
    echo "    Ya existe."
fi

echo ">>> [2/6] Llave SSH para '$DEPLOY_USER'..."
SSH_HOME="/home/$DEPLOY_USER/.ssh"
install -d -m 700 -o "$DEPLOY_USER" -g "$DEPLOY_USER" "$SSH_HOME"
AUTH_KEYS="$SSH_HOME/authorized_keys"
touch "$AUTH_KEYS"
# Añadir la llave solo si no está ya presente (idempotente)
if ! grep -qxF "$PUBKEY" "$AUTH_KEYS"; then
    echo "$PUBKEY" >> "$AUTH_KEYS"
    echo "    Llave instalada."
else
    echo "    La llave ya estaba instalada."
fi
chmod 600 "$AUTH_KEYS"
chown -R "$DEPLOY_USER:$DEPLOY_USER" "$SSH_HOME"

echo ">>> [3/6] sudo sin contraseña (solo comandos del deploy)..."
# Incluye rutas /bin y /usr/bin porque deploy-simple.sh invoca ambas formas
# (p. ej. --test-connection usa /bin/mkdir y /bin/rm explícitamente).
cat > "$SUDOERS_FILE" <<'SUDO'
deploy ALL=(root) NOPASSWD: /bin/mkdir, /usr/bin/mkdir, \
                            /bin/rm, /usr/bin/rm, \
                            /bin/mv, /usr/bin/mv, \
                            /bin/cp, /usr/bin/cp, \
                            /bin/chown, /usr/bin/chown, \
                            /bin/chmod, /usr/bin/chmod, \
                            /usr/bin/find, /usr/bin/unzip
SUDO
chmod 440 "$SUDOERS_FILE"
# Validar sintaxis; si falla, borrar para no romper sudo
if ! visudo -cf "$SUDOERS_FILE"; then
    rm -f "$SUDOERS_FILE"
    echo "ERROR: sudoers inválido, revertido." >&2
    exit 1
fi
echo "    sudo configurado y validado."

echo ">>> [4/6] Directorios web..."
mkdir -p "$ZIP_DIR" "$DEPLOY_DIR"
# El servidor web corre como www-data (Debian/Ubuntu) o nginx según distro
chown -R www-data:www-data "$ZIP_DIR" "$DEPLOY_DIR" 2>/dev/null \
    || chown -R nginx:nginx "$ZIP_DIR" "$DEPLOY_DIR" 2>/dev/null || true
chmod 755 "$ZIP_DIR" "$DEPLOY_DIR"
echo "    $ZIP_DIR y $DEPLOY_DIR listos."

echo ">>> [5/6] SSH en puerto $SSH_PORT (manteniendo 22)..."
cat > "$SSHD_DROPIN" <<SSHD
# Gestionado por setup-server-security.sh
Port $SSH_PORT
Port 22
PubkeyAuthentication yes
SSHD
# Ubuntu 24.04 usa activación por socket (ssh.socket), que IGNORA las
# directivas Port de sshd_config. Pasamos a la unidad ssh.service clásica.
if systemctl list-unit-files 2>/dev/null | grep -q '^ssh.socket'; then
    systemctl disable --now ssh.socket 2>/dev/null || true
fi
systemctl enable ssh 2>/dev/null || systemctl enable sshd 2>/dev/null || true
# Validar config antes de reiniciar para no cortar el acceso
sshd -t
systemctl restart ssh 2>/dev/null || systemctl restart sshd
echo "    sshd escuchando en 22 y $SSH_PORT."

echo ">>> [6/6] Firewall local (ufw)..."
if command -v ufw >/dev/null 2>&1; then
    ufw allow 22/tcp        >/dev/null
    ufw allow "$SSH_PORT"/tcp >/dev/null
    ufw allow 80/tcp        >/dev/null
    ufw allow 443/tcp       >/dev/null
    ufw --force enable      >/dev/null
    echo "    ufw activo: 22, $SSH_PORT, 80, 443 abiertos."
else
    echo "    ufw no instalado; se omite (instala con: apt install ufw)."
fi

cat <<FIN

============================================================================
 LISTO. El usuario 'deploy' puede recibir despliegues por el puerto $SSH_PORT.
============================================================================

 IMPORTANTE — FIREWALL DE IONOS (panel web):
   El firewall del panel de IONOS es SEPARADO de ufw. Debes abrir ahí los
   puertos entrantes: $SSH_PORT (SSH deploy), 80 y 443 (web). Si no, el deploy
   no podrá conectar aunque ufw los permita.

 PRUEBA DESDE TU PC (no cierres esta sesión root hasta confirmar):
   ssh -p $SSH_PORT deploy@$(hostname -I | awk '{print $1}')

 PASOS SIGUIENTES (endurecer, solo cuando la llave ya funcione):
   1. Desactivar login por contraseña y root por SSH:
        echo -e "PasswordAuthentication no\nPermitRootLogin prohibit-password" \\
          > /etc/ssh/sshd_config.d/00-hardening.conf
        sshd -t && systemctl restart ssh
   2. (Opcional) Cerrar el puerto 22:  ufw delete allow 22/tcp
   3. Servidor web + TLS: falta instalar nginx y emitir el certificado de
      downloads.l2-titan.com (el launcher exige https). Eso es un paso aparte.
============================================================================
FIN
