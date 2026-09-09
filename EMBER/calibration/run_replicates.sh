#!/bin/bash
# Run one variant with several random seeds concurrently, pool the logs and score the pool.
#   ./run_replicates.sh <variant> <duration_years> <overrides.txt> <seed> [<seed> ...]
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
NAME="$1"; DUR="$2"; OVR="$3"; shift 3
SEEDS="$@"
[ -z "$SEEDS" ] && { echo "usage: $0 <variant> <duration> <overrides.txt> <seed> ..."; exit 1; }
pids=()
for s in $SEEDS; do
  "$HERE/run_variant.sh" "${NAME}_s$s" "$DUR" "$OVR" "$s" > "$HERE/variant_${NAME}_s$s.log" 2>&1 &
  pids+=($!)
done
fail=0
for p in "${pids[@]}"; do wait "$p" || fail=1; done
[ $fail -ne 0 ] && { echo "one or more replicates failed"; exit 1; }
Rscript "$HERE/R/pool_replicates.R" "$HERE" "$NAME" $SEEDS
Rscript "$HERE/R/score_run.R" "$HERE/runs/${NAME}_pooled" "$HERE/reference/window" "$HERE/runs/${NAME}_pooled" "${NAME}_pooled"
