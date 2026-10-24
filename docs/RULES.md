# Power Chess: rules as implemented

Source of truth is `Assets/Core` (pure C#, no Unity references). Tests live in `tests/PowerChess.Core.Tests`.

Run the tests: `dotnet test tests/PowerChess.Core.Tests`

## Base game
Standard chess including castling, en passant and promotion, checked against published perft counts. Checkmate and
stalemate are real. Stalemate and the 50-move rule are draws; a ply cap ends endless games as a draw.

## Mystery squares
- Mirrored pairs spawn on ranks 3-6 every `SpawnInterval` plies (2 pairs at the start, capped by `MaxMysterySquares`).
- Contents are drawn only when a piece **lands** on the square (passing through does nothing).
- If the mover has a Reroll, the turn pauses on the drawn powerup: **Keep** it, or spend the Reroll to draw again (the new
  draw is final). With no Reroll it applies immediately.
- Kings only ever draw perks, never buffs.

## Piece buffs (lost if the piece is destroyed)
| Buff | Rule |
|---|---|
| Double Move | First time the piece moves it must move twice in a row. The second move is forfeited if the first gives check or no legal second move exists. One-shot. |
| Poison | If a piece captures it by a normal move, the capturer is destroyed too. Kings and armored pieces are immune. Fireball/bomb/steal do not trigger it. |
| Spiked Armor | Immune to every hostile effect for the next 3 opponent turns. |
| Small / Big Bomb | Spend your turn to detonate: destroys every non-king, non-armored piece in the 3x3 / 5x5 area, **including your own**. Also clears boulders. The bomber survives. |
| Transform (square) | Piece becomes a Pawn/Knight/Bishop/Rook/Queen. Never a king; never a pawn on a back rank. |

## Player perks (inventory, max 3 of each; using one costs your turn except Reroll-on-pickup)
| Perk | Rule |
|---|---|
| Fireball | Pick one of your pieces and one of 8 directions (horizontal, vertical or diagonal, unlimited range); any piece type can fire any direction. It destroys the **first** piece on the line if that is a vulnerable enemy (your own piece, a king or an armored piece first means that direction does nothing). A **Queen's** fireball destroys **every** vulnerable enemy on the whole line, flying over its own pieces, kings and armored pieces. Boulders stop any fireball. A shot is only available if it would hit something. |
| Stun | Pick any enemy piece anywhere on the board (not a king, not armored, not already stunned). It cannot move for 3 of its owner's turns. |
| Steal | A piece of yours converts an enemy piece it could capture. The thief does not move. |
| Boulder | Place on an empty square; nothing can move onto or through it. Cleared by spending a Reroll or by a bomb. |
| Reroll | Everyone starts with 1. Re-draws a just-found powerup, or clears a boulder. |

## Cross-cutting rules
- **Kings are immune** to every buff and effect (fireball, bombs, steal, stun, poison, transform).
- **Armored pieces are immune** to capture, fireball, steal, stun and bombs.
- **Stunned pieces exert no attacks**, so they cannot give check or pin. A stun can therefore set up checkmate.
- An action that leaves your own king attacked is illegal, including perks and bombs. Checkmate means no action at all
  (moves, perks, boulders, bombs) escapes the check.
- Perk uses (Fireball, Steal, Stun, Boulder, smashing a boulder) and bomb detonations are alternatives to moving.

## Engine notes
- `MoveGen.LegalActions(state)` returns every legal action including pickup decisions.
- `Rules.Apply(state, action, events)` mutates the state and emits `GameEvent`s for the view layer to animate.
- `Ai` searches in `SearchMode`, so it never sees hidden draws.
