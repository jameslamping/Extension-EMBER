#!/bin/bash
# Build the EMBER DLL locally and layer it onto the base LANDIS-II image as landis-ember:dev.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
BASE_IMAGE="${BASE_IMAGE:-climatelibrary:v6}"

echo "== building EMBER"
dotnet build "$ROOT/src/EMBER.csproj" -c Release -nologo -v q
mkdir -p "$HERE/bin"
cp "$ROOT/src/bin/Release/netstandard2.0/Landis.Extension.EMBER-v1.dll" "$HERE/bin/"
cp "$ROOT/deploy/installer/EMBER 1.0.txt" "$HERE/registration.txt"

echo "== building image landis-ember:dev from $BASE_IMAGE"
docker build --platform linux/amd64 -f "$HERE/Dockerfile.dev" --build-arg BASE_IMAGE="$BASE_IMAGE" -t landis-ember:dev "$HERE"
echo "== done: docker run --rm --platform linux/amd64 -v <scenario>:/scenario -v <mora>:/mora:ro landis-ember:dev dotnet \$LANDIS_CONSOLE Scenario.txt"
