#!/usr/bin/env python3
"""Extract the bitmap font binaries that the Emscripten build embeds as C arrays.

The upstream iBaye web build serves these fonts from memory
(`src/platform/js/fsys.c`), while the native build opens them as ordinary
files.  This script turns the C arrays back into the original binaries.

Usage:
    python3 tools/extract_fonts.py --src /path/to/iBaye/src/platform/js \
                                   --out assets/fonts

Only the font arrays are read; the rest of the Emscripten platform is not
needed by the native build.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

HEX_BYTE = re.compile(r"0x([0-9A-Fa-f]{1,2})")
HEX_PAIR_LINE = re.compile(
    r"^\s*0x([0-9A-Fa-f]{2})\s*,\s*0x([0-9A-Fa-f]{2})\s*,"
)

# (source file, output file, parser)
FONTS = [
    ("font.bin.c", "font.bin", "hex"),
    ("font24.1.bin.c", "font24.cn.1", "hex"),
    ("font24.2.bin.c", "font24.cn.2", "hex"),
    ("font24.3.bin.c", "font24.cn.3", "hex"),
    ("font24.4.bin.c", "font24.cn.4", "hex"),
]

# The 24px ASCII arrays are annotated one glyph at a time.  They are optional
# (the default ASCII font is served from font.bin); their address-table layout
# has not been verified glyph-by-glyph yet, so keep them behind a flag.
ASCII_FONTS = [
    ("font24_ascii.1.c", "font24.en.1", "pair"),
    ("font24_ascii.2.c", "font24.en.2", "pair"),
]


def parse_hex(text: str) -> bytes:
    return bytes(int(value, 16) for value in HEX_BYTE.findall(text))


def parse_pair(text: str) -> bytes:
    out = bytearray()
    for line in text.splitlines():
        match = HEX_PAIR_LINE.match(line)
        if match:
            out += bytes((int(match.group(1), 16), int(match.group(2), 16)))
    return bytes(out)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--src", required=True, type=Path,
                        help="upstream iBaye src/platform/js directory")
    parser.add_argument("--out", required=True, type=Path,
                        help="destination directory (created if missing)")
    parser.add_argument("--quiet", action="store_true")
    parser.add_argument("--with-ascii", action="store_true",
                        help="also extract the unverified 24px ASCII fonts")
    args = parser.parse_args()

    if not args.src.is_dir():
        parser.error(f"source directory not found: {args.src}")

    args.out.mkdir(parents=True, exist_ok=True)
    failures = 0
    fonts = FONTS + (ASCII_FONTS if args.with_ascii else [])
    for source_name, output_name, kind in fonts:
        source = args.src / source_name
        if not source.is_file():
            print(f"skip {source_name}: not found", file=sys.stderr)
            failures += 1
            continue
        text = source.read_text(encoding="utf-8", errors="replace")
        data = parse_hex(text) if kind == "hex" else parse_pair(text)
        if not data:
            print(f"skip {source_name}: no bytes parsed", file=sys.stderr)
            failures += 1
            continue
        target = args.out / output_name
        target.write_bytes(data)
        if not args.quiet:
            print(f"{output_name:16s} {len(data):8d} bytes  <- {source_name}")

    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
