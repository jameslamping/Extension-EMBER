#!/bin/bash
# Run an EMBER parameter variant on the windowed MORA scenario and score it against SCF.
#
#   ./run_variant.sh <name> <duration_years> <overrides.txt> [seed]
#
# <overrides.txt> holds "Parameter value" lines that replace (or append) lines in
# extensions/EMBER.txt; lines starting with "fuelbed:" edit ember_fuelbeds.csv columns:
#   fuelbed: <Name|*> <Column> <value>
# The run lands in runs/<name>/ together with score_<name>.csv and plots.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
NAME="$1"; DUR="$2"; OVR="$3"; SEED="${4:-3333}"
[ -z "$NAME" ] || [ -z "$DUR" ] || [ -z "$OVR" ] && { echo "usage: $0 <name> <duration> <overrides.txt> [seed]"; exit 1; }
SRC="$HERE/../testing/MORA-window"
RUN="$HERE/runs/$NAME"
MORA="${MORA:-/Users/jlamping/Desktop/LANDIS/EMBER/MORA/Hist/Rep1}"
REF="$HERE/reference/window"

rm -rf "$RUN"; mkdir -p "$RUN"
cp -R "$SRC/extensions" "$SRC/input_maps" "$RUN/"
sed -e "s/^Duration .*/Duration          $DUR/" -e "s/^RandomNumberSeed .*/RandomNumberSeed  $SEED/" "$SRC/Scenario.txt" > "$RUN/Scenario.txt"
cp "$OVR" "$RUN/overrides.txt"
python3 - "$RUN/extensions/EMBER.txt" "$RUN/extensions/ember_fuelbeds.csv" "$OVR" <<'EOF'
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
EOF
echo "== $NAME: $DUR years, seed $SEED, overrides:"; cat "$RUN/overrides.txt"
if [ ! -f "$REF/scf_reference_summary.csv" ]; then
  echo "== building SCF reference for the window"
  Rscript "$HERE/R/scf_reference.R" "$MORA" "$REF" 320 440 320 440
fi
cd "$RUN"
docker run --rm --platform linux/amd64 --cpus="${CPUS:-4}" --memory="${MEM:-16g}" \
  -v "$RUN":/scenario -v "$MORA":/mora:ro landis-ember:dev sh -c 'cd /scenario && dotnet $LANDIS_CONSOLE Scenario.txt' > run.log 2>&1 \
  || { echo "RUN FAILED, see $RUN/run.log"; tail -20 run.log; exit 1; }
grep -E 'EMBER year .*fires' run.log | tail -3
Rscript "$HERE/R/score_run.R" "$RUN" "$REF" "$RUN" "$NAME"
