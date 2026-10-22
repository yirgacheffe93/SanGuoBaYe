<style>
img[alt=m] {
    max-width: 350px;
}
img[alt=b] {
    max-width: 500px;
}

</style>
# iBaye

步步高电子词典经典游戏 -- 三国霸业 多平台移植版本

目前已支持的平台：iOS/macOS/Windows/HTML5/微信小游戏

前端：https://gitee.com/bgwp/baye-alpha

交流QQ群：526266208

## 编译指南

### 环境要求

- [Emscripten SDK (emsdk)](https://emscripten.org/docs/getting_started/downloads.html) >= 3.1.51
- CMake >= 3.5

### 安装 emsdk

```bash
git clone https://github.com/emscripten-core/emsdk.git
cd emsdk
./emsdk install 3.1.51
./emsdk activate 3.1.51
source ./emsdk_env.sh
```

### 编译 HTML5 引擎 (baye.js + baye.wasm)

```bash
git clone https://gitee.com/bgwp/iBaye.git
cd iBaye
mkdir -p js/baye-engine && cd js/baye-engine
emcmake cmake ../..
make -j$(nproc)
```

编译产物在 `js/baye-engine/src/` 目录下：
- `baye.js` — 引擎主文件
- `baye.wasm` — WebAssembly 二进制
- `baye.wasm.map` — Source Map（调试用）

### 部署到前端

将编译产物复制到前端仓库 [baye-alpha](https://gitee.com/bgwp/baye-alpha) 的 `js/` 目录下即可：

```bash
cp js/baye-engine/src/baye.js       <baye-alpha>/js/
cp js/baye-engine/src/baye.wasm     <baye-alpha>/js/
cp js/baye-engine/src/baye.wasm.map <baye-alpha>/js/
```

> **注意**：部署后需要更新前端 HTML 文件中的 `?ver=` 参数以刷新浏览器缓存。

## 游戏截图

### iOS原生版
![m](images/ios.jpg)

---
### 微信小游戏端
![b](images/wx.png)

---

### HTML5 - PC端
![](images/pc-h5.png)

---

### HTML5 - Mobile端
![m](images/ios-h5.png)

![m](images/ios-h5-k.png)

单手键盘：

![m](images/ios-h5-k2.png)
![m](images/ios-h5-k3.png)

---

### Windows版
![b](images/windows.png)

---

### macOS版
![b](images/macos.png)

---

### 支持高度定制mod，有丰富的修改mod

即使是农民，也能高度自由定制修改版本。

![b](images/mod1.png)
![b](images/mod2.png)
![m](images/mod.png)
