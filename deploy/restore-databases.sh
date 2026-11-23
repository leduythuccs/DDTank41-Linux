#!/usr/bin/env bash
# Restores the SQL Server backups in ../Database into the ddtank-mssql container.
# Usage: ./restore-databases.sh [--force]   (--force overwrites existing databases)
set -euo pipefail
cd "$(dirname "$0")"
set -a; . ./.env; set +a
CONTAINER=ddtank-mssql
REPLACE=""
[ "${1:-}" = "--force" ] && REPLACE=", REPLACE"

sql() {
  docker exec -e SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" "$CONTAINER" \
    /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -b -h -1 -W -s '|' -Q "SET NOCOUNT ON; $1"
}

for bak in ../Database/*.bak; do
  file=$(basename "$bak")
  db=$(sql "RESTORE HEADERONLY FROM DISK='/backups/$file'" | head -1 | cut -d'|' -f10)
  if [ -z "$REPLACE" ] && [ "$(sql "SELECT COUNT(*) FROM sys.databases WHERE name='$db'")" != "0" ]; then
    echo "skip  $db (already exists, use --force to overwrite)"
    continue
  fi
  moves=""
  while IFS='|' read -r logical physical type _; do
    [ -z "$logical" ] && continue
    ext=mdf; [ "$type" = "L" ] && ext=ldf
    moves+=", MOVE '$logical' TO '/var/opt/mssql/data/${db}_${logical}.${ext}'"
  done < <(sql "RESTORE FILELISTONLY FROM DISK='/backups/$file'")
  echo "restore $file -> $db"
  sql "RESTORE DATABASE [$db] FROM DISK='/backups/$file' WITH STATS=25${moves}${REPLACE}" >/dev/null
done
sql "SELECT name + '  (compat ' + CAST(compatibility_level AS varchar) + ')' FROM sys.databases WHERE database_id > 4"

# Application login (least privilege: db_owner on the game databases only, not sysadmin).
if [ -n "${DDTANK_DB_PASSWORD:-}" ]; then
  sql "IF SUSER_ID('ddtank') IS NULL CREATE LOGIN ddtank WITH PASSWORD='$DDTANK_DB_PASSWORD', CHECK_POLICY=OFF; ELSE ALTER LOGIN ddtank WITH PASSWORD='$DDTANK_DB_PASSWORD';"
  for db in Project_Player34 Project_Game34 Db_Membership; do
    sql "USE [$db]; IF USER_ID('ddtank') IS NOT NULL DROP USER ddtank; CREATE USER ddtank FOR LOGIN ddtank; ALTER ROLE db_owner ADD MEMBER ddtank;"
  done
  echo "login ddtank ready"
fi
