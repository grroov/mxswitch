#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"
PREFIX="${PREFIX:-$HOME/.local}"
APP_DIR="/Applications/EasySwitch-Tandem.app"
CONFIG_DIR="${XDG_CONFIG_HOME:-$HOME/.config}/mxswitch"

echo "[1/3] Building and installing mxswitch CLI to $PREFIX/bin/mxswitch ..."
make -C "$REPO_DIR"
install -d "$PREFIX/bin"
install -m 755 "$REPO_DIR/mxswitch" "$PREFIX/bin/mxswitch"
codesign -f -s - "$PREFIX/bin/mxswitch"

echo "[2/3] Writing config to $CONFIG_DIR/config ..."
mkdir -p "$CONFIG_DIR"
if [ ! -f "$CONFIG_DIR/config" ]; then
    cat << 'EOF' > "$CONFIG_DIR/config"
channel_a=1
channel_b=2
device=all
EOF
fi

echo "[3/3] Building native background app bundle $APP_DIR ..."
mkdir -p "$APP_DIR/Contents/MacOS"
cat << 'EOF' > "$APP_DIR/Contents/Info.plist"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>EasySwitch-Tandem</string>
    <key>CFBundleIdentifier</key>
    <string>com.local.mxswitch.tandem</string>
    <key>CFBundleName</key>
    <string>EasySwitch-Tandem</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0</string>
    <key>CFBundleVersion</key>
    <string>1</string>
    <key>LSBackgroundOnly</key>
    <true/>
    <key>LSUIElement</key>
    <true/>
</dict>
</plist>
EOF

clang -O2 -Wall -Wextra -o "$APP_DIR/Contents/MacOS/EasySwitch-Tandem" \
    -framework IOKit -framework CoreFoundation -framework CoreGraphics \
    -x c - << EOF
#define main mxswitch_main
#include "$REPO_DIR/macos/mxswitch.c"
#undef main

int main(void) {
    char *args[] = { "mxswitch", "--toggle", "--all", NULL };
    return mxswitch_main(3, args);
}
EOF
codesign -f -s - "$APP_DIR"

echo ""
echo "=================================================================="
echo " Logi Options+ Setup (macOS)"
echo "=================================================================="
echo " 1. Grant Input Monitoring permission in System Settings:"
echo "    System Settings -> Privacy & Security -> Input Monitoring"
echo "    Click '+' and enable: $APP_DIR"
echo " 2. Open Logi Options+ -> Signature Plus M750 -> Top button."
echo " 3. Assign 'Open application' -> $APP_DIR"
echo "=================================================================="
