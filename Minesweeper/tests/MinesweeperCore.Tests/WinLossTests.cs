using MinesweeperCore.Abstractions;
using MinesweeperCore.Domain;
using MinesweeperCore.Engine;
using MinesweeperCore.Events;
using MinesweeperCore.Infrastructure;
using MinesweeperCore.Tests.Fakes;
using Xunit;

namespace MinesweeperCore.Tests;

/// <summary>
/// Тесты победы и поражения — состояния, таймер, неверные флаги, Dispose.
/// </summary>
public class WinLossTests
{
    [Fact]
    public void Win_AfterOpeningAllSafeCells_StateIsWon()
    {
        // Arrange: поле 9×9, 10 мин. Seed 42.
        // Находим последнюю безопасную закрытую клетку динамически, а не фиксированную (8,8).
        var random = FakeRandomProvider.WithSeed(42);
        var store = new InMemoryRecordStore();
        var timeProvider = new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) };
        var engine = new GameEngine(random, store, timeProvider);

        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0)); // Запускаем игру

        // Находим все безопасные закрытые клетки, берём последнюю как триггер.
        var board = engine.BoardForTesting;
        CellPoint lastSafe = default;
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (!board[x, y].IsMine && board[x, y].State == CellState.Closed)
                    lastSafe = new CellPoint(x, y);

        // Открываем все безопасные закрытые, кроме lastSafe.
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (!board[x, y].IsMine && !(x == lastSafe.X && y == lastSafe.Y))
                    board.Open(new CellPoint(x, y), CellContent.Empty);

        // Сдвигаем время на 3 секунды до последнего клика, чтобы Elapsed > 0.
        timeProvider.AdvanceSeconds(3);

        // Act: кликаем в последнюю безопасную клетку — победа.
        engine.OpenCell(lastSafe);

        // Assert: победа, Elapsed >= 3 сек, Records содержит запись.
        Assert.Equal(GameState.Won, engine.State);
        Assert.True(engine.Elapsed >= TimeSpan.FromSeconds(3));
        Assert.NotEmpty(engine.Records);
    }

    [Fact]
    public void Lose_AfterClickingMine_StateIsLostAndElapsedFixed()
    {
        // Arrange: поле 9×9, 10 мин (Beginner). Seed 1.
        // С 10 минами каскад не открывает всё поле — можно кликнуть в мину после таймера.
        var random = FakeRandomProvider.WithSeed(1);
        var store = new InMemoryRecordStore();
        var timeProvider = new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) };
        var engine = new GameEngine(random, store, timeProvider);

        engine.StartNewGame(DifficultyPresets.Beginner);

        // Первый клик по безопасной клетке — запускает таймер и lazy generation мин.
        engine.OpenCell(new CellPoint(4, 4));

        // Сдвигаем время на 5 секунд.
        timeProvider.AdvanceSeconds(5);

        // Находим закрытую мину через BoardForTesting.
        var board = engine.BoardForTesting;
        CellPoint mineCell = default;
        bool found = false;
        for (int x = 0; x < board.Width && !found; x++)
        {
            for (int y = 0; y < board.Height && !found; y++)
            {
                if (board[x, y].IsMine && board[x, y].State == CellState.Closed)
                {
                    mineCell = new CellPoint(x, y);
                    found = true;
                }
            }
        }
        Assert.True(found, "Не нашли закрытую мину для теста.");

        // Act: открываем клетку с миной.
        engine.OpenCell(mineCell);

        // Assert: State == Lost, Elapsed > 0 (таймер зафиксирован).
        Assert.Equal(GameState.Lost, engine.State);
        Assert.True(engine.Elapsed >= TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Lose_WrongFlaggedSafeCell_MarkedAsWrongFlag()
    {
        // Arrange: поле 9×9, 10 мин (Beginner). Seed 42.
        var random = FakeRandomProvider.WithSeed(42);
        var store = new InMemoryRecordStore();
        var timeProvider = new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) };
        var engine = new GameEngine(random, store, timeProvider);

        engine.StartNewGame(DifficultyPresets.Beginner);

        // Первый клик — lazy generation мин.
        engine.OpenCell(new CellPoint(4, 4));

        // Находим закрытую безопасную клетку и ставим на неё флаг.
        var board = engine.BoardForTesting;
        CellPoint safeCell = default;
        bool foundSafe = false;
        for (int x = 0; x < board.Width && !foundSafe; x++)
        {
            for (int y = 0; y < board.Height && !foundSafe; y++)
            {
                if (!board[x, y].IsMine && board[x, y].State == CellState.Closed)
                {
                    safeCell = new CellPoint(x, y);
                    foundSafe = true;
                }
            }
        }
        Assert.True(foundSafe, "Не нашли закрытую безопасную клетку для теста.");

        // Act: ставим флаг на безопасную клетку, потом кликаем в мину.
        engine.CycleMark(safeCell);

        // Находим закрытую мину.
        CellPoint mineCell = default;
        bool foundMine = false;
        for (int x = 0; x < board.Width && !foundMine; x++)
        {
            for (int y = 0; y < board.Height && !foundMine; y++)
            {
                if (board[x, y].IsMine && board[x, y].State == CellState.Closed)
                {
                    mineCell = new CellPoint(x, y);
                    foundMine = true;
                }
            }
        }
        Assert.True(foundMine, "Не нашли закрытую мину для теста.");

        engine.OpenCell(mineCell);

        // Assert: клетка с флагом в состоянии WrongFlag.
        var wrongCell = engine.GetCell(safeCell);
        Assert.Equal(CellState.WrongFlag, wrongCell.State);
    }

    [Fact]
    public void Dispose_ThenOpenCell_ThrowsObjectDisposedException()
    {
        // Arrange: создаём engine и дизейблим.
        var random = FakeRandomProvider.WithSeed(1);
        var store = new InMemoryRecordStore();
        var timeProvider = new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) };
        var engine = new GameEngine(random, store, timeProvider);
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.Dispose();

        // Act & Assert: после Dispose все методы бросают ObjectDisposedException.
        Assert.Throws<ObjectDisposedException>(() => engine.OpenCell(new CellPoint(0, 0)));
        Assert.Throws<ObjectDisposedException>(() => engine.CycleMark(new CellPoint(0, 0)));
        Assert.Throws<ObjectDisposedException>(() => engine.StartNewGame(DifficultyPresets.Beginner));
    }

    [Fact]
    public void Lose_EmitsOneCellsRevealed_Not99CellChanged()
    {
        // Arrange: поле 9×9 с 10 минами. Seed 1.
        // Счётчики событий: CellsRevealed = 1, CellChanged <= 1.
        var random = FakeRandomProvider.WithSeed(1);
        var store = new InMemoryRecordStore();
        var timeProvider = new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) };
        var engine = new GameEngine(random, store, timeProvider);

        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(4, 4)); // Первый клик, lazy generation

        // Счётчики событий: CellsRevealed = 1, CellChanged <= 1.
        int cellChangedCount = 0;
        int cellsRevealedCount = 0;

        engine.CellChanged += (s, e) => cellChangedCount++;
        engine.CellsRevealed += (s, e) => cellsRevealedCount++;

        // Находим закрытую мину.
        var board = engine.BoardForTesting;
        CellPoint mineCell = default;
        bool found = false;
        for (int x = 0; x < board.Width && !found; x++)
        {
            for (int y = 0; y < board.Height && !found; y++)
            {
                if (board[x, y].IsMine && board[x, y].State == CellState.Closed)
                {
                    mineCell = new CellPoint(x, y);
                    found = true;
                }
            }
        }
        Assert.True(found, "Не нашли закрытую мину для теста.");

        // Act: кликаем в мину — поражение.
        engine.OpenCell(mineCell);

        // Assert: CellsRevealed = 1 (батч всех мин), CellChanged = 1 (триггерная клетка).
        Assert.Equal(1, cellsRevealedCount);
        // Триггерная клетка эмитит ровно одно CellChanged.
        Assert.Equal(1, cellChangedCount);
    }

    [Fact]
    public void OpenCell_OnOpenedCell_Ignored()
    {
        // Arrange: Beginner (9×9, 10 мин). Seed 1. Кликаем (0,0), клетка открывается.
        // Проверяем правило §6: клик по открытой клетке игнорируется.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));

        int cellChangedCount = 0;
        engine.CellChanged += (s, e) => cellChangedCount++;

        // Act: кликаем по той же клетке повторно.
        engine.OpenCell(new CellPoint(0, 0));

        // Assert: состояние не меняется, событие не эмитится — правило §6.
        Assert.Equal(0, cellChangedCount);
    }

    [Fact]
    public void OpenCell_OnFlaggedCell_Ignored()
    {
        // Arrange: Beginner (9×9, 10 мин). Seed 1. Кликаем (0,0), ставим флаг на Closed.
        // Проверяем правило §6: клик по флаговой клетке игнорируется.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));

        var board = engine.BoardForTesting;
        CellPoint target = default;
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (board[x, y].State == CellState.Closed)
                    target = new CellPoint(x, y);
        engine.CycleMark(target);

        // Act: пытаемся открыть клетку с флагом — должно игнорироваться.
        engine.OpenCell(target);

        // Assert: клетка остаётся Flagged, правило §6.
        var cell = engine.GetCell(target);
        Assert.Equal(CellState.Flagged, cell.State);
    }

    [Fact]
    public void OpenCell_AfterWon_Ignored()
    {
        // Arrange: побеждаем, затем пытаемся кликнуть.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(42),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));

        var board = engine.BoardForTesting;
        CellPoint lastSafe = default;
        bool foundSafe = false;
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (!board[x, y].IsMine && board[x, y].State == CellState.Closed)
                {
                    lastSafe = new CellPoint(x, y);
                    foundSafe = true;
                }
        Assert.True(foundSafe, "Не нашли безопасную Closed-клетку для победы.");
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (!board[x, y].IsMine && !(x == lastSafe.X && y == lastSafe.Y))
                    board.Open(new CellPoint(x, y), CellContent.Empty);
        engine.OpenCell(lastSafe);
        Assert.Equal(GameState.Won, engine.State);

        // Ищем гарантированно Closed клетку после победы.
        CellPoint target = default;
        bool found = false;
        foreach (var info in engine.GetAllCells())
            if (info.State == CellState.Closed)
            { target = info.Position; found = true; break; }
        Assert.True(found, "Не нашли Closed-клетку для теста после завершения партии.");

        int cellChangedCount = 0;
        engine.CellChanged += (s, e) => cellChangedCount++;

        // Act: кликаем по гарантированно Closed клетке после победы — должно игнорироваться.
        engine.OpenCell(target);

        // Assert: событий нет, состояние не меняется — правило §9.
        Assert.Equal(0, cellChangedCount);
        Assert.Equal(GameState.Won, engine.State);
    }

    [Fact]
    public void OpenCell_AfterLost_Ignored()
    {
        // Arrange: проигрываем (клик в мину), затем пробуем кликнуть.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(4, 4));

        var board = engine.BoardForTesting;
        CellPoint mineCell = default;
        bool foundMine = false;
        for (int x = 0; x < board.Width && !foundMine; x++)
            for (int y = 0; y < board.Height && !foundMine; y++)
                if (board[x, y].IsMine && board[x, y].State == CellState.Closed)
                {
                    mineCell = new CellPoint(x, y);
                    foundMine = true;
                }
        Assert.True(foundMine, "Не нашли закрытую мину для теста.");
        engine.OpenCell(mineCell);
        Assert.Equal(GameState.Lost, engine.State);

        // Ищем гарантированно Closed клетку после поражения.
        CellPoint target = default;
        bool found = false;
        foreach (var info in engine.GetAllCells())
            if (info.State == CellState.Closed)
            { target = info.Position; found = true; break; }
        Assert.True(found, "Не нашли Closed-клетку для теста после завершения партии.");

        int cellChangedCount = 0;
        engine.CellChanged += (s, e) => cellChangedCount++;

        // Act: кликаем по гарантированно Closed клетке после поражения — должно игнорироваться.
        engine.OpenCell(target);

        // Assert: событий нет — правило §9.
        Assert.Equal(0, cellChangedCount);
    }

    [Fact]
    public void CycleMark_AfterWon_Ignored()
    {
        // Arrange: побеждаем.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(42),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));

        var board = engine.BoardForTesting;
        CellPoint lastSafe = default;
        bool foundSafe = false;
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (!board[x, y].IsMine && board[x, y].State == CellState.Closed)
                {
                    lastSafe = new CellPoint(x, y);
                    foundSafe = true;
                }
        Assert.True(foundSafe, "Не нашли безопасную Closed-клетку для победы.");
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (!board[x, y].IsMine && !(x == lastSafe.X && y == lastSafe.Y))
                    board.Open(new CellPoint(x, y), CellContent.Empty);
        engine.OpenCell(lastSafe);

        // Ищем гарантированно Closed клетку после победы.
        CellPoint target = default;
        bool found = false;
        foreach (var info in engine.GetAllCells())
            if (info.State == CellState.Closed)
            { target = info.Position; found = true; break; }
        Assert.True(found, "Не нашли Closed-клетку для теста после завершения партии.");

        int cellChangedCount = 0;
        engine.CellChanged += (s, e) => cellChangedCount++;

        // Act: пытаемся поставить метку после победы на Closed-клетке — должно игнорироваться.
        engine.CycleMark(target);

        // Assert: событий нет, состояние игры Won — правило §9.
        Assert.Equal(0, cellChangedCount);
        Assert.Equal(GameState.Won, engine.State);
    }

    [Fact]
    public void CycleMark_AfterLost_Ignored()
    {
        // Arrange: проигрываем.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(4, 4));

        var board = engine.BoardForTesting;
        CellPoint mineCell = default;
        bool foundMine = false;
        for (int x = 0; x < board.Width && !foundMine; x++)
            for (int y = 0; y < board.Height && !foundMine; y++)
                if (board[x, y].IsMine && board[x, y].State == CellState.Closed)
                {
                    mineCell = new CellPoint(x, y);
                    foundMine = true;
                }
        Assert.True(foundMine, "Не нашли закрытую мину для теста.");
        engine.OpenCell(mineCell);

        // Ищем гарантированно Closed клетку после поражения.
        CellPoint target = default;
        bool found = false;
        foreach (var info in engine.GetAllCells())
            if (info.State == CellState.Closed)
            { target = info.Position; found = true; break; }
        Assert.True(found, "Не нашли Closed-клетку для теста после завершения партии.");

        int cellChangedCount = 0;
        engine.CellChanged += (s, e) => cellChangedCount++;

        // Act: пытаемся поставить метку после поражения на Closed-клетке — должно игнорироваться.
        engine.CycleMark(target);

        // Assert: событий нет — правило §9.
        Assert.Equal(0, cellChangedCount);
        Assert.Equal(GameState.Lost, engine.State);
    }

    [Fact]
    public void Elapsed_AfterWon_DoesNotGrow()
    {
        // Arrange: побеждаем.
        var timeProvider = new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) };
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(42),
            new InMemoryRecordStore(),
            timeProvider);
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));

        var board = engine.BoardForTesting;
        CellPoint lastSafe = default;
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (!board[x, y].IsMine && board[x, y].State == CellState.Closed)
                    lastSafe = new CellPoint(x, y);
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (!board[x, y].IsMine && !(x == lastSafe.X && y == lastSafe.Y))
                    board.Open(new CellPoint(x, y), CellContent.Empty);
        timeProvider.AdvanceSeconds(3);
        engine.OpenCell(lastSafe);
        Assert.Equal(GameState.Won, engine.State);
        var elapsedAtWin = engine.Elapsed;

        // Act: сдвигаем время на 10 секунд после победы.
        timeProvider.AdvanceSeconds(10);

        // Assert: Elapsed не растёт — таймер остановлен (регрессия T3).
        Assert.Equal(elapsedAtWin, engine.Elapsed);
    }

    [Fact]
    public void Elapsed_AfterLost_DoesNotGrow()
    {
        // Arrange: проигрываем.
        var timeProvider = new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) };
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            timeProvider);
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(4, 4));
        timeProvider.AdvanceSeconds(5);

        var board = engine.BoardForTesting;
        CellPoint mineCell = default;
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (board[x, y].IsMine && board[x, y].State == CellState.Closed)
                    mineCell = new CellPoint(x, y);
        engine.OpenCell(mineCell);
        Assert.Equal(GameState.Lost, engine.State);
        var elapsedAtLoss = engine.Elapsed;

        // Act: сдвигаем время на 10 секунд после поражения.
        timeProvider.AdvanceSeconds(10);

        // Assert: Elapsed не растёт — таймер остановлен (регрессия T4).
        Assert.Equal(elapsedAtLoss, engine.Elapsed);
    }

    [Fact]
    public void OpenCell_OnQuestionCell_Allowed()
    {
        // Arrange: поле 9×9, 10 мин. Ищем безопасную Closed-клетку, крутим
        // Closed → Flagged → Question.
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
                if (board[x, y].State == CellState.Closed && !board[x, y].IsMine)
                {
                    target = new CellPoint(x, y);
                    found = true;
                }
        Assert.True(found, "Не нашли безопасной Closed-клетки.");
        engine.CycleMark(target);
        engine.CycleMark(target);
        Assert.Equal(CellState.Question, engine.GetCell(target).State);

        // Act: клик левой кнопкой по вопросу — должно открыться.
        engine.OpenCell(target);

        // Assert: клетка открыта, а не заблокирована.
        Assert.Equal(CellState.Opened, engine.GetCell(target).State);
    }

    [Fact]
    public void Lose_CorrectlyFlaggedMine_RemainsFlagged()
    {
        // Arrange: 10 мин. Открываем (4,4), находим закрытую мину и ставим на неё
        // флаг (CycleMark × 1: Closed → Flagged).
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(4, 4));

        var board = engine.BoardForTesting;
        CellPoint correctMine = default;
        CellPoint triggerMine = default;
        bool foundCorrect = false;
        bool foundTrigger = false;
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (board[x, y].IsMine && board[x, y].State == CellState.Closed)
                {
                    if (!foundCorrect)
                    {
                        correctMine = new CellPoint(x, y);
                        foundCorrect = true;
                    }
                    else if (!foundTrigger)
                    {
                        triggerMine = new CellPoint(x, y);
                        foundTrigger = true;
                    }
                }
        Assert.True(foundCorrect && foundTrigger);

        engine.CycleMark(correctMine); // ставим флаг на правильную мину

        // Act: кликаем в другую мину — поражение.
        engine.OpenCell(triggerMine);

        // Assert: правильно помеченная мина осталась Flagged, не открыта.
        var cell = engine.GetCell(correctMine);
        Assert.Equal(CellState.Flagged, cell.State);
        Assert.Equal(CellContent.Empty, cell.Content);
    }

    [Fact]
    public void Lose_MineInQuestionState_RevealsAsMine()
    {
        // Arrange: 10 мин. Открываем (4,4), находим закрытую мину, крутим метку
        // 2 раза: Closed → Flagged → Question.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(4, 4));

        var board = engine.BoardForTesting;
        CellPoint questionMine = default;
        CellPoint triggerMine = default;
        bool foundQuestion = false;
        bool foundTrigger = false;
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (board[x, y].IsMine && board[x, y].State == CellState.Closed)
                {
                    if (!foundQuestion)
                    {
                        questionMine = new CellPoint(x, y);
                        foundQuestion = true;
                    }
                    else if (!foundTrigger)
                    {
                        triggerMine = new CellPoint(x, y);
                        foundTrigger = true;
                    }
                }
        Assert.True(foundQuestion && foundTrigger);

        engine.CycleMark(questionMine); // → Flagged
        engine.CycleMark(questionMine); // → Question
        Assert.Equal(CellState.Question, engine.GetCell(questionMine).State);

        // Act: клик в другую мину — поражение.
        engine.OpenCell(triggerMine);

        // Assert: клетка с вопросом-на-мине открыта и показывает Mine —
        // вопрос не считается «правильным флагом».
        var cell = engine.GetCell(questionMine);
        Assert.Equal(CellState.Opened, cell.State);
        Assert.Equal(CellContent.Mine, cell.Content);
    }

    [Fact]
    public void Lose_CorrectlyFlaggedMine_NotInCellsRevealed()
    {
        // Arrange: 10 мин. Ставим флаг на одну правильную мину, кликаем в другую.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(4, 4));

        var board = engine.BoardForTesting;
        CellPoint correctMine = default;
        CellPoint triggerMine = default;
        bool foundCorrect = false;
        bool foundTrigger = false;
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (board[x, y].IsMine && board[x, y].State == CellState.Closed)
                {
                    if (!foundCorrect)
                    {
                        correctMine = new CellPoint(x, y);
                        foundCorrect = true;
                    }
                    else if (!foundTrigger)
                    {
                        triggerMine = new CellPoint(x, y);
                        foundTrigger = true;
                    }
                }
        engine.CycleMark(correctMine);

        Assert.True(foundCorrect && foundTrigger, "Не нашли правильную мину и триггерную.");

        List<CellInfo>? lastRevealed = null;
        engine.CellsRevealed += (s, e) => lastRevealed = e.CellInfos.ToList();

        // Act
        engine.OpenCell(triggerMine);

        // Assert: правильно помеченная мина не входит в CellsRevealed
        // (её состояние не изменилось — она и так была Flagged).
        Assert.NotNull(lastRevealed);
        Assert.DoesNotContain(lastRevealed!, c => c.Position == correctMine);
    }

    [Fact]
    public void Win_CorrectlyFlaggedMine_RemainsFlagged()
    {
        // Arrange: 10 мин. Находим закрытую мину, ставим на неё флаг —
        // это «правильный» флаг. При Won флаг должен остаться Flagged.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(42),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));

        var board = engine.BoardForTesting;
        // Ищем закрытую мину, ставим на неё флаг — это «правильный» флаг.
        CellPoint mineCell = default;
        bool foundMine = false;
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (board[x, y].IsMine && board[x, y].State == CellState.Closed)
                {
                    mineCell = new CellPoint(x, y);
                    foundMine = true;
                    goto foundMineLabel;
                }
        foundMineLabel:
        Assert.True(foundMine);
        engine.CycleMark(mineCell); // Closed → Flagged
        Assert.Equal(CellState.Flagged, engine.GetCell(mineCell).State);

        // Находим последнюю безопасную закрытую клетку для победы.
        CellPoint lastSafe = default;
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (!board[x, y].IsMine && board[x, y].State == CellState.Closed)
                    lastSafe = new CellPoint(x, y);

        // Открываем все безопасные, кроме последней.
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (!board[x, y].IsMine && !(x == lastSafe.X && y == lastSafe.Y))
                    board.Open(new CellPoint(x, y), CellContent.Empty);

        // Act: победа кликом по последней безопасной.
        engine.OpenCell(lastSafe);
        Assert.Equal(GameState.Won, engine.State);

        // Assert: правильно помеченная мина осталась Flagged — §16.1.
        var cell = engine.GetCell(mineCell);
        Assert.Equal(CellState.Flagged, cell.State);
        Assert.Equal(CellContent.Empty, cell.Content);
    }

    [Fact]
    public void Win_MineInQuestionState_RemainsQuestion()
    {
        // Arrange: 10 мин. Находим закрытую мину, крутим Closed → Flagged →
        // Question. При Won мина в Question должна остаться Question.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(42),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));

        var board = engine.BoardForTesting;
        CellPoint mineCell = default;
        bool foundMine = false;
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (board[x, y].IsMine && board[x, y].State == CellState.Closed)
                {
                    mineCell = new CellPoint(x, y);
                    foundMine = true;
                    goto foundMineLabel2;
                }
        foundMineLabel2:
        Assert.True(foundMine);
        engine.CycleMark(mineCell); // Closed → Flagged
        engine.CycleMark(mineCell); // Flagged → Question
        Assert.Equal(CellState.Question, engine.GetCell(mineCell).State);

        // Открываем все безопасные, кроме последней.
        CellPoint lastSafe = default;
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (!board[x, y].IsMine && board[x, y].State == CellState.Closed)
                    lastSafe = new CellPoint(x, y);
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (!board[x, y].IsMine && !(x == lastSafe.X && y == lastSafe.Y))
                    board.Open(new CellPoint(x, y), CellContent.Empty);

        // Act: победа.
        engine.OpenCell(lastSafe);
        Assert.Equal(GameState.Won, engine.State);

        // Assert: клетка с вопросом-на-мине осталась Question — §16.1.
        var cell = engine.GetCell(mineCell);
        Assert.Equal(CellState.Question, cell.State);
        Assert.Equal(CellContent.Empty, cell.Content);
    }

    [Fact]
    public void GetCell_AfterDispose_Throws()
    {
        // Arrange: создаём engine, дизейблим.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.Dispose();

        // Act & Assert: после Dispose GetCell бросает ObjectDisposedException.
        Assert.Throws<ObjectDisposedException>(() => engine.GetCell(new CellPoint(0, 0)));
    }

    [Fact]
    public void GetAllCells_AfterDispose_Throws()
    {
        // Arrange: создаём engine, дизейблим.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.Dispose();

        // Act & Assert: после Dispose GetAllCells бросает ObjectDisposedException.
        Assert.Throws<ObjectDisposedException>(() => engine.GetAllCells().ToList());
    }

    [Fact]
    public void OpenCell_InvalidPoint_InProgress_Ignored()
    {
        // Arrange: Beginner (9×9, 10 мин). Seed 1. Кликаем (0,0) — игра InProgress.
        // Проверяем правило §6: клик за пределами поля игнорируется.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));
        Assert.Equal(GameState.InProgress, engine.State);

        int cellChangedCount = 0;
        engine.CellChanged += (s, e) => cellChangedCount++;

        // Act: кликаем за пределами поля — должно игнорироваться.
        engine.OpenCell(new CellPoint(-1, 0));
        engine.OpenCell(new CellPoint(9, 0));

        // Assert: состояние не меняется, события не эмитятся.
        Assert.Equal(GameState.InProgress, engine.State);
        Assert.Equal(0, cellChangedCount);
    }

    [Fact]
    public void Lose_AllCellsQuestioned_CounterUnchanged_OpensMineQuestions_KeepsSafeQuestions()
    {
        // Arrange: Beginner (9×9, 10 мин). Seed 1.
        // Открываем (0,0), потом все Closed → Question через 2× CycleMark.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0)); // Запускаем игру.

        var board = engine.BoardForTesting;

        // Все Closed клетки → Question (2× CycleMark: Closed → Flagged → Question).
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (board[x, y].State == CellState.Closed)
                {
                    engine.CycleMark(new CellPoint(x, y)); // → Flagged
                    engine.CycleMark(new CellPoint(x, y)); // → Question
                }

        // Assert: RemainingMines == MineCount (флагов не было, Question не считается).
        Assert.Equal(engine.MineCount, engine.RemainingMines);

        // Открываем все клетки в Question по порядку, пока не Lost.
        List<CellPoint> questionCells = new();
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (board[x, y].State == CellState.Question)
                    questionCells.Add(new CellPoint(x, y));

        CellPoint triggerMine = default;
        foreach (var q in questionCells)
        {
            engine.OpenCell(q);
            if (engine.State == GameState.Lost)
            {
                triggerMine = q;
                break;
            }
        }

        // Assert после Lost:
        // 1) RemainingMines == MineCount (флагов не было).
        Assert.Equal(engine.MineCount, engine.RemainingMines);

        // 2) Каждая клетка со State == Opened && Content == Mine — реально мина.
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                var cell = board[x, y];
                if (cell.State == CellState.Opened && cell.Content == CellContent.Mine)
                    Assert.True(cell.IsMine, $"Открытая мина в ({x},{y}) не является миной.");
            }

        // 3) Каждая клетка со State == Question — безопасная (§16.2: Question на безопасной остаётся).
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                var cell = board[x, y];
                if (cell.State == CellState.Question)
                    Assert.False(cell.IsMine, $"Question в ({x},{y}) оказался на мине — нарушение §16.2.");
            }

        // 4) Триггерная клетка — Opened + Mine.
        var triggerCell = engine.GetCell(triggerMine);
        Assert.Equal(CellState.Opened, triggerCell.State);
        Assert.Equal(CellContent.Mine, triggerCell.Content);
    }

    [Fact]
    public void Lose_AllCellsFlagged_CounterNegative_OpenBlocked_WrongFlagsShown()
    {
        // Arrange: Beginner (9×9, 10 мин). Seed 1.
        // Открываем (0,0), потом все Closed → Flagged (1× CycleMark).
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0)); // Запускаем игру.

        var board = engine.BoardForTesting;

        // Все Closed клетки → Flagged.
        List<CellPoint> allFlagged = new();
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (board[x, y].State == CellState.Closed)
                {
                    engine.CycleMark(new CellPoint(x, y)); // → Flagged
                    allFlagged.Add(new CellPoint(x, y));
                }

        int flaggedCount = allFlagged.Count;
        Assert.True(flaggedCount > engine.MineCount, $"FlaggedCount={flaggedCount} не больше MineCount={engine.MineCount}.");
        Assert.Equal(engine.MineCount - flaggedCount, engine.RemainingMines);
        Assert.True(engine.RemainingMines < 0, $"RemainingMines={engine.RemainingMines} не отрицательный.");

        // Защита от случайного клика: взять один Flagged, OpenCell → клетка осталась Flagged.
        var protectedCell = allFlagged[0];
        int cellChangedBefore = 0;
        engine.CellChanged += (s, e) => cellChangedBefore++;
        engine.OpenCell(protectedCell);
        Assert.Equal(CellState.Flagged, engine.GetCell(protectedCell).State);
        Assert.Equal(0, cellChangedBefore);

        // В цикле: CycleMark (Flagged → Question) + OpenCell, пока не Lost.
        // Каждый Flagged → Question уменьшает FlaggedCount на 1.
        int processedCount = 0;
        CellPoint triggerMine = default;
        foreach (var flagged in allFlagged)
        {
            engine.CycleMark(flagged); // Flagged → Question
            engine.OpenCell(flagged);
            processedCount++;
            if (engine.State == GameState.Lost)
            {
                triggerMine = flagged;
                break;
            }
        }

        // Assert после Lost:
        // 1) Каждая клетка со State == Flagged — реально на мине.
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                var cell = board[x, y];
                if (cell.State == CellState.Flagged)
                    Assert.True(cell.IsMine, $"Flagged в ({x},{y}) не на мине.");
            }

        // 2) Каждая клетка со State == WrongFlag — на безопасной.
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                var cell = board[x, y];
                if (cell.State == CellState.WrongFlag)
                    Assert.False(cell.IsMine, $"WrongFlag в ({x},{y}) оказался на мине.");
            }

        // 3) FlaggedCount = всеFlagged - processedCount (каждый CycleMark Flagged→Question уменьшил на 1).
        // processedCount = сколько клеток прошли через Flagged → Question до Lost.
        Assert.Equal(allFlagged.Count - processedCount, engine.BoardForTesting.FlaggedCount);
    }

    [Fact]
    public void Lose_QuestionOnSafeCell_RemainsQuestion()
    {
        // Arrange: 10 мин. Открываем (4,4), находим безопасную Closed,
        // крутим Closed → Flagged → Question. Также находим закрытую мину
        // для триггера поражения.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(4, 4));

        var board = engine.BoardForTesting;
        CellPoint safeQuestion = default;
        bool foundSafe = false;
        for (int x = 0; x < board.Width && !foundSafe; x++)
            for (int y = 0; y < board.Height && !foundSafe; y++)
                if (!board[x, y].IsMine && board[x, y].State == CellState.Closed)
                {
                    safeQuestion = new CellPoint(x, y);
                    foundSafe = true;
                }
        Assert.True(foundSafe);
        engine.CycleMark(safeQuestion); // → Flagged
        engine.CycleMark(safeQuestion); // → Question
        Assert.Equal(CellState.Question, engine.GetCell(safeQuestion).State);

        CellPoint mineCell = default;
        bool foundMine = false;
        for (int x = 0; x < board.Width && !foundMine; x++)
            for (int y = 0; y < board.Height && !foundMine; y++)
                if (board[x, y].IsMine && board[x, y].State == CellState.Closed)
                {
                    mineCell = new CellPoint(x, y);
                    foundMine = true;
                }
        Assert.True(foundMine);

        // Act: поражение.
        engine.OpenCell(mineCell);
        Assert.Equal(GameState.Lost, engine.State);

        // Assert: вопрос на безопасной клетке остался Question — §16.2.
        var cell = engine.GetCell(safeQuestion);
        Assert.Equal(CellState.Question, cell.State);
        Assert.Equal(CellContent.Empty, cell.Content);
    }

    [Fact]
    public void Lose_GameStateChanged_TriggerCellMatchesClickedPoint()
    {
        // Arrange: Beginner (9×9, 10 мин). Seed 1.
        // Первый клик запускает игру; затем ищем закрытую мину для поражения.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(4, 4));

        var board = engine.BoardForTesting;
        CellPoint mineCell = default;
        bool found = false;
        for (int x = 0; x < board.Width && !found; x++)
            for (int y = 0; y < board.Height && !found; y++)
                if (board[x, y].IsMine && board[x, y].State == CellState.Closed)
                {
                    mineCell = new CellPoint(x, y);
                    found = true;
                }
        Assert.True(found, "Не нашли закрытую мину для теста.");

        CellInfo? capturedTrigger = null;
        engine.GameStateChanged += (s, e) =>
        {
            if (e.NewState == GameState.Lost)
                capturedTrigger = e.TriggerCell;
        };

        // Act: клик в мину — поражение.
        engine.OpenCell(mineCell);

        // Assert: GameStateChanged(Lost) содержит TriggerCell с координатой клика.
        // Без этого UI не сможет отрисовать cell_exploded именно на триггерной клетке.
        Assert.Equal(GameState.Lost, engine.State);
        Assert.NotNull(capturedTrigger);
        Assert.Equal(mineCell, capturedTrigger!.Position);
        Assert.Equal(CellState.Opened, capturedTrigger.State);
        Assert.Equal(CellContent.Mine, capturedTrigger.Content);
    }

    [Fact]
    public void Win_GameStateChanged_TriggerCellIsNull()
    {
        // Arrange: побеждаем на Beginner (seed 42). По §16.1 TriggerCell при Won = null.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(42),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));

        var board = engine.BoardForTesting;
        CellPoint lastSafe = default;
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (!board[x, y].IsMine && board[x, y].State == CellState.Closed)
                    lastSafe = new CellPoint(x, y);
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (!board[x, y].IsMine && !(x == lastSafe.X && y == lastSafe.Y))
                    board.Open(new CellPoint(x, y), CellContent.Empty);

        GameStateChangedEventArgs? capturedWon = null;
        engine.GameStateChanged += (s, e) =>
        {
            if (e.NewState == GameState.Won)
                capturedWon = e;
        };

        // Act: победа.
        engine.OpenCell(lastSafe);

        // Assert: GameStateChanged(Won) с TriggerCell == null (§16.1).
        // При Won нет «клетки-триггера»: победа приходит от последней безопасной,
        // а UI должен рисовать флаги на всех закрытых/помеченных клетках.
        Assert.Equal(GameState.Won, engine.State);
        Assert.NotNull(capturedWon);
        Assert.Null(capturedWon!.TriggerCell);
    }

    [Fact]
    public void Lose_EventOrder_CellChangedThenCellsRevealedThenGameStateChanged()
    {
        // Arrange: Beginner (9×9, 10 мин). Seed 1. Собираем последовательность
        // событий при поражении — порядок важен для UI (§16.2).
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(4, 4));

        var board = engine.BoardForTesting;
        CellPoint mineCell = default;
        bool found = false;
        for (int x = 0; x < board.Width && !found; x++)
            for (int y = 0; y < board.Height && !found; y++)
                if (board[x, y].IsMine && board[x, y].State == CellState.Closed)
                {
                    mineCell = new CellPoint(x, y);
                    found = true;
                }
        Assert.True(found, "Не нашли закрытую мину для теста.");

        var order = new List<string>();
        engine.CellChanged += (s, e) => order.Add("CellChanged");
        engine.CellsRevealed += (s, e) => order.Add("CellsRevealed");
        engine.GameStateChanged += (s, e) => order.Add($"GameStateChanged:{e.NewState}");

        // Act: клик в мину.
        engine.OpenCell(mineCell);

        // Assert: по §16.2 порядок строгий — CellChanged (триггер),
        // затем CellsRevealed (батч мин и ошибочных флагов),
        // затем GameStateChanged(Lost). Нарушение порядка сломает UI:
        // смайлик сменится раньше отрисовки поля и/или ядро эмитит
        // GameStateChanged до того, как UI перерисует триггерную клетку.
        Assert.Equal(new[]
        {
            "CellChanged",
            "CellsRevealed",
            "GameStateChanged:Lost",
        }, order);
    }

    [Fact]
    public void Lose_CellChangedPayload_DescribesOpenedMine()
    {
        // Arrange: Beginner, seed 1. Кликаем в мину и проверяем payload CellChanged.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(4, 4));

        var board = engine.BoardForTesting;
        CellPoint mineCell = default;
        bool found = false;
        for (int x = 0; x < board.Width && !found; x++)
            for (int y = 0; y < board.Height && !found; y++)
                if (board[x, y].IsMine && board[x, y].State == CellState.Closed)
                {
                    mineCell = new CellPoint(x, y);
                    found = true;
                }
        Assert.True(found, "Не нашли закрытую мину для теста.");

        var captured = new List<CellChangedEventArgs>();
        engine.CellChanged += (s, e) => captured.Add(e);

        // Act: клик в мину.
        engine.OpenCell(mineCell);

        // Assert: payload триггерного CellChanged согласован.
        // UI опирается на Change и CellInfo для отрисовки одной клетки;
        // если их поля перепутать, тесты на количество событий останутся
        // зелёными, а UI нарисует не то.
        var single = Assert.Single(captured);
        Assert.Equal(mineCell, single.Change.Position);
        Assert.Equal(CellState.Closed, single.Change.PreviousState);
        Assert.Equal(CellState.Opened, single.Change.NewState);
        Assert.Equal(mineCell, single.CellInfo.Position);
        Assert.Equal(CellState.Opened, single.CellInfo.State);
        Assert.Equal(CellContent.Mine, single.CellInfo.Content);
    }
}
