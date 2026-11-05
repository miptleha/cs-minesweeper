using MinesweeperCore.Domain;
using MinesweeperCore.Engine;
using MinesweeperCore.Events;
using MinesweeperCore.Infrastructure;
using MinesweeperCore.Tests.Fakes;
using Xunit;

namespace MinesweeperCore.Tests;

/// <summary>
/// Тесты на циклическое переключение меток — CycleMark.
/// </summary>
public class FlagTests
{
    [Fact]
    public void CycleMark_ClosedCell_BecomesFlagged()
    {
        // Arrange: Beginner (9×9, 10 мин). Seed 1 — каскад не прочистит всё поле,
        // поэтому после OpenCell(0,0) найдётся Closed-клетка для флага.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));

        var board = engine.BoardForTesting;
        CellPoint target = default;
        bool found = false;
        for (int x = 0; x < board.Width && !found; x++)
            for (int y = 0; y < board.Height && !found; y++)
                if (board[x, y].State == CellState.Closed)
                {
                    target = new CellPoint(x, y);
                    found = true;
                }
        Assert.True(found, "Не нашли ни одной Closed-клетки после первого клика.");

        // Act: ставим флаг на гарантированно Closed-клетку (Closed → Flagged).
        engine.CycleMark(target);

        // Assert: клетка переходит в Flagged.
        var cell = engine.GetCell(target);
        Assert.Equal(CellState.Flagged, cell.State);
    }

    [Fact]
    public void CycleMark_FlaggedCell_BecomesQuestion()
    {
        // Arrange: Beginner (9×9, 10 мин). Seed 1 — каскад не прочистит всё поле.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));

        var board = engine.BoardForTesting;
        CellPoint target = default;
        bool found = false;
        for (int x = 0; x < board.Width && !found; x++)
            for (int y = 0; y < board.Height && !found; y++)
                if (board[x, y].State == CellState.Closed)
                {
                    target = new CellPoint(x, y);
                    found = true;
                }
        Assert.True(found, "Не нашли ни одной Closed-клетки после первого клика.");

        // Ставим флаг (Closed → Flagged).
        engine.CycleMark(target);

        // Act: второй правый клик — Flagged → Question.
        engine.CycleMark(target);

        // Assert: клетка в состоянии Question, а не Closed.
        var cell = engine.GetCell(target);
        Assert.Equal(CellState.Question, cell.State);
    }

    [Fact]
    public void CycleMark_QuestionCell_BecomesClosed()
    {
        // Arrange: Beginner (9×9, 10 мин). Seed 1. Находим Closed и крутим метку 2 раза
        // (Closed → Flagged → Question).
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));

        var board = engine.BoardForTesting;
        CellPoint target = default;
        bool found = false;
        for (int x = 0; x < board.Width && !found; x++)
            for (int y = 0; y < board.Height && !found; y++)
                if (board[x, y].State == CellState.Closed)
                {
                    target = new CellPoint(x, y);
                    found = true;
                }
        Assert.True(found);

        engine.CycleMark(target); // → Flagged
        engine.CycleMark(target); // → Question

        // Act: третий правый клик — Question → Closed.
        engine.CycleMark(target);

        // Assert: клетка вернулась в Closed (полный цикл).
        var cell = engine.GetCell(target);
        Assert.Equal(CellState.Closed, cell.State);
    }

    [Fact]
    public void CycleMark_OpenedCell_Ignored()
    {
        // Arrange: поле 9×9. Seed 1. Открываем клетку (0,0).
        var random = FakeRandomProvider.WithSeed(1);
        var store = new InMemoryRecordStore();
        var timeProvider = new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) };
        var engine = new GameEngine(random, store, timeProvider);

        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));

        int cellChangedBefore = 0;
        engine.CellChanged += (s, e) => cellChangedBefore++;

        // Act: пытаемся поставить метку на открытую клетку.
        engine.CycleMark(new CellPoint(0, 0));

        // Assert: состояние не меняется, событие не эмитится.
        var cell = engine.GetCell(new CellPoint(0, 0));
        Assert.Equal(CellState.Opened, cell.State);
        Assert.Equal(0, cellChangedBefore);
    }

    [Fact]
    public void OpenCell_FirstClickOnFlagged_DoesNotStartGame()
    {
        // Arrange: создаём движок, не вызываем StartNewGame — состояние NotStarted.
        // Ставим флаг на (0,0) через CycleMark.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });

        int boardGeneratedCount = 0;
        engine.BoardGenerated += (s, e) => boardGeneratedCount++;

        // Ставим флаг: Closed → Flagged.
        engine.CycleMark(new CellPoint(0, 0));
        Assert.Equal(CellState.Flagged, engine.GetCell(new CellPoint(0, 0)).State);

        // Act: пытаемся открыть Flagged-клетку — не должна запустить игру.
        engine.OpenCell(new CellPoint(0, 0));

        // Assert: GameState остаётся NotStarted, BoardGenerated не эмитился,
        // клетка осталась Flagged.
        Assert.Equal(GameState.NotStarted, engine.State);
        Assert.Equal(0, boardGeneratedCount);
        Assert.Equal(CellState.Flagged, engine.GetCell(new CellPoint(0, 0)).State);
    }

    [Fact]
    public void OpenCell_FirstClickOnQuestion_StartsAndOpens()
    {
        // Arrange: создаём движок, не вызываем StartNewGame — состояние NotStarted.
        // Крутим (0,0) 2 раза: Closed → Flagged → Question.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });

        int boardGeneratedCount = 0;
        engine.BoardGenerated += (s, e) => boardGeneratedCount++;

        engine.CycleMark(new CellPoint(0, 0)); // → Flagged
        engine.CycleMark(new CellPoint(0, 0)); // → Question
        Assert.Equal(CellState.Question, engine.GetCell(new CellPoint(0, 0)).State);

        // Act: клик левой кнопкой по Question — должен запустить игру и открыть клетку.
        engine.OpenCell(new CellPoint(0, 0));

        // Assert: GameState == InProgress, BoardGenerated эмитился, клетка Opened.
        Assert.Equal(GameState.InProgress, engine.State);
        Assert.Equal(1, boardGeneratedCount);
        Assert.Equal(CellState.Opened, engine.GetCell(new CellPoint(0, 0)).State);
    }

    [Fact]
    public void CycleMark_BeforeFirstClick_SetsFlagAndKeepsNotStarted()
    {
        // Arrange: движок создан, StartNewGame не вызван — состояние NotStarted.
        // Per T41.1: CycleMark разрешён в NotStarted, меняет метку, эмитит CellChanged.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });

        int cellChangedCount = 0;
        engine.CellChanged += (s, e) => cellChangedCount++;

        // Act: ставим метку до первого клика.
        engine.CycleMark(new CellPoint(0, 0));

        // Assert: CellChanged эмитится 1 раз, GameState остаётся NotStarted.
        Assert.Equal(1, cellChangedCount);
        Assert.Equal(GameState.NotStarted, engine.State);
        Assert.Equal(CellState.Flagged, engine.GetCell(new CellPoint(0, 0)).State);
    }

    [Fact]
    public void CycleMark_CellChangedPayload_IsConsistent()
    {
        // Arrange: Beginner (9×9, 10 мин). Seed 1. Ставим флаг на Closed-клетку.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);

        var captured = new List<CellChangedEventArgs>();
        engine.CellChanged += (s, e) => captured.Add(e);

        // Act: Closed → Flagged.
        var point = new CellPoint(4, 4);
        engine.CycleMark(point);

        // Assert: payload согласован — Change и CellInfo описывают один переход.
        // Тесты на «ровно один CellChanged» не ловят перепутанные PreviousState/NewState.
        var single = Assert.Single(captured);
        Assert.Equal(point, single.Change.Position);
        Assert.Equal(CellState.Closed, single.Change.PreviousState);
        Assert.Equal(CellState.Flagged, single.Change.NewState);
        Assert.Equal(point, single.CellInfo.Position);
        Assert.Equal(CellState.Flagged, single.CellInfo.State);
        Assert.Equal(CellContent.Empty, single.CellInfo.Content);
    }
}
