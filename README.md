# GG

All your games. One place. One click.

GG is a Windows desktop game library launcher. It finds the games installed on your PC (Steam, Epic,
Xbox / Microsoft Store, GOG, EA app, Ubisoft Connect, Battle.net, your own shortcuts and plain game
folders), shows them in one grid, and starts any of them with one click.

- C# / .NET 8 / WPF (MVVM with CommunityToolkit.Mvvm)
- Windows 10 and 11, x64. No admin rights needed.
- Data lives in `%AppData%\GG\`:
  - `library.json`: your games (a `library.json.bak` backup is kept on every save)
  - `settings.json`: your settings
  - `cache\`: cover art and icons
  - `logs\gg.log`: what GG did (scanners write here when a launcher isn't installed)

## Status

GG is being built in steps:

| Step | What | State |
|------|------|-------|
| 1 | Project setup, game model, JSON save/load, dark grid with test data | **done** |
| 2 | Shortcut import + launching | **done** |
| 3 | Steam, Epic, Xbox scanners | next |
| 4 | Folder scan + exe picking rules + "Needs review" dialog | |
| 5 | Cover art (SteamGridDB) and icons | |
| 6 | Playtime, favorites, recently played, search, sort, right-click menu | |
| 7 | GOG, EA, Ubisoft, Battle.net scanners | |
| 8 | Settings page, welcome screen, polish, single-exe publish | |

## Build and run

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (x64) on Windows.
Visual Studio 2022 (17.8 or newer) with the ".NET desktop development" workload also works: open `GG.sln`
and press F5 with `GG.App` as the startup project.

From a terminal in the repository folder:

```bash
dotnet build GG.sln
```

```bash
dotnet run --project src/GG.App
```

Run the tests:

```bash
dotnet test tests/GG.Tests
```

### Publish a single GG.exe

```bash
dotnet publish src/GG.App/GG.App.csproj -c Release -r win-x64 -o publish
```

This produces `publish\GG.exe`: one self-contained file (about 70 MB) that runs on any Windows 10/11 x64 PC
without installing .NET.

Every push also runs `.github/workflows/build.yml` on a Windows machine. It builds, runs the tests, and
attaches `GG.exe` to the run (GitHub > Actions > the run > Artifacts > `GG-win-x64`).

## Project layout

```
GG.sln
src/
  GG.Core/       models (Game, AppSettings), library + settings storage, logging (no Windows APIs, fully testable)
  GG.Scanners/   one scanner class per source, all implementing IGameScanner
  GG.App/        WPF app: Views/, ViewModels/, Styles/ (dark theme), Controls/, Converters/
tests/
  GG.Tests/      xUnit tests
```

### Storage details

- Saves are atomic: GG writes `library.json.tmp`, then swaps it in and keeps the previous file as `library.json.bak`.
- If `library.json` ever becomes unreadable, GG copies it to `library.corrupt-<date>.json`, loads the `.bak` instead, and logs what happened.
- Small changes (toggling a favorite, moving the size slider) are batched and written about a second later, and always on exit.

## How scanning works (so far)

- **Shortcuts:** pick a folder of `.lnk` / `.url` files (subfolders included). Each shortcut's file name becomes the game
  name. `.lnk` files are resolved to their exe, arguments and working folder. `.url` files can be `steam://`,
  `com.epicgames.launcher://` or `file://` links; web links, uninstallers and readmes are skipped.
- **Duplicates:** the same game found twice is merged into one entry, matched by a shared id (Steam appid, Epic app name),
  then by install folder, then by name with punctuation ignored. The launch details come from the highest-priority source:
  your own choice (Locate exe...) > shortcut > launcher > folder scan.
- **Rescans** keep each game's favorite flag, hidden flag, playtime and date added. A shortcut you delete disappears from
  GG on the next scan, unless it's a favorite or has playtime.
- **Launching** uses the launcher URI if there is one, otherwise the exe with its arguments and working folder. If the file
  is gone, GG offers **Locate exe...**. The exe you pick is kept forever and never overwritten by scans.

## Resetting

Close GG and delete `%AppData%\GG` to start fresh. (A brand-new library shows a few sample games until
your first scan, which removes them.)
