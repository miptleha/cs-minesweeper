namespace MinesweeperCore.Infrastructure;

using MinesweeperCore.Abstractions;

/// <summary>
/// Реализация ITimeProvider на основе DateTime.UtcNow.
/// </summary>
public sealed class SystemTimeProvider : ITimeProvider
{
    public DateTime GetUtcNow() => DateTime.UtcNow;
}
