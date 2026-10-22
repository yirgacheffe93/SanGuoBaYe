#!/usr/bin/env bash
# 打包 macOS 应用（Apple Silicon）。
#
#   npm run pack    → 只生成 .app（dist/…/三国霸业.app），便于本机试跑
#   npm run dist    → 生成 .app + .dmg + .zip
#
# 说明：
# - 使用本地 node_modules/electron/dist 作为 Electron 来源，打包过程不需要联网。
# - 没有 Developer ID 证书，因此只做 ad-hoc 签名：本机能运行，分发给别人会被 Gatekeeper 拦。
#   正式发行需要 Apple Developer 账号 + 公证（见 docs/desktop-macos.md）。
set -euo pipefail
cd "$(dirname "$0")/.."

TARGET="${1:-dir}"

if [ ! -d node_modules/electron/dist ]; then
  echo "没有找到 Electron 运行时，请先执行：ELECTRON_CACHE=\"\$PWD/.electron-cache\" node node_modules/electron/install.js" >&2
  exit 1
fi

export CSC_IDENTITY_AUTO_DISCOVERY=false
export ELECTRON_CACHE="$PWD/.electron-cache"
export ELECTRON_BUILDER_CACHE="$PWD/.eb-cache"

BUILDER="./node_modules/.bin/electron-builder"
if [ ! -x "$BUILDER" ]; then
  echo "没有找到 electron-builder，请先执行 npm install" >&2
  exit 1
fi

rm -rf dist

if [ "$TARGET" = "dmg" ]; then
  "$BUILDER" --mac --arm64
else
  "$BUILDER" --mac --dir --arm64
fi

APP="$(find dist -maxdepth 2 -name '*.app' -print -quit)"
if [ -n "$APP" ]; then
  echo "==> 签名校验：$APP"
  codesign --verify --strict "$APP" && echo "==> 签名校验通过"
  echo "==> 产物：$APP"
else
  echo "没有找到生成的 .app" >&2
  exit 1
fi
