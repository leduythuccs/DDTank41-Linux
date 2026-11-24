#!/usr/bin/env bash
# Differential test of the database layer: the SQL Server version (branch version-1.0) against this one (MariaDB).
# Both start from the original backups, in throwaway containers (SQL Server on 127.0.0.1:1434, MariaDB on 3308;
# the game's own containers are not touched), run the same calls, and the results are compared.
#
# Usage: tools/DbTest/run.sh [method filter]      e.g. run.sh PlayerBussiness.GetUser
#        tools/DbTest/run.sh --setup              only create the two fresh databases, and leave them running
#                                                 (e.g. as the source for tools/DbMigrate: DDTANK_MSSQL=...1434...)
# Needs: deploy/.env (MSSQL_SA_PASSWORD, MARIADB_ROOT_PASSWORD, DDTANK_DB_PASSWORD), docker, python3, and the
# version-1.0 code checked out next to this one:  git worktree add ../DDTank41-v1 version-1.0
#   DBTEST_V1   path of the version-1.0 checkout (default ../DDTank41-v1)
#   DBTEST_KEEP=1  keep the containers running afterwards (to look at the data); run again to reuse them
# Output: tools/DbTest/out/report.txt (and the raw calls-*.jsonl, tables-*.json).
set -euo pipefail
cd "$(dirname "$0")"
HERE=$PWD
ROOT=$(cd ../.. && pwd)
V1=$(cd "${DBTEST_V1:-$ROOT/../DDTank41-v1}" && pwd)
OUT=$HERE/out
FILTER="${1:-}"
SETUP_ONLY=0
if [ "$FILTER" = --setup ]; then SETUP_ONLY=1; FILTER=""; DBTEST_KEEP=1; docker rm -f ddtank-dbtest-mssql ddtank-dbtest-mariadb >/dev/null 2>&1 || true; fi
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
set -a; . "$ROOT/deploy/.env"; set +a
MSSQL=ddtank-dbtest-mssql
MARIA=ddtank-dbtest-mariadb
mkdir -p "$OUT"

running() { [ "$(docker inspect -f '{{.State.Running}}' "$1" 2>/dev/null)" = true ]; }
cleanup() { [ "${DBTEST_KEEP:-}" = 1 ] || docker rm -f "$MSSQL" "$MARIA" >/dev/null 2>&1 || true; }
trap cleanup EXIT

# ---------------------------------------------------------------- databases
fresh=0
if ! running "$MSSQL" || ! running "$MARIA"; then
  fresh=1
  docker rm -f "$MSSQL" "$MARIA" >/dev/null 2>&1 || true
  echo "start throwaway SQL Server (1434) and MariaDB (3308)"
  docker run -d --name "$MSSQL" -p 127.0.0.1:1434:1433 -e ACCEPT_EULA=Y -e MSSQL_PID=Developer \
    -e MSSQL_SA_PASSWORD="$MSSQL_SA_PASSWORD" -e MSSQL_COLLATION=SQL_Latin1_General_CP1_CI_AS \
    -v "$ROOT/Database:/backups:ro" mcr.microsoft.com/mssql/server:2022-latest >/dev/null
  docker run -d --name "$MARIA" -p 127.0.0.1:3308:3306 -e MARIADB_ROOT_PASSWORD="$MARIADB_ROOT_PASSWORD" mariadb:11.4 \
    --lower-case-table-names=1 --character-set-server=utf8mb4 --collation-server=utf8mb4_uca1400_as_ci \
    --max-allowed-packet=64M >/dev/null
fi

msql() {
  docker exec -e SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" "$MSSQL" \
    /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -b -h -1 -W -s '|' -Q "SET NOCOUNT ON; $1"
}

if [ $fresh = 1 ]; then
  for i in $(seq 1 60); do msql "SELECT 1" >/dev/null 2>&1 && break; sleep 2; done
  for bak in "$ROOT"/Database/*.bak; do
    file=$(basename "$bak")
    db=$(msql "RESTORE HEADERONLY FROM DISK='/backups/$file'" | head -1 | cut -d'|' -f10)
    moves=""
    while IFS='|' read -r logical physical type _; do
      [ -z "$logical" ] && continue
      ext=mdf; [ "$type" = "L" ] && ext=ldf
      moves+=", MOVE '$logical' TO '/var/opt/mssql/data/${db}_${logical}.${ext}'"
    done < <(msql "RESTORE FILELISTONLY FROM DISK='/backups/$file'")
    msql "RESTORE DATABASE [$db] FROM DISK='/backups/$file' WITH REPLACE${moves}" >/dev/null
  done
  msql "IF SUSER_ID('ddtank') IS NULL CREATE LOGIN ddtank WITH PASSWORD='$DDTANK_DB_PASSWORD', CHECK_POLICY=OFF;"
  for db in Project_Player34 Project_Game34 Db_Membership; do
    msql "USE [$db]; IF USER_ID('ddtank') IS NOT NULL DROP USER ddtank; CREATE USER ddtank FOR LOGIN ddtank; ALTER ROLE db_owner ADD MEMBER ddtank;"
  done
  DDTANK_MSSQL_CONTAINER=$MSSQL python3 -I "$ROOT/deploy/patch-procedures.py" >/dev/null
  echo "SQL Server ready"
  DDTANK_MARIADB_CONTAINER=$MARIA "$ROOT/deploy/restore-databases.sh" --force >/dev/null
  echo "MariaDB ready"
else
  echo "reusing the running test containers (their data is NOT fresh; remove them for a clean run)"
fi

[ $SETUP_ONLY = 1 ] && { echo "test databases left running: SQL Server 127.0.0.1:1434, MariaDB 127.0.0.1:3308"; exit 0; }

# ---------------------------------------------------------------- build
build() { # <name> <Bussiness.csproj>
  local src="$OUT/src-$1"
  rm -rf "$src"; mkdir -p "$src"
  cp "$HERE/DbTest.csproj" "$HERE/Program.cs" "$src/"
  "$DOTNET" build "$src/DbTest.csproj" -nologo -v q -c Debug -p:BussinessProject="$2" -o "$OUT/bin-$1" 2>&1 \
    | grep -E " error |rror\(s\)" | grep -v "^ *0 Error" || true
  [ -f "$OUT/bin-$1/DbTest.dll" ] || { echo "build $1 failed"; exit 1; }
}
build mssql "$V1/Bussiness/Bussiness.csproj"
build mariadb "$ROOT/Bussiness/Bussiness.csproj"

xml() { printf '%s' "$1" | sed 's/&/\&amp;/g; s/"/\&quot;/g; s/</\&lt;/g'; }
config() { # <bin dir> <connection string with {db}>
  {
    echo '<?xml version="1.0" encoding="utf-8"?><configuration><appSettings>'
    for kv in conString:Project_Player34 crosszoneString:Project_Game34 membershipDb:Db_Membership; do
      echo "<add key=\"${kv%%:*}\" value=\"$(xml "${2//\{db\}/${kv#*:}}")\" />"
    done
    echo '</appSettings></configuration>'
  } > "$1/DbTest.dll.config"
  chmod 600 "$1/DbTest.dll.config"
}
config "$OUT/bin-mssql" "Data Source=127.0.0.1,1434;Initial Catalog={db};User ID=ddtank;Password=$DDTANK_DB_PASSWORD;TrustServerCertificate=True"
config "$OUT/bin-mariadb" "Server=127.0.0.1;Port=3308;Database={db};User ID=ddtank;Password=\"$DDTANK_DB_PASSWORD\";Pooling=true;ConvertZeroDateTime=true"

# ---------------------------------------------------------------- run and compare
echo "running the calls on both"
(cd "$OUT/bin-mssql" && DBTEST_KIND=mssql "$DOTNET" DbTest.dll calls "$OUT/calls-mssql.jsonl" $FILTER > "$OUT/log-mssql.txt" 2>&1) &
(cd "$OUT/bin-mariadb" && DBTEST_KIND=mariadb "$DOTNET" DbTest.dll calls "$OUT/calls-mariadb.jsonl" $FILTER > "$OUT/log-mariadb.txt" 2>&1) &
wait %1 || { echo "SQL Server run failed:"; tail -20 "$OUT/log-mssql.txt"; exit 1; }
wait %2 || { echo "MariaDB run failed:"; tail -20 "$OUT/log-mariadb.txt"; exit 1; }
(cd "$OUT/bin-mssql" && DBTEST_KIND=mssql "$DOTNET" DbTest.dll dump "$OUT/tables-mssql.json" >> "$OUT/log-mssql.txt" 2>&1)
(cd "$OUT/bin-mariadb" && DBTEST_KIND=mariadb "$DOTNET" DbTest.dll dump "$OUT/tables-mariadb.json" >> "$OUT/log-mariadb.txt" 2>&1)
grep -h "seed" "$OUT/log-mssql.txt" "$OUT/log-mariadb.txt"
python3 -I "$HERE/compare.py" "$OUT" | tee "$OUT/report.txt" | tail -25
echo "full report: $OUT/report.txt"
