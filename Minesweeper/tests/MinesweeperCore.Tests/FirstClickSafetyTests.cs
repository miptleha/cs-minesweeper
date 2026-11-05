using MinesweeperCore.Abstractions;
using MinesweeperCore.Domain;
using MinesweeperCore.Engine;
using MinesweeperCore.Infrastructure;
using MinesweeperCore.Tests.Fakes;
using Xunit;

namespace MinesweeperCore.Tests;

/// <summary>
/// Тесты безопасности первого клика — мина не попадает в кликнутую клетку.
/// Классическое поведение: соседи кликнутой клетки могут содержать мины.
/// </summary>
public class FirstClickSafetyTests
{
    [Fact]
    public void FirstClick_ClickedCell_IsNotMine()
    {
        // Arrange: поле 9×9, 10 мин. Seed 42 — детерминированная расстановка.
        var random = FakeRandomProvider.WithSeed(42);
        var store = new InMemoryRecordStore();
        var timeProvider = new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) };
        var engine = new GameEngine(random, store, timeProvider);

        engine.StartNewGame(DifficultyPresets.Beginner);

        // Act: открываем клетку (0,0) — первый клик.
        engine.OpenCell(new CellPoint(0, 0));

        // Assert: клетка открыта и не содержит мины.
        var cell = engine.GetCell(new CellPoint(0, 0));
        Assert.Equal(CellState.Opened, cell.State);
        Assert.NotEqual(CellContent.Mine, cell.Content);
    }

    [Fact]
    public void FirstClick_StartsTimer()
    {
        // Arrange: поле 9×9. Seed 1. FakeTimeProvider с фиксированным временем.
        var random = FakeRandomProvider.WithSeed(1);
        var store = new InMemoryRecordStore();
        var timeProvider = new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1, 0, 0, 0) };
        var engine = new GameEngine(random, store, timeProvider);

        engine.StartNewGame(DifficultyPresets.Beginner);

        // Act: первый клик запускает таймер.
        engine.OpenCell(new CellPoint(0, 0));

        // Assert: состояние InProgress.
        // Elapsed = 0, потому что _startTime = UtcNow, и разница = 0.
        Assert.Equal(GameState.InProgress, engine.State);

        // Сдвигаем время — Elapsed должен вырасти.
        timeProvider.AdvanceSeconds(5);
        Assert.True(engine.Elapsed >= TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void OpenCell_InvalidFirstClick_GameStaysNotStarted()
    {
        // Arrange: поле 9×9, движок запущен.
        var random = FakeRandomProvider.WithSeed(1);
        var store = new InMemoryRecordStore();
        var timeProvider = new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) };
        var engine = new GameEngine(random, store, timeProvider);
        engine.StartNewGame(DifficultyPresets.Beginner);

        bool boardGeneratedFired = false;
        engine.BoardGenerated += (s, e) => boardGeneratedFired = true;

        // Act: клик за пределами поля.
        engine.OpenCell(new CellPoint(-1, 0));

        // Assert: игра не запустилась, событий нет.
        Assert.Equal(GameState.NotStarted, engine.State);
        Assert.False(boardGeneratedFired);
    }

    [Fact]
    public void FirstClick_NotAlwaysCascade_NeighborsCanBeMines()
    {
        // Arrange: перебираем seeds. Классическое поведение: соседи первого клика
        // могут содержать мины, значит при некоторых seeds клетка (4,4) — Number,
        // а не Empty. Если ни один seed из 100 не даёт Number рядом с центром —
        // значит код всё ещё исключает всех соседей (баг).
        // Act + Assert: нашли Number → тест прошёл.
        for (int seed = 1; seed <= 100; seed++)
        {
            var engine = new GameEngine(
                FakeRandomProvider.WithSeed(seed),
                new InMemoryRecordStore(),
                new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
            engine.StartNewGame(DifficultyPresets.Beginner);
            engine.OpenCell(new CellPoint(4, 4));

            var cell = engine.GetCell(new CellPoint(4, 4));
            if (cell.Content == CellContent.Number)
                return;
        }

        Assert.Fail("Ни один seed из 100 не дал Number рядом с (4,4) — " +
                    "вероятно, MinePlacement всё ещё исключает соседей из расстановки.");
    }

    [Fact]
    public void GetCell_OutOfBounds_ReturnsClosedCell()
    {
        // Arrange: движок запущен.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);

        // Act: запрашиваем клетку за пределами поля.
        var cell = engine.GetCell(new CellPoint(-1, 0));

        // Assert: возвращается default Closed cell, не бросает.
        Assert.Equal(CellState.Closed, cell.State);
        Assert.Equal(new CellPoint(-1, 0), cell.Position);
    }

    [Fact]
    public void OpenCell_FirstClick_EmitsBoardGeneratedBeforeRevealEvents()
    {
        // Arrange: Beginner (9×9, 10 мин). Seed 1. Собираем последовательность событий.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);

        var order = new List<string>();
        engine.BoardGenerated += (s, e) => order.Add("BoardGenerated");
        engine.CellChanged += (s, e) => order.Add("CellChanged");
        engine.CellsRevealed += (s, e) => order.Add("CellsRevealed");

        // Act: первый клик запускает игру.
        engine.OpenCell(new CellPoint(0, 0));

        // Assert: BoardGenerated идёт первым — до того, как UI начнёт перерисовывать
        // поле по CellChanged/CellsRevealed. Если порядок нарушен, UI покажет
        // открытые клетки раньше, чем обновит счётчик мин (см. docs/09 §4).
        // Одиночный клик по Number/Mine не эмитит CellsRevealed, каскад — не эмитит
        // CellChanged (см. docs/08 §«События»). Точный состав «после BoardGenerated»
        // зависит от seed, но первым должен быть строго BoardGenerated.
        Assert.NotEmpty(order);
        Assert.Equal("BoardGenerated", order[0]);
    }

    [Fact]
    public void OpenCell_SecondClick_DoesNotEmitBoardGenerated()
    {
        // Arrange: игра уже InProgress после первого клика.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));

        int boardGeneratedCount = 0;
        engine.BoardGenerated += (s, e) => boardGeneratedCount++;

        // Ищем безопасную Closed-клетку для второго клика.
        var board = engine.BoardForTesting;
        CellPoint secondClick = default;
        bool found = false;
        for (int x = 0; x < board.Width && !found; x++)
            for (int y = 0; y < board.Height && !found; y++)
                if (board[x, y].State == CellState.Closed && !board[x, y].IsMine)
                {
                    secondClick = new CellPoint(x, y);
                    found = true;
                }
        Assert.True(found, "Не нашли безопасную Closed-клетку для второго клика.");

        // Act: второй клик — игра уже запущена, мины уже стоят.
        engine.OpenCell(secondClick);

        // Assert: BoardGenerated не эмитится повторно — контракт «один раз за партию».
        Assert.Equal(0, boardGeneratedCount);
    }
}
