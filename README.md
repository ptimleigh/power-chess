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

### Opponent levels

Five AI levels: Easy (depth 1, a quarter of its moves random), Normal (depth 2 with check extension and quiescence), Hard (depth 3, full evaluation), Expert (iterative deepening to depth 6 within 1 s) and Master (depth 8 within 2 s). Harder levels pay more XP (x0.7, 1, 1.4, 1.8, 2.2). Normal used to fall to scholar's mate because the search stopped without checking for mate at its horizon; the leaf check extension fixed that. `tools/LadderSim` plays adjacent levels against each other on plain chess; results are in `docs/BALANCE.md`.

### Custom powerup rules

Levelling up unlocks ways to change the powerup experience (all in `Assets/Core/Presets.cs`, chosen in the New game sheet): at level 5 a square frequency setting (Rare, Normal, Frequent, Chaos) plus Chaos Mode and Calm Mode; level 6 "Oops! All Big Bombs"; 7 Ghost Town (ghosts and teleporters); 8 Fireworks (Fireball, Steal, Stun); 9 Royal Court (Armor, Ghost, Triple Move, bombs); 10 a Custom builder with Off/Low/Normal/High per powerup. A preset ignores the player's level-based powerup unlocks because the player chose it. Games with a preset or a non-normal frequency pay half XP; trophies and daily puzzles are unaffected.

### Installable app and Android build

The web build is a PWA (`manifest.webmanifest`, `service-worker.js`, icons rendered by `tools/make-icons.js`): open the Pages URL on a phone and use "Add to Home screen" / Install. The service worker is network-first, so a new deploy shows up on the next open, and once loaded the game works offline.

`app/` wraps the same web build with Capacitor as an Android app. `app/scripts/build-android.sh` publishes the web app, injects `app/native/pcads-bridge.js` (AdMob through `window.pcads`), generates the Android project, patches the manifest and icons and runs `gradlew assembleDebug`. The `Build Android debug APK` workflow runs it on every push that touches `app/`, `web/` or `Assets/Core/` and attaches `power-chess-debug.apk` to the rolling prerelease `android-latest`: open the repository's Releases page on your phone, download it and allow installs from your browser. It uses Google's public **test** ad units and a debug signing key.

Ads: `Assets/Core/Ads.cs` holds the rules (`AdPolicy`: interstitial only between games, not in the first 2 games, at most one per 3 games and 4 minutes, never before a daily puzzle; `RewardedXp.Double`: a rewarded ad doubles the XP of the game just finished, once). The browser build has no ads, so the "Watch an ad: double XP" button only appears in the app.

For real ads and a store release you still need: an AdMob account with real app and ad-unit ids (replace the test ids in `build-android.sh` and `pcads-bridge.js`), a release keystore, a Play Console listing with a privacy policy, and review of the consent (UMP) flow. iOS needs a macOS runner (`cap add ios`) and an Apple Developer account.

### Debug tools in the UI

Tap the logo five times (or open `?debug=1`) for a panel that gives perks, puts any buff on any piece, drops powerup squares or teleporters, and removes them. Tap a tool, then the board; tap the tool again to stop.

### Debug URL options

Handy for reproducing a situation. All optional:

| Param | Example | Meaning |
|---|---|---|
| `ai` | `ai=0` | 0 = two players, 1 easy, 2 normal, 3 hard, 4 expert, 5 master |
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
| `preset`, `freq`, `custom` | `preset=bigbombs&freq=3` | Custom powerup rules (see below): a preset id, square frequency 0-3 (rare, normal, frequent, chaos) and the custom builder string. Locked choices are ignored for the `level` in play |
| `day` | `day=20752` | Pretend it is this day (days since 1970-01-01) for the daily challenge |
| `training` | `training=1` | Start on the small training board |

With no options the game resumes where you left off after a refresh (saved in the browser), and a first-ever game starts on the training board.
