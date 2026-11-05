using MinesweeperCore.Abstractions;

namespace MinesweeperCore.Tests.Fakes;

/// <summary>
/// Фейковый таймпровайдер для детерминированных тестов.
/// Время управляется вручную через свойство UtcNow.
/// </summary>
public sealed class FakeTimeProvider : ITimeProvider
{
    public DateTime UtcNow { get; set; }

    public DateTime GetUtcNow() => UtcNow;

    /// <summary>
    /// Сдвигает время на указанное количество секунд.
    /// </summary>
    public void AdvanceSeconds(double seconds)
    {
        UtcNow = UtcNow.AddSeconds(seconds);
    }
}
