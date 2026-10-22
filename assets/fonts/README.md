# 字体文件

这些文件不是手绘资源，而是从上游 iBaye 的 Emscripten 字体数组还原出来的点阵字库。原生构建通过 `GamSetResourcePath(lib, fonts)` 直接打开它们。

| 文件 | 大小（字节） | 来源 | 用途 |
|---|---|---|---|
| `font.bin` | 163,840 | `src/platform/js/font.bin.c` | 默认 12px 中文/英文 |
| `font24.cn.1` | 600,048 | `font24.1.bin.c` | 24px 中文 1/4 |
| `font24.cn.2` | 600,048 | `font24.2.bin.c` | 24px 中文 2/4 |
| `font24.cn.3` | 600,048 | `font24.3.bin.c` | 24px 中文 3/4 |
| `font24.cn.4` | 600,048 | `font24.4.bin.c` | 24px 中文 4/4 |
| `font24.en.1` | 6,144 | `font24_ascii.1.c` | 可选 24px 英文 |
| `font24.en.2` | 6,144 | `font24_ascii.2.c` | 可选 24px 英文 |

## 重新生成

需要一份上游 iBaye 源码（含 `src/platform/js`）：

```bash
python3 tools/extract_fonts.py \
    --src /path/to/iBaye/src/platform/js \
    --out assets/fonts \
    --with-ascii
```

`--with-ascii` 会额外输出 `font24.en.*`；这两个文件的地址表布局尚未逐字验证，但加载不会报错。

## 注意

- `font24.cn.*` 每个 600,048 字节。原生 `gam_fopen` 用 stdio，路径过长曾触发 `comIn.c` 的缓冲区溢出；构建补丁已修复（`path[512]`）。
- 字体属于原游戏资源，权利状态未确认；不要单独分发。
