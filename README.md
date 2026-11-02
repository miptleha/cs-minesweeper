Classic **Minesweeper** game. It has two implementations: WinForms and MonoGame.

Download from the official Microsoft page: [Download .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).  
On the download page, choose the correct package:
- Select **SDK** if you plan to build or debug the project.
- Select **.NET Desktop Runtime** if you only need to run the compiled game.

Debugging is available in Visual Studio 2022 and VS Code.  
For publishing, run:
```bash
dotnet publish -c Release
