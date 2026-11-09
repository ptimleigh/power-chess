#!/usr/bin/env python3
"""
Generates the 32x32 16-bit style chess sprites for Chess Fighter.

Each piece is described as a stack of simple shapes (ellipses, rounded rects, polygons). Every shape is lit
from the upper left as a slightly puffy surface, quantised to a small colour ramp with light ordered dithering,
given a dark inner outline where another shape sits on top of it, and a black outer outline. The result is
written as text grids to web/ChessFighter.Web/Game/Sprites.Data.cs, so the sprites can still be touched up by
hand there (the legend is in Sprites.cs), but re-running this script rebuilds them from the shape definitions.

Usage:  python3 chess-fighter/tools/make-sprites.py            (writes Sprites.Data.cs)
        python3 chess-fighter/tools/make-sprites.py --json X   (also writes the grids as JSON, for previews)
"""
import json
import math
import os
import sys

N = 32
BODY = "12345"   # dark -> light, 5 steps for the main material (armour / demon hide)
TRIM = "abcd"    # dark -> light, 4 steps for the gold trim
LIGHT = (-0.55, -0.65, 0.52)
_l = math.sqrt(sum(c * c for c in LIGHT))
LIGHT = tuple(c / _l for c in LIGHT)
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


class Shape:
    def __init__(self, kind, p, mat="body", spec=False, bevel=4.5, flat=None):
        self.kind, self.p, self.mat, self.spec, self.bevel, self.flat = kind, p, mat, spec, bevel, flat

    def d(self, x, y):
        """Signed depth: positive inside, distance to the edge."""
        k, p = self.kind, self.p
        if k == "ell":
            cx, cy, rx, ry = p
            r = math.hypot((x - cx) / rx, (y - cy) / ry)
            return (1 - r) * min(rx, ry)
        if k == "rect":
            x0, y0, x1, y1, r = p
            cx, cy, hw, hh = (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2, (y1 - y0) / 2
            qx, qy = abs(x - cx) - (hw - r), abs(y - cy) - (hh - r)
            sdf = math.hypot(max(qx, 0), max(qy, 0)) + min(max(qx, qy), 0) - r
            return -sdf
        if k == "poly":
            inside = False
            best = 1e9
            n = len(p)
            for i in range(n):
                ax, ay = p[i]
                bx, by = p[(i + 1) % n]
                if (ay > y) != (by > y) and x < (bx - ax) * (y - ay) / (by - ay) + ax:
                    inside = not inside
                dx, dy = bx - ax, by - ay
                t = max(0, min(1, ((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy)))
                best = min(best, math.hypot(x - (ax + t * dx), y - (ay + t * dy)))
            return best if inside else -best
        raise ValueError(k)

    def h(self, x, y):
        d = self.d(x, y)
        if d <= 0:
            return 0.0
        return math.sqrt(min(d, self.bevel) / self.bevel)


def E(cx, cy, rx, ry, **kw): return Shape("ell", (cx, cy, rx, ry), **kw)
def R(x0, y0, x1, y1, r=1.5, **kw): return Shape("rect", (x0, y0, x1, y1, r), **kw)
def P(*pts, **kw): return Shape("poly", pts, **kw)


def base(x0=6, x1=26):
    return [
        R(x0, 25.2, x1, 30.0, 2.0, spec=True, bevel=3.0),
    ]


def pawn():
    return base(7, 25) + [
        P((10.5, 26), (13, 15), (19, 15), (21.5, 26), spec=True, bevel=5),
        R(9.5, 13.2, 22.5, 16.8, 1.7, mat="trim", bevel=2.5),
        E(16, 9, 5.4, 5.4, spec=True, bevel=5),
    ]


def rook():
    return base(5, 27) + [
        P((9, 26), (10.5, 13), (21.5, 13), (23, 26), spec=True, bevel=5),
        R(7.5, 10.5, 24.5, 14.5, 1.4, bevel=2.5),
        R(7.5, 4.0, 12.0, 11.0, 0.8, bevel=2.2),
        R(13.8, 4.0, 18.2, 11.0, 0.8, bevel=2.2),
        R(20.0, 4.0, 24.5, 11.0, 0.8, bevel=2.2),
        R(9.0, 19.5, 23.0, 22.0, 1.0, mat="trim", bevel=2.0),
        R(15.0, 15.0, 17.0, 18.8, 0.6, flat="1"),
    ]


def bishop():
    return base(7, 25) + [
        R(9.5, 21.8, 22.5, 25.8, 1.5, mat="trim", bevel=2.2),
        P((16.0, 3.4), (20.0, 7.5), (22.4, 13.5), (22.0, 18.5), (19.5, 22.5), (12.5, 22.5), (10.0, 18.5), (9.6, 13.5), (12.0, 7.5),
          spec=True, bevel=6.0),
        E(16, 3.6, 2.2, 2.2, mat="trim", bevel=2.2),
        P((17.9, 8.6), (19.8, 10.0), (15.2, 18.2), (13.2, 16.6), flat="1"),
    ]


def queen():
    pts = [(7.2, 11.2), (12.0, 7.0), (16.0, 5.0), (20.0, 7.0), (24.8, 11.2)]
    return base(6, 26) + [
        P((9.5, 26), (8.0, 14.0), (12.5, 17.5), (16.0, 12.0), (19.5, 17.5), (24.0, 14.0), (22.5, 26), spec=True, bevel=5),
        R(13.8, 7.5, 18.2, 13.5, 1.2, bevel=2.0),
        R(9.0, 21.0, 23.0, 23.8, 1.2, mat="trim", bevel=2.0),
    ] + [E(x, y, 2.5, 2.5, mat="trim", bevel=2.5) for (x, y) in pts] + [
        E(16, 17.6, 1.7, 1.7, flat="e"),
    ]


def king():
    return base(6, 26) + [
        P((9.5, 26), (10.2, 16.0), (12.8, 13.0), (19.2, 13.0), (21.8, 16.0), (22.5, 26), spec=True, bevel=5.5),
        R(10.4, 11.4, 21.6, 14.6, 1.4, mat="trim", bevel=2.2),
        R(14.6, 1.6, 17.4, 11.6, 0.8, mat="trim", bevel=1.6),
        R(11.6, 3.8, 20.4, 6.6, 0.8, mat="trim", bevel=1.6),
        R(9.5, 21.0, 22.5, 23.8, 1.2, mat="trim", bevel=2.0),
        E(16, 17.8, 1.8, 1.8, flat="e"),
    ]


def knight():
    head = P((9.5, 26), (10.5, 20.0), (13.0, 16.5), (9.0, 15.8), (5.6, 18.0), (3.0, 16.8), (3.2, 13.6),
             (6.8, 8.8), (8.6, 3.8), (11.4, 6.4), (14.2, 4.0), (17.5, 5.8), (21.8, 10.0), (24.2, 16.0), (24.5, 26),
             spec=True, bevel=6.0)
    mane = P((17.5, 6.8), (21.8, 10.8), (24.0, 16.5), (24.4, 22.5), (21.0, 22.5), (20.6, 16.8), (18.2, 11.2),
             mat="trim", bevel=2.0)
    return base(7, 25) + [
        head, mane,
        E(8.2, 11.4, 1.3, 1.3, flat="e"),
        E(4.4, 15.4, 0.8, 0.8, flat="1"),
    ]


PIECES = [("Pawn", pawn), ("Rook", rook), ("Knight", knight), ("Bishop", bishop), ("Queen", queen), ("King", king)]


def shade(shape, x, y, levels):
    e = 0.7
    hx = (shape.h(x + e, y) - shape.h(x - e, y)) / (2 * e)
    hy = (shape.h(x, y + e) - shape.h(x, y - e)) / (2 * e)
    k = 3.2
    nx, ny, nz = -hx * k, -hy * k, 1.0
    ln = math.sqrt(nx * nx + ny * ny + nz * nz)
    v = max(0.0, (nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]) / ln)
    t = (v - 0.05) / 0.9 * (levels - 0.001)
    t += (BAYER[int(y) % 4][int(x) % 4] / 16.0 - 0.5) * 0.55
    return max(0, min(levels - 1, int(math.floor(t)))), v


def render(shapes):
    owner = [[-1] * N for _ in range(N)]
    for y in range(N):
        for x in range(N):
            for i, s in enumerate(shapes):
                if s.d(x + 0.5, y + 0.5) > 0:
                    owner[y][x] = i
    grid = [["."] * N for _ in range(N)]
    best = {}
    for y in range(N):
        for x in range(N):
            i = owner[y][x]
            if i < 0:
                continue
            s = shapes[i]
            if s.flat:
                grid[y][x] = s.flat
                continue
            ramp = TRIM if s.mat == "trim" else BODY
            idx, v = shade(s, x + 0.5, y + 0.5, len(ramp))
            grid[y][x] = ramp[idx]
            if s.spec and (i not in best or v > best[i][0]):
                best[i] = (v, x, y)
    # inner outline: a shape's edge pixels that touch a shape drawn on top of it become that shape's darkest tone
    for y in range(N):
        for x in range(N):
            i = owner[y][x]
            if i < 0 or shapes[i].flat:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < N and 0 <= ny < N and owner[ny][nx] > i and not shapes[owner[ny][nx]].flat:
                    grid[y][x] = (TRIM if shapes[i].mat == "trim" else BODY)[0]
                    break
    # specular highlight: a short bright dash on the lit side of the big shapes
    for i, (v, x, y) in best.items():
        for k in (0, 1):
            if x + k < N and owner[y][x + k] == i and not shapes[i].flat:
                grid[y][x + k] = "h"
    # black outer outline
    out = [row[:] for row in grid]
    for y in range(N):
        for x in range(N):
            if grid[y][x] != ".":
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < N and 0 <= ny < N and grid[ny][nx] != ".":
                    out[y][x] = "k"
                    break
    return ["".join(r) for r in out]


def main():
    data = {name: render(fn()) for name, fn in PIECES}
    here = os.path.dirname(os.path.abspath(__file__))
    target = os.path.join(here, "..", "web", "ChessFighter.Web", "Game", "Sprites.Data.cs")
    lines = [
        "// <auto-generated> by chess-fighter/tools/make-sprites.py. Hand edits survive until the script is re-run. </auto-generated>",
        "namespace ChessFighter.Web.Game;",
        "",
        "public static partial class Sprites",
        "{",
    ]
    for name, rows in data.items():
        lines.append(f"    static readonly string[] {name} =")
        lines.append("    {")
        for r in rows:
            lines.append(f'        "{r}",')
        lines.append("    };")
        lines.append("")
    lines.append("}")
    with open(target, "w", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    if "--json" in sys.argv:
        with open(sys.argv[sys.argv.index("--json") + 1], "w") as f:
            json.dump(data, f)
    print("wrote", os.path.normpath(target))


if __name__ == "__main__":
    main()
