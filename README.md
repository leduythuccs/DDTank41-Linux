# DDTank 4.1 (Gunny) on Linux

A DDTank / Gunny 4.1 private server ported to **.NET 8 on Linux**, with the Flash client running in
the browser through [Ruffle](https://ruffle.rs). No Windows, IIS or Flash Player needed.



> **Stable version:** this branch runs on **MariaDB** and is new. The version that has been played the
> most is on the [`version-1.0`](../../tree/version-1.0) branch, which needs **SQL Server**. If you want
> the stable one, use that branch and follow its README:
>
> ```bash
> git clone -b version-1.0 <this repo>
> ```

Disclamer: I have no idea how this code works, all I did was ask claude do port to Linux, then I do the testing & ask
Claude to fix any bug I found while playing

- All servers (Center, Fighting, Road and the web site/API) run as .NET 8 processes.
- The databases run in MariaDB 11.4 in Docker, created from the SQL scripts in `Database/mariadb/`
  (converted from the original SQL Server backups, see [Database](#database)).
- The game client is the original SWF, patched where Ruffle behaves differently from Flash Player
  (see [Client patches](#client-patches)).
- A small admin site sends mail, items and money to characters.

## Requirements

- Linux. Tested on **x86_64** only.
- [Docker](https://docs.docker.com/engine/install/) with the compose plugin (`docker compose`). Your user
  must be able to run `docker` without sudo (the `docker` group).
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). `run-local.sh` uses `dotnet` from the
  PATH, otherwise `~/.dotnet/dotnet`.
- Python 3.

## Quick start

```bash
git clone <this repo> && cd DDTank41

# 1. Settings (see the table below). This file holds passwords: it is git-ignored, never commit it.
cat > deploy/.env <<'EOF'
MARIADB_ROOT_PASSWORD=Change-me-Root-1
DDTANK_DB_PASSWORD=Change-me-Db-1
DDTANK_RUFFLE_URL=https://unpkg.com/@ruffle-rs/ruffle@0.8.0/ruffle.js
DDTANK_RESOURCE_UPSTREAM=http://gunny.vcdn.vn/
EOF

# 2. Database: start MariaDB, create the databases and the app login, write the service configs.
cd deploy
(cd mariadb && docker compose --env-file ../.env up -d)
./restore-databases.sh
./configure-local.sh
cd ..

# 3. Build.
dotnet build DDTank.Server.Linux.sln

# 4. Run everything (Ctrl+C stops all services cleanly).
deploy/run-local.sh
```

Open <http://127.0.0.1:8080/index.htm>, click **Đăng ký nhanh** to create an account, then log in.
The first time you load the game is slow, because the art is downloaded and cached (see [Game art](#game-art)).

### deploy/.env

| Variable | Required | Meaning |
|---|---|---|
| `MARIADB_ROOT_PASSWORD` | yes | MariaDB `root` password, used only by the setup scripts. |
| `DDTANK_DB_PASSWORD` | yes | Password of the `ddtank` user the servers use. It has rights on the 3 game databases only. |
| `DDTANK_RUFFLE_URL` | yes, in practice | The Ruffle build the game page loads. **Use 0.8.0.** The built-in default, 0.7.1, stalls at 85% while loading. |
| `DDTANK_RESOURCE_UPSTREAM` | yes, in practice | Where art and sound files are downloaded from when they are not local. Without it the game has no images. |
| `DDTANK_ADMIN_PASSWORD` | no | Admin site password. Generated and saved to `.env` by `configure-local.sh` when missing. |
| `DDTANK_DB_HOST` | no | MariaDB address, default `127.0.0.1`. |
| `DDTANK_MARIADB_PORT` | no | MariaDB port, default `3306`. Change it if another MySQL/MariaDB already uses 3306. |
| `DDTANK_PORT_OFFSET` | no | Added to every game port (8080 becomes 8080 + offset, and so on), to run a second copy next to another one. Default 0. |

After changing `.env`, run `deploy/configure-local.sh` again and restart `run-local.sh`.

## Services and ports

Everything listens on `127.0.0.1` only.

| Service | Port | What it does |
|---|---|---|
| Tank.Web | 8080 | Login page, game page, the client's HTTP API, art files, and a WebSocket proxy to the game server (Ruffle cannot open raw TCP sockets). |
| GameAdmin.Web | 8081 | Admin site: mail with items, money. |
| Road (game server) | 9500 | Game socket, reached through Tank.Web at `/socket/9500`. |
| Center | 9202, 2008, 2009 | Login and server coordination. |
| Fighting | 9208 | Matchmaking and battles. |
| MariaDB (Docker) | 3306 | `Project_Player34`, `Project_Game34`, `Db_Membership`. |

Logs are written to `deploy/logs/*.log`. If a service hangs at "starting...", another process
probably still holds its port. Check with `ss -tlnp`.

## Database

The game was written for SQL Server: about 550 stored procedures in T-SQL. This branch runs on MariaDB:

- `Database/mariadb/<Db>.schema.sql`, `.data.sql` and `.routines.sql` create the tables, the starting data
  and the procedures. `deploy/restore-databases.sh` loads them; `--force` drops and recreates databases
  that already exist (**this deletes all accounts and characters**).
- These scripts were generated from the SQL Server backups in `Database/*.bak` by `tools/DbMigrate`, which
  translates the T-SQL procedures to MariaDB. A few procedures are written by hand in
  `Database/mariadb/manual/`.
- `SqlDataProvider` talks to MariaDB through MySqlConnector, and gives the game code the same value types
  SQL Server did.
- `tools/DbTest` runs the game's database layer against MariaDB without the game client, to check the
  procedures quickly (see its README).

The data lives in the Docker volume `ddtank-mariadb_mariadb-data`. To back it up:

```bash
docker exec -e MYSQL_PWD="<root password>" ddtank-mariadb mariadb-dump -uroot --routines \
  --databases Project_Player34 Project_Game34 Db_Membership > ddtank-backup.sql
```

`deploy/docker-compose.yml` (SQL Server) and `deploy/patch-procedures.py` are only needed to regenerate
the scripts from the backups.

## Game art

The art and sound files (about 1 GB) are **not in this repository**. They are third-party assets. Tank.Web
serves `/resource/` from:

1. `resource/`: a local resource pack, if you have one (git-ignored);
2. `resource-cache/`: files already downloaded (git-ignored);
3. otherwise it downloads the file from `DDTANK_RESOURCE_UPSTREAM` and saves it to `resource-cache/`.

To download everything ahead of time, run this while the servers are running. It works out the file names from the database, and it
skips files that are already cached or known to be missing, so you can stop it and run it again:

```bash
python3 deploy/prefetch-resources.py --jobs 2 --delay 0.2
```

If the upstream site goes away, only the files already in `resource-cache/` or `resource/` will work.

## Admin site

<http://127.0.0.1:8081/>. The password is `DDTANK_ADMIN_PASSWORD` in `deploy/.env`.

You can send mail with up to 5 items (strengthen level, hợp thành, expiry), or send xu, vàng and lễ kim.
The receiver can be an account name, a character name or a character ID.
Online players get the mail immediately.

## Bug fixes

Bugs fixed that I found while playing, and ask Claude to fix

### Game client (browser / Ruffle)

- **Music:** every room and map played the same track. Ruffle kept playing the previous track when the
  music stream was reused, so each track now gets its own stream.
- **Music volume:** the in-game music setting does nothing under Ruffle. The game page has its own
  volume slider (music and effects together), which the browser remembers.
- **Stutter before the battle rewards:** the reward screen took a snapshot of the map and used motion blur,
  both slow in Ruffle. The reward screen now shows over the live map, without the blur.
- **Cannot walk after being it:** The map layout was broken when it get hit, the issue was how Ruffle draw image i think? Fixed by masking the
  crater edges with a method Ruffle renders correctly.
- **Drag and drop:** selling items and dragging them in the bag did nothing. Drop targets are now found
  with `hitTestPoint`, because Ruffle's `getObjectsUnderPoint` returns the wrong objects.
- **Vietnamese text:** characters with stacked diacritics were cut off ("Gà Sát Thủ" showed as "Gà Sát Th").
  Text in the game now uses the browser's fonts.
- **Blank texts:** alerts, tips, button labels and tooltips were empty because `language.txt` had lost its
  CRLF line endings. `.gitattributes` now keeps them.
- **Item tooltips** never appeared: the UI config was older than the game code. The missing piece is added
  by `deploy/patch-ui-config.py`.
- **Improve loading time:** item and shop lists are parsed in time slices, instead of a fixed number per frame
  that added about 5 seconds to the loading screen.


### Game server
- **Pet manh hóa:** with "use all", the stones needed for the next level were used, but only one stone's
  exp was added. Every stone now counts. Upgrading with no stones no longer throws an error.
- **Random login failures ("connection failed"):** about 1 login in 256 failed because the client
  sometimes sends its encrypted login data one byte short. The server now accepts it.
- **Road wouldn't start on Linux:** data files were opened as `datas/` while the folder is `Datas/`.
  This works on Windows but not on Linux, where file names are case-sensitive.
- **Battle stuck at 91% loading:** the pet skill "Hơi Thở Băng Giá" uses a ball with no explosion set, so
  the client waited forever for a file that does not exist. Fixed in the ball list and in
  `Database/mariadb/fixes.sql`.
- **Database moved to MariaDB:** SQL Server needs about 2 GB of RAM and has no ARM build. The
  procedures were translated to MariaDB (see [Database](#database)).
- **Old code, newer database:** some stored procedures in the restored database expect different
  parameters than this code sends. `deploy/patch-procedures.py` patches them when the database is restored.
- **Game AI scripts** were rebuilt from the original `Game.Server.dll`, because the scripts in git didn't
  match the shipped server.

## Changes from the original game

These are deliberate gameplay changes in this branch:

- **Strengthening (cường hoá) always succeeds.** To restore the normal odds, set `AlwaysSucceeds` in
  `Game.Server/Packets/Client/ItemStrengthenHandler.cs` to false.
- **Bot matches:** the player always takes the first turn. In the PvP room, the host chooses with the
  **"Đấu với: N bot"** button how many bots (1-4) matchmaking falls back to.
- **Selling:** valuable equipment (+7 and higher) can be sold. The game still asks for confirmation.
- **Tu luyện:** one click uses the whole stack of potions in the slot. The daily limit and the stat
  cap still apply.
- **Bot matches start right away:** `AutoBotWaitSeconds` is 0 in `Fighting.Service/App.config`. The
  original waited about 20 seconds for real players before falling back to a bot.
- **Win rate**: now show in battles

## Client patches

The game code is `Source Flash/FlashSV1/2.png`, a SWF despite its name. Its decompiled source is in
`Source Flash/src`. Some classes are patched to work around Ruffle differences, such as broken crater
masks, slow loading, drag and drop, music switching and stutter on the reward screen. Each patch is
described at the top of `deploy/patch-client.sh`.

To change the client: edit the class in `Source Flash/src`, add it to `CLASSES` in `deploy/patch-client.sh`,
then rebuild `2.png` with [JPEXS FFDec](https://github.com/jindrapetrik/jpexs-decompiler/releases) 26.5.0
(needs Java):

```bash
FFDEC=/path/to/ffdec.jar deploy/patch-client.sh
```

Browsers check for a new client on every load, so a normal page reload picks up a rebuilt `2.png`.

