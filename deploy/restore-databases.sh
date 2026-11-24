#!/usr/bin/env bash
# Creates the game databases in the ddtank-mariadb container (deploy/mariadb/docker-compose.yml) from the scripts in
# ../Database/mariadb: <Db>.schema.sql, <Db>.data.sql and <Db>.routines.sql (generated from the SQL Server backups
# by tools/DbMigrate), and the application login "ddtank" (password DDTANK_DB_PASSWORD in deploy/.env).
# Usage: ./restore-databases.sh [--force]   (--force drops and recreates databases that already exist)
set -euo pipefail
cd "$(dirname "$0")"
set -a; . ./.env; set +a
CONTAINER="${DDTANK_MARIADB_CONTAINER:-ddtank-mariadb}"
FORCE="${1:-}"

sql() { docker exec -i -e MYSQL_PWD="$MARIADB_ROOT_PASSWORD" "$CONTAINER" mariadb -uroot "$@"; }

for i in $(seq 1 30); do
  sql -e "SELECT 1" >/dev/null 2>&1 && break
  [ "$i" = 30 ] && { echo "MariaDB is not reachable (cd mariadb && docker compose --env-file ../.env up -d)"; exit 1; }
  sleep 2
done

for db in Project_Player34 Project_Game34 Db_Membership; do
  exists=$(sql -N -e "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = '$db'")
  if [ "$exists" != 0 ] && [ "$FORCE" != "--force" ]; then
    echo "skip  $db (already exists, use --force to recreate)"
    continue
  fi
  echo "load  $db"
  sql -e "DROP DATABASE IF EXISTS \`$db\`"
  for part in schema data routines; do
    sql < "../Database/mariadb/$db.$part.sql"
  done
  loaded=1
done
# Fixes to the original data (see the file).
[ "${loaded:-}" = 1 ] && sql < ../Database/mariadb/fixes.sql

# Application login (least privilege: the game databases only, not the whole server).
password=$(printf '%s' "$DDTANK_DB_PASSWORD" | sed "s/'/''/g; s/\\\\/\\\\\\\\/g")
sql <<SQL
CREATE USER IF NOT EXISTS 'ddtank'@'%' IDENTIFIED BY '$password';
ALTER USER 'ddtank'@'%' IDENTIFIED BY '$password';
GRANT ALL PRIVILEGES ON \`Project_Player34\`.* TO 'ddtank'@'%';
GRANT ALL PRIVILEGES ON \`Project_Game34\`.* TO 'ddtank'@'%';
GRANT ALL PRIVILEGES ON \`Db_Membership\`.* TO 'ddtank'@'%';
SQL
echo "login ddtank ready"
sql -N -e "SELECT CONCAT(TABLE_SCHEMA, ': ', COUNT(*), ' tables') FROM information_schema.TABLES WHERE TABLE_SCHEMA IN ('Project_Player34','Project_Game34','Db_Membership') GROUP BY TABLE_SCHEMA"
