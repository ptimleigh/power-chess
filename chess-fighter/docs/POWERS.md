# Chess Fighter: powers and cards

The design source of truth. Status: **Built** = in the engine, tested and playable. **Planned** = decided, not built.

## Global rules

- Powerup squares come in three families: **banked moves**, **instant moves** and **cards**. Teleporters are an environmental
  effect (kept, not a power).
- A banked move lives on a **piece** until used. A piece holds **one banked category at a time**. Landing on a different
  category replaces it (the old one is lost) and the new one starts at tier I. Landing on the same category upgrades it to
  tier II. At the top tier the square is used up and nothing changes. Powers with no tier II (Poison, Ghost) stay at I and the
  square is used up.
- Using a banked move **costs the turn** and the piece stays where it is.
- A banked move counts as a **threat for check**, like the piece it imitates. Kings are never removed by an effect; if the
  logic leaves a king with no way out, that is checkmate.
- Names are plain: Fireball I, Fireball II. No account-level unlocks for now (every power is available from game one).
- Cards: playing a card **costs the turn**. Cards come from **Draw Card squares** (a power-up square). Players' cards are
  **visible to both**, no hand limit. When any card is activated, the rival is prompted to play **Nuh-uh** if they hold one.

## Banked moves

| Power | Tier I | Tier II | King rule | Status |
|---|---|---|---|---|
| **Fireball** | Fires along ranks and files like a rook at the first piece in line (enemy only). One use. | Also fires diagonally, like a queen. | Counts as a rook (I) or queen (II) for check and mate. A king can hold one. | **Built** |
| **Poison** | If the piece is captured, the capturer is also lost. Lasts 5 of the owner's moves. | None. | A king cannot capture a poisoned piece. | **Built** |
| **Leap** | A checkers jump: the piece jumps diagonally over an adjacent rival into the empty square directly behind it and captures it. Indicator: a checker lying flat on the square (red for P1, black for P2) with one groove ring per tier and a gold rim at III. | Tier II chains a second jump in the same turn; tier III keeps jumping as long as a jump is available. After each jump you may stop. | Kings can hold and use it, but never be jumped. Armored pieces and kings cannot be jumped; the landing square must be empty (no boulder); landing on a powerup square or teleporter triggers it and ends the chain; a pawn never leaps onto the last rank. | **Built** |
| **Bomb** | Detonate: destroys all pieces in the 3x3 around the piece, itself included. Chain-detonates other bomb pieces in the blast. | 5x5. | A king in the blast zone counts as in check (chains too; spell it out to the player). A king landing on a Bomb square gets nothing and the square is used up. A detonation that would destroy your own king is illegal. | **Built** |
| **Ghost** | 3 moves as a ghost. The counter does not go down if the piece does not move. | None. | A ghost can threaten check and mate. | **Built** |

## Instant moves

| Power | Effect | Status |
|---|---|---|
| **Combo +2** | The piece that lands moves 2 more times in a row: 3 moves in all. Giving check ends the combo. Landing on another Combo adds its moves. | **Built** (same piece only) |
| **Combo +4** | Same with 4 more moves: 5 in all. | **Built** |
| **Stun** | Immediately shoot a stun beam (lightning bolt) in any direction. The piece cannot move for 2 turns (was 1; `Rules.StunBoltTurns`). A king cannot be stunned. | **Built** |
| **Steal** | Steal any rival piece in the 3x3 around the power-up square. Not kings. | **Built** |
| **Draw Card** | Draw a card, to use instantly or at will depending on the card. | **Built** |
| **Armor** | Use immediately. The piece cannot be captured for 3 turns, even if it never moves. Not spiked. | Built (as Spiked Armor, renamed in the UI) |

## Cards

| Card | Effect | Timing |
|---|---|---|
| No more powers | Removes all active buffs and resets the squares. New squares appear after 1 round of moves. | at will |
| Role reversal | Switch any 2 of your pieces. | at will |
| Horse Sacrifice | Sacrifice 1 knight to promote a pawn, bishop or knight to a rook. If you have no knight, hold the card. Kings cannot be turned into rooks. | at will |
| World turned upside down | The board flips 180 degrees with all pieces in place; you now use the other pieces. Unplayable only if it would put yourself in check. | immediately |
| You get a promotion! | Promote 1 piece by 1 level: Pawn to Knight or Bishop (choice), Knight or Bishop to Rook, Rook to Queen. | immediately |
| Hit restart | All remaining pieces return to their starting squares and keep their powers. Captured pieces stay captured. A promoted piece returns to the pawn's start square. Unplayable only if it would put yourself in check. | immediately |
| Place a boulder | Put a boulder on the board. Blocks moves. Destroyed only by bombs or Smash boulder. | immediately |
| Smash boulder | Remove 1 boulder. Draw weight is the same as Place a boulder, but zero while the board has no boulder, so nobody hoards dead cards. | at will |
| Missile Strike | Pick a quadrant. Each player has 5 turns (10 moves in all) to evacuate the highlighted zone, counting down. If the king cannot get out, the missile shows incoming and freezes before the strike to show the checkmate animation. | at will |
| Summon Mars | Tells the player the best possible move for 3 turns, starting now. Does not cost the turn. | at will |
| Summon Caïssa | Bring back 1 captured piece and place it anywhere on the board. | at will |
| Summon Shiva | Strikes 3 random rival pieces with falling weapons, capturing them (never kings). | at will |
| Dragon Strike | A dragon wipes out 1 row with dragonfire. Rows 3 to 6 only, and not a row occupied by a king. | at will |
| Nuh-uh | Cancel the other player's card. Prompted whenever a card is activated. | reaction |
| Take back | Undo 1 round of moves. | at will |

**All fifteen cards are built:** Place a boulder, Smash boulder, You get a promotion!, Role reversal, Horse Sacrifice, No more powers, Summon Caïssa,
Nuh-uh, Take back, Summon Mars, Dragon Strike, Summon Shiva, Hit restart, World turned upside down and Missile Strike.

## Assumptions made while building (change any of these)

- **Poison lasts the pickup turn plus 5 more owner turns**, so the opponent gets 5 replies while it is active.
- **Leap is one use**, like Fireball, and uses the turn (the whole chain is one turn). Each jump must leave your own king safe.
- **A leap pulls a rival as far toward the shooter as it can, but never lands it on a powerup square or teleporter** (nothing is triggered); it stops short of one. If it cannot move at all it is stunned where it stands. A hook that would leave your own king in check is not offered, and the interface outlines such a rival and says why when tapped.
- **Leap is not a check threat** (only Fireball, Ghost and Bomb are).
- **Armored pieces cannot be hooked.** A king can.
- **Bomb:** the bomber is destroyed by its own blast, except an armored bomber, which survives with the bomb spent. A blast never removes
  a king or an armored piece, and clears boulders. A detonation needs at least one other target, and a stunned bomber cannot detonate or
  threaten. Taking a Bomb square that would put your own king inside an enemy blast chain is refused and the square is used up.
  The chain follows bombs of either colour caught in the blast. The status line spells out a bomb check.
- **Stun and Steal are aimed right after landing** (the lander picks the target, and nothing else can be done until then). With no
  legal target the power fizzles and the square is used up. A choice that would leave your own king in check is not offered.
  Stun hits the first enemy on any of 8 lines for 2 turns (`Rules.StunBoltTurns`) (a king and armored pieces are immune). Steal takes a non-king,
  non-armored rival in the 3x3 around the lander; the stolen piece stays put and loses its buffs.
- **Cards:** an immediate card (boulder, promotion) is aimed right after it is drawn; if it has no legal use it goes into the bag instead.
  Bag cards are played from the bag on your turn and cost it. A card that could never be used is never drawn (Smash needs a boulder on the
  board, Caissa needs something you lost). The AI keeps cards out of its search: it plays one only when a one-ply look shows a clear gain,
  and it always answers with Nuh-uh when it holds one.
- **Nuh-uh and check:** if you play a card to get out of check and it is cancelled, you are still in check at the end of your turn, which
  is **checkmate**. Change this if you would rather the cancelled card just fail.
- **Take back** returns the board to the start of your previous turn (so your move and the rival's reply are both undone, and pieces lost in
  that round come back). Your bag, the rival's bag and the random sequence are **not** undone. It needs at least three turns of history.
  It does **not** cost the turn: after rewinding, the player who played it moves again. (If the rival cancels it with Nuh-uh, the turn is spent as usual.)
- **Summon Mars** shows the best move automatically this turn and your next 2 (free, no XP penalty). Playing it does **not** cost your turn (the other summons do). It does not stack.
- **Dragon Strike** burns every piece on rows 3 to 6 that has no king on it, both colors, except armored pieces. Boulders and powerup squares stay.
- **Summon Shiva** strikes up to 3 random rival pieces (never kings, never armored), skipping any whose removal would leave your own king in check.
- **Hit restart** sends each piece to the square it started the game on (a promoted pawn returns to the pawn's square; a stolen piece keeps its
  origin; a piece with no known origin, such as one Caissa brought back, stays put). Boulders, powerup squares and teleporters on a landing
  square are cleared. Unplayable only if it would leave your own king in check.
- **World turned upside down** rotates the board 180 degrees and swaps every piece's color, so you keep your seat and command the army that was
  facing you. Castling rights are gone, and each side's lost pieces go with the army. Unplayable only if it would leave your own king in check.
- **Missile Strike** picks one of the four 4x4 quadrants. It lands after 10 moves (5 turns each). Everything in the quadrant is destroyed except
  kings and armored pieces; a king still there **loses the game**, both kings there is a draw. Only one missile at a time. The AI treats the zone
  as danger and heads for the nearest edge.
- **Role reversal, Horse Sacrifice and Promotion never touch kings;** promotion never touches queens.
- **Caïssa** places the piece on any free square (no powerup square, boulder or teleporter, and no pawn on a back rank). The sacrificed
  knight of Horse Sacrifice counts as lost, so Caissa can bring it back.
- **No more powers** also removes armor. Powerup squares stay away for one round. Teleporters stay.
- **Ghost's three moves exclude the pickup move itself**, and a stunned ghost neither moves nor threatens.
- The generated daily puzzles were regenerated under these rules (`tools/PuzzleGen`).

## Not carried over from Power Chess

Transform squares, Reroll, Rewind (now the Take back card), Swap (now Role reversal), the Boulder perk (now a card), Double Move
and Triple Move (replaced by Combo), and the account-level unlock tiers (for now).

## Open items

- Combo assumes **the same piece** makes all the extra moves. Say so if any piece should be allowed.
- The Power Chess level-up cards, unlock previews and rule presets still exist in the app and refer to the old powers.
- Tutorials and the demo boards for the new powers are not written yet.

## Level plan (20 levels)

Level 1 is the starter kit; every later level opens one thing. A level below zero (debug links, tests) opens everything. Cards only join the
deck through Draw Card squares, so they appear once Draw Card (level 7) and their pack are both open. Both sides draw from the same open set.

| Level | Unlock |
|---|---|
| 1 | Fireball I, Armor, Poison |
| 2 | Bomb I |
| 3 | Ghost |
| 4 | Teleporters |
| 5 | Stun Bolt |
| 6 | Combo +2 |
| 7 | Draw Card squares, cards: Boulder, Smash Boulder, Promotion |
| 8 | Leap I |
| 9 | Fireball II |
| 10 | Square frequency, Calm and Chaos modes |
| 11 | Steal |
| 12 | Cards: Nuh-uh, Take Back, Horse Sacrifice, Role Reversal |
| 13 | Bomb II, Leap II |
| 16 | Leap III |
| 14 | Ghost Town mode |
| 15 | Combo +4 |
| 16 | Cards: Summon Mars, Summon Caïssa, No More Powers |
| 17 | Fireworks and Royal Court modes |
| 18 | Cards: Dragon Strike, Summon Shiva |
| 19 | Cards: Hit Restart, World Turned Upside Down, Missile Strike |
| 20 | Oops! All Bombs mode, custom rules builder |

Until a power's second tier opens, landing on the same power square again uses the square up ("maxed out") instead of upgrading.
XP: going from level n to n+1 costs 200 + 30n XP (level 2 at 230 XP, level 10 at 3150, level 20 at 9500), so a win is about one level early on.
Code: `FighterUnlocks` in `Unlocks.cs`.

## Trophies (30)

Original: First Win, Checkmate, Flawless, Pyro (burn 3 pieces with fireballs in one game), Demolition, Collector (5 power squares of any kind),
Thief, Warp Drive, Phantom, Hard Won, Regular, Daily Dose, On a Roll, Week Warrior, Devoted, Grandmaster Slayer.
Added: Nuh-uh!, Card Shark (5 cards in a game), Caïssa's Favor (bring back a queen), Upside Down, Take Two, King Me, Deep Freeze (stun a queen),
Combo Breaker (5 moves in a row), Level II (upgrade a power), Dragon's Breath (3+ pieces with one Dragon Strike), Smite (3 pieces with Summon Shiva),
Incoming! (win with a missile), Veteran (level 10), Master of Squares (level 20).
Each new trophy is worth 25 XP, but one game pays at most 50 XP in trophies.

## Late-game ramp

Rare powers (Combo +4, Steal, Leap) and the big cards (Missile Strike, Dragon Strike, Summon Shiva, World Turned Upside Down, Hit Restart) get likelier as a
game runs: 1x until ply 30 (move 15), rising to 2x at ply 100 (move 50). A card draw by the side behind in material gets up to +0.5 more, with 2x as the
ceiling. Common powers are unchanged. Code: `LateRamp.cs`; the simulator can compare settings (`BalanceSim ... ramp start full`).

800 simulated Normal games each, same seeds:

| | No ramp | Ramp ply 50 to 160 | Ramp ply 30 to 100 (used) |
|---|---|---|---|
| Draws | 6% | 5% | 4% |
| Average length (plies) | 78 | 80 | 77 |
| Side with more squares wins | 81% | 81% | 80% |
| Ended by missile | 2% | 3% | 3% |
| Pieces struck by Shiva per game | 0.25 | - | 0.42 |
