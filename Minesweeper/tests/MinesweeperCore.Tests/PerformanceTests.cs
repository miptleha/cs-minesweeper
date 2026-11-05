using System.Diagnostics;
using MinesweeperCore.Domain;
using MinesweeperCore.Engine;
using MinesweeperCore.Infrastructure;
using MinesweeperCore.Tests.Fakes;
using Xunit;

namespace MinesweeperCore.Tests;

/// <summary>
/// Тесты производительности: скорость клика не зависит от размера поля.
/// </summary>
public class PerformanceTests
{
    [Fact]
    public void Speed_30x24WithManyMines_RandomClicksUntilLost_Under1Second()
    {
        // Arrange: 30×24, 500 мин (плотность ~69%). Seed 42.
        // Замер включает StartNewGame + первый клик (ленивое создание поля).
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(42),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        var settings = new GameSettings(30, 24, 500);
        var rnd = new Random(42);
        var sw = Stopwatch.StartNew();

        // Act: первый клик + случайные клики до Lost. При 69% плотности
        // Lost наступает за 1–3 клика.
        engine.StartNewGame(settings);
        engine.OpenCell(new CellPoint(0, 0));
        int extraClicks = 0;
        for (int i = 0; i < 1000 && engine.State == GameState.InProgress; i++)
        {
            engine.OpenCell(new CellPoint(rnd.Next(30), rnd.Next(24)));
            extraClicks++;
        }

        sw.Stop();

        // Assert: Lost, всё в 1 с.
        Assert.Equal(GameState.Lost, engine.State);
        Assert.True(sw.ElapsedMilliseconds < 1000,
            $"Слишком медленно: {sw.ElapsedMilliseconds} мс, кликов после первого={extraClicks}.");
    }

    [Fact]
    public void Speed_30x24WithFewMines_OpenAllSafeCellsUntilWon_Under1Second()
    {
        // Arrange: 30×24, 1 мина. Seed 42. Замер включает StartNewGame + все клики.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(42),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        var settings = new GameSettings(30, 24, 1);
        var sw = Stopwatch.StartNew();

        // Act: первый клик запускает игру и каскад, дальше открываем все
        // безопасные Closed-клетки через BoardForTesting. Клик по мине
        // невозможен: проверяем !IsMine перед OpenCell.
        engine.StartNewGame(settings);
        engine.OpenCell(new CellPoint(0, 0));

        var board = engine.BoardForTesting;
        int extraClicks = 0;
        for (int x = 0; x < board.Width && engine.State == GameState.InProgress; x++)
            for (int y = 0; y < board.Height && engine.State == GameState.InProgress; y++)
            {
                var cell = board[x, y];
                if (!cell.IsMine && cell.State == CellState.Closed)
                {
                    engine.OpenCell(new CellPoint(x, y));
                    extraClicks++;
                }
            }

        sw.Stop();

        // Assert: Won, всё в 1 с.
        Assert.Equal(GameState.Won, engine.State);
        Assert.True(sw.ElapsedMilliseconds < 1000,
            $"Слишком медленно: {sw.ElapsedMilliseconds} мс, кликов после первого={extraClicks}.");
    }

    [Fact]
    public void Stress_1000x1000With100kMines_CreateAndFirstOpen_Under5Seconds()
    {
        // Arrange: 1000×1000 (1M клеток), 100 000 мин — плотность 10%. Seed 42.
        // Замер — StartNewGame + первый OpenCell (ленивое создание поля + PlaceMines
        // + возможный каскад от (0,0)). Это верхняя граница игрового поля:
        // 1000×1000 >> 30×16 (Expert) и 9×9 (Beginner), но не требует гигабайт памяти.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(42),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        var settings = new GameSettings(1000, 1000, 100_000);
        var sw = Stopwatch.StartNew();

        // Act: Board + PlaceMines + первый клик (первый клик безопасен по §5).
        engine.StartNewGame(settings);
        engine.OpenCell(new CellPoint(0, 0));

        sw.Stop();

        // Assert: InProgress, первый клик безопасен, всё в 5 с.
        Assert.Equal(GameState.InProgress, engine.State);
        var firstCell = engine.GetCell(new CellPoint(0, 0));
        Assert.Equal(CellState.Opened, firstCell.State);
        Assert.NotEqual(CellContent.Mine, firstCell.Content);
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"Слишком медленно: {sw.ElapsedMilliseconds} мс для 1000×1000 с 100 000 мин.");
    }
}    