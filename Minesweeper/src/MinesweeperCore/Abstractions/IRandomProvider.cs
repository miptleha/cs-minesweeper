namespace MinesweeperCore.Abstractions;

/// <summary>
/// Даёт случайные числа. Нужен, чтобы в тестах подменять Random на детерминированный фейк.
/// </summary>
public interface IRandomProvider
{
    /// <summary>Возвращает случайное целое число в диапазоне [0, maxValue).</summary>
    int Next(int maxValue);

    /// <summary>Возвращает случайное целое число в диапазоне [minValue, maxValue).</summary>
    int Next(int minValue, int maxValue);
}
