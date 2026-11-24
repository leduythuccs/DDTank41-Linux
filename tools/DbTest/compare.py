"""Compares the SQL Server and MariaDB runs of DbTest (tools/DbTest/run.sh). Usage: compare.py <out dir>

Sections, most useful first:
  ERRORS ONLY ON MARIADB   the call logged an error or threw on MariaDB but not on SQL Server: almost surely a bug
  RESULT DIFFERS           both ran, but returned different values (first differences shown)
  ERRORS ON BOTH           fails on SQL Server too (old code vs newer database, or bad test arguments): usually not
                           a MariaDB problem, listed to check the MariaDB error is the same kind
  TABLES DIFFER            after all calls, a table's content differs (what the calls wrote)
"""
import json
import os
import sys

out = sys.argv[1]


def load(name):
    calls = {}
    with open(os.path.join(out, name), encoding="utf-8") as f:
        for line in f:
            o = json.loads(line)
            calls[o["call"]] = o
    return calls


def diffs(a, b, path="", found=None, limit=4):
    """Paths where two JSON values differ (a few)."""
    found = [] if found is None else found
    if len(found) >= limit:
        return found
    if isinstance(a, dict) and isinstance(b, dict):
        for k in sorted(set(a) | set(b)):
            diffs(a.get(k), b.get(k), f"{path}.{k}", found, limit)
    elif isinstance(a, list) and isinstance(b, list):
        if len(a) != len(b):
            found.append(f"{path or '.'}: {len(a)} items vs {len(b)}")
        for i, (x, y) in enumerate(zip(a, b)):
            diffs(x, y, f"{path}[{i}]", found, limit)
    elif a != b:
        found.append(f"{path or '.'}: {short(a)} vs {short(b)}")
    return found


def short(v):
    s = json.dumps(v, ensure_ascii=False)
    return s if len(s) <= 80 else s[:77] + "..."


def failed(o):
    return bool(o.get("errors") or o.get("exception") or o.get("timeout"))


def why(o):
    parts = list(o.get("errors") or [])
    if o.get("exception"):
        parts.append("exception " + o["exception"])
    if o.get("timeout"):
        parts.append("timeout (30 s)")
    return "; ".join(p.replace("\n", " ")[:300] for p in parts)


ms, ma = load("calls-mssql.jsonl"), load("calls-mariadb.jsonl")
only_ma, differ, both = [], [], []
same = 0
for call in sorted(set(ms) | set(ma)):
    a, b = ms.get(call), ma.get(call)
    if a is None or b is None:
        differ.append((call, ["only in " + ("MariaDB" if a is None else "SQL Server") + " code"]))
        continue
    if failed(b) and not failed(a):
        only_ma.append((call, why(b)))
    elif failed(b) and failed(a):
        both.append((call, why(a), why(b)))
    else:
        d = diffs({"ret": a.get("ret"), "refs": a.get("refs")}, {"ret": b.get("ret"), "refs": b.get("refs")})
        if failed(a):
            d.insert(0, "fails on SQL Server only: " + why(a))
        if d:
            differ.append((call, d))
        else:
            same += 1

print(f"calls: {len(set(ms) | set(ma))}  same: {same}  errors only on MariaDB: {len(only_ma)}  "
      f"result differs: {len(differ)}  errors on both: {len(both)}")

print(f"\n=== ERRORS ONLY ON MARIADB ({len(only_ma)})")
for call, w in only_ma:
    print(f"- {call}\n    {w}")

print(f"\n=== RESULT DIFFERS ({len(differ)})  (SQL Server vs MariaDB)")
for call, d in differ:
    print(f"- {call}")
    for x in d:
        print(f"    {x}")

print(f"\n=== ERRORS ON BOTH ({len(both)})")
for call, wa, wb in both:
    print(f"- {call}\n    SQL Server: {wa}\n    MariaDB:    {wb}")

ta = json.load(open(os.path.join(out, "tables-mssql.json"), encoding="utf-8"))
tb = json.load(open(os.path.join(out, "tables-mariadb.json"), encoding="utf-8"))
bad = [t for t in sorted(set(ta) | set(tb)) if t not in ta or t not in tb or ta[t]["hash"] != tb[t]["hash"]]
print(f"\n=== TABLES DIFFER ({len(bad)} of {len(set(ta) | set(tb))})")
for t in bad:
    if t not in ta or t not in tb:
        print(f"- {t}: only in {'MariaDB' if t not in ta else 'SQL Server'}")
        continue
    x, y = ta[t], tb[t]
    print(f"- {t}: {x['rows']} rows vs {y['rows']}")
    if x["columns"] != y["columns"]:
        print(f"    columns: {x['columns']}\n         vs: {y['columns']}")
    if x.get("sample") is not None and y.get("sample") is not None:
        sa, sb = set(x["sample"]), set(y["sample"])
        for r in sorted(sa - sb)[:3]:
            print(f"    SQL Server only: {r[:250]}")
        for r in sorted(sb - sa)[:3]:
            print(f"    MariaDB only:    {r[:250]}")
