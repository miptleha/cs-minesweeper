using MinesweeperCore.Abstractions;

namespace MinesweeperCore.Infrastructure;

/// <summary>
/// Реализация IRandomProvider на основе System.Random.
/// </summary>
public sealed class SystemRandomProvider : IRandomProvider
{
    private readonly Random _random;

    /// <summary>
    /// Создаёт провайдер. Если seed не указан, используется
    /// системное время как seed (новый Random()).
    /// </summary>
    public SystemRandomProvider(int? seed = null)
    {
        _random = seed is null ? new Random() : new Random(seed.Value);
    }

    /// <summary>
    /// Возвращает случайное целое число в диапазоне [0, maxValue).
    /// </summary>
    public int Next(int maxValue) => _random.Next(maxValue);

    /// <summary>
    /// Возвращает случайное целое число в диапазоне [minValue, maxValue).
    /// </summary>
    public int Next(int minValue, int maxValue) => _random.Next(minValue, maxValue);
}
