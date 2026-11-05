namespace MinesweeperCore.Domain;

/// <summary>
/// Координаты клетки на игровом поле (столбец, строка).
/// Начало координат (0, 0) — верхний левый угол.
/// </summary>
public readonly record struct CellPoint(int X, int Y)
{
    /// <summary>
    /// Возвращает всех соседей данной точки (до 8 штук).
    /// Соседние клетки — по горизонтали, вертикали и диагонали.
    /// </summary>
    public IEnumerable<CellPoint> Neighbors()
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                yield return new CellPoint(X + dx, Y + dy);
            }
        }
    }
}
