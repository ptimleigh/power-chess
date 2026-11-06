# Piece Lathe

Browser tool for designing 3D chess pieces. No build step: serve this folder and open `index.html`
(for example `python3 -m http.server -d tools/PieceLathe 8000`). Three.js is vendored in `vendor/`.

## Model

A piece is plain JSON (see `pieces/`): `{ name, parts: [...] }`. Each part is one of:

| type | fields |
|---|---|
| `lathe` | `profile: [[radius, height, corner?], ...]` swept around the vertical axis. A third value of `1` marks a sharp corner; runs between corners are smoothed (`smooth` = samples per segment, 0 = straight). Also `segments`. |
| `extrude` | `outline: [[x, y], ...]` side silhouette, extruded by `depth` with `bevel`. Use it for non-symmetric shapes such as the knight head. |

Both take `id`, `color`, `offset [x,y,z]`, `scale`, `rotationY` (degrees). A variant such as a pawn with a hat is the
same file plus extra parts (`pieces/pawn_tophat.json`).

## UI

Pick a part, then click the profile canvas to add points, drag to move, double-click to toggle sharp/smooth
(lathe), right-click to delete. Save/load `.json`, export `.glb` (Unity, Godot, Blender, Three.js), screenshot.

## Scripting API (`window.pieceLathe`)

`getState`, `setState`, `loadURL`, `addPart`, `updatePart`, `removePart`, `selectPart`, `setProfile`, `movePoint`,
`undo`, `setView`, `screenshot` (PNG data URL), `exportGLB`, `lineup([states])` (compare pieces side by side).
The UI uses the same calls, so anything done by hand can be done by a script or by Claude.

## Headless render

```
node render.js pieces/pawn.json pieces/pawn_tophat.json --out=compare.png --view=iso
node render.js pieces/knight.json --out=k.png --glb=knight.glb
```

Needs Playwright. Several input files render as a lineup.
