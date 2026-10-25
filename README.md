# Power Chess

Chess with mystery powerup squares: fireballs, bombs, stuns, steals, poison and more. Built to be fun to watch as well
as play (short, juicy moments), with ads as the revenue model.

- Rules reference: [docs/RULES.md](docs/RULES.md)
- Roadmap and design decisions live in the project plan.

**Play it:** https://ptimleigh.github.io/power-chess/ (deployed from the default branch by `.github/workflows/pages.yml`; needs Settings > Pages > Source = GitHub Actions once).

## Layout

| Path | What |
|---|---|
| `Assets/Core` | The rules engine: pure C#, no Unity references. Shared by Unity, the web prototype, tests and a future server. |
| `src/PowerChess.Core` | A `.csproj` that compiles `Assets/Core` outside Unity. |
| `tests/PowerChess.Core.Tests` | NUnit tests: perft counts, every powerup, seeded random-game simulations, AI. |
| `web/PowerChess.Web` | Playable Blazor WebAssembly prototype that reuses the engine unchanged. |
| `tools/PuzzleGen` | Generates and verifies the daily puzzles into `Assets/Core/PuzzleData.cs`. |
| `tools/BalanceSim` | Plays thousands of AI-vs-AI games and writes [docs/BALANCE.md](docs/BALANCE.md). |

## Run the tests

```
dotnet test tests/PowerChess.Core.Tests
```

## Play the web prototype

```
dotnet run --project web/PowerChess.Web
```

or publish static files (`dotnet publish web/PowerChess.Web -c Release -o out`) and serve `out/wwwroot` with any static
file server.

Tap a piece, then a highlighted square. Land on a glowing powerup square to claim it (a purple `?` square is a mystery). Use the buttons under the board for perks,
and the Detonate button when a bomber is selected.

### Daily challenge

One puzzle a day (Mon-Tue easy, Wed-Fri medium, weekends hard): a fixed position with set powerups and a goal of checkmate in 1-3 of your moves. Solving it pays XP that grows with a day streak, and unlocks streak trophies. Puzzles are generated and verified by `tools/PuzzleGen` (a forced mate within the budget exists, none one move shorter, and none without the powerups) and checked in as `Assets/Core/PuzzleData.cs`. Regenerate with `dotnet run --project tools/PuzzleGen -c Release -- 600 Assets/Core/PuzzleData.cs 14`. The solver and day-selection logic live in `Assets/Core/Daily.cs`.

### Progression

Finishing a game earns XP (more for harder opponents, wins, captures, powerups and checkmate), levels up to 10, and trophies. Levels unlock powerups in stages: level 1 has Double Move, Poison, Spiked Armor, Stun, Boulder, Reroll, Fireball and Steal; 2 adds Small Bomb, Ghost and the basic Transforms; 3 Big Bomb, Become Queen, Swap and teleporters; 4 Triple Move and Rewind. Level 2 needs 60 XP, level 3 180, level 4 360 (30 x (level-1) x level). The rules live in `Assets/Core/Progression.cs` (tested, pure C#) and the profile is stored in the browser (`powerchess.profile.v1`). Debug games and games started with scenario options pay nothing unless `rewards=1`.

### Debug tools in the UI

Tap the logo five times (or open `?debug=1`) for a panel that gives perks, puts any buff on any piece, drops powerup squares or teleporters, and removes them. Tap a tool, then the board; tap the tool again to stop.

### Debug URL options

Handy for reproducing a situation. All optional:

| Param | Example | Meaning |
|---|---|---|
| `ai` | `ai=0` | 0 = two players, 1 easy, 2 normal, 3 hard |
| `seed` | `seed=7` | Deterministic spawns and draws |
| `fen` | `fen=4k3/8/8/8/8/1N6/8/4K3%20w%20-%20-` | Start from a position |
| `mystery` | `mystery=d4,e5` | Put mystery squares on these squares |
| `only` | `only=poison,steal` | Restrict what mystery squares can contain |
| `perks` | `perks=w:fireball:2,b:steal` | Give perks |
| `buffs` | `buffs=d4:smallbomb` | Give pieces a buff |
| `pairs`, `spawn`, `rerolls` | `spawn=2` | Initial pairs, spawn interval, starting rerolls |
| `tut` | `tut=1` | Powerup tutorials are off whenever scenario options are used; `tut=1` turns them on |
| `demo` | `demo=fireball` | Open one powerup's animated tutorial directly (`doublemove`, `poison`, `spikedarmor`, `stun`, `smallbomb`, `bigbomb`, `fireball`, `steal`, `boulder`, `reroll`, `rewind`, `triplemove`, `ghost`, `swap`, `transformqueen`) |
| `mode` | `mode=mystery` | `revealed` (default: powerups shown on squares, 20% hidden) or `mystery` (all hidden) |
| `hidden` | `hidden=0.5` | Chance a revealed pair stays hidden |
| `portal`, `portals` | `portal=c3` | Place a teleporter pair (c3 and its point mirror f6); `portals=0` stops them spawning |
| `drama` | `drama=0` | Turn the strike effects (slash, hit-stop, callouts) off |
| `level` | `level=1` | Act as an account of this level: only the powerups unlocked by then can appear (scenarios default to everything) |
| `rewards` | `rewards=1` | Pay XP and trophies even in a scenario, so progression can be tested |
| `day` | `day=20752` | Pretend it is this day (days since 1970-01-01) for the daily challenge |
| `training` | `training=1` | Start on the small training board |

With no options the game resumes where you left off after a refresh (saved in the browser), and a first-ever game starts on the training board.
