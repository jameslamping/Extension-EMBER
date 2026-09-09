#!/bin/bash
# Apply a variant override file to an EMBER input file and its fuelbed table in place
# (the same override syntax as run_variant.sh). Use it to promote an accepted variant
# into a scenario's extensions folder.
#   ./apply_variant.sh <overrides.txt> <EMBER.txt> <ember_fuelbeds.csv>
set -e
python3 - "$2" "$3" "$1" <<'PY'
import sys, re, csv
ember, fuelbeds, ovr = sys.argv[1:4]
lines = open(ember).read().split("\n")
fb_rows = list(csv.reader(open(fuelbeds)))
header = fb_rows[0]
for raw in open(ovr):
    raw = raw.split("<<")[0].strip()
    if not raw or raw.startswith(">>") or raw.startswith("#"): continue
    if raw.startswith("fuelbed:"):
        _, name, col, val = raw.split()
        ci = header.index(col)
        for r in fb_rows[1:]:
            if name == "*" or r[0] == name: r[ci] = val
        continue
    key, val = raw.split(None, 1)
    done = False
    for i, l in enumerate(lines):
        if re.match(r"^\s*(>>\s*)?" + re.escape(key) + r"\s", l) or l.strip() == key:
            lines[i] = f"{key:<28s} {val}"; done = True
    if not done: lines.append(f"{key:<28s} {val}")
open(ember, "w").write("\n".join(lines))
csv.writer(open(fuelbeds, "w", newline="")).writerows(fb_rows)
PY
echo "applied $1 to $2 and $3"
