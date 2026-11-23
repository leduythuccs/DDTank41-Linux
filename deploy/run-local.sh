#!/usr/bin/env bash
# Starts the whole game locally: Center -> Fighting -> Road (game server) -> Tank.Web (website + API)
# -> GameAdmin.Web (admin site: send mail, items and money to characters).
# Prerequisites (once): docker compose up -d && ./restore-databases.sh && ./configure-local.sh
#                       dotnet build ../DDTank.Server.Linux.sln
# Logs go to deploy/logs/*.log. Ctrl+C stops everything (SIGTERM, so the servers save and exit cleanly).
set -euo pipefail
cd "$(dirname "$0")"
ROOT="$(cd .. && pwd)"
LOGS="$PWD/logs"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
mkdir -p "$LOGS"

docker start ddtank-mssql >/dev/null 2>&1 || true

pids=()
stop_all() {
  echo; echo "Stopping..."
  for ((i=${#pids[@]}-1; i>=0; i--)); do kill -TERM "${pids[i]}" 2>/dev/null || true; done
  wait 2>/dev/null || true
  echo "Stopped."
}
trap stop_all EXIT INT TERM

start() { # name, project folder, dll, ready-pattern
  local name=$1 dir="$ROOT/$2/bin/Debug/net8.0" dll=$3 ready=$4 log="$LOGS/$1.log"
  [ -f "$dir/$dll" ] || { echo "$dir/$dll not found: build first (dotnet build DDTank.Server.Linux.sln)"; exit 1; }
  (cd "$dir" && exec "$DOTNET" "$dll" < /dev/null > "$log" 2>&1) &
  pids+=($!)
  printf '%-9s starting' "$name"
  for _ in $(seq 1 120); do
    if grep -qE "$ready" "$log" 2>/dev/null; then echo " ok"; return; fi
    if ! kill -0 "${pids[-1]}" 2>/dev/null; then echo " FAILED (see $log)"; tail -20 "$log"; exit 1; fi
    sleep 1; printf '.'
  done
  echo " timed out (see $log)"; exit 1
}

start center   Center.Service   Center.Service.dll   "GameServer is now open for connections"
start fighting Fighting.Service Fighting.Service.dll "GameServer is now open for connections"
start road     Road.Service     Road.Service.dll     "GameServer is now open for connections"
start web      Tank.Web         Tank.Web.dll         "Tank.Web listening"
start admin    GameAdmin.Web    GameAdmin.Web.dll    "GameAdmin listening"

cat <<EOF

All services are running. Open the login page:
  http://127.0.0.1:8080/index.htm
Admin site (password: DDTANK_ADMIN_PASSWORD in deploy/.env):
  http://127.0.0.1:8081/
Logs: $LOGS   (Ctrl+C to stop)
EOF
wait
