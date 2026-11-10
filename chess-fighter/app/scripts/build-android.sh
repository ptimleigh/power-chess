#!/usr/bin/env bash
# Builds Chess Fighter for Android: publish the web app, wrap it with Capacitor, patch in the ad ids, version, icons and signing.
#
#   chess-fighter/app/scripts/build-android.sh              debug APK with Google's TEST ads (always works, no secrets needed)
#   RELEASE=1 ... build-android.sh                          also a signed release APK and Play Store bundle (.aab)
#   SKIP_GRADLE=1 ... build-android.sh                      generate and patch the project only
#
# Environment (all optional):
#   ADMOB_APP_ID, ADMOB_INTERSTITIAL_ID, ADMOB_REWARDED_ID   real AdMob ids; without all three the build uses test ads
#   VERSION_NAME (default 0.1.0), VERSION_CODE (default 1; the workflow uses the run number)
#   KEYSTORE_PATH, KEYSTORE_PASSWORD, KEY_ALIAS, KEY_PASSWORD  release signing key (RELEASE=1 needs them)
set -euo pipefail

FIGHTER="$(cd "$(dirname "$0")/../.." && pwd)"
APP="$FIGHTER/app"
ADMOB_TEST_APP_ID="ca-app-pub-3940256099942544~3347511713"
APP_ID="${ADMOB_APP_ID:-$ADMOB_TEST_APP_ID}"
VERSION_NAME="${VERSION_NAME:-0.1.0}"
VERSION_CODE="${VERSION_CODE:-1}"

echo "== publish web app"
rm -rf "$APP/www" "$FIGHTER/out-native"
dotnet publish "$FIGHTER/web/ChessFighter.Web" -c Release -o "$FIGHTER/out-native"
cp -r "$FIGHTER/out-native/wwwroot" "$APP/www"
rm -rf "$FIGHTER/out-native"
# Android packaging rejects a file and its precompressed twin; the app serves the plain files.
find "$APP/www" \( -name "*.gz" -o -name "*.br" \) -delete
# The native shell serves from the root and has its own offline copy, so no service worker is needed.
rm -f "$APP/www/service-worker.js"

echo "== inject the ad bridge, the ad ids and the back-button handler (base href stays / in the native shell)"
cp "$APP/native/pcads-bridge.js" "$APP/www/js/pcads-bridge.js"
cp "$APP/native/backbutton.js" "$APP/www/js/backbutton.js"
if [ -n "${ADMOB_INTERSTITIAL_ID:-}" ] && [ -n "${ADMOB_REWARDED_ID:-}" ] && [ -n "${ADMOB_APP_ID:-}" ]; then
  echo "   real AdMob ids"
  printf 'window.PCADS_CONFIG = { interstitial: "%s", rewarded: "%s" };\n' "$ADMOB_INTERSTITIAL_ID" "$ADMOB_REWARDED_ID" > "$APP/www/js/pcads-config.js"
else
  echo "   TEST ads (set ADMOB_APP_ID, ADMOB_INTERSTITIAL_ID and ADMOB_REWARDED_ID for real ones)"
  echo 'window.PCADS_CONFIG = {};' > "$APP/www/js/pcads-config.js"
fi
python3 - "$APP/www/index.html" <<'PY'
import sys
p = sys.argv[1]
s = open(p).read()
tag = '<script src="js/ads.js"></script>'
assert tag in s, "ads.js tag not found"
inject = ('<script src="js/pcads-config.js"></script>\n    <script src="js/pcads-bridge.js"></script>\n    '
          '<script src="js/backbutton.js"></script>\n    ')
s = s.replace(tag, inject + tag, 1)
open(p, 'w').write(s)
PY

cd "$APP"
echo "== npm ci"
npm ci --no-audit --no-fund 2>/dev/null || npm install --no-audit --no-fund

echo "== add android platform"
rm -rf android
npx cap add android
npx cap sync android

echo "== patch manifest, version, sdk levels and icons"
python3 - "$APP/android" "$APP_ID" "$VERSION_NAME" "$VERSION_CODE" <<'PY'
import sys, re, os
root, app_id, vname, vcode = sys.argv[1:5]
mf = os.path.join(root, 'app/src/main/AndroidManifest.xml')
s = open(mf).read()
if 'com.google.android.gms.ads.APPLICATION_ID' not in s:
    meta = '<meta-data android:name="com.google.android.gms.ads.APPLICATION_ID" android:value="%s"/>\n    ' % app_id
    s = s.replace('</application>', meta + '</application>', 1)
if 'screenOrientation' not in s:
    s = s.replace('<activity', '<activity android:screenOrientation="portrait"', 1)
open(mf, 'w').write(s)
vg = os.path.join(root, 'variables.gradle')
v = open(vg).read()
v = re.sub(r'minSdkVersion = \d+', 'minSdkVersion = 24', v)
v = re.sub(r'targetSdkVersion = \d+', 'targetSdkVersion = 34', v)
open(vg, 'w').write(v)
bg = os.path.join(root, 'app/build.gradle')
b = open(bg).read()
b = re.sub(r'versionCode \d+', 'versionCode ' + vcode, b)
b = re.sub(r'versionName "[^"]*"', 'versionName "' + vname + '"', b)
open(bg, 'w').write(b)
PY

ICON_DIR="$FIGHTER/web/ChessFighter.Web/wwwroot/icons"
for d in android/app/src/main/res/mipmap-*; do
  [ -d "$d" ] || continue
  for f in ic_launcher.png ic_launcher_round.png; do [ -f "$d/$f" ] && cp "$ICON_DIR/icon-192.png" "$d/$f"; done
  [ -f "$d/ic_launcher_foreground.png" ] && cp "$ICON_DIR/icon-maskable-512.png" "$d/ic_launcher_foreground.png"
done
mkdir -p android/app/src/main/res/values
cat > android/app/src/main/res/values/ic_launcher_background.xml <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<resources>
    <color name="ic_launcher_background">#07061A</color>
</resources>
XML

if [ "${SKIP_GRADLE:-0}" = "1" ]; then echo "== SKIP_GRADLE set, stopping before gradle"; exit 0; fi

cd android
chmod +x gradlew
echo "== gradle assembleDebug"
./gradlew assembleDebug --no-daemon
DEBUG_APK="$APP/android/app/build/outputs/apk/debug/app-debug.apk"
ls -la "$DEBUG_APK"

if [ "${RELEASE:-0}" = "1" ]; then
  : "${KEYSTORE_PATH:?RELEASE=1 needs KEYSTORE_PATH}" "${KEYSTORE_PASSWORD:?needs KEYSTORE_PASSWORD}" "${KEY_ALIAS:?needs KEY_ALIAS}" "${KEY_PASSWORD:?needs KEY_PASSWORD}"
  echo "== gradle bundleRelease and assembleRelease (signed)"
  SIGN=(-Pandroid.injected.signing.store.file="$KEYSTORE_PATH" -Pandroid.injected.signing.store.password="$KEYSTORE_PASSWORD"
        -Pandroid.injected.signing.key.alias="$KEY_ALIAS" -Pandroid.injected.signing.key.password="$KEY_PASSWORD")
  ./gradlew bundleRelease assembleRelease "${SIGN[@]}" --no-daemon
  ls -la "$APP/android/app/build/outputs/bundle/release/app-release.aab" "$APP/android/app/build/outputs/apk/release/"*.apk
fi
