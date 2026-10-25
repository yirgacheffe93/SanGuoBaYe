#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export DOTNET_ROOT="$ROOT/.tools/dotnet"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
cd "$ROOT"
dotnet build SanGuoBaYe.sln --nologo
dotnet test SanGuoBaYe.sln --no-build --nologo
.tools/godot/Godot_mono.app/Contents/MacOS/Godot --headless --path "$ROOT" -- --spike-smoke
