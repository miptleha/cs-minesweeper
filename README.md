Classic **Minesweeper** game with two equal UI implementations built on the same core:
- **WinForms** (Phase 5) — done (playable MVP).
- **MonoGame** (Phase 6) — done (playable MVP).

Both versions are **feature-equivalent**: same rules, same input layout, same
records file, same look & feel. Pick whichever you prefer — there is no
"main" or "secondary" version.

Game logic (`MinesweeperCore`) is shared between both UIs and covered by unit tests.

## For Players

You don't need to install anything! Just download the release archive from the
[Releases page](https://github.com/miptleha/cs-minesweeper/releases) and pick
**one** of the two builds:

| Archive | What it is | When to pick it |
|---|---|---|
| `MinesweeperWinForms-vX.X.X-win-x64.zip` | Native Windows Forms UI, single self-contained `.exe` | You want the simplest launch: one file, no extra folders. |
| `MinesweeperMonoGame-vX.X.X-win-x64.zip` | MonoGame (DesktopGL) UI, `.exe` + `Content/` folder | You want the sprite-based renderer, or you're on a machine where the MonoGame backend behaves better. |

Both archives are self-contained — the .NET Runtime is already bundled, nothing
to install. Extract and run the `.exe` inside.

## For Developers

To build or develop the project, you need the **.NET SDK** installed.
*Note: The solution uses the `.slnx` format, which requires **.NET SDK 9.0.100 or newer**.*

You can get the latest SDK from the official download page: [Download .NET](https://dotnet.microsoft.com/download).

Debugging is available in Visual Studio 2022 (open `slnx` file) and VS Code (open `Minesweeper` folder).

### Commands

All commands run from the `Minesweeper` folder — the one that contains `Minesweeper.slnx`.

```bash
# Build the whole solution (Debug)
dotnet build

# Run unit tests (Debug)
dotnet test

# Run the WinForms UI (Debug)
dotnet run --project src/MinesweeperWinForms

# Run the MonoGame UI (Debug)
dotnet run --project src/MinesweeperMonoGame

# Publish (Release, win-x64)
dotnet publish -c Release
```

[![hits](https://myhits.vercel.app/api/hit/https%3A%2F%2Fgithub.com%2Fmiptleha%2Fcs-minesweeper?color=blue&label=hits&size=small)](https://myhits.vercel.app)
