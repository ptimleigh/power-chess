#!/usr/bin/env python3
"""Builds the Chess Fighter app icons from the knight art (wwwroot/img/pieces/white_knight.png): sunset stage, gold bevel frame, the white knight.
Usage: python3 -I tools/make-icons.py      (writes web/ChessFighter.Web/wwwroot/icons/*.png)"""
import os, re
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "web", "ChessFighter.Web", "wwwroot", "icons")

PAL = {'k': '#0a1230', '1': '#2a3a8a', '2': '#4f6bc8', '3': '#86a3ee', '4': '#c4d6ff', '5': '#f4f8ff', 'e': '#2d6bff',
       'a': '#6b4400', 'b': '#b87a10', 'c': '#ffd23f', 'd': '#fff2a0', 'h': '#ffffff'}

def lerp(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))
def hexrgb(h): return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))

def stage(size):
    img = Image.new("RGB", (size, size))
    d = ImageDraw.Draw(img)
    stops = [(0.0, hexrgb('#1b1260')), (0.45, hexrgb('#2c1b73')), (0.7, hexrgb('#6b2f8f')), (1.0, hexrgb('#07061a'))]
    for y in range(size):
        t = y / (size - 1)
        for (t0, c0), (t1, c1) in zip(stops, stops[1:]):
            if t0 <= t <= t1:
                col = lerp(c0, c1, (t - t0) / (t1 - t0)); break
        d.line([(0, y), (size, y)], fill=col)
    # sun
    r = size * 0.22
    cx, cy = size / 2, size * 0.62
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=hexrgb('#ff9a3d'))
    d.rectangle([0, int(size * 0.7), size, size], fill=hexrgb('#07061a'))
    # floor lines
    for i in range(-6, 7):
        d.line([(size / 2 + i * size * 0.03, size * 0.7), (size / 2 + i * size * 0.18, size)], fill=hexrgb('#4a2a8a'), width=max(1, size // 256))
    for i in range(4):
        y = size * (0.74 + 0.07 * i * (i + 1) / 4)
        d.line([(0, y), (size, y)], fill=hexrgb('#4a2a8a'), width=max(1, size // 256))
    return img

def frame(img):
    size = img.width
    d = ImageDraw.Draw(img)
    f = size // 24
    d.rectangle([0, 0, size - 1, size - 1], outline=(0, 0, 0), width=f)
    for i in range(f, f * 3):  # gold-to-orange diagonal gradient border
        t = i / (f * 3)
        col = lerp(hexrgb('#ffd23f'), hexrgb('#b8570a'), t)
        d.rectangle([i, i, size - 1 - i, size - 1 - i], outline=col, width=1)
    d.rectangle([f * 3, f * 3, size - 1 - f * 3, size - 1 - f * 3], outline=(0, 0, 0), width=max(2, f // 2))
    return img

KNIGHT = os.path.join(HERE, "..", "web", "ChessFighter.Web", "wwwroot", "img", "pieces", "white_knight.png")

def sprite(px):
    img = Image.open(KNIGHT).convert("RGBA")
    img = img.crop(img.getchannel("A").getbbox())
    return img.resize((img.width * px, img.height * px), Image.NEAREST)

def make(size, maskable):
    img = stage(size)
    if not maskable:
        img = frame(img)
    # the maskable icon keeps its art inside the central 66% so any launcher mask leaves it whole
    target = size * (0.5 if maskable else 0.56)
    kh = Image.open(KNIGHT).convert('RGBA').getchannel('A').getbbox()
    px = max(1, int(target // (kh[3] - kh[1])))
    k = sprite(px)
    # a hard black drop shadow, like the game's pieces
    sh = Image.new("RGBA", k.size, (0, 0, 0, 0))
    sh.paste((0, 0, 0, 255), mask=k.getchannel("A"))
    x = (size - k.width) // 2
    y = int(size * (0.47 if maskable else 0.45) - k.height / 2)
    img.paste(sh, (x + px, y + px), sh)
    img.paste(k, (x, y), k)
    return img

os.makedirs(OUT, exist_ok=True)
make(512, False).save(os.path.join(OUT, "icon-512.png"))
make(512, True).save(os.path.join(OUT, "icon-maskable-512.png"))
make(192, False).resize((192, 192), Image.NEAREST if False else Image.LANCZOS).save(os.path.join(OUT, "icon-192.png"))
make(512, False).resize((180, 180), Image.LANCZOS).save(os.path.join(OUT, "apple-touch-icon.png"))
print("icons written to", os.path.normpath(OUT))
