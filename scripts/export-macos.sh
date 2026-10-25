#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export DOTNET_ROOT="$ROOT/.tools/dotnet"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
cd "$ROOT"
mkdir -p dist
rm -rf "$ROOT/dist/ProjectThreeKingdoms-Spike.app"
.tools/godot/Godot_mono.app/Contents/MacOS/Godot --headless --path "$ROOT" --export-release macOS "$ROOT/dist/ProjectThreeKingdoms-Spike.app"
"dist/ProjectThreeKingdoms-Spike.app/Contents/MacOS/Project Three Kingdoms - R0 Spike" --headless -- --spike-smoke
