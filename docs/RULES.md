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
- Kings can draw every perk and every buff except **Poison** and **Transform**. If a king lands on a revealed Poison or Transform square it cannot use it: a toast explains and the square is used up.

### Revealed squares (the web default)
- In `Revealed` mode each pair is drawn when it spawns and shows its powerup on the square. Both squares of a pair hold
  the same powerup (fair for both sides). About 20% of pairs (`MysteryChance`) stay hidden and are drawn on landing.
- Landing on a revealed square applies the powerup shown, through the same Keep / Reroll step. If the lander cannot use
  it (a king and a buff, a capped perk, a duplicate buff, a no-op transform) nothing happens and the square stays, so a
  piece can stand on it and a king can walk over it.
- `Mystery` mode keeps every square hidden, as above.

## Teleporters
- Occasionally (`PortalChance`, 12% of spawns in the web game, never in the opening) a spawn is a pair of **teleporter** squares instead of powerup squares. They are always visible, mirrored like other pairs, and at most one pair exists at a time.
- A piece that lands on one is sent to its twin if that square is empty; then both squares vanish (a blocked exit still uses them up, with a toast). A king may not use a teleporter whose exit would leave it in check; tapping it explains why. A Double/Triple Move piece carries on from the twin.

## Piece buffs (lost if the piece is destroyed)
| Buff | Rule |
|---|---|
| Double Move | First time the piece moves it must move twice in a row. The second move is forfeited if the first gives check or no legal second move exists. One-shot. |
| Triple Move | Like Double Move, but three moves in a row. Check on any of the moves forfeits the rest. Rare. A piece never holds Double and Triple at once. |
| Ghost | For 3 of its owner's turns the piece passes through pieces and boulders (it still captures only enemies and never lands on its own side or a boulder). It never gives check through a blocker. **Sliders** slide through. A **pawn** that is blocked (any piece or a boulder straight ahead) leapfrogs it and lands one square beyond if that is free (no en passant is created); captures stay diagonal. A **knight** also takes one king-step. A **king** may also move exactly two squares in any direction, through anything, landing on an empty square or an enemy; a ghost king does not castle. |
| Fireball | The piece that picks it up can fire it **once**, instead of moving. Choose one of 8 directions (horizontal, vertical or diagonal, unlimited range); any piece type can fire any direction. It destroys the **first** piece on the line if that is a vulnerable enemy (your own piece, a king or an armored piece first means that direction does nothing). A **Queen's** fireball destroys **every** vulnerable enemy on the whole line, flying over its own pieces, kings and armored pieces. Boulders stop any fireball. A shot is only offered if it would hit something. Lost if the piece is destroyed or stolen; kings cannot earn it. |
| Poison | If a piece captures it by a normal move, the capturer is destroyed too. Kings and armored pieces are immune. Fireball/bomb/steal do not trigger it. |
| Spiked Armor | Immune to every hostile effect for the next 3 opponent turns. On a **king** it also means the king cannot be put in check: it may stand on attacked squares and cannot be checkmated. When the armor expires the king is simply in check (or mated) if it is attacked. |
| Small / Big Bomb | Spend your turn to detonate: destroys every non-king, non-armored piece in the 3x3 / 5x5 area, **including your own**. Also clears boulders. The bomber survives. |
| Transform (square) | Piece becomes a Pawn/Knight/Bishop/Rook/Queen. Never a king; never a pawn on a back rank. |

## Player perks (inventory, max 3 of each; using one costs your turn except Reroll-on-pickup)
| Perk | Rule |
|---|---|
| Stun | Pick any enemy piece anywhere on the board (not a king, not armored, not already stunned). It cannot move for 3 of its owner's turns. |
| Steal | A piece of yours converts an enemy piece it could capture. The thief does not move. |
| Boulder | Place on an empty square; nothing can move onto or through it. Cleared by spending a Reroll or by a bomb. |
| Reroll | Everyone starts with 1. Re-draws a just-found powerup, or clears a boulder. |
| Swap | Trade places between two of your own pieces (not kings; a pawn may not be swapped onto a back rank). Buffs travel with the pieces. A pawn swapped onto the far rank promotes to a Queen (never onto its own back rank). Costs your turn, must leave your king safe. The AI opponent never draws or uses it. |
| Rewind | Take back your last turn and the opponent's reply, returning to the position before you last moved. Anything gained since then (including other perks) is undone and any mystery square you opened is back, but the Rewind itself is spent. Not available on your first turn or mid-turn. The AI opponent never draws it. |

## Cross-cutting rules
- **Kings are immune** to every buff and effect (fireball, bombs, steal, stun, poison, transform).
- **Armored pieces are immune** to capture, fireball, steal, stun and bombs.
- **Stunned pieces exert no attacks**, so they cannot give check or pin. A stun can therefore set up checkmate.
- An action that leaves your own king attacked is illegal, including perks and bombs. Checkmate means no action at all
  (moves, perks, boulders, bombs) escapes the check.
- Firing a fireball, detonating a bomb, and using Steal, Stun, Boulder or smashing a boulder are alternatives to moving. Rewind is used on your turn and does not cost it.

## Engine notes
- `MoveGen.LegalActions(state)` returns every legal action including pickup decisions.
- `Rules.Apply(state, action, events)` mutates the state and emits `GameEvent`s for the view layer to animate.
- `Ai` searches in `SearchMode`, so it never sees hidden draws.

## Reviewing moves
The **Moves** button steps back through earlier positions (with the move that led to each) and forward again. It is read-only: nothing
you do while reviewing changes the game, and it is available after the game ends too.
