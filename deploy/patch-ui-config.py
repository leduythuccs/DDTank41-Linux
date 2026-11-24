#!/usr/bin/env python3
"""
Patches the client's UI component config (Source Flash/FlashSV1/ui/vietnam/xml/xml.png, a zip of
XML files that the client loads at startup) where it is older than the game code in 2.png.

  * coreI.xml, core.goodTip.MainPropertyItem / core.goodTip.armAngleItem: GoodTip (the item tooltip)
    expects a GhostStarContainer as the second fore item; without it every item tooltip throws
    "RangeError: Error #1125" and nothing is shown.
  * store.xml, HoleExpModel: holes 5 and 6 stop at level 4 in the client (maxLv/oprationLv 4), while the
    game server goes to level 5 with Mũi khoan cấp 5. Raise the client to 5, with the server's exp list
    (HoleLevelUpExpList in Server_Config).

The loose copies next to xml.png get the same edit. Safe to run repeatedly.
Usage: deploy/patch-ui-config.py
"""
import os
import re
import shutil
import tempfile
import zipfile

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Source Flash", "FlashSV1", "ui", "vietnam", "xml")
ARCHIVE = os.path.join(ROOT, "xml.png")

GHOST = "ddt.view.tips.GhostStarContainer"
# file -> list of (stylename, foreStyle it must have)
PATCHES = {
    "coreI.xml": [
        ("core.goodTip.MainPropertyItem", "core.GoodsTipTxt," + GHOST),
        ("core.goodTip.armAngleItem", None),  # keep its text style, append the container
    ],
}

# file -> list of (stylename, {attribute: value}) for <custom> elements
ATTRIBUTES = {
    "store.xml": [
        ("HoleExpModel", {"maxLv": "5", "oprationLv": "5", "explist": "400|600|700|800|1200"}),
    ],
}


def patch_attributes(text, stylename, values):
    m = re.search(r'<custom\b[^>]*\bstylename="' + re.escape(stylename) + r'"[^>]*>', text)
    if not m:
        raise RuntimeError(f"{stylename} not found")
    element = m.group(0)
    for name, value in values.items():
        element, n = re.subn(r'(\b' + name + r'=")[^"]*(")', lambda a: a.group(1) + value + a.group(2), element)
        if n != 1:
            raise RuntimeError(f"{stylename}: attribute {name} not found")
    return text[:m.start()] + element + text[m.end():], element != m.group(0)


def patch_text(text, stylename, fore):
    pattern = re.compile(r'(<component\b[^>]*\bstylename="' + re.escape(stylename) + r'"[^>]*?\bforeStyle=")([^"]*)(")', re.S)
    m = pattern.search(text)
    if not m:
        raise RuntimeError(f"{stylename} not found")
    current = m.group(2)
    wanted = fore if fore else (current if GHOST in current else current + "," + GHOST)
    if current == wanted:
        return text, False
    return text[:m.start(2)] + wanted + text[m.end(2):], True


def patch_file_text(name, text):
    changed = False
    for stylename, fore in PATCHES.get(name, []):
        text, c = patch_text(text, stylename, fore)
        changed |= c
    for stylename, values in ATTRIBUTES.get(name, []):
        text, c = patch_attributes(text, stylename, values)
        changed |= c
    return text, changed


FILES = list(PATCHES) + [name for name in ATTRIBUTES if name not in PATCHES]


def main():
    # 1. the zip the client actually loads
    with zipfile.ZipFile(ARCHIVE) as src:
        infos = src.infolist()
        data = {i.filename: src.read(i) for i in infos}
    changed_any = False
    for name in FILES:
        text, changed = patch_file_text(name, data[name].decode("utf-8"))
        if changed:
            data[name] = text.encode("utf-8")
            changed_any = True
    if changed_any:
        fd, tmp = tempfile.mkstemp(suffix=".zip", dir=ROOT)
        os.close(fd)
        with zipfile.ZipFile(tmp, "w") as dst:
            for i in infos:
                info = zipfile.ZipInfo(i.filename, date_time=i.date_time)
                info.compress_type = i.compress_type
                info.external_attr = i.external_attr
                dst.writestr(info, data[i.filename])
        shutil.move(tmp, ARCHIVE)
    print(("patched " if changed_any else "ok      ") + os.path.relpath(ARCHIVE))

    # 2. loose copies, kept in sync for reference
    for name in FILES:
        path = os.path.join(ROOT, name)
        if not os.path.exists(path):
            continue
        with open(path, encoding="utf-8", newline="") as f:
            text = f.read()
        text, changed = patch_file_text(name, text)
        if changed:
            with open(path, "w", encoding="utf-8", newline="") as f:
                f.write(text)
        print(("patched " if changed else "ok      ") + os.path.relpath(path))


if __name__ == "__main__":
    main()
