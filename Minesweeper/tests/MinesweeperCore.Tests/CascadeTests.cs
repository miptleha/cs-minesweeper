using MinesweeperCore.Domain;
using MinesweeperCore.Engine;
using MinesweeperCore.Infrastructure;
using MinesweeperCore.Tests.Fakes;
using Xunit;

namespace MinesweeperCore.Tests;

/// <summary>
/// Тесты каскадного открытия — Flood Fill и события.
/// Каскад гарантирован: 0 мин в настройках, 1 мина вручную в углу (8,8),
/// первый клик в противоположном углу (0,0) — соседей-мин нет.
/// </summary>
public class CascadeTests
{
    [Fact]
    public void Cascade_9x9WithOneMineAtCorner_ClickOppositeCorner_Reveals80Cells()
    {
        // Arrange: 0 мин в настройках (валидно по §2), 1 мину ставим вручную в (8,8).
        // BoardForTesting — internal для тестов. Для каскада мы вручную ставим мину,
        // потому что первый клик не гарантирует каскад (§5).
        // PlaceMines (а не CellRef.IsMine=true): обновляет AdjacentMines у соседей.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });

        engine.StartNewGame(new GameSettings(9, 9, 0));
        engine.BoardForTesting.PlaceMines(new[] { new CellPoint(8, 8) });

        // Act: клик в (0,0) — соседей-мин нет, клетка Empty, запускается каскад.
        engine.OpenCell(new CellPoint(0, 0));

        // Assert: 9×9 = 81 клетка, 1 мина → 80 открытых.
        var opened = engine.GetAllCells().Count(c => c.State == CellState.Opened);
        Assert.Equal(80, opened);

        // Проверяем, что клетки-соседи мины имеют правильные цифры.
        // Мина в (8,8) — у (7,7), (7,8), (8,7) должен быть AdjacentMines=1.
        // Если бы PlaceMines не считал диагональных соседей,
        // (7,7).AdjacentMines был бы 0, и клетка была бы Empty, а не Number.
        var cell77 = engine.GetCell(new CellPoint(7, 7));
        Assert.Equal(CellContent.Number, cell77.Content);
        Assert.Equal(1, cell77.AdjacentMines);

        var cell78 = engine.GetCell(new CellPoint(7, 8));
        Assert.Equal(CellContent.Number, cell78.Content);
        Assert.Equal(1, cell78.AdjacentMines);

        var cell87 = engine.GetCell(new CellPoint(8, 7));
        Assert.Equal(CellContent.Number, cell87.Content);
        Assert.Equal(1, cell87.AdjacentMines);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 0)]
    [InlineData(0, 4)]
    [InlineData(4, 4)]
    public void Cascade_5x5WithCenterMine_CornerClickOpensAllSafeAndWins(int clickX, int clickY)
    {
        // Arrange: поле 5×5, 1 мина в центре (2,2). 4 угла — все безопасные.
        // Board.PlaceMines — internal, считает AdjacentMines у соседей.
        var board = new Board(5, 5);
        board.PlaceMines(new[] { new CellPoint(2, 2) });

        // Открываем угловую клетку — должна запустить каскад.
        new RevealService().RevealCell(board, new CellPoint(clickX, clickY), out _);
        new RevealService().CascadeReveal(board, new CellPoint(clickX, clickY));

        // Act + Assert: победа — все безопасные открыты (24 из 25).
        Assert.True(WinCondition.IsWon(board, 1));
        Assert.Equal(24, board.OpenedSafeCells);
    }

    [Fact]
    public void Cascade_EmitsZeroCellChanged_AndOneCellsRevealed()
    {
        // Arrange: та же сцена — каскад гарантирован.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });

        engine.StartNewGame(new GameSettings(9, 9, 0));
        engine.BoardForTesting.PlaceMines(new[] { new CellPoint(8, 8) });

        int cellChangedCount = 0;
        int cellsRevealedCount = 0;
        engine.CellChanged += (s, e) => cellChangedCount++;
        engine.CellsRevealed += (s, e) => cellsRevealedCount++;

        // Act: клик запускает каскад.
        engine.OpenCell(new CellPoint(0, 0));

        // Assert: 0 CellChanged, 1 CellsRevealed.
        Assert.Equal(0, cellChangedCount);
        Assert.Equal(1, cellsRevealedCount);
    }

    [Fact]
    public void Cascade_3x3WithZeroMines_CornerClickOpensAllAndWins()
    {
        // Arrange: 3×3, 0 мин. Первый клик в угол должен каскадом открыть все 9 клеток.
        var board = new Board(3, 3);
        new RevealService().RevealCell(board, new CellPoint(0, 0), out _);
        new RevealService().CascadeReveal(board, new CellPoint(0, 0));

        // Assert: все 9 безопасных открыты, победа.
        Assert.True(WinCondition.IsWon(board, 0));
        Assert.Equal(9, board.OpenedSafeCells);
    }

    [Fact]
    public void Cascade_9x9WithOneMine_CellsRevealedHasExactly80UniqueCells()
    {
        // Arrange: та же сцена, что и Cascade_9x9WithOneMine_ClickOppositeCorner_Reveals80Cells,
        // но ловим CellsRevealed и считаем размер/уникальность CellInfos.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(new GameSettings(9, 9, 0));
        engine.BoardForTesting.PlaceMines(new[] { new CellPoint(8, 8) });

        List<CellPoint>? revealedPositions = null;
        engine.CellsRevealed += (s, e) =>
            revealedPositions = e.CellInfos.Select(c => c.Position).ToList();

        // Act: клик в (0,0) — каскад.
        engine.OpenCell(new CellPoint(0, 0));

        // Assert: 80 уникальных клеток (9×9 минус мина).
        // До фикса T49.1 Empty-клетки попадали в список дважды — было бы ~157 записей.
        Assert.NotNull(revealedPositions);
        Assert.Equal(80, revealedPositions!.Count);
        Assert.Equal(80, revealedPositions.Distinct().Count());
    }
}