using MinesweeperCore.Abstractions;
using MinesweeperCore.Domain;

namespace MinesweeperCore.Engine;

/// <summary>
/// Расставляет мины при первом клике (лениво). Классическое поведение:
/// безопасна только клетка первого клика, соседи могут содержать мины.
/// </summary>
internal sealed class MinePlacement
{
    private readonly IRandomProvider _random;

    public MinePlacement(IRandomProvider random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <summary>
    /// Расставляет мины на поле, исключая из расстановки только клетку первого клика
    /// (классическое поведение: соседи могут содержать мины).
    /// </summary>
    public void PlaceMines(Board board, CellPoint safePoint, int mineCount)
    {
        int w = board.Width, h = board.Height;

        // Массив вместо List: избегаем реаллокаций и List.Add в цикле на весь Board.
        var availableCells = new CellPoint[board.TotalCells - 1];
        int idx = 0;
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                // Прямое сравнение struct — дешевле, чем HashSet.Contains при обходе всей доски.
                if (x == safePoint.X && y == safePoint.Y)
                    continue;
                availableCells[idx++] = new CellPoint(x, y);
            }
        }

        // Защита в глубину: если мин больше, чем доступных клеток — это внутренняя
        // ошибка (GameSettings должен был отсечь), но лучше явный exception,
        // чем ArgumentOutOfRangeException в цикле расстановки.
        if (mineCount > availableCells.Length)
            throw new InvalidOperationException(
                $"Нельзя расставить {mineCount} мин: доступно только {availableCells.Length} клеток.");

        // Partial Fisher-Yates: ровно mineCount swaps — на порядок меньше, чем
        // полное перемешивание всей доски.
        for (int i = 0; i < mineCount; i++)
        {
            int j = i + _random.Next(availableCells.Length - i);
            (availableCells[i], availableCells[j]) = (availableCells[j], availableCells[i]);
        }

        // Расстановка + AdjacentMines за один проход: обходим mineCount мин,
        // а не всё поле (см. Board.PlaceMines).
        board.PlaceMines(availableCells.AsSpan(0, mineCount));
    }
}