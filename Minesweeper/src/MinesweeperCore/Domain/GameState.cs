namespace MinesweeperCore.Domain;

/// <summary>
/// Текущее состояние игровой партии.
/// </summary>
public enum GameState
{
    /// <summary>Поле подготовлено, ни одна клетка не открыта.</summary>
    NotStarted,

    /// <summary>Игра в процессе — открыта хотя бы одна клетка.</summary>
    InProgress,

    /// <summary>Все безопасные клетки открыты — победа.</summary>
    Won,

    /// <summary>Открыта мина — поражение.</summary>
    Lost
}
