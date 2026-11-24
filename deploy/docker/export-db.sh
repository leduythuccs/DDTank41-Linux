#!/usr/bin/env bash
# Dumps the game databases of the local MariaDB (deploy/mariadb, container ddtank-mariadb) to seed/ddtank.sql.
# On a new server, a MariaDB container that starts with an empty volume loads seed/*.sql instead of the
# original data in Database/mariadb: copy this folder (with seed/) to the server before the first start.
set -euo pipefail
cd "$(dirname "$0")"
set -a; . ../.env; set +a
mkdir -p seed
docker exec -e MYSQL_PWD="$MARIADB_ROOT_PASSWORD" "${DDTANK_MARIADB_CONTAINER:-ddtank-mariadb}" \
  mariadb-dump -uroot --databases Project_Player34 Project_Game34 Db_Membership \
  --routines --triggers --events --single-transaction --skip-dump-date > seed/ddtank.sql
echo "wrote seed/ddtank.sql ($(du -h seed/ddtank.sql | cut -f1))"
