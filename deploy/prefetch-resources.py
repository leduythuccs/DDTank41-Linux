#!/usr/bin/env python3
"""
Downloads the game's art/sound files ahead of time, so the local cache has them even if the
resource CDN (Web.ResourceUpstream) goes away.

The CDN cannot be listed, so the file names are rebuilt the way the client builds them
(ddt.manager.PathManager in 2.png) from the game data in the database:

  * items (Shop_Goods: CategoryID, Pic, NeedSex): icons, show/game images per sex and colour layer
  * balls (Ball): bullet / blast-out swf, crater images
  * maps (Game_Map, Pve_Info): fore/back/dead layers, icons, previews, background music
  * monsters (NPC_Info.ResourcesPath), pets (Pet_Template_Info), pet skills, achievements
  * files the mission scripts load (AddLoadingFile in Game.Server/GameServerScript)
  * numbered families seen in the cache (skill effects, equipment glow) probed over a range

Candidates the client may never ask for are included on purpose (both sexes, several colour
layers): the CDN answers 404 for those. Each URL is requested through the running Tank.Web
(/resource/...), which stores every hit in resource-cache/; files already in resource/ or
resource-cache/ are skipped, and 404s are remembered in resource-cache/.prefetch-missing.txt so
a second run only tries new names (--retry-missing to try them again).

Usage: deploy/prefetch-resources.py [--base http://127.0.0.1:8080] [--jobs 8] [--dry-run] [--sample N]
Needs: Tank.Web running with Web.ResourceUpstream set, the database container (deploy/.env).
"""
import argparse
import concurrent.futures
import os
import re
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
CACHE = os.path.join(ROOT, "resource-cache")
LOCAL = os.path.join(ROOT, "resource")
MISSING = os.path.join(CACHE, ".prefetch-missing.txt")
CONFIG_XML = os.path.join(ROOT, "Source Flash", "FlashSV1", "config.xml")
SCRIPTS = os.path.join(ROOT, "Game.Server", "GameServerScript")

CONTAINER = "ddtank-mssql"
DATABASE = "Project_Game34"

# EquipType.TYPES (index = CategoryID)
TYPES = ["", "head", "glass", "hair", "eff", "cloth", "face", "arm", "armlet", "ring", "", "", "",
         "suits", "necklace", "wing", "chatball", "", "", "", "", "", "", "", "", "", "", "", "armlet", "ring"]
SEXED = {1, 2, 3, 4, 5, 6, 13}          # image/equip/{m|f}/{type}/{pic}/...
UNSEXED = {8, 9, 14, 28, 29}            # image/equip/{type}/{pic}/icon.png
LAYERS = (1, 2, 3)                      # colour layers: icon_N.png and N/show.png


def load_env():
    env = {}
    with open(os.path.join(HERE, ".env")) as f:
        for line in f:
            if "=" in line and not line.lstrip().startswith("#"):
                k, v = line.rstrip("\n").split("=", 1)
                env[k] = v
    return env


def query(password, sql):
    """Rows of a query, as lists of strings."""
    cmd = ["docker", "exec", "-e", "SQLCMDPASSWORD=" + password, CONTAINER, "/opt/mssql-tools18/bin/sqlcmd",
           "-C", "-S", "localhost", "-U", "sa", "-d", DATABASE, "-h", "-1", "-W", "-s", "\t",
           "-Q", "SET NOCOUNT ON; " + sql]
    r = subprocess.run(cmd, capture_output=True)
    if r.returncode != 0:  # report sqlcmd's message, not the command line (it holds the password)
        sys.exit(f"query failed: {sql}\n{(r.stdout + r.stderr).decode('utf-8', 'replace').strip()}")
    out = r.stdout.decode("utf-8", "replace")
    return [line.split("\t") for line in out.splitlines() if line.strip()]


def item_paths(category, pic, need_sex):
    pic = pic.strip()
    if not pic:
        return []
    sexes = {1: ["m"], 2: ["f"]}.get(need_sex, ["m", "f"])
    if category in SEXED:
        out = []
        for s in sexes:
            base = f"image/equip/{s}/{TYPES[category]}/{pic}"
            for n in LAYERS:
                out.append(f"{base}/icon_{n}.png")
                if category == 3:  # hair: front/back parts
                    for part in ("a", "b"):
                        out += [f"{base}/{n}/{part}/show.png", f"{base}/{n}/{part}/game.png"]
                else:
                    out += [f"{base}/{n}/show.png", f"{base}/{n}/game.png"]
        return out
    if category in UNSEXED:
        return [f"image/equip/{TYPES[category]}/{pic}/icon.png"]
    if category in (7, 27):
        return [f"image/arm/{pic}/1/icon.png"] + [f"image/arm/{pic}/1/{g}/{f}" for g in (0, 1) for f in ("show.png", "game.png")]
    if category == 15:
        return [f"image/equip/wing/{pic}/icon.png", f"image/equip/wing/{pic}/wings.swf"]
    if category in (11, 20, 23, 30, 34, 35, 40):
        return [f"image/unfrightprop/{pic}/icon.png"]
    simple = {12: "image/task/{}/icon.png", 16: "image/specialprop/chatball/{}/icon.png",
              17: "image/equip/offhand/{}/icon.png", 31: "image/equip/offhand/{}/icon.png",
              18: "image/cardbox/{}/icon.png", 19: "image/equip/recover/{}/icon.png",
              25: "image/gift/{}/icon.png", 26: "image/card/{}/icon.jpg",
              32: "image/farm/crops/{}/seed.png", 36: "image/farm/crops/{}/seed.png",
              33: "image/farm/fertilizer/{}/icon.png",
              50: "image/petequip/arm/{}/icon.png", 51: "image/petequip/hat/{}/icon.png",
              52: "image/petequip/cloth/{}/icon.png"}
    if category in simple:
        return [simple[category].format(pic)]
    return [f"image/prop/{pic}/icon.png"]


def map_paths(map_id, fore, back, dead):
    base = f"image/map/{map_id}"
    out = [f"{base}/{fore or 'fore'}.png", f"{base}/{back or 'back'}.jpg", f"{base}/{dead or 'dead'}.png",
           f"{base}/icon.png", f"{base}/samll_map.png", f"{base}/small.png"]
    out += [f"{base}/show{n}.jpg" for n in range(1, 5)]
    return out


def candidates(password):
    paths = set()

    for cat, pic, sex in query(password, "SELECT DISTINCT CategoryID, Pic, NeedSex FROM Shop_Goods WHERE Pic <> ''"):
        paths.update(item_paths(int(cat), pic, int(sex)))
    for cat in SEXED:  # the "default" (nothing equipped) look
        for s in ("m", "f"):
            for n in LAYERS:
                if cat == 3:
                    paths.update(f"image/equip/{s}/hair/default/{n}/{p}/{f}" for p in "ab" for f in ("show.png", "game.png"))
                else:
                    paths.update(f"image/equip/{s}/{TYPES[cat]}/default/{n}/{f}" for f in ("show.png", "game.png"))

    for ball_id, blast, crater in query(password, "SELECT ID, BombPartical, Crater FROM Ball"):
        paths.add(f"image/bomb/bullet/bullet{ball_id}.swf")
        paths.add(f"image/bomb/blastout/blastout{ball_id}.swf")
        if blast.strip() and blast != "NULL":
            paths.add(f"image/bomb/blastout/blastout{blast.strip()}.swf")
        if crater.strip() and crater not in ("0", "NULL"):
            paths.update(f"image/bomb/crater/{crater.strip()}/{f}" for f in ("crater.png", "craterbrink.png"))

    music = set()
    for map_id, fore, back, dead, bgm in query(password, "SELECT ID, ISNULL(ForePic,''), ISNULL(BackPic,''), ISNULL(DeadPic,''), ISNULL(BackMusic,'') FROM Game_Map"):
        paths.update(map_paths(map_id, fore.strip(), back.strip(), dead.strip()))
        music.update(m for m in re.split(r"[,|]", bgm) if m.strip().isdigit())
    for (pic,) in query(password, "SELECT DISTINCT Pic FROM Pve_Info WHERE Pic <> ''"):
        paths.update(map_paths(pic.strip(), "", "", ""))
    try:
        with open(CONFIG_XML, encoding="utf-8") as f:
            m = re.search(r'<MUSIC_LIST value="([^"]*)"', f.read())
            if m:
                music.update(x.strip() for x in m.group(1).split(",") if x.strip())
    except OSError:
        pass
    paths.update(f"sound/{m.strip()}.flv" for m in music)

    for (res,) in query(password, "SELECT DISTINCT ResourcesPath FROM NPC_Info WHERE ResourcesPath LIKE 'image/%'"):
        paths.add(res.strip())
    for pic, asset in query(password, "SELECT DISTINCT Pic, ISNULL(GameAssetUrl,'') FROM Pet_Template_Info"):
        if pic.strip():
            paths.update([f"image/pet/{pic.strip()}.png", f"image/pet/{pic.strip()}/icon.png"])
        if asset.strip():
            paths.update([f"image/game/pet/{asset.strip()}.swf", f"image/gameasset/{asset.strip()}.swf"])
    for table in ("Pet_Skill_Info", "Pet_Skill_Element_Info"):
        for pic, effect in query(password, f"SELECT DISTINCT ISNULL(Pic,''), ISNULL(EffectPic,'') FROM {table}"):
            if pic.strip() and pic.strip() != "-1":
                paths.add(f"image/petskill/{pic.strip()}/icon.png")
            if effect.strip():
                paths.add(f"image/skilleffect/{effect.strip()}.swf")
    for (pic,) in query(password, "SELECT DISTINCT PicID FROM Achievement"):
        paths.add(f"image/effort/{pic.strip()}/icon.png")

    # files the mission scripts make the client load (bombs/... come from the flash site, not here)
    for dirpath, _, files in os.walk(SCRIPTS):
        for name in files:
            if name.endswith(".cs"):
                with open(os.path.join(dirpath, name), encoding="utf-8", errors="replace") as f:
                    paths.update(re.findall(r'AddLoadingFile\(\s*\d+\s*,\s*"(image/[^"]+)"', f.read()))

    # numbered families with no data table
    paths.update(f"image/skilleffect/effect{n:03d}.swf" for n in range(1, 201))
    paths.update(f"image/equip/effects/specialeffect/effect_{n}.png" for n in range(1, 31))
    paths.update(f"image/equip/effects/state/{s}/{n}/show{k}.png" for s in "mf" for n in range(1, 21) for k in (1, 2, 3))
    paths.update(f"image/equip/{kind}/{n}.swf" for kind in ("circlelight", "sinplelight") for n in range(1, 21))
    # room / mission icons use map ids that are not in Game_Map
    for map_id in list(range(1, 1501)) + list(range(10000, 10021)):
        paths.update(f"image/map/{map_id}/{f}" for f in ("icon.png", "samll_map.png", "show1.jpg"))
    paths.update(f"sound/{n:03d}.flv" for n in range(1, 201))  # lobby / room music

    return sorted(p.lower() for p in paths)


def existing_files():
    """Lower-case relative paths already in resource/ or resource-cache/ (served case-insensitively)."""
    have = set()
    for root in (LOCAL, CACHE):
        for dirpath, _, files in os.walk(root):
            for name in files:
                have.add(os.path.relpath(os.path.join(dirpath, name), root).replace(os.sep, "/").lower())
    return have


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--base", default="http://127.0.0.1:8080", help="Tank.Web address")
    ap.add_argument("--jobs", type=int, default=8, help="parallel requests (be gentle with the CDN)")
    ap.add_argument("--dry-run", action="store_true", help="only count what would be fetched")
    ap.add_argument("--sample", type=int, default=0, help="only try N names spread over the list (to estimate size)")
    ap.add_argument("--retry-missing", action="store_true", help="also retry names that were 404 before")
    args = ap.parse_args()

    paths = candidates(load_env()["MSSQL_SA_PASSWORD"])
    have = existing_files()
    missing = set()
    if os.path.exists(MISSING) and not args.retry_missing:
        with open(MISSING, encoding="utf-8") as f:
            missing = {line.strip() for line in f if line.strip()}
    todo = [p for p in paths if p not in have and p not in missing]
    print(f"{len(paths)} candidate files: {len(paths) - len(todo)} already cached or known missing, {len(todo)} to try")
    if args.sample and len(todo) > args.sample:
        todo = todo[::len(todo) // args.sample][:args.sample]
        print(f"sampling {len(todo)} of them")
    if args.dry_run or not todo:
        return

    lock = threading.Lock()
    stats = {"ok": 0, "missing": 0, "error": 0, "bytes": 0}
    new_missing = []
    started = time.time()

    def fetch(path):
        url = args.base.rstrip("/") + "/resource/" + urllib.request.quote(path)
        try:
            with urllib.request.urlopen(url, timeout=120) as r:
                size = len(r.read())
            with lock:
                stats["ok"] += 1
                stats["bytes"] += size
        except urllib.error.HTTPError as e:
            with lock:
                if e.code == 404:
                    stats["missing"] += 1
                    new_missing.append(path)
                else:
                    stats["error"] += 1
        except (urllib.error.URLError, OSError):
            with lock:
                stats["error"] += 1

    done = 0
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.jobs) as pool:
        for _ in pool.map(fetch, todo):
            done += 1
            if done % 500 == 0 or done == len(todo):
                rate = done / max(time.time() - started, 0.001)
                print(f"  {done}/{len(todo)}  downloaded {stats['ok']} ({stats['bytes'] / 1e6:.1f} MB), "
                      f"not on CDN {stats['missing']}, errors {stats['error']}  [{rate:.0f}/s]", flush=True)
                if new_missing:
                    with lock:
                        batch, new_missing[:] = list(new_missing), []
                    os.makedirs(CACHE, exist_ok=True)
                    with open(MISSING, "a", encoding="utf-8") as f:
                        f.writelines(p + "\n" for p in batch)

    if args.sample:
        hit = stats["ok"] / max(stats["ok"] + stats["missing"], 1)
        print(f"sample: {hit:.0%} exist, {stats['bytes'] / max(stats['ok'], 1) / 1e3:.0f} KB each on average")
    if stats["error"]:
        print(f"{stats['error']} requests failed (network/server errors): run again to retry them", file=sys.stderr)


if __name__ == "__main__":
    main()
