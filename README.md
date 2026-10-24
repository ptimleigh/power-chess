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

Tap a piece, then a highlighted square. Land on a gold `?` square to open it. Use the buttons under the board for perks,
and the Detonate button when a bomber is selected.

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
| `demo` | `demo=fireball` | Open one powerup's animated tutorial directly (`doublemove`, `poison`, `spikedarmor`, `stun`, `smallbomb`, `bigbomb`, `fireball`, `steal`, `boulder`, `reroll`, `rewind`, `transformqueen`) |
| `training` | `training=1` | Start on the small training board |

With no options the game resumes where you left off after a refresh (saved in the browser), and a first-ever game starts on the training board.
