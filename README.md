# AutoWikiBrowser for Mac

An unofficial native macOS version of [AutoWikiBrowser](https://en.wikipedia.org/wiki/Wikipedia:AutoWikiBrowser) (AWB), the semi-automated wiki editor.

It runs AWB's own editing engine, WikiFunctions, compiled for .NET 10. That means the same general fixes, typo fixing (RegexTypoFix), find and replace, list makers, CheckPage rules and settings files as Windows AWB, with a new Mac interface built with [Avalonia](https://avaloniaui.net).

## Installing

1. Download the latest `.zip` from [Releases](../../releases) (for Apple silicon Macs, macOS 12 or later) and unzip it.
2. Move **AutoWikiBrowser** to your Applications folder.
3. Open it. macOS will block it the first time, because it isn't signed with an Apple Developer ID. Go to **System Settings → Privacy & Security**, scroll down and click **Open Anyway**. You only need to do this once.

To edit English Wikipedia (and some other wikis), your account must be approved on that wiki's [AutoWikiBrowser CheckPage](https://en.wikipedia.org/wiki/Wikipedia:AutoWikiBrowser/CheckPageJSON), as with Windows AWB. A [bot password](https://en.wikipedia.org/wiki/Special:BotPasswords) is the recommended way to log in.

## What works

- Wikimedia projects, Fandom wikis, and any other MediaWiki wiki (paste the wiki's address)
- Logging in with bot passwords, optionally saved in the macOS Keychain
- Making lists: category (and subcategories), what links here, transclusions, links on a page, search, user contributions, watchlist, text files
- General fixes, auto tagging, Unicodify, RegexTypoFix, find and replace, advanced find and replace (Replace Special), categories, files, appending and prepending text, skip options
- Reviewing every change in a colour-coded diff before saving, with previews rendered by the wiki
- Windows AWB settings files (`.xml`): open, save, and default settings
- There is no bot mode: every edit is reviewed before it's saved

Not ported yet: plugins, move/delete/protect, disambiguation, the list filter and comparer, the database scanner and custom modules.

## Building from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```sh
cd MacPort
./build.sh test          # run AWB's unit tests (840 tests)
./build.sh run           # build and launch
./package.sh --install   # build AutoWikiBrowser.app and install it in /Applications
./package.sh --zip       # build a zip for sharing (in MacPort/dist/)
```

## How it's put together

- `AWB/`: AWB's own code (WikiFunctions, the Windows app, plugins, unit tests), unchanged from the upstream repository (SourceForge SVN r13054).
- `MacPort/WikiFunctions.Core`: builds the WikiFunctions engine for .NET 8 on macOS. Most files are compiled straight from `AWB/WikiFunctions`; `Shims/` replaces the Windows-only parts. Before compiling, `MacPort/WindowsSource.targets` gives the engine source Windows line endings, which AWB's text processing depends on.
- `MacPort/AWB.Mac`: the Mac app (Avalonia). The processing flow follows AWB's `Main.cs`.
- `MacPort/UnitTests`: AWB's unit tests, run against the Mac engine.

## Licence and credits

AutoWikiBrowser is free software under the GNU General Public License, version 2 or later (see `COPYING`), copyright © 2006–2026 Martin Richards, Max Semenik, Sam Reed, Stephen Kennedy and the AWB contributors. This Mac port is under the same licence.

This is not an official AWB release. Please report problems with the Mac version here, not to the AWB developers.
