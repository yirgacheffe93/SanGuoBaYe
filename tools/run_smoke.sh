#!/usr/bin/env bash
# Run the native engine smoke test and dump frames to build/frames.
#
# Environment overrides:
#   BAYE_LIB        path to a .lib game pack (default libs/SGBY.lib)
#   BAYE_FONTS      font directory (default assets/fonts)
#   BAYE_SMOKE_OUT  frame output directory (default build/frames)
#   BAYE_SAVES      save directory (default build/saves)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD="$ROOT/build"
BIN="$BUILD/baye_smoke"

LIB="${BAYE_LIB:-$ROOT/libs/SGBY.lib}"
FONTS="${BAYE_FONTS:-$ROOT/assets/fonts}"
OUT="${BAYE_SMOKE_OUT:-$BUILD/frames}"
SAVES="${BAYE_SAVES:-$BUILD/saves}"

if [[ ! -x "$BIN" ]]; then
    echo "missing $BIN; run tools/build_native.sh first" >&2
    exit 1
fi
if [[ ! -f "$LIB" ]]; then
    echo "game pack not found: $LIB" >&2
    echo "set BAYE_LIB to a .lib file (this repo keeps them in libs/)" >&2
    exit 1
fi
if [[ ! -f "$FONTS/font.bin" ]]; then
    echo "font.bin not found in $FONTS" >&2
    echo "run tools/extract_fonts.py against an upstream iBaye checkout" >&2
    exit 1
fi

mkdir -p "$OUT" "$SAVES"

echo "==> lib:   $LIB"
echo "==> fonts: $FONTS"
echo "==> out:   $OUT"
"$BIN" "$LIB" "$FONTS" "$SAVES" "$OUT"

echo
echo "final frame: $OUT/final.ppm"
echo "view it with:"
echo "  python3 tools/ppm2png.py \"$OUT/final.ppm\" \"$OUT/final.png\""
