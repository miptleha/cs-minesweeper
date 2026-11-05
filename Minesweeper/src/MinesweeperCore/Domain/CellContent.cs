namespace MinesweeperCore.Domain;

/// <summary>
/// Содержимое открытой клетки.
/// </summary>
public enum CellContent
{
    /// <summary>Клетка пуста (0 мин вокруг).</summary>
    Empty,

    /// <summary>Цифра — количество мин среди соседей (1–8).</summary>
    Number,

    /// <summary>В клетке мина.</summary>
    Mine
}
