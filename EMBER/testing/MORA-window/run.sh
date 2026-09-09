#!/bin/bash
# Run the windowed MORA test scenario in the dev image.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
MORA="${MORA:-/Users/jlamping/Desktop/LANDIS/EMBER/MORA/Hist/Rep1}"
docker run --rm --platform linux/amd64 \
  --cpus="${CPUS:-4}" --memory="${MEM:-16g}" \
  -v "$HERE":/scenario -v "$MORA":/mora:ro \
  landis-ember:dev sh -c 'cd /scenario && dotnet $LANDIS_CONSOLE Scenario.txt'
