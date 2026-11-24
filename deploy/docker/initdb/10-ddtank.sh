#!/bin/bash
# Runs once, when the MariaDB container starts with an empty data volume (/docker-entrypoint-initdb.d).
# Loads the game databases, then creates the login "ddtank" (DDTANK_DB_PASSWORD).
#   - deploy/docker/seed/*.sql present (made by export-db.sh): those dumps, e.g. your current local data;
#   - otherwise the scripts of the repository (Database/mariadb) and their fixes, as deploy/restore-databases.sh.
set -euo pipefail
sql() { mariadb -uroot -p"$MARIADB_ROOT_PASSWORD" "$@"; }

shopt -s nullglob
seeds=(/ddtank/seed/*.sql)
if [ ${#seeds[@]} -gt 0 ]; then
  for f in "${seeds[@]}"; do
    echo "ddtank: loading $f"
    sql < "$f"
  done
else
  for db in Project_Player34 Project_Game34 Db_Membership; do
    for part in schema data routines; do
      echo "ddtank: loading $db.$part.sql"
      sql < "/ddtank/sql/$db.$part.sql"
    done
  done
  sql < /ddtank/sql/fixes.sql
fi

password=$(printf '%s' "${DDTANK_DB_PASSWORD:?set DDTANK_DB_PASSWORD}" | sed "s/'/''/g; s/\\\\/\\\\\\\\/g")
sql <<SQL
CREATE USER IF NOT EXISTS 'ddtank'@'%' IDENTIFIED BY '$password';
GRANT ALL PRIVILEGES ON \`Project_Player34\`.* TO 'ddtank'@'%';
GRANT ALL PRIVILEGES ON \`Project_Game34\`.* TO 'ddtank'@'%';
GRANT ALL PRIVILEGES ON \`Db_Membership\`.* TO 'ddtank'@'%';
-- The game server runs next to Tank.Web's socket proxy, inside the game container.
UPDATE Project_Player34.Server_List SET IP = '127.0.0.1', Port = 16202;
SQL
echo "ddtank: databases ready"
