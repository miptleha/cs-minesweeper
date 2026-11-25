using MinesweeperFna;

namespace MinesweeperFna;

internal static class Program
{
    /// <summary>
    /// Точка входа: создаёт игру и запускает цикл.
    /// </summary>
    internal static void Main()
    {
        using var game = new Game1();
        game.Run();
    }
}
