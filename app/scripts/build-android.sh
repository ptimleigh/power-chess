#!/usr/bin/env bash
# Builds the debug APK: publish the web app, wrap it with Capacitor, patch in the AdMob TEST ids and the icons.
# usage: app/scripts/build-android.sh            (full build, needs the Android SDK)
#        SKIP_GRADLE=1 app/scripts/build-android.sh   (generate and patch the project only)
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
APP="$ROOT/app"
ADMOB_TEST_APP_ID="ca-app-pub-3940256099942544~3347511713"

echo "== publish web app"
rm -rf "$APP/www" "$ROOT/out-native"
dotnet publish "$ROOT/web/PowerChess.Web" -c Release -o "$ROOT/out-native"
cp -r "$ROOT/out-native/wwwroot" "$APP/www"
rm -rf "$ROOT/out-native"

echo "== inject the ad bridge (base href stays / because the native shell serves from the root)"
cp "$APP/native/pcads-bridge.js" "$APP/www/js/pcads-bridge.js"
python3 - "$APP/www/index.html" <<'PY'
import sys
p = sys.argv[1]
s = open(p).read()
tag = '<script src="js/ads.js"></script>'
assert tag in s, "ads.js tag not found"
s = s.replace(tag, '<script src="js/pcads-bridge.js"></script>\n    ' + tag, 1)
open(p, 'w').write(s)
PY

cd "$APP"
echo "== npm ci"
npm ci --no-audit --no-fund 2>/dev/null || npm install --no-audit --no-fund

echo "== add android platform"
rm -rf android
npx cap add android
npx cap sync android

echo "== patch manifest, minSdk and icons"
python3 - "$APP/android" "$ADMOB_TEST_APP_ID" <<'PY'
import sys, re, os
root, app_id = sys.argv[1], sys.argv[2]
mf = os.path.join(root, 'app/src/main/AndroidManifest.xml')
s = open(mf).read()
if 'com.google.android.gms.ads.APPLICATION_ID' not in s:
    meta = '<meta-data android:name="com.google.android.gms.ads.APPLICATION_ID" android:value="%s"/>\n    ' % app_id
    s = s.replace('</application>', meta + '</application>', 1)
s = s.replace('<activity', '<activity android:screenOrientation="portrait"', 1) if 'screenOrientation' not in s else s
open(mf, 'w').write(s)
vg = os.path.join(root, 'variables.gradle')
v = open(vg).read()
v = re.sub(r'minSdkVersion = \d+', 'minSdkVersion = 24', v)
open(vg, 'w').write(v)
PY

ICON_DIR="$ROOT/web/PowerChess.Web/wwwroot/icons"
for d in android/app/src/main/res/mipmap-*; do
  [ -d "$d" ] || continue
  for f in ic_launcher.png ic_launcher_round.png; do [ -f "$d/$f" ] && cp "$ICON_DIR/icon-192.png" "$d/$f"; done
  [ -f "$d/ic_launcher_foreground.png" ] && cp "$ICON_DIR/icon-maskable-512.png" "$d/ic_launcher_foreground.png"
done
mkdir -p android/app/src/main/res/values
cat > android/app/src/main/res/values/ic_launcher_background.xml <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<resources>
    <color name="ic_launcher_background">#0D0B1E</color>
</resources>
XML

if [ "${SKIP_GRADLE:-0}" = "1" ]; then echo "== SKIP_GRADLE set, stopping before gradle"; exit 0; fi

echo "== gradle assembleDebug"
cd android
chmod +x gradlew
./gradlew assembleDebug --no-daemon
APK="$APP/android/app/build/outputs/apk/debug/app-debug.apk"
ls -la "$APK"
echo "$APK"
