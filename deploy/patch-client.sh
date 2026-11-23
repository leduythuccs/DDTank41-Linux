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
#
# Usage: FFDEC=/path/to/ffdec.jar deploy/patch-client.sh     (needs java and python3)
set -euo pipefail
cd "$(dirname "$0")/.."
FFDEC="${FFDEC:?set FFDEC to the path of ffdec.jar}"
SWF="Source Flash/FlashSV1/2.png"
CLASSES=(
  phy.maps.Tile
  ddt.data.analyze.ItemTempleteAnalyzer
  ddt.data.analyze.ShopItemAnalyzer
  ddt.data.analyze.ShopItemDisCountAnalyzer
  ddt.data.analyze.ShopItemSortAnalyzer
  ddt.data.analyze.InventoryItemAnalyzer
)

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# CWS (zlib) -> FWS for FFDec
python3 - "$SWF" "$work/in.swf" <<'EOF'
import sys, zlib
d = open(sys.argv[1], "rb").read()
assert d[:3] == b"CWS", "expected a CWS file"
open(sys.argv[2], "wb").write(b"FWS" + d[3:8] + zlib.decompress(d[8:]))
EOF

cur="$work/in.swf"
for cls in "${CLASSES[@]}"; do
  src="Source Flash/src/${cls//.//}.as"
  out="$work/$cls.swf"
  java -jar "$FFDEC" -replace "$cur" "$out" "$cls" "$src" 2>&1 | grep -v -e "EXPERIMENTAL" -e "^Replacing" || true
  [ -s "$out" ] || { echo "replacing $cls failed"; exit 1; }
  cur="$out"
done

# FWS -> CWS, header length updated
python3 - "$cur" "$SWF" <<'EOF'
import sys, zlib, struct
d = open(sys.argv[1], "rb").read()
body = d[8:]
out = b"CWS" + d[3:4] + struct.pack("<I", 8 + len(body)) + zlib.compress(body, 9)
open(sys.argv[2], "wb").write(out)
print(f"wrote {sys.argv[2]} ({len(out)} bytes)")
EOF
