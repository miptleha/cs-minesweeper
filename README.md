Classic **Minesweeper** game with three equal UI implementations built on the same core: **WinForms**, **MonoGame**, **WPF**, **Avalonia**.

All versions are **feature-equivalent**: same rules, same input layout, same
records file, same look & feel. Pick whichever you prefer — there is no
"main" or "secondary" version.

Game logic (`MinesweeperCore`) is shared between all UIs and covered by unit tests.  
More information in the [documents](Minesweeper/docs) folder.

## For Players

You don't need to install anything! Just download the release archive from the
[Releases page](https://github.com/miptleha/cs-minesweeper/releases) and pick
**one** of the builds:

| Archive | What it is | When to pick it |
|---|---|---|
| `MinesweeperWinForms-vX.X.X-win-x64.zip` | Native Windows Forms UI, single self-contained `.exe` | You want the simplest launch: one file, no extra folders. |
| `MinesweeperMonoGame-vX.X.X-win-x64.zip` | MonoGame (DesktopGL) UI, `.exe` + `Content/` folder | You want the sprite-based renderer, or you're on a machine where the MonoGame backend behaves better. |
| `MinesweeperWpf-vX.X.X-win-x64.zip` | WPF (Windows Presentation Foundation) UI, single self-contained `.exe` | You prefer modern XAML/MVVM rendering and smooth native Windows controls. |
| `MinesweeperAvalonia-vX.X.X-win-x64.zip` | Avalonia UI, single self-contained `.exe` | You want a cross-platform XAML UI with smooth rendering. |
| `MinesweeperBlazorHybrid-vX.X.X-win-x64.zip` | Blazor Hybrid UI, `.exe` + `wwwroot/` folder | You want a web-based UI inside a native desktop window. |

All archives are self-contained — the .NET Runtime is already bundled, nothing
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

# Run the WPF UI (Debug)
dotnet run --project src/MinesweeperWpf

# Run the Avalonia UI (Debug)
dotnet run --project src/MinesweeperAvalonia

# Publish (Release, win-x64)
dotnet publish -c Release
```

[![hits](https://myhits.vercel.app/api/hit/https%3A%2F%2Fgithub.com%2Fmiptleha%2Fcs-minesweeper?color=blue&label=hits&size=small)](https://myhits.vercel.app)
