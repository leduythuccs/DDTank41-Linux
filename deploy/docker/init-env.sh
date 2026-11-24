#!/usr/bin/env bash
# Writes deploy/docker/.env for docker-compose.yml, with random passwords.
# Usage: ./init-env.sh http://<server ip or domain>:16200
set -euo pipefail
cd "$(dirname "$0")"
url="${1:?usage: ./init-env.sh http://<server ip or domain>:16200}"
[ -e .env ] && { echo ".env already exists: edit it, or delete it to start over"; exit 1; }
pw() { head -c 18 /dev/urandom | base64 | tr '+/' '-_'; }
umask 077
cat > .env <<ENV
# Address players open (Tank.Web, port 16200).
DDTANK_PUBLIC_URL=${url%/}
MARIADB_ROOT_PASSWORD=$(pw)
DDTANK_DB_PASSWORD=$(pw)
# GameAdmin.Web login (http://127.0.0.1:16201 through an SSH tunnel, see docker-compose.yml)
DDTANK_ADMIN_PASSWORD=$(pw)
# Site to fetch missing game art (/resource/) from; cached in data/resource-cache.
DDTANK_RESOURCE_UPSTREAM=http://gunny.vcdn.vn/
# Optional:
# DDTANK_RUFFLE_URL=https://unpkg.com/@ruffle-rs/ruffle@0.8.0/ruffle.js
# DDTANK_ADMIN_BIND=0.0.0.0     # expose the admin site to the internet (plain HTTP: prefer the SSH tunnel)
# DDTANK_DB_BIND=0.0.0.0        # expose MariaDB (16207) to the internet
# DDTANK_TZ=Asia/Ho_Chi_Minh
ENV
echo "wrote .env (admin password: DDTANK_ADMIN_PASSWORD in it)"
