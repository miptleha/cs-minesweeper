using MinesweeperCore.Abstractions;

namespace MinesweeperCore.Tests.Fakes;

/// <summary>
/// Детерминированный провайдер случайности для тестов.
/// </summary>
public sealed class FakeRandomProvider : IRandomProvider
{
    private readonly Random _random;

    public FakeRandomProvider(int? seed = null)
    {
        if (seed == null)
            throw new InvalidOperationException("FakeRandomProvider требует seed для детерминизма");
        _random = new Random(seed.Value);
    }

    public int Next(int maxValue) => _random.Next(maxValue);

    public int Next(int minValue, int maxValue) => _random.Next(minValue, maxValue);

    /// <summary>
    /// Создаёт провайдер с фиксированным seed для воспроизводимости.
    /// </summary>
    public static FakeRandomProvider WithSeed(int seed) => new(seed);
}
