# Chess Fighter

A mobile/web chess game with a **16-bit arcade fighter** look. Board squares hold powerups, and each player gets buffs from
randomly drawn cards. It is a sibling of [Power Chess](../README.md) in this repository: same proven chess engine and app
shell, new theme, and a reworked set of powers.

- Design notes: [docs/DESIGN.md](docs/DESIGN.md)
- **Powers and cards spec, with build status:** [docs/POWERS.md](docs/POWERS.md)

## Status

Early build. The engine, AI, tests, PWA and web shell come from Power Chess; the visual identity is new (32x32 sprites,
arcade HUD, scanlines). **Built so far:** all five banked moves (Fireball, Leap, Bomb, Poison, Ghost), the instant squares (Combo +2/+4, Stun, Steal, Armor) the Draw Card square with a visible card bag and all fifteen cards (see POWERS.md), and the one-banked-move-per-piece rule
(see [docs/POWERS.md](docs/POWERS.md)). Next: balance, tutorials for the new powers, sound, and the old Power Chess menus.

## Layout

| Path | What |
|---|---|
| `src/ChessFighter.Core` | Rules engine: pure C#, no UI references. Namespace `ChessFighter.Core`. |
| `tests/ChessFighter.Core.Tests` | NUnit tests (perft, powerups, simulations, AI). |
| `web/ChessFighter.Web` | Blazor WebAssembly app. |
| `web/ChessFighter.Web/wwwroot/css/app.css` | Layout and components inherited from Power Chess. |
| `web/ChessFighter.Web/wwwroot/css/fighter.css` | **The 16-bit theme.** Loaded after `app.css` and overrides it. |
| `web/ChessFighter.Web/Game/Sprites.cs`, `Sprites.Data.cs` | 32x32 piece sprites as text grids (`Sprites.Data.cs` is generated). |
| `tools/PuzzleGen` | Regenerates the verified daily puzzles into `PuzzleData.cs` after a rules change. |
| `tools/make-sprites.py` | Builds the sprites from shape definitions: lighting, 5-step color ramps, dithering, gold trim, outlines. |

## Run

```bash
dotnet test chess-fighter/tests/ChessFighter.Core.Tests
dotnet run --project chess-fighter/web/ChessFighter.Web
```

The debug URL options and the tap-the-logo-five-times debug panel work as described in the Power Chess README.

## The look

- Pixel font: Press Start 2P (SIL OFL, bundled in `wwwroot/fonts`, so the PWA works offline). Body text uses a monospace stack.
- Pieces: steel-armour P1 vs crimson-demon P2, 32x32 sprites with shaded color ramps, dithering and gold trim. Change a shape or palette in `tools/make-sprites.py` and re-run it (`python3 chess-fighter/tools/make-sprites.py`), or hand-edit `Sprites.Data.cs`. Palettes are CSS variables in `fighter.css`.
- HUD (Street Fighter II style): P1 and P2/CPU health bars across the top with a K.O. badge and move counter between them. A bar drains with the material a side has lost (yellow over red, the rival's drains toward the centre) and empties on checkmate.
- Round flow: a ROUND n / FIGHT! banner on every new game, "K.O.!" on checkmate, YOU WIN / YOU LOSE with a short quote, and PERFECT! when the winner lost no material.
- Stage: sunset sky behind the board.
- Effects: hard offset shadows instead of glows, stepped animation, scanline + vignette overlay (`body::after`).

## Debug tools and URL options

Tap the logo five times (or open `?debug=1`) for the debug panel: hand cards to you or the rival, give a piece any banked power at tier I or II
(or strip or stun it), drop any powerup square, spawn a pair, clear every power, **scramble the board** into a random mid-game position (Early, Middle or Late; it plays random legal actions from a fresh game, so the position is always legal), and the old progress buttons. Handy URL options for setting up a
position: `fen=`, `seed=`, `ai=`, `buffs=e4:fireball,e4:fireball` (listing a power twice makes it tier II), `reveal=a3:combo2`,
`cards=w:takeback,b:nuhuh`, `pending=placeboulder` (an immediate card to aim first), `perks=`, `mystery=`, `spawn=`, `pairs=`, `tut=`, `drama=`, and `scramble=40` to start from a scrambled position.

## Replay clip

`web/ChessFighter.Web/wwwroot/js/clip.js` plays the last few moves as a 9:16 highlight in the game's 16-bit style (sunset stage, hazard-stripe frame,
fight bars with K.O., FIGHT! intro, YOU WIN end card) and records it for sharing. Pieces are drawn from the same 32x32 sprite grids the game uses
(`ClipBuilder` passes them in), so a sprite change shows up in the replay automatically. It animates every power and card event the engine reports.

## Known gaps

- Sound is still the Power Chess synth (`wwwroot/js/fx.js` and the replay's own tones): no arcade music or voice yet.
- App icons in `wwwroot/icons` are still the Power Chess icons.
- No deploy workflow yet: GitHub Pages for this repo already serves Power Chess at `/power-chess/`.
