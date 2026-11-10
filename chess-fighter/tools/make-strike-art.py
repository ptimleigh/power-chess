#!/usr/bin/env python3
"""Cleans the Shiva sword and the two dragons: hard alpha edge, cropped, scaled, palette limited.
The sword is turned point-down so it can fall. Usage: python3 -I tools/make-strike-art.py <sword.png> <dragon_black.png> <dragon_white.png>"""
import os, sys
from PIL import Image
out = os.path.join(os.path.dirname(__file__), "..", "web", "ChessFighter.Web", "wwwroot", "img", "summons")

def clean(src, height=None, width=None, rotate=0, colors=48):
    im = Image.open(src).convert("RGBA")
    a = im.getchannel("A").point(lambda v: 255 if v >= 128 else 0)
    im.putalpha(a)
    im = im.crop(a.getbbox())
    if rotate: im = im.rotate(rotate, expand=True)
    if height: w, h = round(im.width * height / im.height), height
    else: w, h = width, round(im.height * width / im.width)
    rgb = Image.new("RGB", im.size, (0, 0, 0)); rgb.paste(im, mask=im.getchannel("A"))
    small = rgb.resize((w, h), Image.BOX).quantize(colors, dither=Image.Dither.NONE).convert("RGBA")
    small.putalpha(im.getchannel("A").resize((w, h), Image.BOX).point(lambda v: 255 if v >= 128 else 0))
    return small

clean(sys.argv[1], height=96, rotate=180, colors=24).save(os.path.join(out, "sword.png"), optimize=True)
clean(sys.argv[2], width=170).save(os.path.join(out, "dragon-black.png"), optimize=True)
clean(sys.argv[3], width=170).save(os.path.join(out, "dragon-white.png"), optimize=True)
print("ok")
