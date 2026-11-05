using MinesweeperCore.Domain;
using MinesweeperCore.Engine;
using MinesweeperCore.Infrastructure;
using MinesweeperCore.Tests.Fakes;
using Xunit;

namespace MinesweeperCore.Tests;

public class GameSettingsTests
{

    [Theory]
    [InlineData(3, 3, 0)]      // минимальный размер, 0 мин — валидно
    [InlineData(3, 3, 4)]      // минимальный размер, максимум мин для 3×3
    [InlineData(1000, 1000, 0)]// максимальный размер, 0 мин
    public void GameSettings_AcceptsValidBoundaryValues(int w, int h, int mines)
    {
        // Arrange + Act: на границах и внутри диапазона — не бросает.
        var settings = new GameSettings(w, h, mines);

        // Assert: параметры сохранены как есть.
        Assert.Equal(w, settings.Width);
        Assert.Equal(h, settings.Height);
        Assert.Equal(mines, settings.MineCount);
    }

    [Theory]
    [InlineData(2, 9, 0)]      // ширина меньше 3
    [InlineData(9, 2, 0)]      // высота меньше 3
    [InlineData(1001, 9, 0)]   // ширина больше 1000
    [InlineData(9, 1001, 0)]   // высота больше 1000
    [InlineData(3, 3, 5)]      // мин больше (3-1)*(3-1) = 4
    public void GameSettings_RejectsOutOfRangeValues(int w, int h, int mines)
    {
        // Arrange + Act + Assert: на границах и вне — ArgumentOutOfRangeException.
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameSettings(w, h, mines));
    }
}