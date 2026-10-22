#!/usr/bin/env python3
"""Convert the smoke test's binary PPM (P6) frames to PNG.

No third-party packages required; only zlib from the standard library.

Usage:
    python3 tools/ppm2png.py build/frames/final.ppm build/frames/final.png
"""

from __future__ import annotations

import struct
import sys
import zlib
from pathlib import Path


def read_ppm(path: Path) -> tuple[int, int, bytes]:
    with path.open("rb") as handle:
        magic = handle.readline().strip()
        if magic != b"P6":
            raise ValueError(f"{path}: expected binary PPM (P6), got {magic!r}")
        line = handle.readline()
        while line.startswith(b"#"):
            line = handle.readline()
        width, height = (int(part) for part in line.split())
        max_value = int(handle.readline())
        if max_value != 255:
            raise ValueError(f"{path}: only 8-bit PPM is supported")
        data = handle.read()
    expected = width * height * 3
    if len(data) != expected:
        raise ValueError(f"{path}: expected {expected} bytes, got {len(data)}")
    return width, height, data


def write_png(path: Path, width: int, height: int, rgb: bytes) -> None:
    raw = bytearray()
    stride = width * 3
    for y in range(height):
        raw.append(0)  # PNG filter: none
        raw.extend(rgb[y * stride:(y + 1) * stride])

    def chunk(kind: bytes, payload: bytes) -> bytes:
        crc = zlib.crc32(kind + payload) & 0xFFFFFFFF
        return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", crc)

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 6))
    png += chunk(b"IEND", b"")
    path.write_bytes(png)


def main(argv: list[str]) -> int:
    if len(argv) != 3:
        print(__doc__, file=sys.stderr)
        return 2
    source, target = Path(argv[1]), Path(argv[2])
    width, height, rgb = read_ppm(source)
    write_png(target, width, height, rgb)
    print(f"{target} ({width}x{height})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
