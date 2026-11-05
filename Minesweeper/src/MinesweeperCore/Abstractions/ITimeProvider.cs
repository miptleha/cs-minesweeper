namespace MinesweeperCore.Abstractions;

/// <summary>
/// Абстракция времени для тестируемости.
/// Ядро не использует DateTime.UtcNow напрямую.
/// </summary>
public interface ITimeProvider
{
    /// <summary>
    /// Возвращает текущее время UTC.
    /// </summary>
    DateTime GetUtcNow();
}
