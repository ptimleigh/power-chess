#!/usr/bin/env python3
"""Cleans the three summon portraits: hard alpha edge, cropped to the figure, scaled to one height on a pixel grid, palette limited.
Usage: python3 -I tools/make-summons.py <shiva.png> <caissa.png> <mars.png>   (writes web/ChessFighter.Web/wwwroot/img/summons/*.png)"""
import os, sys
from PIL import Image
H = 160
out = os.path.join(os.path.dirname(__file__), "..", "web", "ChessFighter.Web", "wwwroot", "img", "summons")
for name, src in zip(("shiva", "caissa", "mars"), sys.argv[1:4]):
    im = Image.open(src).convert("RGBA")
    a = im.getchannel("A").point(lambda v: 255 if v >= 128 else 0)
    im.putalpha(a)
    im = im.crop(a.getbbox())
    w = max(1, round(im.width * H / im.height))
    rgb = Image.new("RGB", im.size, (0, 0, 0)); rgb.paste(im, mask=im.getchannel("A"))
    small = rgb.resize((w, H), Image.BOX).quantize(40, dither=Image.Dither.NONE).convert("RGBA")
    mask = im.getchannel("A").resize((w, H), Image.BOX).point(lambda v: 255 if v >= 128 else 0)
    small.putalpha(mask)
    small.save(os.path.join(out, name + ".png"), optimize=True)
    print(name, small.size)
