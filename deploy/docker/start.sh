#!/usr/bin/env bash
# Entrypoint of the DDTank game image (deploy/docker/Dockerfile).
# 1. writes /app/<Service>/appSettings.local.config from the environment (App.config merges it);
# 2. starts Center -> Fighting -> Road -> Tank.Web -> GameAdmin.Web, each after the previous one is ready;
# 3. if one of them exits, stops the others and exits, so Docker restarts the container.
# Environment (see deploy/docker/.env.example):
#   DDTANK_PUBLIC_URL (required)   address players open, e.g. http://203.0.113.5:16200
#   DDTANK_DB_PASSWORD (required)  password of the MariaDB login "ddtank"
#   DDTANK_ADMIN_PASSWORD          GameAdmin.Web login; empty = the admin site refuses every login
#   DDTANK_DB_HOST, DDTANK_DB_PORT (default mariadb, 16207)
# Ports inside the container (all 162xx): 16200 Tank.Web, 16201 GameAdmin.Web, 16202 game server,
# 16203 center, 16204 fighting, 16205/16206 center WCF (net.tcp/http).
#   DDTANK_RUFFLE_URL, DDTANK_RESOURCE_UPSTREAM
set -euo pipefail

if [ "$(id -u)" = 0 ]; then
  chown -R app:app /data /var/log/ddtank
  HOME=/home/app exec setpriv --reuid=app --regid=app --init-groups "$0" "$@"
fi

: "${DDTANK_PUBLIC_URL:?set DDTANK_PUBLIC_URL, e.g. http://<server ip>:16200}"
: "${DDTANK_DB_PASSWORD:?set DDTANK_DB_PASSWORD}"
PUBLIC="${DDTANK_PUBLIC_URL%/}"
HOST="${DDTANK_DB_HOST:-mariadb}"
DBPORT="${DDTANK_DB_PORT:-16207}"
WEB=16200; ADMIN=16201; GAME=16202; CENTER=16203; FIGHT=16204; CENTER_TCP=16205; CENTER_HTTP=16206
CENTER_ADDRESS="net.tcp://127.0.0.1:$CENTER_TCP/"
SITE=/srv/ddtank
LOGS=/var/log/ddtank

xml() { printf '%s' "$1" | sed 's/&/\&amp;/g; s/"/\&quot;/g; s/</\&lt;/g'; }
add() { printf '\t<add key="%s" value="%s" />\n' "$1" "$(xml "$2")"; }
cs() { printf 'Server=%s;Port=%s;Database=%s;User ID=ddtank;Password="%s";Pooling=true;ConvertZeroDateTime=true' "$HOST" "$DBPORT" "$1" "$DDTANK_DB_PASSWORD"; }

settings() {
  add conString "$(cs Project_Player34)"
  add crosszoneString "$(cs Project_Game34)"
  add membershipDb "$(cs Db_Membership)"
  case "$1" in
    Center.Service)
      add Port $CENTER
      add CenterServiceTcpPort $CENTER_TCP
      add CenterServiceHttpPort $CENTER_HTTP
      ;;
    Fighting.Service)
      add Port $FIGHT
      ;;
    Road.Service)
      add Port $GAME
      add LoginServerPort $CENTER
      add CenterServiceAddress "$CENTER_ADDRESS"
      ;;
    Tank.Web)
      add Web.Urls "http://0.0.0.0:$WEB"
      add Web.SocketPorts $GAME
      add CenterServiceAddress "$CENTER_ADDRESS"
      add Web.PublicBaseUrl "$PUBLIC"
      add Web.RequestRoot "$SITE/Tank.Request"
      add Web.SiteRoot "$SITE/Tank.Flash"
      add Web.FlashRoot "$SITE/flash"
      add Web.ResourceRoot /data/resource
      add Web.ResourceCacheRoot /data/resource-cache
      [ -n "${DDTANK_RUFFLE_URL:-}" ] && add Web.RuffleUrl "$DDTANK_RUFFLE_URL"
      [ -n "${DDTANK_RESOURCE_UPSTREAM:-}" ] && add Web.ResourceUpstream "$DDTANK_RESOURCE_UPSTREAM"
      add LoginUrl "$PUBLIC/Request/CreateLogin.aspx"
      add LoginOnUrl "$PUBLIC/index.htm?open=self"
      add FlashUrl "$PUBLIC/playgame.aspx"
      add FlashConfig "$PUBLIC/flash/config.xml"
      add FlashSite "$PUBLIC/flash/"
      ;;
    GameAdmin.Web)
      add Admin.Urls "http://0.0.0.0:$ADMIN"
      add CenterServiceAddress "$CENTER_ADDRESS"
      add Admin.Root "$SITE/GameAdmin"
      add Admin.Password "${DDTANK_ADMIN_PASSWORD:-}"
      add Resource "$PUBLIC/resource/"
      # LanguageMgr appends this to the program folder (/app/GameAdmin.Web/).
      add LanguagePath "../../srv/ddtank/Tank.Request/bin/Languages/Language-vn.txt"
      ;;
  esac
}

for svc in Center.Service Fighting.Service Road.Service Tank.Web GameAdmin.Web; do
  umask 077
  { echo '<?xml version="1.0" encoding="utf-8"?>'; echo '<appSettings>'; settings "$svc"; echo '</appSettings>'; } \
    > "/app/$svc/appSettings.local.config"
done
umask 022

# Road.Service finds Fighting.Service only through battle.xml (BattleMgr.Setup), not appSettings.
cat > "/app/Road.Service/battle.xml" <<EOF
<?xml version="1.0" encoding="utf-8" ?>
<list>
	<server id="4" ip="127.0.0.1" port="$FIGHT" key="1,7road" />
</list>
EOF

pids=()
stop_all() {
  trap - TERM INT
  echo "Stopping..."
  for ((i=${#pids[@]}-1; i>=0; i--)); do kill -TERM "${pids[i]}" 2>/dev/null || true; done
  wait 2>/dev/null || true
  echo "Stopped."
}
trap 'stop_all; exit 0' TERM INT

start() { # name, folder, ready pattern
  local name=$1 dir="/app/$2" ready=$3 log="$LOGS/$1.log"
  : > "$log"
  (cd "$dir" && exec dotnet "$2.dll" < /dev/null >> "$log" 2>&1) &
  pids+=($!)
  echo "starting $name"
  for _ in $(seq 1 300); do
    if grep -qE "$ready" "$log"; then echo "$name ready"; return; fi
    if ! kill -0 "${pids[-1]}" 2>/dev/null; then echo "$name FAILED:"; tail -30 "$log"; stop_all; exit 1; fi
    sleep 1
  done
  echo "$name did not start in 5 minutes:"; tail -30 "$log"; stop_all; exit 1
}

start center   Center.Service   "GameServer is now open for connections"
start fighting Fighting.Service "GameServer is now open for connections"
start road     Road.Service     "GameServer is now open for connections"
start web      Tank.Web         "Tank.Web listening"
start admin    GameAdmin.Web    "GameAdmin listening"
echo "All services are running: $PUBLIC/index.htm (logs in $LOGS)"

# Show the logs in "docker compose logs", and stop everything when one server exits.
tail -n 0 -F "$LOGS"/*.log &
wait -n "${pids[@]}" || true
echo "A server exited:"; tail -30 "$LOGS"/*.log
stop_all
exit 1
