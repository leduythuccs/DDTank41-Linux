# Changes from the original server

Everything this repository changes compared with the original DDTank 4.1 server and client: gameplay changes,
new admin features, and the bugs I found while playing and asked Claude to fix. How to run the server is in
the [README](README.md).

## Gameplay changes

These are deliberate changes from the original game.

### Options on the admin site (Cấu hình)

Each one can be switched on or off on the admin site's **Cấu hình** page, and applies to the running server at
once.

| Option | What it does | Default |
|---|---|---|
| `StrengthenAlwaysSucceed` | Strengthening (cường hoá) always succeeds. | on |
| `ItemAdvanceAlwaysSucceed` | Đá tăng bậc always succeeds. | off |
| `WishBeadAlwaysSucceed` | Châu báu chúc phúc always succeeds. | off |
| `CardResetAlwaysMax` | Tẩy điểm thẻ bài always rolls the highest value. | off |
| `TexpNoDailyLimit` | No daily Tu luyện limit. | off |
| `FreeMatchCountsAsGuildWar` | Every free match, even played alone, counts as a guild war for quests and guild tasks. | off |
| `SkipCheckCode` | The anti-bot captcha ("pick the number") never shows. It used to appear now and then when starting a ranked match, moving items into the store or disbanding a guild. | off |

### Accounts and new characters

- **Sign up with a username and a password only:** "Đăng ký nhanh" on the login page no longer asks for an
  email, a password confirmation or a captcha, and logs straight in after signing up.
- **"Người Chơi Cũ" (old player) on the character creation screen:** next to "Tân Thủ" (new player, as
  before). An old player's character skips the newbie guide and the newbie quests that unlock fight items
  (the "Tranh đoạt báu vật" chain: +2, 3 tia, +1, máy bay, +10% to +50% damage...). Those quests count as
  done, with their EXP, gold and gifts, so the character starts around level 4 (with the default EXP rate) with every fight item unlocked.
  In the original client the only visible button skipped the intro tutorial and nothing else.

### Battles and bots

- **Bot matches start right away:** `AutoBotWaitSeconds` is 0 in `Fighting.Service/App.config`. The original
  waited about 20 seconds for real players before falling back to a bot.
- **How many bots:** in the PvP room, the host chooses with the **"Đấu với: N bot"** button how many bots
  (1-4) matchmaking falls back to.
- **The player always takes the first turn** against bots.
- **Win rate** is shown in battles (it was always 0%).

### Items and upgrades

- **One click uses everything:** card upgrades in the album use all the copies, drilling a hole uses all the
  matching Mũi khoan, and Tu luyện uses the whole stack of potions in the slot (the daily limit and the stat
  cap still apply).
- **Holes 5 and 6 go to level 5** with Mũi khoan cấp 5 (the client stopped at level 4).
- **Selling:** valuable equipment (+7 and higher) can be sold. The game still asks for confirmation.

## Admin site

The original GameAdmin site (WebForms) runs again on .NET 8 (`GameAdmin.Web`), with these additions:

- **Cấu hình:** the game options above.
- **Thông báo:** add, edit and delete the server notices players see when they log in (the `Edictum_List`
  table), with start and end dates. Changes apply without a restart.
- **SendMail:** the last receiver is remembered; attached items default to permanent and fully composed.

## Bug fixes

### Game client (browser / Ruffle)

- **Music:** every room and map played the same track. Ruffle kept playing the previous track when the
  music stream was reused, so each track now gets its own stream.
- **Music volume:** the in-game music setting does nothing under Ruffle. The game page has its own
  volume slider (music and effects together), which the browser remembers.
- **Stutter before the battle rewards:** the reward screen took a snapshot of the map and used motion blur,
  both slow in Ruffle. The reward screen now shows over the live map, without the blur.
- **Cannot walk after being hit:** the map was broken where a shot landed, because of how Ruffle draws the
  crater edges. They are now masked with a method Ruffle renders correctly.
- **Drag and drop:** selling items and dragging them in the bag did nothing. Drop targets are now found
  with `hitTestPoint`, because Ruffle's `getObjectsUnderPoint` returns the wrong objects.
- **Vietnamese text:** characters with stacked diacritics were cut off ("Gà Sát Thủ" showed as "Gà Sát Th").
  Text in the game now uses the browser's fonts.
- **Blank texts:** alerts, tips, button labels and tooltips were empty because `language.txt` had lost its
  CRLF line endings. `.gitattributes` now keeps them.
- **Item tooltips** never appeared: the UI config was older than the game code. The missing piece is added
  by `deploy/patch-ui-config.py`.
- **Weapon tooltip** lost the strengthen bonus next to the damage ("60(+216)"); it shows again.
- **Loading time:** item and shop lists are parsed in time slices, instead of a fixed number per frame
  that added about 5 seconds to the loading screen.
- **Old client after an update:** browsers kept the client files from their cache; they now check for a new
  version on every load.
- **Disconnects** sent the player to `server_list.html`; they now go back to the login page.

### Game server

- **Bot stuck at 0% loading** now and then: the game server handled the fight server's packets on several
  threads, so the "start loading" packet could arrive at the bot before the bot knew its own game. The bot's
  reply was dropped. Packets are now handled in order.
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
- **Server notices:** the start date was ignored, and deleting the last notice never took effect until a
  restart.
- **Game AI scripts** were rebuilt from the original `Game.Server.dll`, because the scripts in git didn't
  match the shipped server.

### Database and hosting

- **Database moved to MariaDB:** SQL Server needs about 2 GB of RAM and has no ARM build. The
  procedures were translated to MariaDB (see [Database](README.md#database)).
- **Old code, newer database:** some stored procedures in the restored database expect different
  parameters than this code sends. `deploy/patch-procedures.py` patches them when the database is restored.
- **Linux and the browser:** every server runs on .NET 8 on Linux. Tank.Web serves the site, the client's
  HTTP API and the art files, and passes the game socket through a WebSocket, because Ruffle cannot open raw
  TCP sockets.
