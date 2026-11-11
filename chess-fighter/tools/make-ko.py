#!/usr/bin/env python3
"""Draws the big graffiti "K" and "O" used for the replay's K.O. (same look as the Chess Fighter logo: yellow to orange with a dithered band,
thick black outline, hard drop shadow). Writes web/ChessFighter.Web/wwwroot/img/ko_k.png and ko_o.png.
Usage: python3 -I tools/make-ko.py"""
import os
from PIL import Image, ImageDraw
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "web", "ChessFighter.Web", "wwwroot", "img")
GW, GH, SS = 62, 68, 8          # glyph grid (pixel-art cells) and the supersampling used to draw it smoothly first
SCALE = 6                       # output pixel size of one cell

def glyph(points_list):
    im = Image.new("L", (GW * SS, GH * SS), 0)
    d = ImageDraw.Draw(im)
    for pts in points_list:
        d.polygon([((x + 5 + (34 - y) * 0.15) * SS, y * SS) for x, y in pts], fill=255)   # a graffiti lean
    return im

def shapes(letter):
    """Chunky, slanted, slightly jagged graffiti letters on a 52x68 grid."""
    if letter == "K":
        return [
            [(8, 4), (22, 2), (24, 64), (6, 66)],                           # stem (leans slightly)
            [(22, 34), (44, 2), (51, 7), (33, 38), (50, 62), (42, 67), (22, 42)],  # arm + leg joined at the stem
        ]
    # O: a thick, angular ring with a slanted counter
    outer = [(14, 3), (38, 1), (49, 12), (50, 52), (40, 66), (14, 67), (3, 56), (2, 14)]
    inner = [(18, 17), (33, 15), (35, 22), (36, 50), (30, 54), (18, 54), (16, 48)]
    return [outer], [inner]

def render(letter):
    if letter == "K":
        mask = glyph(shapes("K"))
    else:
        outer, inner = shapes("O")
        mask = glyph(outer)
        hole = glyph(inner)
        mask = Image.fromarray(np.where(np.array(hole) > 0, 0, np.array(mask)).astype("uint8"))
    m = np.array(mask.resize((GW, GH), Image.BOX)) > 110
    pad = 8
    H, W = GH + pad * 2, GW + pad * 2
    body = np.zeros((H, W), bool); body[pad:pad + GH, pad:pad + GW] = m
    def dilate(a, n):
        out = a.copy()
        for _ in range(n):
            p = np.pad(out, 1)
            out = out | p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
        return out
    outline = dilate(body, 3)
    shadow = np.zeros_like(outline); shadow[3:, 3:] = outline[:-3, :-3]
    img = np.zeros((H, W, 4), np.uint8)
    img[shadow & ~outline] = (0, 0, 0, 255)          # hard drop shadow (black, like the outline)
    img[outline & ~body] = (0, 0, 0, 255)
    ys, xs = np.where(body)
    top, bot = ys.min(), ys.max()
    for y, x in zip(ys, xs):
        t = (y - top) / max(1, bot - top)
        if t < .52: c = (255, 236, 96)               # yellow top
        elif t < .6: c = (255, 236, 96) if (x + y) % 2 == 0 else (255, 160, 30)   # dithered band, like the logo
        else: c = (255, 140, 28)                      # orange bottom
        img[y, x] = (*c, 255)
    # lit top-left edge, darker bottom-right edge
    up = np.pad(body, 1)
    lit = body & ~up[:-2, 1:-1] | body & ~up[1:-1, :-2]
    dark = body & ~up[2:, 1:-1] | body & ~up[1:-1, 2:]
    img[lit] = (255, 250, 190, 255)
    img[dark & ~lit] = (200, 80, 10, 255)
    out = Image.fromarray(img).resize((W * SCALE, H * SCALE), Image.NEAREST)
    return out

os.makedirs(OUT, exist_ok=True)
for L in ("K", "O"):
    render(L).save(os.path.join(OUT, "ko_%s.png" % L.lower()), optimize=True)
print("wrote ko_k.png and ko_o.png to", os.path.normpath(OUT))
