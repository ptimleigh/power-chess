#!/usr/bin/env python3
"""Builds the piece sprites from art/pieces-sheet-3.png (a neutral gray-blue Staunton set with gold trim bands: king, queen, bishop, knight, rook, pawn).
The sheet is a 6 px "pixel" grid, so it is first snapped back to its native grid (135x65 cells). Each piece is cut out and recoloured
(White steel blue, Black crimson; the gold trim stays gold), then resized per piece (the small ones a little bigger) and wider (18% for the knight, 38% for the rest),
limited to 40 colours, and written to Sprites.Art.cs (replay canvas) and wwwroot/img/pieces/*.png (the board).
Usage: python3 -I tools/make-pieces.py   (tools/make-pieces-sheet2.py and -sheet1.py built the earlier sets.)"""
import os, sys, colorsys
from PIL import Image, ImageDraw
import numpy as np
HERE=os.path.dirname(os.path.abspath(__file__))
SRC=os.path.join(HERE,'..','art','pieces-sheet-3.png')
OUT=os.path.join(HERE,'..','web','ChessFighter.Web','Game','Sprites.Art.cs')
PNG_DIR=os.path.join(HERE,'..','web','ChessFighter.Web','wwwroot','img','pieces')
PX,OX,PY,OY=6.0,1.75,5.82,4.0
raw=Image.open(SRC).convert('RGBA'); alpha=np.array(raw)[...,3].astype(int)
white=Image.new('RGBA',raw.size,(255,255,255,255)); white.alpha_composite(raw)
im=white.convert('RGB'); a=np.array(im).astype(int); H,W,_=a.shape
nx=int((W-OX)/PX); ny=int((H-OY)/PY)
native=np.zeros((ny,nx,3),int)
for j in range(ny):
    for i in range(nx):
        cx=int(OX+(i+0.5)*PX); cy=int(OY+(j+0.5)*PY)
        blk=a[max(0,cy-1):cy+2,max(0,cx-1):cx+2].reshape(-1,3)
        native[j,i]=np.median(blk,axis=0)
nalpha=np.zeros((ny,nx),int)
for j in range(ny):
    for i in range(nx):
        cx=int(OX+(i+0.5)*PX); cy=int(OY+(j+0.5)*PY)
        nalpha[j,i]=np.median(alpha[max(0,cy-1):cy+2,max(0,cx-1):cx+2])
bg=nalpha<128
fg=~bg
cols=np.where(fg.any(axis=0))[0]
gaps=np.where(np.diff(cols)>1)[0]
starts=[cols[0]]+[cols[g+1] for g in gaps]; ends=[cols[g] for g in gaps]+[cols[-1]]
segs=[(s,e) for s,e in zip(starts,ends) if e-s>=6]
# the knight and the rook touch: split a wide segment at its thinnest column
fixed=[]
for s,e in segs:
    if e-s>30:
        cnt=fg[:,s:e+1].sum(axis=0); lo_,hi_=int((e-s)*0.45),int((e-s)*0.75)
        cut=s+lo_+int(np.argmin(cnt[lo_:hi_]))
        fixed+= [(s,cut-1),(cut,e)] if False else [(s,cut),(cut+1,e)]
    else: fixed.append((s,e))
segs=fixed
names=['king','queen','bishop','knight','rook','pawn']
print('native',nx,ny,'segments',len(segs),[(s,e) for s,e in segs])
pieces={}
for n,(s,e) in zip(names,segs):
    sub=native[:,s:e+1].copy(); m=fg[:,s:e+1]
    ys=np.where(m.any(axis=1))[0]
    rgba=np.zeros(sub.shape[:2]+(4,),np.uint8); rgba[...,:3]=sub; rgba[...,3]=np.where(m,255,0)
    pieces[n]=Image.fromarray(rgba).crop((0,ys.min(),sub.shape[1],ys.max()+1))
print({k:v.size for k,v in pieces.items()})
def hexrgb(h): return np.array([int(h[i:i+2],16) for i in (1,3,5)],float)
def lum(arr): return (0.299*arr[...,0]+0.587*arr[...,1]+0.114*arr[...,2])/255
def is_trim(arr):
    out=np.zeros(arr.shape[:2],bool)
    for y in range(arr.shape[0]):
        for x in range(arr.shape[1]):
            r,g,b=arr[y,x]/255; h,s,v=colorsys.rgb_to_hsv(r,g,b)
            out[y,x]= s>0.35 and 0.04<h<0.17 and v>0.35
    return out
BODY={'white':['#14214f','#2f4aa8','#6d8fe0','#a9c2f5','#e6efff'],'black':['#2a0610','#7a1228','#b81f3a','#e0384a','#ff8a78']}
GOLD=['#6b4400','#b87a10','#ffd23f','#fff2a0']
king=np.array(pieces['king']).astype(float)
body_mask=(king[...,3]>0)&~is_trim(king[...,:3])
Lk=lum(king[...,:3])[body_mask]; lo,hi=np.percentile(Lk,3),np.percentile(Lk,97)
def recolor(img,side):
    arr=np.array(img).astype(float); rgb=arr[...,:3]; trim=is_trim(rgb)
    L=lum(rgb); t=np.clip((L-lo)/(hi-lo),0,1)
    out=arr.copy()
    ramp=np.array([hexrgb(c) for c in BODY[side]]); g=np.array([hexrgb(c) for c in GOLD])
    for y in range(arr.shape[0]):
        for x in range(arr.shape[1]):
            if arr[y,x,3]==0: continue
            if trim[y,x]:
                k=t[y,x]*(len(g)-1); i=int(min(len(g)-2,np.floor(k))); f=k-i
                out[y,x,:3]=g[i]*(1-f)+g[i+1]*f
            else:
                k=t[y,x]*(len(ramp)-1); i=int(min(len(ramp)-2,np.floor(k))); f=k-i
                c=ramp[i]*(1-f)+ramp[i+1]*f
                out[y,x,:3]=c
    return Image.fromarray(out.astype('uint8'))

CW,CH,COLORS=38,60,40
OUTLINE=(8,4,14,255)
def outline(img):
    # a 2 px black outline: the piece's own edge ring turns black, and one more ring goes on the outside (the canvas has room for it)
    a=np.array(img.convert('RGBA')); al=a[...,3]>=128
    a[~al]=(0,0,0,0)
    pad=np.pad(al,2)
    def n4(m): return m[:-2,1:-1]&m[2:,1:-1]&m[1:-1,:-2]&m[1:-1,2:]
    def any4(m): return m[:-2,1:-1]|m[2:,1:-1]|m[1:-1,:-2]|m[1:-1,2:]
    edge=al&~n4(np.pad(al,1))
    grow=~al&any4(np.pad(al,1))
    a[edge]=OUTLINE; a[grow]=OUTLINE
    return Image.fromarray(a)
CHARS="0123456789abcdefghijklmnopqrstuvwxyzABCDEFGH"
TARGET={'king':52,'queen':49,'bishop':46,'knight':45,'rook':41,'pawn':36}   # +2 px of outline each = the earlier sizes
os.makedirs(PNG_DIR,exist_ok=True)
cs=["// <auto-generated> by chess-fighter/tools/make-pieces.py from art/pieces-sheet-3.png. Re-run the script instead of editing. </auto-generated>",
 "namespace ChessFighter.Web.Game;","","public static partial class Sprites","{",
 f"    public const int ArtW = {CW}, ArtH = {CH};","",
 "    static void LoadArt(System.Collections.Generic.Dictionary<(ChessFighter.Core.PieceType, ChessFighter.Core.Side), ArtSprite> d)","    {"]
for side in ('black','white'):
    for n in names:
        p=recolor(pieces[n],side); h=TARGET[n]; w=round(p.width*h/p.height*(1.18 if n=='knight' else 1.38))
        small=p.resize((w,h),Image.NEAREST)
        small=Image.fromarray(np.pad(np.array(small.convert('RGBA')),((1,1),(1,1),(0,0)))); small=outline(small); w,h=small.size
        alpha=np.array(small.getchannel('A'))>=128
        q=small.convert('RGB').quantize(COLORS,dither=Image.Dither.NONE)
        pal=q.getpalette(); idx=np.array(q)
        used=sorted({int(v) for v in idx[alpha]}); char_of={u:CHARS[i] for i,u in enumerate(used)}
        ox,oy=(CW-w)//2,CH-1-h; assert ox>=0 and oy>=0,(side,n,w,h)
        canvas=[["." for _ in range(CW)] for _ in range(CH)]
        png=Image.new('RGBA',(CW,CH),(0,0,0,0))
        for y in range(h):
            for x in range(w):
                if alpha[y,x]:
                    u=int(idx[y,x]); canvas[oy+y][ox+x]=char_of[u]
                    png.putpixel((ox+x,oy+y),(pal[u*3],pal[u*3+1],pal[u*3+2],255))
        png.save(os.path.join(PNG_DIR,f"{side}_{n}.png"),optimize=True)
        legend=";".join(f"{char_of[u]}=#{pal[u*3]:02x}{pal[u*3+1]:02x}{pal[u*3+2]:02x}" for u in used)
        cs.append(f"        d[(ChessFighter.Core.PieceType.{n.capitalize()}, ChessFighter.Core.Side.{side.capitalize()})] = new ArtSprite(new[]")
        cs.append("        {")
        for row in canvas: cs.append('            "'+"".join(row)+'",')
        cs.append(f'        }}, "{legend}");')
cs+=["    }","}",""]
open(OUT,"w").write("\n".join(cs)); print('wrote',os.path.normpath(OUT))
