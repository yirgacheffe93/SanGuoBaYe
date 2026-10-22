#!/usr/bin/env python3
"""生成 macOS 应用图标（build/icon.png → build/icon.icns）。

用法：
    python3 tools/make_icon.py

说明：这是第一阶段的占位图标，正式发行前应替换为独立设计的图标。
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
BUILD = ROOT / "build"
SIZE = 1024
FONT_CANDIDATES = [
    ("/System/Library/Fonts/Supplemental/Songti.ttc", [0, 1, 2, 3, 4, 5]),
    ("/System/Library/Fonts/STHeiti Medium.ttc", [0, 1]),
    ("/System/Library/Fonts/Hiragino Sans GB.ttc", [0, 1]),
]


def lerp(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))


def vertical_gradient(size, top, bottom):
    img = Image.new("RGB", (1, size), top)
    draw = ImageDraw.Draw(img)
    for y in range(size):
        draw.point((0, y), fill=lerp(top, bottom, y / max(1, size - 1)))
    return img.resize((size, size))


def load_glyph_font(size):
    glyph = "霸"
    for path, indexes in FONT_CANDIDATES:
        if not Path(path).exists():
            continue
        for index in indexes:
            try:
                font = ImageFont.truetype(path, size, index=index)
            except Exception:
                continue
            try:
                mask = font.getmask(glyph)
            except Exception:
                continue
            if mask.size[0] <= 1 or mask.size[1] <= 1:
                continue
            return font
    raise SystemExit("没有找到可用的中文字体")


def main():
    BUILD.mkdir(exist_ok=True)

    # 圆角底板 + 渐变
    margin = 48
    radius = 224
    plate = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    mask = Image.new("L", (SIZE, SIZE), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        [margin, margin, SIZE - margin, SIZE - margin], radius=radius, fill=255
    )

    gradient = vertical_gradient(SIZE, (32, 38, 48), (10, 12, 16)).convert("RGBA")
    plate.paste(gradient, (0, 0), mask)

    draw = ImageDraw.Draw(plate)
    # 内描边：暗金细线
    draw.rounded_rectangle(
        [margin + 10, margin + 10, SIZE - margin - 10, SIZE - margin - 10],
        radius=radius - 10,
        outline=(150, 118, 44, 190),
        width=4,
    )
    # 外描边：提亮
    draw.rounded_rectangle(
        [margin, margin, SIZE - margin, SIZE - margin],
        radius=radius,
        outline=(255, 255, 255, 22),
        width=3,
    )

    font = load_glyph_font(560)
    glyph = "霸"
    bbox = draw.textbbox((0, 0), glyph, font=font)
    gw, gh = bbox[2] - bbox[0], bbox[3] - bbox[1]
    gx = (SIZE - gw) / 2 - bbox[0]
    gy = (SIZE - gh) / 2 - bbox[1]

    # 金色字：底色 + 轻微外发光
    glow = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    ImageDraw.Draw(glow).text((gx, gy), glyph, font=font, fill=(201, 162, 39, 70))
    from PIL import ImageFilter

    plate = Image.alpha_composite(plate, glow.filter(ImageFilter.GaussianBlur(18)))

    draw = ImageDraw.Draw(plate)
    draw.text((gx, gy), glyph, font=font, fill=(230, 200, 106, 255))

    # 顶部高光
    sheen = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    sd = ImageDraw.Draw(sheen)
    sd.rounded_rectangle(
        [margin + 6, margin + 6, SIZE - margin - 6, margin + 240],
        radius=radius - 6,
        fill=(255, 255, 255, 26),
    )
    plate = Image.alpha_composite(plate, sheen.filter(ImageFilter.GaussianBlur(30)))

    out = BUILD / "icon.png"
    plate.save(out)
    print("wrote", out)


if __name__ == "__main__":
    main()
