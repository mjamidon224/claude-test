# Spider Solitaire for Windows 11

Spider Solitaire with no ads, no accounts and no internet connection. The table, rules,
scoring, menus and shortcuts are modelled on the version that shipped with Windows 7, in a
single Windows desktop app.

## Download

Every push builds the game in GitHub Actions. Open the
[Actions tab](https://github.com/mjamidon224/claude-test/actions), pick the newest
**Build Spider Solitaire** run and download one of its artifacts:

| Artifact | Needs .NET installed? |
| --- | --- |
| `SpiderSolitaire-win-x64` | No — everything is bundled |
| `SpiderSolitaire-win-arm64` | No — for Arm devices (Surface Pro X, Snapdragon laptops) |
| `SpiderSolitaire-win-x64-requires-dotnet8` | Yes — the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |

Artifacts arrive as a `.zip`; unzip it and run `SpiderSolitaire.exe`. Windows SmartScreen
may warn the first time, since the exe is not code-signed: choose **More info → Run anyway**.

Pushing a tag that starts with `spider-v` (for example `git tag spider-v1.0.0 && git push
origin spider-v1.0.0`) also attaches the executables to a GitHub release.

## How it plays

- **Difficulty**: Beginner (one suit), Intermediate (two suits) or Advanced (four suits),
  asked on the first run and changed under *Game → Options*.
- **The deal**: 54 cards in ten columns, top cards face up, and 50 in the stock in the
  bottom right, shown as one pile per remaining deal.
- **Moving**: a card goes on any card one rank higher, whatever its suit; a run of one suit
  moves as a unit; anything goes into an empty column. Drag cards, or click a card to send
  it and the cards on it to the best spot (its own suit first, then any suit, then a space).
  Face-down cards turn over when uncovered.
- **Dealing**: click the stock or press **D**. As in Windows, you can't deal while a
  column is empty. The one exception is when fewer than ten cards are left on the table,
  where the rule would otherwise make the game impossible to finish.
- **Finished suits**: a King-to-Ace run in one suit flies off to the bottom left by itself.
- **Scoring**: start on 500, lose a point per move (deals and undos count), gain 100 per
  finished suit.
- **Undo** (Ctrl+Z) as far back as the start of the game, including after resuming a
  saved game. Undoing a finished suit takes its 100 points back too.
- **Hint** (**H**): highlights a move worth making, then where it goes; press again for the
  next one. Moves that uncover a card or empty a column come first, and pointless shuffles
  are left out. With no moves left it points at the stock, or offers to undo, restart or
  start over.
- **Winning**: fireworks, then your score, time and record, with *Play again*.

### Menus and keys

| Key | Action |
| --- | --- |
| F2 | New game |
| Ctrl+Z | Undo |
| H | Hint |
| D | Deal a new row |
| F4 | Statistics |
| F5 | Options |
| F7 | Change appearance |
| F1 | How to play |

*Game → Restart This Game* replays the same deal from the start.

### Statistics

Kept separately for each difficulty: games played and won, win rate, longest winning and
losing streaks, current streak, fastest win and the five best scores. As in Windows, a
game counts once it is won, or once it is given up after at least one move: starting a new
game, restarting, or exiting without saving all count as a loss. *Reset* clears one
difficulty.

### Options and appearance

- Play animations (dealing, moving, cards turning over, finished suits, fireworks)
- Play sounds (synthesised in the app, so there are no audio files to ship)
- Always continue saved games, instead of asking at startup
- Always save game on exit, instead of asking
- Five card backs (classic blue, classic red, emerald, spider web, midnight) and five
  table colours

The window remembers its size and position, and the cards scale to fit any window size or
display scaling.

## Where it keeps things

Everything lives in `%APPDATA%\SpiderSolitaire` as plain JSON:

| File | Contents |
| --- | --- |
| `settings.json` | Difficulty, options, appearance, window position |
| `statistics.json` | Per-difficulty statistics and best scores |
| `saved-game.json` | A game saved on exit, deleted as soon as it is resumed |

A missing or damaged file just means starting with defaults. Nothing is sent anywhere.

## Build and run

Needs the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows.

```powershell
git clone https://github.com/mjamidon224/claude-test.git
cd claude-test
dotnet build SpiderSolitaire.sln -c Release
.\src\SpiderSolitaire\bin\Release\net8.0-windows\SpiderSolitaire.exe
```

Or open `SpiderSolitaire.sln` in Visual Studio 2022 and press F5. For a single exe that
runs without .NET installed:

```powershell
.\publish-spider.ps1                      # publish\SpiderSolitaire\SpiderSolitaire.exe, x64
.\publish-spider.ps1 -Runtime win-arm64   # for Arm devices
```

The game rules live in a separate library with no Windows dependency, and their tests run
on any OS:

```
dotnet test tests/SpiderSolitaire.Core.Tests
```

## Project layout

| Path | Purpose |
| --- | --- |
| `src/SpiderSolitaire.Core/` | The rules: cards, seeded shuffle, board, moves, deals, undo, scoring, hints, save format, statistics |
| `src/SpiderSolitaire/MainForm.cs` | Window, menus and shortcuts, clock, new/restart/save/resume/win flow |
| `src/SpiderSolitaire/BoardView.cs` | The table: mouse input, drag and drop, click-to-move, animations, hints |
| `src/SpiderSolitaire/Rendering/` | Card faces and backs drawn from vectors, table layout, painting, fireworks |
| `src/SpiderSolitaire/Dialogs/` | Difficulty, options, statistics, appearance, help and prompt dialogs |
| `src/SpiderSolitaire/SoundEffects.cs` | Synthesised sound effects |
| `src/SpiderSolitaire/Storage.cs` | Settings, statistics and saved game on disk |
| `tests/SpiderSolitaire.Core.Tests/` | Tests for the rules, including whole games played to the end |

No NuGet packages are used by the game itself; the tests use xUnit.

## Not covered

Keyboard-only play (moving cards with the arrow keys), the Windows game's "show tips"
messages, and touch gestures beyond what a mouse click or drag already does.
