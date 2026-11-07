# Piece Lathe

Browser tool for designing 3D chess pieces. No build step: serve this folder and open `index.html`
(for example `python3 -m http.server -d tools/PieceLathe 8000`). Three.js is vendored in `vendor/`.

## Model

A piece is plain JSON (see `pieces/`): `{ name, parts: [...] }`. Each part is one of:

| type | fields |
|---|---|
| `lathe` | `profile: [[radius, height, corner?], ...]` swept around the vertical axis. A third value of `1` marks a sharp corner; runs between corners are smoothed (`smooth` = samples per segment, 0 = straight). Also `segments`. |
| `extrude` | `outline: [[x, y, corner?], ...]` side silhouette, extruded by `depth`. `smooth` rounds the outline (a 3rd value of `1` keeps a point sharp), `bevel` + `bevelSegments` round the edges so the shape looks inflated, `taper` slims it toward +x (e.g. a head narrowing to the nose). Use it for non-symmetric shapes such as the knight head. |
| `ribbon` | A flexible strip that trails behind the piece with spring physics: `offset` (anchor), `dir` (rest direction), `length`, `width`, `tipWidth`, `segments`, `stiffness`, `damping`, `gravity`. Used for bandana tails, scarves, capes. |

`lathe` and `extrude` take `id`, `color`, `offset [x,y,z]`, `scale`, `rotationY` (degrees). A variant such as a pawn with a hat is the
same file plus extra parts (`pieces/pawn_tophat.json`).

A top-level `idle` block (`speed`, `bounce`, `sway`, `lean`, `breathe`, `squash`) defines the looping "ready to fight"
animation. One loop is 2 hops, 1 sway and 1 breath and lasts `2 / speed` seconds; the base stays on the board.

## Phones and tablets

- Narrow screens get the 3D stage on top and the panel below, split into **Parts / Shape / Motion / File** tabs. Wide
  screens keep the side panel with everything visible.
- Touch has no right-click, double-click or Shift, so the Shape tab has **Add / move, Delete and Sharp / smooth** tools, and
  the stage has a **Move piece** toggle (drag the piece around the floor instead of orbiting the camera).
- The piece is remembered in the browser (localStorage), so a reload or a discarded tab does not lose work.
- `node build-single.js` writes `dist/piece-lathe.html` (open anywhere) and `dist/piece-lathe.artifact.html` (the page body
  for publishing as a claude.ai Artifact). Inside the Claude viewer, downloads are blocked and `.glb` is not an allowed
  save type, so saving goes through the `downloads` capability and the 3D model is saved as a `.zip` holding the `.glb`.

## Motion preview

- **Slide test** moves the piece along the floor and back so you can watch ribbons lag, swing and settle.
- **Shift + drag** in the 3D view pushes the piece around by hand.
- **Idle: ready to fight** plays the idle loop (ribbons react to it too). Sliders are saved with the piece.
- Ribbon physics runs in the tool only. In an exported `.glb` each ribbon is a skinned mesh with a bone chain
  (`<id>_b0` ... `<id>_bN`) in rest pose so Unity/Godot can drive it with their own spring-bone physics, and the idle
  becomes a looping animation clip named `idle` (tick "include idle animation"). Tuned physics numbers stay in the JSON.

## UI

Pick a part, then click the profile canvas to add points, drag to move, double-click to toggle sharp/smooth
(lathe), right-click to delete. Save/load `.json`, export `.glb` (Unity, Godot, Blender, Three.js), screenshot.

## Scripting API (`window.pieceLathe`)

`getState`, `setState`, `loadURL`, `addPart`, `updatePart`, `removePart`, `selectPart`, `setProfile`, `movePoint`,
`undo`, `setView`, `screenshot` (PNG data URL), `exportGLB({idle})`, `lineup([states])` (compare pieces side by side),
`slideTest`, `setIdle(on, cfg?)`, `setPhysics(on)`, `freeze(on)` and `advance(seconds)` (step time by hand, used for deterministic renders).
The UI uses the same calls, so anything done by hand can be done by a script or by Claude.

## Headless render

```
node render.js pieces/pawn.json pieces/pawn_tophat.json --out=compare.png --view=iso
node render.js pieces/knight.json --out=k.png --glb=knight.glb
node render.js pieces/pawn_ryu.json --view=back --slide=0.6 --out=slide.png     # mid-slide, ribbons trailing
node render.js pieces/pawn_ryu.json --view=front --idle --t=0.5 --out=idle.png  # idle pose 0.5s into the loop
```

Needs Playwright. Several input files render as a lineup.
