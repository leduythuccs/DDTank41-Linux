#!/usr/bin/env python3
"""
Makes stored procedures in the restored Project_Player34 database callable by this repo's C# code.

The database backup comes from a newer game version than the code: some procedures require
parameters the code never sends (the C# models do not even have those fields), so every call
fails - e.g. SP_Users_Update, which saves the player. This script reads each procedure's current
definition and, for those parameters only:
  * update procedures: default the parameter to NULL and write  col = ISNULL(@param, col),
    so values the old code does not know about are preserved instead of overwritten;
  * insert procedures: default the parameter to the value a new row should get.
Nothing else in the procedure changes. Safe to run repeatedly.

Usage: deploy/patch-procedures.py [--dry-run]   (reads MSSQL_SA_PASSWORD from deploy/.env)
"""
import os
import re
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
CONTAINER = os.environ.get("DDTANK_MSSQL_CONTAINER", "ddtank-mssql")
DATABASE = "Project_Player34"

# procedure -> list of (parameter, mode); mode "keep" = NULL default + ISNULL(@p, col), else a default literal
PATCHES = {
    "SP_Users_Update": [("@GhostEquipList", "keep"), ("@fineSuitExp", "keep")],
    "SP_Users_Items_Update": [("@curExp", "keep"), ("@cellLocked", "keep")],
    "SP_Users_Items_Add": [("@curExp", "0"), ("@cellLocked", "0")],
    "SP_UpdateVIPInfo": [("@typeVIP", "keep")],
}


def load_env():
    env = {}
    with open(os.path.join(HERE, ".env")) as f:
        for line in f:
            if "=" in line and not line.lstrip().startswith("#"):
                k, v = line.rstrip("\n").split("=", 1)
                env[k] = v
    return env


def sqlcmd(password, args, input_file=None):
    cmd = ["docker", "exec", "-e", "SQLCMDPASSWORD=" + password]
    if input_file:
        cmd.append("-i")
    cmd += [CONTAINER, "/opt/mssql-tools18/bin/sqlcmd", "-C", "-S", "localhost", "-U", "sa", "-d", DATABASE, "-b"] + args
    if input_file:
        cmd += ["-i", "/dev/stdin"]
    stdin = open(input_file, "rb") if input_file else None
    try:
        return subprocess.run(cmd, stdin=stdin, capture_output=True, check=True).stdout.decode("utf-8", "replace")
    finally:
        if stdin:
            stdin.close()


def definition(password, proc):
    out = sqlcmd(password, ["-y", "0", "-Q", f"SET NOCOUNT ON; SELECT OBJECT_DEFINITION(OBJECT_ID('{proc}'))"])
    return out.strip()


def patch(text, param, mode):
    name = re.escape(param)
    # 1. parameter declaration: "@param type" (not already defaulted) -> "@param type = default"
    decl = re.compile(r"(?im)^(\s*,?\s*" + name + r"\s+[a-z]+(?:\s*\(\s*(?:max|\d+(?:\s*,\s*\d+)?)\s*\))?)(\s*)(,|\r?$)")
    default = "NULL" if mode == "keep" else mode
    text, n = decl.subn(lambda m: m.group(1) + " = " + default + m.group(2) + m.group(3), text, count=1)
    if n == 0 and not re.search(name + r"\s+[a-z]+[^,\n]*=", text, re.I):
        raise RuntimeError(f"declaration of {param} not found")
    # 2. assignments "col = @param" -> "col = ISNULL(@param, col)"
    if mode == "keep":
        assign = re.compile(r"(?<![@\w])(\[?[A-Za-z_][A-Za-z0-9_]*\]?)\s*=\s*" + name + r"\b(?!\s*,\s*\1\))")
        text = assign.sub(lambda m: f"{m.group(1)} = ISNULL({param}, {m.group(1)})", text)
    return text


def main():
    dry_run = "--dry-run" in sys.argv
    password = load_env()["MSSQL_SA_PASSWORD"]
    for proc, params in PATCHES.items():
        text = definition(password, proc)
        if not text or text == "NULL":
            print(f"skip  {proc}: not found")
            continue
        original = text
        text = re.sub(r"(?i)\bCREATE\s+(PROC|PROCEDURE)\b", r"CREATE OR ALTER \1", text, count=1)
        for param, mode in params:
            text = patch(text, param, mode)
        if dry_run:
            print(f"--- {proc}")
            for a, b in zip(original.splitlines(), text.splitlines()):
                if a != b:
                    print("  - " + a.strip() + "\n  + " + b.strip())
            continue
        with tempfile.NamedTemporaryFile("w", suffix=".sql", delete=False, encoding="utf-8") as f:
            f.write(text)
            path = f.name
        try:
            sqlcmd(password, [], input_file=path)
        except subprocess.CalledProcessError as e:
            print(f"FAIL  {proc}: {e.stdout.decode(errors='replace')}{e.stderr.decode(errors='replace')}", file=sys.stderr)
            sys.exit(1)
        finally:
            os.unlink(path)
        print(("patched " if text.replace("CREATE OR ALTER", "CREATE") != original else "ok      ") + proc + "  (" + ", ".join(p for p, _ in params) + ")")


if __name__ == "__main__":
    main()
