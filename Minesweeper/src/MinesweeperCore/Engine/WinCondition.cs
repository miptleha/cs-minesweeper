using MinesweeperCore.Domain;

namespace MinesweeperCore.Engine;

/// <summary>
/// Проверяет, все ли безопасные клетки открыты. O(1) через OpenedSafeCells.
/// </summary>
internal sealed class WinCondition
{
    /// <summary>
    /// Проверяет условие победы: все безопасные клетки открыты.
    /// O(1): board.OpenedSafeCells == board.TotalCells - mineCount.
    /// </summary>
    public static bool IsWon(Board board, int mineCount)
    {
        return board.OpenedSafeCells == board.TotalCells - mineCount;
    }
}
