#!/usr/bin/env python3
"""Generate ShikkhaDesk branding assets: SVG logo, PNG icon set and a genuine Windows ICO.

Deterministic, dependency-light: Pillow is used only for rasterisation, and the ICO container is
written byte-by-byte so the result is a real multi-image icon file (not a renamed PNG).
"""
from __future__ import annotations

import math
import struct
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
BRAND = ROOT / "assets" / "branding"
PNG = BRAND / "png"

NAVY = (16, 42, 67, 255)          # #102A43
NAVY_2 = (36, 59, 83, 255)        # #243B53
TEAL = (37, 194, 160, 255)        # #25C2A0
TEAL_DARK = (19, 138, 114, 255)   # #138A72
WHITE = (255, 255, 255, 255)
SURFACE = (245, 247, 250, 255)    # #F5F7FA


def rounded_rect(draw: ImageDraw.ImageDraw, box, radius, fill):
    draw.rounded_rectangle(box, radius=radius, fill=fill)


def scale(points, size):
    """Points are authored on a 256x256 grid."""
    factor = size / 256.0
    return [(x * factor, y * factor) for x, y in points]


# Geometry authored on a 256 grid. The cover is drawn first, then the two pages are inset so the
# cover shows through at the spine: that gap is what makes the silhouette read as an open book
# instead of a filled rectangle.
BOOK_COVER = [(34, 116), (128, 142), (222, 116), (222, 198), (128, 222), (34, 198)]
BOOK_LEFT = [(46, 128), (122, 147), (122, 196), (46, 178)]
BOOK_RIGHT = [(210, 128), (134, 147), (134, 196), (210, 178)]
CHECK = [(92, 88), (114, 110), (162, 58)]
BAR = (60, 230, 196, 240)


def draw_mark(draw: ImageDraw.ImageDraw, size: int, page, check, bar, cover=None):
    width = max(2, round(size * 0.055))
    if cover is not None:
        draw.polygon(scale(BOOK_COVER, size), fill=cover)
    draw.polygon(scale(BOOK_LEFT, size), fill=page)
    draw.polygon(scale(BOOK_RIGHT, size), fill=page)
    draw.line(scale(CHECK, size), fill=check, width=width, joint="curve")
    x0, y0, x1, y1 = [v * size / 256.0 for v in BAR]
    rounded_rect(draw, [x0, y0, x1, y1], radius=(y1 - y0) / 2, fill=bar)


def make_icon(size: int, background=NAVY, page=WHITE, check=TEAL, bar=TEAL_DARK,
              transparent_background: bool = False, cover=None) -> Image.Image:
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    if not transparent_background:
        rounded_rect(draw, [0, 0, size - 1, size - 1], radius=size * 0.22, fill=background)

    draw_mark(draw, size, page, check, bar, cover)
    return image


def write_svg(path: Path, background: str | None, page: str, check: str, bar: str, title: str,
              cover: str = "#138A72") -> None:
    def poly(points):
        return " ".join(f"{x},{y}" for x, y in points)

    bg = ""
    if background:
        bg = (f'  <rect x="0" y="0" width="256" height="256" rx="56" ry="56" fill="{background}"/>\n'
              if background != "none" else "")

    svg = f"""<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" width="256" height="256" role="img" aria-label="{title}">
  <title>{title}</title>
{bg}  <polygon points="{poly(BOOK_COVER)}" fill="{cover}"/>
  <polygon points="{poly(BOOK_LEFT)}" fill="{page}"/>
  <polygon points="{poly(BOOK_RIGHT)}" fill="{page}"/>
  <path d="M92 88 L114 110 L162 58" fill="none" stroke="{check}" stroke-width="14"
        stroke-linecap="round" stroke-linejoin="round"/>
  <rect x="60" y="230" width="136" height="10" rx="5" ry="5" fill="{bar}"/>
</svg>
"""
    path.write_text(svg, encoding="utf-8")


def png_icon_bytes(image: Image.Image) -> bytes:
    import io
    buffer = io.BytesIO()
    image.save(buffer, format="PNG", optimize=True)
    return buffer.getvalue()


def bmp_icon_bytes(image: Image.Image) -> bytes:
    """32bpp BGRA DIB (XOR bitmap + empty AND mask) as required for classic ICO entries."""
    width, height = image.size
    pixels = image.convert("RGBA").load()

    rows = bytearray()
    for y in range(height - 1, -1, -1):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            rows += bytes((b, g, r, a))

    mask_row = bytes(((width + 31) // 32) * 4)
    mask = mask_row * height

    header = struct.pack(
        "<IiiHHIIiiII",
        40,            # biSize
        width,
        height * 2,    # XOR + AND
        1,             # biPlanes
        32,            # biBitCount
        0,             # biCompression
        len(rows) + len(mask),
        0, 0, 0, 0,
    )
    return header + bytes(rows) + mask


def build_ico(images: list[tuple[int, Image.Image]], path: Path) -> None:
    entries: list[bytes] = []
    blobs: list[bytes] = []
    offset = 6 + 16 * len(images)

    for size, image in images:
        # PNG keeps the big sizes small; the classic BMP entries keep every legacy consumer happy.
        blob = bmp_icon_bytes(image) if size <= 48 else png_icon_bytes(image)
        blobs.append(blob)
        entries.append(struct.pack(
            "<BBBBHHII",
            0 if size >= 256 else size,   # width (0 means 256)
            0 if size >= 256 else size,   # height
            0,                            # colour count (0 = 256+)
            0,                            # reserved
            1,                            # planes
            32,                           # bits per pixel
            len(blob),
            offset,
        ))
        offset += len(blob)

    path.write_bytes(
        struct.pack("<HHH", 0, 1, len(images)) + b"".join(entries) + b"".join(blobs)
    )


def make_banner(width: int, height: int, path: Path) -> None:
    """Inno Setup wizard image: brand gradient with the mark and product name."""
    image = Image.new("RGBA", (width, height), NAVY)
    draw = ImageDraw.Draw(image)

    for y in range(height):
        t = y / max(1, height - 1)
        draw.line([(0, y), (width, y)], fill=(
            round(NAVY[0] + (NAVY_2[0] - NAVY[0]) * t),
            round(NAVY[1] + (NAVY_2[1] - NAVY[1]) * t),
            round(NAVY[2] + (NAVY_2[2] - NAVY[2]) * t),
            255,
        ))

    mark_size = int(min(width, height) * 0.42)
    mark = make_icon(256, transparent_background=True, page=WHITE, check=TEAL, cover=TEAL_DARK)
    mark = mark.resize((mark_size, mark_size), Image.LANCZOS)
    image.alpha_composite(mark, ((width - mark_size) // 2, int(height * 0.16)))

    draw.rounded_rectangle(
        [width * 0.12, height * 0.70, width * 0.88, height * 0.735],
        radius=height * 0.02, fill=TEAL)

    image.save(path, format="PNG")


def make_preview_sheet(path: Path) -> None:
    sheet = Image.new("RGBA", (1100, 720), SURFACE)
    draw = ImageDraw.Draw(sheet)

    draw.rectangle([0, 0, 1099, 96], fill=NAVY)
    draw.text((32, 40), "ShikkhaDesk — Brand Preview", fill=WHITE)

    variants = [
        ("Full colour", make_icon(160, cover=TEAL_DARK)),
        ("Light background", make_icon(160, background=SURFACE, page=NAVY, check=TEAL_DARK, bar=NAVY_2,
                                       cover=NAVY_2)),
        ("Dark background", make_icon(160, background=NAVY_2, page=WHITE, check=TEAL, bar=TEAL,
                                      cover=TEAL_DARK)),
        ("Transparent", make_icon(160, transparent_background=True, page=NAVY, check=TEAL, bar=TEAL_DARK,
                                  cover=TEAL_DARK)),
    ]

    x = 40
    for index, (label, image) in enumerate(variants):
        y = 150
        sheet.alpha_composite(image, (x, y))
        draw.text((x, y + 176), label, fill=NAVY)
        x += 220
        _ = index

    sizes = [16, 32, 48, 64, 128]
    x = 40
    for size in sizes:
        sheet.alpha_composite(make_icon(size), (x, 400))
        draw.text((x, 440), f"{size}px", fill=NAVY)
        x += 90

    palette = [NAVY, NAVY_2, TEAL, TEAL_DARK, SURFACE, WHITE]
    x = 40
    for colour in palette:
        draw.rectangle([x, 520, x + 120, 580], fill=colour)
        draw.text((x, 590), "#%02X%02X%02X" % colour[:3], fill=NAVY)
        x += 140

    sheet.save(path, format="PNG")


def main() -> None:
    PNG.mkdir(parents=True, exist_ok=True)

    write_svg(BRAND / "logo.svg", "#102A43", "#FFFFFF", "#25C2A0", "#138A72", "ShikkhaDesk")
    write_svg(BRAND / "logo-light.svg", "none", "#FFFFFF", "#25C2A0", "#FFFFFF",
              "ShikkhaDesk light mark", cover="#25C2A0")
    write_svg(BRAND / "logo-dark.svg", "none", "#102A43", "#138A72", "#243B53",
              "ShikkhaDesk dark mark", cover="#243B53")

    sizes = [16, 32, 48, 64, 128, 256]
    images: list[tuple[int, Image.Image]] = []
    for size in sizes:
        image = make_icon(size, cover=TEAL_DARK)
        images.append((size, image))
        image.save(PNG / f"app-icon-{size}.png", format="PNG")

    make_icon(512, cover=TEAL_DARK).save(PNG / "app-icon-512.png", format="PNG")
    make_icon(512, cover=TEAL_DARK).save(BRAND / "logo.png", format="PNG")

    build_ico(images, BRAND / "app-icon.ico")

    installer = ROOT / "installer" / "windows"
    installer.mkdir(parents=True, exist_ok=True)
    make_banner(164, 314, installer / "wizard-image.bmp".replace(".bmp", ".png"))
    make_banner(55, 58, installer / "wizard-small-image.png")

    (BRAND / "preview").mkdir(exist_ok=True)
    make_preview_sheet(BRAND / "preview" / "branding-preview.png")

    print("generated:")
    for path in sorted(BRAND.rglob("*")):
        if path.is_file():
            print(f"  {path.relative_to(ROOT)}  ({path.stat().st_size} bytes)")
    for path in sorted(installer.glob("*.png")):
        print(f"  {path.relative_to(ROOT)}  ({path.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
