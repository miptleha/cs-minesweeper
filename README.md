Classic **Minesweeper** game. Two UI implementations:
- **WinForms** (Phase 5) — done (playable MVP).
- **MonoGame** (Phase 6) — planned.

Game logic (`MinesweeperCore`) is complete and covered by unit tests.

## For Players

You don't need to install anything! Just download the latest `Minesweeper-vX.X.X-win-x64.zip` from the [Releases page](https://github.com/miptleha/cs-minesweeper/releases), extract it, and run `MinesweeperWinForms.exe`. 

The game is shipped as a self-contained single file, so the .NET Runtime is already bundled inside.

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
