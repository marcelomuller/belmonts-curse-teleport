#!/usr/bin/env bash
# Builds the release zip: dist/SaveRoomTeleport-<version>.zip, laid out to extract into the game folder.
set -euo pipefail
cd "$(dirname "$0")"
DOTNET=${DOTNET:-$(command -v dotnet || echo ~/.dotnet-sdk/dotnet)}
version=$(grep -oP 'Version = "\K[^"]+' src/Plugin.cs)

"$DOTNET" build SaveRoomTeleport.csproj -c Release -nologo -v quiet
stage=$(mktemp -d)
trap 'rm -rf "$stage"' EXIT
mkdir -p "$stage/BepInEx/plugins" dist
cp bin/Release/net6.0/SaveRoomTeleport.dll "$stage/BepInEx/plugins/"
rm -f "dist/SaveRoomTeleport-$version.zip"
(cd "$stage" && zip -qr "$OLDPWD/dist/SaveRoomTeleport-$version.zip" BepInEx)
echo "dist/SaveRoomTeleport-$version.zip"
