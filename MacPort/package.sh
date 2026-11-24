#!/bin/zsh
# Builds AutoWikiBrowser.app in MacPort/dist/ (self-contained: no .NET install needed to run it).
#   ./package.sh             build dist/AutoWikiBrowser.app for this Mac's processor
#   ./package.sh --install   build it, copy it to /Applications, and open it
#   ./package.sh --zip       build it and make a zip for sharing in dist/
#   ARCH=x64 ./package.sh    build for Intel Macs instead (ARCH=arm64 for Apple silicon)
# Output goes to MacPort/package.log so Claude can read it.
set -e
set -o pipefail
cd "$(dirname "$0")"

if [[ -z "$ARCH" ]]; then
  [[ "$(uname -m)" == "arm64" ]] && ARCH=arm64 || ARCH=x64
fi
OUT=dist
APP="$OUT/AutoWikiBrowser.app"

echo "Building AutoWikiBrowser for osx-$ARCH…"
rm -rf "$OUT/publish" "$APP"
dotnet publish AWB.Mac/AWB.Mac.csproj -c Release -r "osx-$ARCH" --self-contained true \
  -nologo -clp:ErrorsOnly -o "$OUT/publish" 2>&1 | tee package.log

mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
ditto "$OUT/publish" "$APP/Contents/MacOS"
cp AWB.Mac/Info.plist "$APP/Contents/Info.plist"
cp AWB.Mac/Assets/AutoWikiBrowser.icns "$APP/Contents/Resources/AutoWikiBrowser.icns"
chmod +x "$APP/Contents/MacOS/AutoWikiBrowser"
rm -rf "$OUT/publish"

# Ad-hoc signature: required to run on Apple silicon; fine for your own Mac (not for distributing)
codesign --force --deep --sign - "$APP" 2>&1 | tee -a package.log
echo "Built $APP ($(du -sh "$APP" | cut -f1))" | tee -a package.log

if [[ "$1" == "--zip" ]]; then
  VERSION=$(/usr/libexec/PlistBuddy -c "Print :CFBundleShortVersionString" AWB.Mac/Info.plist)
  ZIP="$OUT/AutoWikiBrowser-$VERSION-mac-$ARCH.zip"
  rm -f "$ZIP"
  ditto -c -k --keepParent "$APP" "$ZIP"
  echo "Zip for sharing: $ZIP ($(du -sh "$ZIP" | cut -f1))" | tee -a package.log
fi

if [[ "$1" == "--install" ]]; then
  osascript -e 'tell application "AutoWikiBrowser" to quit' >/dev/null 2>&1 || true
  rm -rf /Applications/AutoWikiBrowser.app
  ditto "$APP" /Applications/AutoWikiBrowser.app
  echo "Installed to /Applications/AutoWikiBrowser.app" | tee -a package.log
  open /Applications/AutoWikiBrowser.app
fi
