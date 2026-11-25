#!/usr/bin/env bash
# Recompiles patched client classes into the game client SWF (Source Flash/FlashSV1/2.png).
#
# 2.png is the main game code: a plain compressed SWF that Loading.swf loads (it is not md5-checked,
# only 1.png is). Each class listed below is compiled from "Source Flash/src" with JPEXS FFDec
# (https://github.com/jindrapetrik/jpexs-decompiler) and replaces the same class in the SWF.
#
# Patched classes:
#   phy.maps.Tile  crater brink masked with copyPixels instead of a BlendMode.ALPHA draw, which
#                  Ruffle renders wrong (filled discs over every crater and broken walking).
#   ddt.data.analyze.{ItemTemplete,ShopItem,ShopItemDisCount,ShopItemSort,InventoryItem}Analyzer
#                  parse for up to 25 ms per tick instead of 40 items per 30 ms tick (the item
#                  templates alone took a fixed ~5.6 s of the loading screen).
#   ddt.manager.DragManager
#                  finds the drop target with hitTestPoint instead of stage.getObjectsUnderPoint, a rough
#                  stub in Ruffle that returns cells the point is not on (selling and dragging items in the
#                  bag did nothing).
#   room.view.roomView.MatchRoomView, ddt.manager.GameInSocketOut
#                  "Đấu với: N bot" button in the PvP room (1-4 bots when matchmaking falls back to bots);
#                  the count is sent with GAME_START.
#   bagAndInfo.bag.SellGoodsBtn, bagAndInfo.cell.BagCell
#                  valuable equipment (+7 and up, etc.) can be sold; the sell frame still asks to confirm.
#   ddt.manager.SoundManager
#                  a new NetStream for each music track: Ruffle kept playing the previous track when the
#                  same stream was closed and played again (same music in the lobby, rooms and every map).
#   game.view.GameView, game.view.experience.ExpView
#                  the reward screen after a battle skips the map snapshot and the motion-blur tweens: both
#                  use BitmapData.draw, a slow GPU readback in Ruffle that made the game stutter there.
#   ddt.view.tips.GoodTip
#                  the weapon tooltip shows the strengthen bonus on the damage again, e.g. "60(+216)" like
#                  hats and clothes: the decompiled code overwrote it with an unset variable.
#
# 1.png is the character creation module. It starts with a 37-byte "road7<md5>" header that Loading.swf
# compares with md5.xml; the header is put back unchanged, so md5.xml needs no change. Its classes are
# compiled from "Source Flash/src-register", which holds only the patched classes (decompiled from 1.png):
#   register.view.MainFigureView
#                  sends OldPlayer=true with VisualizeRegister.ashx after the "Người Chơi Cũ" button, so the
#                  server marks the character as an old player and skips its fight-item newbie quests. Both
#                  buttons skip the intro tutorial (3.png), as the only button did before.
#
# Usage: FFDEC=/path/to/ffdec.jar deploy/patch-client.sh     (needs java and python3)
set -euo pipefail
cd "$(dirname "$0")/.."
FFDEC="${FFDEC:?set FFDEC to the path of ffdec.jar}"
CLASSES=(
  phy.maps.Tile
  ddt.data.analyze.ItemTempleteAnalyzer
  ddt.data.analyze.ShopItemAnalyzer
  ddt.data.analyze.ShopItemDisCountAnalyzer
  ddt.data.analyze.ShopItemSortAnalyzer
  ddt.data.analyze.InventoryItemAnalyzer
  ddt.manager.DragManager
  ddt.manager.GameInSocketOut
  room.view.roomView.MatchRoomView
  bagAndInfo.bag.SellGoodsBtn
  bagAndInfo.cell.BagCell
  ddt.manager.SoundManager
  game.view.GameView
  game.view.experience.ExpView
  ddt.view.tips.GoodTip
)

REGISTER_CLASSES=(
  register.view.MainFigureView
)

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# patch_swf <file> <header bytes before the SWF> <source folder> <class>...
patch_swf() {
  local file="$1" head="$2" root="$3"
  shift 3
  # CWS (zlib) -> FWS for FFDec
  python3 - "$file" "$work/in.swf" "$head" <<'EOF'
import sys, zlib
d = open(sys.argv[1], "rb").read()[int(sys.argv[3]):]
assert d[:3] == b"CWS", "expected a CWS file"
open(sys.argv[2], "wb").write(b"FWS" + d[3:8] + zlib.decompress(d[8:]))
EOF

  local cur="$work/in.swf" cls src out
  for cls in "$@"; do
    src="$root/${cls//.//}.as"
    out="$work/$cls.swf"
    java -jar "$FFDEC" -replace "$cur" "$out" "$cls" "$src" 2>&1 | grep -v -e "EXPERIMENTAL" -e "^Replacing" || true
    [ -s "$out" ] || { echo "replacing $cls failed"; exit 1; }
    cur="$out"
  done

  # FWS -> CWS, header length updated, the bytes before the SWF put back
  python3 - "$cur" "$file" "$head" <<'EOF'
import sys, zlib, struct
d = open(sys.argv[1], "rb").read()
head = open(sys.argv[2], "rb").read()[:int(sys.argv[3])]
body = d[8:]
out = head + b"CWS" + d[3:4] + struct.pack("<I", 8 + len(body)) + zlib.compress(body, 9)
open(sys.argv[2], "wb").write(out)
print(f"wrote {sys.argv[2]} ({len(out)} bytes)")
EOF
}

patch_swf "Source Flash/FlashSV1/2.png" 0 "Source Flash/src" "${CLASSES[@]}"
patch_swf "Source Flash/FlashSV1/1.png" 37 "Source Flash/src-register" "${REGISTER_CLASSES[@]}"
