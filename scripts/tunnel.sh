#!/usr/bin/env bash
# Túnel rápido Cloudflare → http://localhost:5098
# Uso: ./scripts/tunnel.sh   (con la API ya en dotnet run)

set -euo pipefail

PORT="${API_PORT:-5098}"
TARGET="http://localhost:${PORT}"

find_cloudflared() {
  if command -v cloudflared >/dev/null 2>&1; then
    command -v cloudflared
    return
  fi
  for path in \
    "/c/Program Files (x86)/cloudflared/cloudflared.exe" \
    "/c/Program Files/cloudflared/cloudflared.exe"; do
    if [[ -f "$path" ]]; then
      echo "$path"
      return
    fi
  done
  return 1
}

if ! CF="$(find_cloudflared)"; then
  echo "cloudflared no encontrado. Instala con:"
  echo "  winget install Cloudflare.cloudflared"
  exit 1
fi

echo "Iniciando túnel hacia ${TARGET} ..."
echo "Cuando aparezca https://....trycloudflare.com:"
echo "  1. Abre la app por esa URL"
echo "  2. Google OAuth redirect: https://TU-URL.trycloudflare.com/signin-google"
echo "  3. Actualiza App:PublicOrigin en Api/appsettings.Development.json"
echo ""

exec "$CF" tunnel --url "$TARGET"
