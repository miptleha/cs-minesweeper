using MinesweeperCore.Domain;
using MinesweeperCore.Engine;
using MinesweeperCore.Infrastructure;
using MinesweeperCore.Tests.Fakes;
using Xunit;

namespace MinesweeperCore.Tests;

/// <summary>
/// Тесты корректности подсчёта соседних мин (AdjacentMines).
/// Проверяют, что Board.PlaceMines правильно считает горизонтальных,
/// вертикальных и диагональных соседей.
/// </summary>
public class MinePlacementTests
{
    [Fact]
    public void AdjacentMines_SingleMineInCorner_NeighborsReportOne()
    {
        // Arrange: 0 мин в настройках, одну ставим вручную в (0,0)
        // через Board.PlaceMines — он же считает AdjacentMines у соседей.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(new GameSettings(9, 9, 0));
        engine.BoardForTesting.PlaceMines(new[] { new CellPoint(0, 0) });

        // Act: открываем (1,1) — соседняя с миной.
        engine.OpenCell(new CellPoint(1, 1));

        // Assert: у (1,1) AdjacentMines=1 (только (0,0)), Content=Number.
        // Если бы PlaceMines не считал диагональных соседей, (1,1) было бы Empty=0.
        var cell = engine.GetCell(new CellPoint(1, 1));
        Assert.Equal(CellState.Opened, cell.State);
        Assert.Equal(CellContent.Number, cell.Content);
        Assert.Equal(1, cell.AdjacentMines);

        // Проверяем остальных соседей мины: (0,1) и (1,0) — тоже должны
        // иметь AdjacentMines=1. Открываем их, чтобы проверить Content.
        engine.OpenCell(new CellPoint(0, 1));
        var cellRight = engine.GetCell(new CellPoint(0, 1));
        Assert.Equal(CellContent.Number, cellRight.Content);
        Assert.Equal(1, cellRight.AdjacentMines);

        engine.OpenCell(new CellPoint(1, 0));
        var cellDown = engine.GetCell(new CellPoint(1, 0));
        Assert.Equal(CellContent.Number, cellDown.Content);
        Assert.Equal(1, cellDown.AdjacentMines);
    }

    [Fact]
    public void AdjacentMines_TwoMinesAroundCell_ReportsTwo()
    {
        // Arrange: 0 мин, ставим две вручную: (0,0) и (2,2).
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(new GameSettings(9, 9, 0));
        engine.BoardForTesting.PlaceMines(new[]
        {
            new CellPoint(0, 0),
            new CellPoint(2, 2),
        });

        // Act: открываем (1,1) — соседняя с обеими минами (диагональ).
        engine.OpenCell(new CellPoint(1, 1));

        // Assert: AdjacentMines=2, Content=Number.
        // Если бы PlaceMines не считал диагональных соседей, было бы 0/Empty.
        var cell = engine.GetCell(new CellPoint(1, 1));
        Assert.Equal(CellContent.Number, cell.Content);
        Assert.Equal(2, cell.AdjacentMines);
    }
}