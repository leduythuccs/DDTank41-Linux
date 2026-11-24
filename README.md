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
- The databases run in MariaDB 11.4, created from the SQL scripts in `Database/mariadb/`
  (converted from the original SQL Server backups, see [Database](#database)).
- The game client is the original SWF, patched where Ruffle behaves differently from Flash Player
  (see [Client patches](#client-patches)).
- A small admin site sends mail, items and money to characters, and switches game options on and off.

## Quick start (Docker)

You need [Docker](https://docs.docker.com/engine/install/) with the compose plugin (`docker compose`).
Nothing else: the image builds the servers itself. It runs on x86_64 and arm64 (tested on x86_64).

```bash
git clone <this repo> && cd DDTank41/deploy/docker

# 1. Init the env with random passwords
./init-env.sh http://127.0.0.1:16200

# 2. Build and start the docker
docker compose up -d --build
docker compose logs -f game        # wait for "All services are running"
```

Open `http://127.0.0.1:16200/index.htm`, click **Đăng ký nhanh** to create an account, then log in.

Only port **16200** has to be reachable from the internet (open it in the firewall). The game socket goes
through the web site, so no other port is needed.

Everyday commands, from `deploy/docker`:

| What | Command |
|---|---|
| Stop / start | `docker compose stop` / `docker compose start` (players are saved on stop) |
| Update after `git pull` | `docker compose up -d --build` |
| Logs | `docker compose logs -f game`, or the files in `deploy/docker/data/logs/` |
| Delete everything, including all accounts | `docker compose down -v` |

### Settings (deploy/docker/.env)

`init-env.sh` writes this file; you could edit it and run `docker compose up -d` to apply.

| Variable | Meaning |
|---|---|
| `DDTANK_PUBLIC_URL` | The address players open, e.g. `http://127.0.0.1:16200`, or a domain: `http://example.com` |
| `DDTANK_ADMIN_PASSWORD` | Admin site password. |
| `MARIADB_ROOT_PASSWORD`, `DDTANK_DB_PASSWORD` | MariaDB `root` and the `ddtank` user the servers use. Read only when the databases are created: changing them later does not change the database. |
| `DDTANK_RESOURCE_UPSTREAM` | Where art and sound files are downloaded from when they are not cached. Default to VNG CDN |
| `DDTANK_RUFFLE_URL` | Optional. The Ruffle build the game page loads; default 0.8.0. |
| `DDTANK_ADMIN_BIND`, `DDTANK_DB_BIND` | Optional. Default `127.0.0.1` |
| `DDTANK_TZ` | Optional. Time zone of the game (daily resets, events), default `Asia/Ho_Chi_Minh`. |

### Ports

All in the 162xx range:

| Port | Service | Reachable from |
|---|---|---|
| 16200 | Tank.Web: login page, game page, the client's HTTP API, art files, and a WebSocket proxy to the game server (Ruffle cannot open raw TCP sockets). | the internet |
| 16201 | GameAdmin.Web: the admin site. | the server (127.0.0.1) |
| 16207 | MariaDB: `Project_Player34`, `Project_Game34`, `Db_Membership`. | the server (127.0.0.1) |
| 16202 | Road, the game server. Reached through Tank.Web at `/socket/16202`. | inside the container |
| 16203, 16205, 16206 | Center: login and server coordination. | inside the container |
| 16204 | Fighting: matchmaking and battles. | inside the container |

### Behind nginx (domain and HTTPS)

To serve the game on a domain, let nginx forward it to port 16200:

1. In `.env`, set `DDTANK_PUBLIC_URL` to the address players open, without the port, e.g.
   `DDTANK_PUBLIC_URL=https://game.example.com`. Then `docker compose up -d`.
2. Forward everything to 16200. The game socket is a WebSocket under `/socket/`.

```nginx
server {
    server_name game.example.com;

    location / {
        proxy_pass http://127.0.0.1:16200;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }

    location /socket/ {
        proxy_pass http://127.0.0.1:16200;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_read_timeout 1h;
        proxy_send_timeout 1h;
    }
}
```

With `https://` in `DDTANK_PUBLIC_URL`, the client uses `wss://` for the game socket by itself.

## Database

The game was written for SQL Server: about 550 stored procedures in T-SQL. This branch runs on MariaDB:

- `Database/mariadb/<Db>.schema.sql`, `.data.sql` and `.routines.sql` create the tables, the starting data
  and the procedures. Docker loads them on the first start; `deploy/restore-databases.sh` does the same for
  [Development without Docker](#development-without-docker).
- These scripts were generated from the SQL Server backups in `Database/*.bak` by `tools/DbMigrate`, which
  translates the T-SQL procedures to MariaDB. A few procedures are written by hand in
  `Database/mariadb/manual/`.
- `SqlDataProvider` talks to MariaDB through MySqlConnector, and gives the game code the same value types
  SQL Server did.
- `tools/DbTest` runs the game's database layer against MariaDB without the game client, to check the
  procedures quickly (see its README).

To back up the data of the Docker setup, from `deploy/docker`:

```bash
docker compose exec mariadb sh -c 'MYSQL_PWD="$MARIADB_ROOT_PASSWORD" mariadb-dump -uroot -P16207 --routines \
  --databases Project_Player34 Project_Game34 Db_Membership' > ddtank-backup.sql
```

`deploy/docker-compose.yml` (SQL Server) and `deploy/patch-procedures.py` are only needed to regenerate
the scripts from the backups.

## Game art

The art and sound files (about 1 GB) are **not in this repository**. They are third-party assets. Tank.Web
downloads each file from `DDTANK_RESOURCE_UPSTREAM` the first time it is needed and keeps it in
`deploy/docker/data/resource-cache/` (`resource-cache/` without Docker). A local resource pack in `resource/`
is used first, if you have one.

To download everything ahead of time, run this while the servers run without Docker (with Docker, copy a
filled `resource-cache/` into `deploy/docker/data/resource-cache/` instead). It works out the file names from
the database and skips files that are already cached or known to be missing, so you can stop it and run it
again:

```bash
python3 deploy/prefetch-resources.py --jobs 2 --delay 0.2
```

If the upstream site goes away, only the files already cached will work.

## Admin site

Port 16201 (see [Ports](#ports)); <http://127.0.0.1:8081/> without Docker. The password is
`DDTANK_ADMIN_PASSWORD` in the `.env` file.

- **SendMail:** mail with up to 5 items (strengthen level, hợp thành, expiry), or xu, vàng and lễ kim. The
  receiver can be an account name, a character name or a character ID. Online players get the mail
  immediately.
- **Cấu hình:** game options (see [Changes from the original game](#changes-from-the-original-game)).
  A change applies to the running server at once.

## Development without Docker

To work on the code, run the servers directly. You need Linux, Docker (for MariaDB only, with your user in
the `docker` group), the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (`dotnet` on the
PATH, or `~/.dotnet/dotnet`) and Python 3. The servers then use the original ports (8080, 8081, 9500...),
all on `127.0.0.1`.

1. Create `deploy/.env` (git-ignored, it holds passwords) with `MARIADB_ROOT_PASSWORD`, `DDTANK_DB_PASSWORD`,
   `DDTANK_RUFFLE_URL=https://unpkg.com/@ruffle-rs/ruffle@0.8.0/ruffle.js` and
   `DDTANK_RESOURCE_UPSTREAM=http://gunny.vcdn.vn/`.
2. Start MariaDB, create the databases and write the service configs:
   `cd deploy && (cd mariadb && docker compose --env-file ../.env up -d) && ./restore-databases.sh && ./configure-local.sh`
3. Build and run (Ctrl+C stops all services cleanly):
   `dotnet build DDTank.Server.Linux.sln && deploy/run-local.sh`

Open <http://127.0.0.1:8080/index.htm>; the admin site is <http://127.0.0.1:8081/>. Logs are in
`deploy/logs/*.log`. If a service hangs at "starting...", another process probably still holds its port
(check with `ss -tlnp`).

Optional in `deploy/.env`: `DDTANK_ADMIN_PASSWORD` (generated when missing), `DDTANK_DB_HOST` /
`DDTANK_MARIADB_PORT` (default `127.0.0.1` / `3306`), and `DDTANK_PORT_OFFSET`, added to every port to run a
second copy next to another one. After changing it, run `deploy/configure-local.sh` again and restart
`run-local.sh`. `deploy/restore-databases.sh --force` recreates the databases (**this deletes all accounts
and characters**).

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
- **Pet auto-attack never happened** for most pets: the skill's damage was 0 in the data (34 skills) or tuned
  for a hidden division by 3. Every auto-attack skill now deals the % its description says, set in
  `Database/mariadb/fixes.sql`. The skill has to be equipped in one of the pet's 3 skill slots.
- **Database moved to MariaDB:** SQL Server needs about 2 GB of RAM and has no ARM build. The
  procedures were translated to MariaDB (see [Database](#database)).
- **Old code, newer database:** some stored procedures in the restored database expect different
  parameters than this code sends. `deploy/patch-procedures.py` patches them when the database is restored.
- **Game AI scripts** were rebuilt from the original `Game.Server.dll`, because the scripts in git didn't
  match the shipped server.

## Changes from the original game

These are deliberate gameplay changes in this branch:

- **Options on the admin site (Cấu hình):** strengthening (cường hoá) always succeeds (on by default);
  Đá tăng bậc and Châu báu chúc phúc always succeed; Tẩy điểm always rolls the highest value; no daily
  Tu luyện limit; free matches, even alone, count as guild wars for quests and guild tasks.
- **One click uses everything:** card upgrades in the album use all the copies, and drilling a hole uses
  all the matching Mũi khoan.
- **Holes 5 and 6 go to level 5** with Mũi khoan cấp 5 (the client stopped at level 4).
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

