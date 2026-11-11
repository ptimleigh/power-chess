# Chess Fighter: store release guide

## What is built
- `app/` is the Android shell (Capacitor): app id `app.chessfighter.game`, name "Chess Fighter", portrait only, minSdk 24, targetSdk 34.
- `app/scripts/build-android.sh` publishes the web app, injects the ad bridge and the back-button handler, patches the version and icons,
  and builds. Without secrets it makes a **debug APK with Google test ads**. With `RELEASE=1` and a keystore it also makes a signed
  release APK and the Play Store bundle (`.aab`).
- `.github/workflows/chess-fighter-android.yml` runs it on every push that touches `chess-fighter/`, and publishes the debug APK to the
  rolling prerelease `fighter-android-latest`.
- Icons: `tools/make-icons.py` (knight sprite on the sunset stage, gold frame).
- Privacy policy page: `web/ChessFighter.Web/wwwroot/privacy.html`, served at `https://ptimleigh.github.io/power-chess/fighter/privacy.html`.

## What you need to do (repository secrets)
Add these in the repository's Settings, Secrets and variables, Actions. Nothing real turns on until they exist.

| Secret | What it is |
|---|---|
| `FIGHTER_ADMOB_APP_ID` | AdMob app id (`ca-app-pub-...~...`) |
| `FIGHTER_ADMOB_INTERSTITIAL_ID` | AdMob interstitial ad unit id |
| `FIGHTER_ADMOB_REWARDED_ID` | AdMob rewarded ad unit id |
| `FIGHTER_KEYSTORE_BASE64` | your release keystore, base64-encoded (`base64 -w0 fighter.keystore`) |
| `FIGHTER_KEYSTORE_PASSWORD`, `FIGHTER_KEY_ALIAS`, `FIGHTER_KEY_PASSWORD` | its passwords and key alias |

Make the keystore once and **keep a backup**: Play ties your app to it (or enrol in Play App Signing and keep the upload key safe).
`keytool -genkeypair -v -keystore fighter.keystore -alias fighter -keyalg RSA -keysize 2048 -validity 10000`

The privacy policy lists chessfighterapp@gmail.com as the contact; use the same address in the Play Console developer and store-listing contact fields.

## Ad rules (already in the game)
Interstitials only between games, never in your first 2 games, at most one every 3 games and 4 minutes, never before a daily puzzle.
Rewarded ads are always optional (double XP after a game, an extra hint). Ads start only after the consent form (UMP) is answered where it
is required. "Ad choices" in the help sheet reopens it. The browser build has no ads.

## Play Console checklist
1. Developer account ($25 once). New personal accounts must run a **closed test with about 20 testers for 14 days** before production.
2. Create the app, upload the `.aab` from the workflow's `chess-fighter-play-store-bundle` artifact.
3. Store listing (draft below), screenshots (phone, 9:16), feature graphic (1024x500), icon 512 (`icon-512.png`).
4. Privacy policy URL (above). **Data safety form:** collects device or other IDs and approximate location for advertising (AdMob), shared with
   Google; no account data; data is encrypted in transit; users cannot request deletion because the app holds no personal data on a server.
5. **Content rating (IARC):** cartoon violence only (chess pieces destroyed, no blood), no gambling, no user-generated content, contains ads.
6. **Ads declaration:** yes, the app contains ads. Target audience: 13 and over / general audience, not a children's app.

## Listing draft
**Title:** Chess Fighter: Arcade Chess

**Short description (80):** Chess with power squares, cards and 16-bit fighter flair. Fireballs, dragons, combos.

**Long description:**
Chess, but every game is a fight. Powerup squares appear on the board in matching pairs. Land on one to load a Fireball, hook a rival
with a Leap, arm a Bomb, turn a piece into a Ghost, take over an enemy piece, or chain a Combo of extra moves. Draw Card squares add
cards to your bag that anyone can see: cancel your rival's card with Nuh-uh, rewind a turn, call a Dragon Strike down a row, summon Mars,
Caïssa or Shiva, or launch a Missile at a corner of the board.

- Play the computer at five levels, or a friend on one device
- 16-bit arcade look: health bars, K.O., FIGHT! banners and pixel art
- 20 levels that unlock new powers, card packs and rule variants
- Daily chess puzzle with a streak
- Share a replay video of your finishing move
- Works offline

**Keywords/tags:** chess, arcade, fighter, strategy, puzzle, board game, pixel, retro

## Do not use in the listing or the app
"Street Fighter", "Hadouken" or anything that looks like Capcom's characters. Keep the summon portraits respectful and check you may use
the supplied artwork commercially.

## Before each release
- Bump `VERSION_NAME` in the workflow (the version code is the run number).
- Test on a real phone: install the debug APK, play a full game, background and reopen the app, watch a rewarded ad (test ad), press Back
  with a sheet open, rotate (locked to portrait), turn sound off.
