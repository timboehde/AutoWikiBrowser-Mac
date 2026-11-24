#!/bin/zsh
# Puts this project on GitHub (run from Terminal on your Mac).
#   ./publish.sh            first time: create a public repository and upload the source;
#                           after that: upload new changes
#   ./publish.sh --release  build the app and publish it as a GitHub release (a zip your friends can download)
#   REPO_NAME=Something ./publish.sh   use another repository name (default: AutoWikiBrowser-Mac)
set -e
cd "$(dirname "$0")/.."
REPO_NAME=${REPO_NAME:-AutoWikiBrowser-Mac}

if ! command -v git >/dev/null 2>&1 || ! git --version >/dev/null 2>&1; then
  echo "Git isn't installed. Run:  xcode-select --install   then run this script again."
  exit 1
fi
if ! command -v gh >/dev/null 2>&1; then
  if command -v brew >/dev/null 2>&1; then
    brew install gh
  else
    echo "This needs GitHub's command-line tool. Download the macOS installer from https://cli.github.com, install it, then run this script again."
    exit 1
  fi
fi
if ! gh auth status >/dev/null 2>&1; then
  echo "Signing in to GitHub (a browser window will open)…"
  gh auth login --hostname github.com --git-protocol https --web
fi
gh auth setup-git

if [[ "$1" == "--release" ]]; then
  ./MacPort/package.sh --zip
  VERSION=$(/usr/libexec/PlistBuddy -c "Print :CFBundleShortVersionString" MacPort/AWB.Mac/Info.plist)
  ZIP=$(ls -t MacPort/dist/AutoWikiBrowser-$VERSION-mac-*.zip | head -1)
  TAG="mac-v$VERSION-$(date +%Y%m%d-%H%M)"
  gh release create "$TAG" "$ZIP" --title "AutoWikiBrowser $VERSION for Mac ($(date +%Y-%m-%d))" \
    --notes-file MacPort/release-notes.md
  echo "Release published: $(gh repo view --json url -q .url)/releases/tag/$TAG"
  exit 0
fi

[[ -d .git ]] || git init -b main
if [[ -z "$(git config user.name)" ]]; then
  git config user.name "$(gh api user -q '.name // .login')"
  git config user.email "$(gh api user -q '.id')+$(gh api user -q '.login')@users.noreply.github.com"
fi

git add -A
if git diff --cached --quiet; then
  echo "No new changes to upload."
else
  if git rev-parse --verify HEAD >/dev/null 2>&1; then
    git commit -q -m "Update the Mac port"
  else
    git commit -q -F - <<'MSG'
AutoWikiBrowser for Mac

AWB trunk (SourceForge SVN r13054) plus a native macOS port:
- MacPort/WikiFunctions.Core: the WikiFunctions engine on .NET 8
- MacPort/AWB.Mac: Avalonia app
- MacPort/UnitTests: AWB's unit tests against the Mac engine

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_012iD4spVYPspGH8mcBWpjpu
MSG
  fi
fi

if ! git remote get-url origin >/dev/null 2>&1; then
  LOGIN=$(gh api user -q .login)
  if gh repo view "$LOGIN/$REPO_NAME" >/dev/null 2>&1; then
    # the repository already exists on GitHub (e.g. created on github.com): use it
    git remote add origin "https://github.com/$LOGIN/$REPO_NAME.git"
  else
    gh repo create "$REPO_NAME" --public \
      --description "Unofficial native macOS version of AutoWikiBrowser, the semi-automated wiki editor"
    git remote add origin "https://github.com/$LOGIN/$REPO_NAME.git"
  fi
fi
git push -u origin main
echo "\nDone: $(gh repo view --json url -q .url)"
