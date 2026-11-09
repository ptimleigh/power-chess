# Chess Fighter: design notes

## Pitch

Chess as a fighting game. Pieces are fighters, captures are hits, and the board is a stage with pickups. Quick, readable
rounds with arcade presentation.

## Theme

- 16-bit arcade fighter: pixel sprites, chunky UI, a CRT feel, announcer-style callouts ("FIGHT!", "K.O.", "CHECKMATE").
- Two sides: P1 (blue/steel) and P2/CPU (crimson). Fighting-game HUD with a health bar per side.
- Done so far: sprites, HUD, board, sheets, buttons, boot screen. Not done: VS splash and round banner, announcer
  callouts restyle, replay clip, icons, sound.

## Two power systems (to be designed)

1. **Square powerups.** Squares on the board hold a powerup that the piece landing on it claims. The engine already
   supports this (revealed/mystery squares, buffs on pieces, perks in an inventory, symmetric spawning). The *set* of
   powers is currently the inherited Power Chess set and is to be replaced. Rework this list in `Types.cs`
   (`PowerupKind`, `Buff`), `Rules.cs`, `Presets.cs`, `Unlocks.cs`, `Web/Game/Names.cs`, `Web/Game/Icons.cs`.
2. **Card buffs.** Each player gets buffs from randomly selected cards. **Not built yet.** Open questions:
   - When are cards drawn? (start of game, every N turns, on capture, a draft of 1 of 3?)
   - Do cards last the whole game, a few turns, or one use? Are they player-wide or attached to a piece?
   - Are they public or hidden? Are they shown to the opponent?
   - How many can a player hold?
   - How does the AI value and use them? (`Ai.cs` and `Hints.cs` need to learn any new rule.)

## Engine notes for whoever builds the new powers

- State changes go through `Rules.cs`; the view layer reads `GameEvent`s (`Types.cs`) to animate, so each new power
  needs an event kind and a toast/FX hook.
- The AI search walks `MoveGen.cs`; anything that adds actions must be generated there, or the AI cannot use it.
- `GameStateCodec.cs` serialises saves and history; new state must be encoded or a refresh loses it.
- Tests: perft pins plain-chess correctness and `SimulationTests` plays random games, so a bad rule tends to show up
  as an illegal state there. Add a focused test per power in the style of `PowerupTests.cs`.
