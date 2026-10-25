# Balance report

Generated with `dotnet run --project tools/BalanceSim -c Release -- <games> <depth> <outFile> [spawnInterval]`.

## Findings and what changed

- **Transform to Queen was the one big outlier.** Sides that picked one up won about 25 points more often than baseline (68% vs 44%, 101 samples), which makes sense: it turns a cheap piece into a queen. I cut its weight in `GameConfig.DefaultPool` from 3 to 1. It now turns up about 0.10 times per game instead of 0.25 and is a rare jackpot square. Holders still win more often (the table below, 61 samples), which is expected for a free queen; if it still feels swingy in play, the next lever is restricting what it can upgrade.
- **Big Bomb is consistently a little strong** (+7 to +9 points across four runs) but is rare (weight 2), so I left it. Worth watching.
- **Everything else is within a few points of baseline** at depth 2, so no other powerup is dominant or useless.
- **Stun looked weak at depth 1 (-6 points) but is neutral at depth 2 (-1 point).** The depth-1 AI wasted turns on it, so that was AI behaviour, not the rule.
- **Spawn rate does not change game length.** Median length was 70 plies with a new pair every 2 plies, 64 every 4, and 69 every 7, with about 10 squares opened per game in each case. I kept 4. Game length comes from how the AI plays, so tune it with AI strength or a move limit rather than spawn rate.
- **Endings:** about 85% of depth-2 games end in checkmate; most of the rest are 50-move draws. The depth-1 AI is too weak to finish games (37% 50-move draws), so use depth 2 or higher for balance questions.
- The AI cannot use Rewind (so it never draws it), and uses Reroll only when a pickup comes up, so those two perks are under-represented here.

Method, 600 games below after the change; the "before" numbers in this section come from 400 games at depth 2 with Transform-to-Queen at weight 3.

600 AI-vs-AI games at search depth 2, all powerups enabled (default pool), spawn every 4 plies, 300-ply limit, 4 random opening actions.
The AI is shallow, so treat absolute numbers loosely and look for outliers. "Win rate when obtained" is how often a side won in games where it picked that powerup up at least once; it is a correlation (stronger sides also grab more squares), not proof of cause.

## Game length and endings

- Plies per game: median 64, 25th percentile 44, 75th percentile 107, longest 300
- Mystery squares opened per game: 9.9
- Win: Checkmate: 511 (85.2%)
- Draw: 50-move rule: 74 (12.3%)
- Draw: Move limit reached: 15 (2.5%)

## Powerups

Baseline for any side: win 42.6%, draw 14.8%, loss 42.6%.

| Powerup | Times a side obtained it | Win rate when obtained | Draw | Loss | Win vs baseline |
|---|---|---|---|---|---|
| TransformQueen | 61 | 57.4% | 14.8% | 27.9% | +14.8% |
| BigBomb | 130 | 50.0% | 23.1% | 26.9% | +7.4% |
| SpikedArmor | 474 | 46.2% | 15.2% | 38.6% | +3.6% |
| Fireball | 445 | 46.1% | 17.5% | 36.4% | +3.5% |
| SmallBomb | 304 | 45.7% | 18.8% | 35.5% | +3.1% |
| TransformRook | 150 | 45.3% | 17.3% | 37.3% | +2.8% |
| Poison | 471 | 43.9% | 18.0% | 38.0% | +1.4% |
| DoubleMove | 538 | 42.8% | 18.0% | 39.2% | +0.2% |
| Stun | 527 | 42.5% | 19.7% | 37.8% | -0.1% |
| Reroll | 515 | 41.2% | 17.9% | 41.0% | -1.4% |
| TransformBishop | 151 | 41.1% | 19.9% | 39.1% | -1.5% |
| Boulder | 404 | 40.3% | 19.8% | 39.9% | -2.2% |
| TransformKnight | 111 | 38.7% | 19.8% | 41.4% | -3.8% |
| Steal | 405 | 38.3% | 18.8% | 43.0% | -4.3% |
| TransformPawn | 7 | 14.3% | 42.9% | 42.9% | -28.3% |

## Uses

- Stun used: 586 total, 0.98 per game
- Steal used: 411 total, 0.69 per game
- Fireball fired: 402 total, 0.67 per game
- Boulder placed: 299 total, 0.50 per game
- Poison triggered: 243 total, 0.41 per game
- Small Bomb detonated: 113 total, 0.19 per game
- Boulder cleared: 83 total, 0.14 per game
- Big Bomb detonated: 53 total, 0.09 per game
- Double Move cancelled by check: 5 total, 0.01 per game

## Mystery vs Revealed squares

500 games each, depth 2, same settings (see `bal-*` runs; `BalanceSim ... mystery|revealed`).

| | Mystery | Revealed (20% hidden) |
|---|---|---|
| Median plies | 64 | 64 |
| 75th percentile plies | 109 | 134 |
| Checkmates | 85.2% | 83.6% |
| 50-move / ply-cap draws | 12.2% / 2.6% | 12.0% / 4.2% |
| Squares opened per game | 10.0 | 14.8 |

Revealed play is about 50% more active (the AI now chases squares it can see) with the same median length and
checkmate rate. Spread across powerups stays within about +-4 points except Transform to Queen (+12.5 to +15.6 in both
modes, already down-weighted) and tiny samples (Transform to Pawn). Big Bomb looks strong in Mystery (+8.3) but falls to
-0.8 in Revealed, which is within noise at this sample size. No pool changes made for Revealed.

## After adding Ghost, Triple Move and Swap

500 games, depth 2, revealed squares, spawn every 4 plies (same settings as above). Teleporters are off in the simulator, and Swap is off because the AI never uses it.

- Median 59 plies, checkmates 83.0%, 50-move draws 12.4%, ply-cap draws 4.4%: no change in game shape.
- Ghost: obtained 216 times, win rate 47.7% against a 41.5% baseline (+6.2). The strongest of the new three and the second-biggest outlier after Transform to Queen (+11.8). Watch it; if phone playtests agree, drop its weight from 6 to 4 or shorten it from 4 to 3 turns.
- Triple Move: 189 times, +1.4. Fine at weight 3 (it has few safe uses, and giving check cancels it).
- Everything else stayed within about +-4 points of baseline.

## Playtest tuning: more big powerups

Feedback: strong powerups felt too rare. The pool now favours the big hitters (Fireball 11, Steal 9, Small Bomb 8, Ghost 8, Big Bomb 5, Triple Move 5, Become Queen 3) over the mild buffs (Double Move 6, Poison 5, Reroll 5, Boulder 4, Become Pawn 1), spawns come every 3 plies (was 4) and up to 8 squares can exist (was 6). Unlocks are also much faster (see `Progression.UnlockLevel`): Fireball and Steal are available from the first game.

500 games each, depth 2, revealed squares.

| | Before (old weights, every 4 plies, max 6) | After (new weights, every 3 plies, max 8) |
|---|---|---|
| Median plies | 59 | 57 |
| Checkmates | 83.0% | 87.4% |
| 50-move / ply-cap draws | 12.4% / 4.4% | 10.4% / 2.0% |

Times obtained and win rate vs baseline:

| Powerup | Before | After |
|---|---|---|
| TransformQueen | 60 / +11.8% | 218 / +5.4% |
| TripleMove | 189 / +1.4% | 319 / +5.2% |
| Ghost | 216 / +6.2% | 343 / +3.8% |
| BigBomb | 124 / +1.2% | 342 / +2.5% |
| Fireball | 436 / +0.2% | 586 / +2.0% |
| Steal | 433 / +0.1% | 596 / +0.3% |
| SmallBomb | 292 / -0.1% | 459 / +0.3% |
| Stun | 452 / +1.4% | 547 / -0.4% |
| Reroll | 472 / -3.4% | 377 / +0.3% |
| Boulder | 379 / -4.0% | 300 / -6.0% |

The rare ones are drawn far more often (Become Queen 60 to 218, Big Bomb 124 to 342, Triple Move 189 to 319, Ghost 216 to 343) and the common big ones somewhat more (Fireball 436 to 586, Steal 433 to 596). Game length barely moved, and the strongest outlier is now Become Queen at +5.4 (it was +11.8 when rare), so no further trimming was needed. Boulder looks weak (-6.0), which fits it being a setup perk the AI uses poorly.
