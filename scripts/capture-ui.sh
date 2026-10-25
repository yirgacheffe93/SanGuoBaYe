#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export DOTNET_ROOT="$ROOT/.tools/dotnet"
export PATH="$DOTNET_ROOT:$PATH"
cd "$ROOT"
mkdir -p docs/assets
.tools/godot/Godot_mono.app/Contents/MacOS/Godot --path "$ROOT" -- --capture-ui "--capture-dir=$ROOT/docs/assets"
