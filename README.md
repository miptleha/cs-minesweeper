Classic **Minesweeper** game. It has two implementations: WinForms and MonoGame.

To run or develop the project, you need `.NET 8` from Microsoft.  
- Use the **.NET 8 SDK** for building, debugging, and publishing.  
- Use the **.NET Desktop Runtime** if you only want to run the compiled game.

You can get the correct package from the official download page: [Download .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).  
On the page, just pick the option that matches your needs (SDK or Desktop Runtime).

Debugging is available in Visual Studio 2022 (open slnx file) and VS Code (open Minesweeper folder).  
For publishing, run in `Minesweeper` folder:
```bash
dotnet publish -c Release
```
