# DbTest: fast check of the MariaDB port

Runs the game's whole database layer (`Bussiness`, about 440 public methods) against MariaDB and against the
original SQL Server version (branch `version-1.0`), from the same starting data, and compares what comes back.
No game client, no browser, no servers: a full run takes about 30 seconds.

```bash
git worktree add ../DDTank41-v1 version-1.0      # once: the SQL Server version, next to this checkout
tools/DbTest/run.sh                              # everything
tools/DbTest/run.sh PlayerBussiness.GetUser      # only the methods whose name contains this
```

It needs `deploy/.env` with `MSSQL_SA_PASSWORD`, `MARIADB_ROOT_PASSWORD` and `DDTANK_DB_PASSWORD`, Docker, the
.NET 8 SDK and Python 3.

## What it does

1. Starts two throwaway containers: SQL Server on `127.0.0.1:1434` (restored from `Database/*.bak`, patched by
   `deploy/patch-procedures.py`) and MariaDB on `127.0.0.1:3308` (loaded by `deploy/restore-databases.sh`). The
   game's own containers (`ddtank-mssql`, `ddtank-mariadb`) are not touched.
2. Builds `DbTest` twice: against `../DDTank41-v1/Bussiness` (SQL Server) and `Bussiness` (MariaDB).
3. Both create the same two test characters, then call every public method of every `*Bussiness` class, in the
   same order, with arguments made up from the parameter names (`userID` is the first test character, `templateID`
   a real item, ...). Methods that delete or reset run last.
4. Records each call's return value, ref/out values and the errors logged during it, then a hash of every table.
5. `compare.py` writes `tools/DbTest/out/report.txt`.

`DBTEST_KEEP=1` leaves the containers running afterwards, to look at the data (run with `--setup` to only create
them). The next run reuses running containers, so remove them for a clean run:
`docker rm -f ddtank-dbtest-mssql ddtank-dbtest-mariadb`.

## Reading the report

- **ERRORS ONLY ON MARIADB**: the call failed on MariaDB but worked on SQL Server. Almost surely a port bug.
- **RESULT DIFFERS**: both worked but returned different values. Known harmless ones:
  - Vietnamese text: SQL Server stores some of it as `?` (non-Unicode columns), MariaDB keeps it.
  - Rows that tie in a ranking (two test characters with the same value): SQL Server orders ties arbitrarily.
  - `GetALllShop`: every shop row has `sort` 0 or 1, and SQL Server orders the ties differently from one query
    to the next.
  - Calls that pass more parameters than the procedure has: SQL Server refuses them, the MariaDB layer ignores
    the extra ones (and later calls see the difference).
- **ERRORS ON BOTH**: also broken on SQL Server: missing procedures, old code calling newer procedures, or
  made-up arguments that make no sense. Check that MariaDB fails for the same reason.
- **TABLES DIFFER**: what the calls wrote differs.

The arguments are made up, so a method can pass here and still fail with real game data. When the game hits a
database error, it is logged by `SqlDataProvider` in the service's log; reproduce it here with a filter.
