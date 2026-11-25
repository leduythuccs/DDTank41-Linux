# DDTank 4.1 (Gunny) on Linux

A DDTank / Gunny 4.1 private server ported to Linux, with the Flash client running in
the browser through [Ruffle](https://ruffle.rs)

Disclamer: I have no idea how this code works, all I did was ask claude do port to Linux, then I do the testing & ask
Claude to fix any bug I found while playing. I migrate the database to mariadb because it require less resource to run.

Game resources/art/sound files ... are NOT in this repo, the code get the resource from VNG CDN. More on it in [game-art](#game-art)

**What's different from the original server** (gameplay changes, admin site features, bug fixes) is listed in
[CHANGES.md](CHANGES.md).

## Quick start (Docker)

```bash
git clone https://github.com/leduythuccs/DDTank41-Linux.git && cd DDTank41/deploy/docker

# 1. Init the env with random passwords
./init-env.sh http://127.0.0.1:16200

# 2. Build and start the docker
docker compose up -d --build
docker compose logs -f game        # wait for "All services are running"
```

Open `http://127.0.0.1:16200/index.htm`, click **Đăng ký nhanh** to create an account, then log in.

Only port **16200** has to be reachable from the internet (open it in the firewall). The game socket goes
through the web site, so no other port is needed.


That's all you need to do to spin up a server

### Settings (deploy/docker/.env)

`init-env.sh` writes this file; you could edit it and run `docker compose up -d` to apply.

| Variable | Meaning |
|---|---|
| `DDTANK_PUBLIC_URL` | The address players open, e.g. `http://127.0.0.1:16200`, or a domain: `https://example.com` |
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
- These scripts were translated from the original SQL Server backups, T-SQL procedures included; a few
  procedures are written by hand in `Database/mariadb/manual/`. They are now the source of the database:
  change them directly.
- `SqlDataProvider` talks to MariaDB through MySqlConnector, and gives the game code the same value types
  SQL Server did.

To back up the data of the Docker setup, from `deploy/docker`:

```bash
docker compose exec mariadb sh -c 'MYSQL_PWD="$MARIADB_ROOT_PASSWORD" mariadb-dump -uroot -P16207 --routines \
  --databases Project_Player34 Project_Game34 Db_Membership' > ddtank-backup.sql
```

## Game art

The art and sound files (about 1 GB) are **not in this repository**. When the client ask for a file, we download it from
`DDTANK_RESOURCE_UPSTREAM` (default to VNG CDN: `http://gunny.vcdn.vn/`), put it in a cache at `deploy/docker/data/resource-cache/`. 

If you have your own resource files, put it in `deploy/docker/data/resource`. The order of lookup is:
1. Check if file is in `/resource`
2. Check if file is in `/resource-cache`
3. Get file from `DDTANK_RESOURCE_UPSTREAM`


Instead of get the file on demain, you could download everything ahead of time, run this while the servers run without Docker (with Docker, copy a
filled `resource-cache/` into `deploy/docker/data/resource-cache/` instead). It works out the file names from
the database and skips files that are already cached or known to be missing, so you can stop it and run it
again:

```bash
python3 deploy/prefetch-resources.py --jobs 2 --delay 0.2
```

## Admin site

Port 16201 (see [Ports](#ports)); <http://127.0.0.1:8081/> without Docker. The password is
`DDTANK_ADMIN_PASSWORD` in the `.env` file.

- **SendMail:** mail with up to 5 items (strengthen level, hợp thành, expiry), or xu, vàng and lễ kim. The
  receiver can be an account name, a character name or a character ID. Online players get the mail
  immediately.
- **Cấu hình:** game options, such as strengthening always succeeding or no captcha (the full list is in
  [CHANGES.md](CHANGES.md#options-on-the-admin-site-cấu-hình)). A change applies to the running server at once.
- **Thông báo:** the server notices players see when they log in. Changes apply without a restart.

## Development without Docker

To work on the code, run the servers directly. You need Linux, Docker (for MariaDB only, with your user in
the `docker` group), the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (`dotnet` on the
PATH, or `~/.dotnet/dotnet`) and Python 3. The servers then use the original ports (8080, 8081, 9500...),
all on `127.0.0.1`.

1. Create `deploy/.env` with `MARIADB_ROOT_PASSWORD`, `DDTANK_DB_PASSWORD`,
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

The character creation screen is a separate file, `Source Flash/FlashSV1/1.png`. Its patched classes are in
`Source Flash/src-register` (listed in `REGISTER_CLASSES`), and the same script rebuilds it.

Browsers check for a new client on every load, so a normal page reload picks up a rebuilt `2.png`.

