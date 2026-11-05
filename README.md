Classic **Minesweeper** game. Two UI implementations are planned:
- **WinForms** (Phase 5) — in progress. Specification: `docs/09-PHASE-5-WINFROMS.md`.
- **MonoGame** (Phase 6) — planned.

Game logic (`MinesweeperCore`) is complete and covered by unit tests.

To run or develop the project, you need `.NET 8` from Microsoft.  
- Use the **.NET 8 SDK** for building, debugging, and publishing.  
- Use the **.NET Desktop Runtime** if you only want to run the compiled game.

You can get the correct package from the official download page: [Download .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).  
On the page, just pick the option that matches your needs (SDK or Desktop Runtime).

Debugging is available in Visual Studio 2022 (open `slnx` file) and VS Code (open `Minesweeper` folder).

## Commands

All commands run from the `Minesweeper` folder — the one that contains
`Minesweeper.slnx`. The `.slnx` solution format requires .NET SDK 9.0.100 or
later; target frameworks are `net8.0` / `net8.0-windows`.

```bash
# Build the whole solution (Debug)
dotnet build

# Run unit tests (Debug)
dotnet test

# Run the WinForms UI (Phase 5)
dotnet run --project src/MinesweeperWinForms

# Run the MonoGame UI (Phase 6)
dotnet run --project src/MinesweeperMonoGame

# Publish UI projects to dist/ (Release, win-x64)
dotnet publish -c Release
```

[![hits](https://myhits.vercel.app/api/hit/https%3A%2F%2Fgithub.com%2Fmiptleha%2Fcs-minesweeper?color=blue&label=hits&size=small)](https://myhits.vercel.app)
