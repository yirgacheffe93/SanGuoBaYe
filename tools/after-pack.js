'use strict';

/**
 * electron-builder 的 afterPack 钩子：在打包 .app 之后、生成 zip/dmg 之前做 ad-hoc 签名。
 *
 * 这样 `npm run pack`（目录）和 `npm run dist`（zip/dmg）拿到的都是可运行的签名包。
 * 正式发行时把 identity 换成 Developer ID 证书，并补上公证步骤。
 */

const { execFileSync } = require('node:child_process');
const path = require('node:path');

exports.default = async function afterPack(context) {
  if (context.electronPlatformName !== 'darwin') return;

  const appName = context.packager.appInfo.productFilename;
  const appPath = path.join(context.appOutDir, `${appName}.app`);

  try {
    execFileSync('codesign', ['--force', '--deep', '--sign', '-', appPath], { stdio: 'inherit' });
    execFileSync('codesign', ['--verify', '--strict', appPath], { stdio: 'inherit' });
    console.log(`[afterPack] ad-hoc 签名完成: ${appPath}`);
  } catch (err) {
    console.error('[afterPack] 签名失败:', err.message);
    throw err;
  }
};
