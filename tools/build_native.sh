#!/usr/bin/env bash
# Build the native iBaye core plus the headless smoke harness.
#
# The vendored core in native/ibaye_core/src is kept close to upstream.  Local
# changes live in native/patches/ and are applied to a throw-away copy under
# build/src, so `git diff` against the upstream source stays meaningful.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CORE="$ROOT/native/ibaye_core"
BUILD="$ROOT/build"
SRC="$BUILD/src"
CC="${CC:-clang}"

if [[ ! -d "$CORE/src" ]]; then
    echo "missing vendored core: $CORE/src" >&2
    exit 1
fi

echo "==> copying engine source"
rm -rf "$SRC"
mkdir -p "$BUILD"
cp -R "$CORE/src" "$SRC"

for patch_file in "$ROOT"/native/patches/*.patch; do
    echo "==> applying $(basename "$patch_file")"
    patch -p1 -d "$SRC" < "$patch_file"
done

echo "==> installing POSIX backend"
cp -R "$ROOT/native/platform/posix" "$SRC/platform/posix"

# Source list mirrors upstream src/CMakeLists.txt for the portable part of the
# engine, plus platform/common and our POSIX backend.  The Emscripten, Win32
# and iOS platform folders are intentionally not used.
SOURCES=(
    FgtCount.c FgtPkAi.c Fight.c FightSub.c PublicFun.c Size.c
    citycmd.c citycmdb.c citycmdc.c citycmdd.c citycmde.c cityedit.c
    comIn.c comOut.c datman.c debug.c gamEng.c infdeal.c itoa.c
    paccount.c pconst.c pstring.c showface.c strcnt.c tactic.c varma.c
    data-bind.c bind-objects.c touch.c config.c miniz.c version.c
    platform/common/bios.c platform/common/gui.c platform/common/sys.c
    platform/posix/fsys.c platform/posix/sem.c platform/posix/timer.c
    platform/posix/sys.c platform/posix/script.c platform/posix/smoke.c
)

echo "==> building $BUILD/baye_smoke with $CC"
(
    cd "$SRC"
    "$CC" -std=gnu99 -O1 -g ${CFLAGS:-} \
        -I. -I./platform/common -D_GNU_SOURCE -pthread \
        "${SOURCES[@]}" \
        -o "$BUILD/baye_smoke" -lm
)

echo "==> ok: $BUILD/baye_smoke"
echo "    run: tools/run_smoke.sh"
