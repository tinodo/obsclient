#!/usr/bin/env python3
"""Draws OBSClient/icon.png, the icon of the NuGet package, from the same drawing as docs/images/logo.svg.

NuGet takes a PNG or JPEG for an icon (not SVG), of at most 1 MB, and recommends 128 by 128 pixels: this makes 256 by 256, with
transparent corners. The drawing is that of the logo, which is 64 by 64 units: a rounded square with a diagonal gradient, and two arrows
(a connection that goes both ways). Keep it in line with docs/images/logo.svg when that changes.

Usage: python tools/make-icon.py [OUTPUT.png]    (default: OBSClient/icon.png)
Needs Pillow (pip install pillow).
"""
import os
import sys

from PIL import Image, ImageDraw

SIZE = 256
SUPERSAMPLE = 4  # drawn four times as large and scaled down, for smooth edges
UNITS = 64
START = (0x8B, 0x7B, 0xFF)
END = (0x4A, 0x3B, 0xC7)
CORNER_RADIUS = 14
STROKE_WIDTH = 5
ARROWS = [
    [(14, 24), (45, 24), (38, 17)],
    [(50, 40), (19, 40), (26, 47)],
]


def draw(size=SIZE):
    canvas = size * SUPERSAMPLE
    unit = canvas / UNITS

    # The gradient of the logo runs from the top left to the bottom right corner (objectBoundingBox 0,0 to 1,1).
    gradient = Image.new("RGB", (canvas, canvas))
    pixels = gradient.load()
    for y in range(canvas):
        for x in range(canvas):
            t = (x + y) / (2 * (canvas - 1))
            pixels[x, y] = tuple(round(a + (b - a) * t) for a, b in zip(START, END))

    mask = Image.new("L", (canvas, canvas), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, canvas - 1, canvas - 1], radius=CORNER_RADIUS * unit, fill=255)

    icon = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
    icon.paste(gradient, (0, 0), mask)

    pen = ImageDraw.Draw(icon)
    width = round(STROKE_WIDTH * unit)
    radius = width / 2
    for arrow in ARROWS:
        points = [(x * unit, y * unit) for x, y in arrow]
        pen.line(points, fill=(255, 255, 255, 255), width=width, joint="curve")
        for x, y in points:
            pen.ellipse([x - radius, y - radius, x + radius, y + radius], fill=(255, 255, 255, 255))

    return icon.resize((size, size), Image.LANCZOS)


def main(arguments):
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    path = arguments[0] if arguments else os.path.join(root, "OBSClient", "icon.png")
    draw().save(path, optimize=True)
    print(f"{path}: {os.path.getsize(path):,} bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
