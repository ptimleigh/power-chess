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
| **Harpoon** | Fires a small bishop-shaped projectile on a rope along one diagonal at a rival piece and pulls it next to the shooter. If already adjacent, it stays and is stunned. | Fires on all four diagonals at once. | A king can be pulled. A king holding Harpoon cannot pull a piece that would give check to it; show an X where the option would be. | **Built** (see assumptions below) |
| **Bomb** | Detonate: destroys all pieces in the 3x3 around the piece, itself included. Chain-detonates other bomb pieces in the blast. | 5x5. | A king in the blast zone counts as in check (chains too; spell it out to the player). A king landing on a Bomb square gets nothing and the square is used up. A detonation that would destroy your own king is illegal. | **Built** |
| **Ghost** | 3 moves as a ghost. The counter does not go down if the piece does not move. | None. | A ghost can threaten check and mate. | **Built** |

## Instant moves

| Power | Effect | Status |
|---|---|---|
| **Combo +2** | The piece that lands moves 2 more times in a row: 3 moves in all. Giving check ends the combo. Landing on another Combo adds its moves. | **Built** (same piece only) |
| **Combo +4** | Same with 4 more moves: 5 in all. | **Built** |
| **Stun** | Immediately shoot a stun beam (lightning bolt) in any direction. The piece cannot move for 1 turn. A king stunned with no other way out of check is mate. | **Built** |
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
| Summon Mars | Tells the player the best possible move for 3 turns. | at will |
| Summon Caissa | Bring back 1 captured piece and place it anywhere on the board. | at will |
| Summon Shiva | Strikes 3 random rival pieces with falling weapons, capturing them (never kings). | at will |
| Dragon Strike | A dragon wipes out 1 row with dragonfire. Rows 3 to 6 only, and not a row occupied by a king. | at will |
| Nuh-uh | Cancel the other player's card. Prompted whenever a card is activated. | reaction |
| Take back | Undo 1 round of moves. | at will |

**All fifteen cards are built:** Place a boulder, Smash boulder, You get a promotion!, Role reversal, Horse Sacrifice, No more powers, Summon Caissa,
Nuh-uh, Take back, Summon Mars, Dragon Strike, Summon Shiva, Hit restart, World turned upside down and Missile Strike.

## Assumptions made while building (change any of these)

- **Poison lasts the pickup turn plus 5 more owner turns**, so the opponent gets 5 replies while it is active.
- **Harpoon is one use**, like Fireball, and uses the turn. A hooked rival that is already adjacent is stunned for **1 turn**.
- **A harpoon will not pull a piece onto a powerup square or teleporter**, so those shots are not offered.
- **Harpoon is not a check threat** (only Fireball, Ghost and Bomb are).
- **Armored pieces cannot be hooked.** A king can.
- **Bomb:** the bomber is destroyed by its own blast, except an armored bomber, which survives with the bomb spent. A blast never removes
  a king or an armored piece, and clears boulders. A detonation needs at least one other target, and a stunned bomber cannot detonate or
  threaten. Taking a Bomb square that would put your own king inside an enemy blast chain is refused and the square is used up.
  The chain follows bombs of either colour caught in the blast. The status line spells out a bomb check.
- **Stun and Steal are aimed right after landing** (the lander picks the target, and nothing else can be done until then). With no
  legal target the power fizzles and the square is used up. A choice that would leave your own king in check is not offered.
  Stun hits the first enemy on any of 8 lines for 1 turn (a king can be hit; armored pieces are immune). Steal takes a non-king,
  non-armored rival in the 3x3 around the lander; the stolen piece stays put and loses its buffs.
- **Cards:** an immediate card (boulder, promotion) is aimed right after it is drawn; if it has no legal use it goes into the bag instead.
  Bag cards are played from the bag on your turn and cost it. A card that could never be used is never drawn (Smash needs a boulder on the
  board, Caissa needs something you lost). The AI keeps cards out of its search: it plays one only when a one-ply look shows a clear gain,
  and it always answers with Nuh-uh when it holds one.
- **Nuh-uh and check:** if you play a card to get out of check and it is cancelled, you are still in check at the end of your turn, which
  is **checkmate**. Change this if you would rather the cancelled card just fail.
- **Take back** returns the board to the start of your previous turn (so your move and the rival's reply are both undone, and pieces lost in
  that round come back). Your bag, the rival's bag and the random sequence are **not** undone. It needs at least three turns of history.
- **Summon Mars** shows the best move automatically at the start of your next 3 turns (free, no XP penalty). It does not stack.
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
- **Caissa** places the piece on any free square (no powerup square, boulder or teleporter, and no pawn on a back rank). The sacrificed
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
