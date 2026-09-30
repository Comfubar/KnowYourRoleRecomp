#!/usr/bin/env bash
# Builds a Linux AppImage of Know Your Role Recomp from your own disc image, for desktop Linux and SteamOS-like
# handhelds (Steam Deck, Legion Go, ROG Ally on Bazzite, ...).
#
#   tools/linux/build-appimage.sh [path/to/KnowYourRole.cue]
#
# Needs: the .NET 10 SDK (dotnet on PATH or in ~/.dotnet), git, curl, python3, and x86_64 Linux (WSL works).
# Output: dist/KnowYourRole-x86_64.AppImage. It contains code generated from YOUR disc: do not share it.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
export PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
command -v dotnet >/dev/null || { echo "dotnet not found: install the .NET 10 SDK (https://dot.net)"; exit 1; }

if [ $# -ge 1 ]; then
  CUE="$(readlink -f "$1")"
  mkdir -p disc
  # the config expects disc/KnowYourRole.cue; write a cue pointing at the given image's .bin
  BIN="$(dirname "$CUE")/$(sed -n 's/^FILE "\(.*\)".*/\1/p' "$CUE" | head -1)"
  [ -f "$BIN" ] || BIN="$(find "$(dirname "$CUE")" -maxdepth 1 -iname '*.bin' | head -1)"
  [ -f "$BIN" ] || { echo "no .bin found next to $CUE"; exit 1; }
  ln -sf "$BIN" disc/KnowYourRole.bin
  printf 'FILE "KnowYourRole.bin" BINARY\r\n  TRACK 01 MODE2/2352\r\n    INDEX 01 00:00:00\r\n' > disc/KnowYourRole.cue
fi
[ -f disc/KnowYourRole.cue ] || { echo "put your disc at disc/KnowYourRole.cue (+ .bin) or pass the .cue path"; exit 1; }

echo "== recompiling from your disc"
[ -f RecompOne/RecompOne.sln ] || git submodule update --init --recursive
dotnet build RecompOne/RecompOne.Recompiler -c Release -v quiet
(cd RecompOne/RecompOne.Recompiler && dotnet run -c Release --no-build -- ../../config/KnowYourRole.json)

echo "== publishing (self-contained linux-x64)"
rm -rf publish
dotnet publish KnowYourRole.csproj -c Release -r linux-x64 --self-contained true -m:1 -o publish -v quiet

echo "== packaging AppImage"
APPDIR="$ROOT/dist/AppDir"
rm -rf "$APPDIR" && mkdir -p "$APPDIR/usr/bin"
cp -a publish/. "$APPDIR/usr/bin/"
rm -f "$APPDIR"/usr/bin/*.pdb
chmod +x "$APPDIR/usr/bin/KnowYourRole.Game"
install -m 755 tools/linux/AppRun "$APPDIR/AppRun"
install -m 644 tools/linux/knowyourrole.desktop "$APPDIR/knowyourrole.desktop"
# icon: the largest PNG inside Launcher/app.ico
python3 - "$ROOT/Launcher/app.ico" "$APPDIR/knowyourrole.png" <<'PY'
import struct, sys
d = open(sys.argv[1], 'rb').read(); best = None
for i in range(struct.unpack_from('<H', d, 4)[0]):
    w, _, _, _, _, _, size, off = struct.unpack_from('<BBBBHHII', d, 6 + 16 * i)
    blob = d[off:off + size]
    if blob[:8] == b'\x89PNG\r\n\x1a\n' and (best is None or (w or 256) > best[0]): best = (w or 256, blob)
open(sys.argv[2], 'wb').write(best[1])
PY

TOOL="$ROOT/dist/appimagetool"
[ -x "$TOOL" ] || { curl -fsSL -o "$TOOL" https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage; chmod +x "$TOOL"; }
# --appimage-extract-and-run: works without FUSE (containers, WSL)
ARCH=x86_64 "$TOOL" --appimage-extract-and-run "$APPDIR" "$ROOT/dist/KnowYourRole-x86_64.AppImage" >/dev/null
echo "== done: dist/KnowYourRole-x86_64.AppImage"
echo "   first run: pick your .cue in the game's disc picker (or: echo /path/to/game.cue > ~/.local/share/KnowYourRoleRecomp/disc.txt)"
