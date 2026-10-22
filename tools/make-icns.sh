#!/usr/bin/env bash
# 从 build/icon.png 生成 macOS 的 build/icon.icns
set -euo pipefail
cd "$(dirname "$0")/.."

if [ ! -f build/icon.png ]; then
  echo "缺少 build/icon.png，请先运行 npm run icon:png（或 python3 tools/make_icon.py）" >&2
  exit 1
fi

rm -rf build/icon.iconset
mkdir -p build/icon.iconset

for spec in \
  "16:16x16" "32:16x16@2x" "32:32x32" "64:32x32@2x" \
  "128:128x128" "256:128x128@2x" "256:256x256" "512:256x256@2x" \
  "512:512x512" "1024:512x512@2x"; do
  size="${spec%%:*}"
  name="${spec##*:}"
  sips -z "$size" "$size" build/icon.png --out "build/icon.iconset/icon_${name}.png" >/dev/null
done

iconutil -c icns build/icon.iconset -o build/icon.icns
echo "已生成 build/icon.icns"
