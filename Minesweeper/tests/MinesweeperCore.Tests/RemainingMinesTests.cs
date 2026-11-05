using MinesweeperCore.Domain;
using MinesweeperCore.Engine;
using MinesweeperCore.Infrastructure;
using MinesweeperCore.Tests.Fakes;
using Xunit;

namespace MinesweeperCore.Tests;

/// <summary>
/// Тесты свойства RemainingMines - счётчик оставшихся мин.
/// </summary>
public class RemainingMinesTests
{
    [Fact]
    public void RemainingMines_AtStart_EqualsMineCount()
    {
        // Arrange: создаём движок, запускаем игру.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);

        // Assert: до первого клика RemainingMines == MineCount (10).
        Assert.Equal(10, engine.RemainingMines);

        // Act: открываем первую клетку — PlaceMines + InProgress.
        engine.OpenCell(new CellPoint(0, 0));

        // Assert: PlaceMines не трогает счётчик флагов игрока.
        Assert.Equal(10, engine.RemainingMines);
    }

    [Fact]
    public void RemainingMines_QuestionDoesNotDecrease()
    {
        // Arrange: Beginner (9x9, 10 мин). Ставим 3 Question через 2x CycleMark.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0)); // Запускаем игру.

        var board = engine.BoardForTesting;
        int questionCount = 0;
        for (int x = 0; x < board.Width && questionCount < 3; x++)
            for (int y = 0; y < board.Height && questionCount < 3; y++)
                if (board[x, y].State == CellState.Closed)
                {
                    engine.CycleMark(new CellPoint(x, y)); // -> Flagged
                    engine.CycleMark(new CellPoint(x, y)); // -> Flagged -> Question
                    questionCount++;
                }

        // Assert: Question не уменьшает RemainingMines (считаются только Flagged).
        Assert.Equal(10, engine.RemainingMines);
    }

    [Fact]
    public void RemainingMines_AfterWon_EqualsMineCount_WhenNoFlags()
    {
        // Arrange: побеждаем без единого флага.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(42),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));

        var board = engine.BoardForTesting;

        // Открываем все безопасные клетки (без флагов).
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

        // Assert: состояние Won и счётчик == MineCount (флагов не было).
        Assert.Equal(GameState.Won, engine.State);
        Assert.Equal(10, engine.RemainingMines);
    }

    [Fact]
    public void RemainingMines_AfterWon_EqualsMineCountMinusFlags()
    {
        // Arrange: победа с 3 флагами на минах.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(42),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));

        var board = engine.BoardForTesting;

        // Находим и помечаем 3 мины флагами.
        int flaggedCount = 0;
        foreach (var point in GetAllPoints(board))
        {
            if (board[point].IsMine && board[point].State == CellState.Closed && flaggedCount < 3)
            {
                engine.CycleMark(point);
                flaggedCount++;
            }
        }
        Assert.Equal(3, flaggedCount);

        // Открываем все безопасные клетки.
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

        // Assert: счётчик = MineCount - 3 = 7.
        Assert.Equal(GameState.Won, engine.State);
        Assert.Equal(7, engine.RemainingMines);
    }

    [Fact]
    public void RemainingMines_AfterLost_UnchangedByWrongFlag()
    {
        // Arrange: ставим флаг на безопасную клетку, потом проигрываем.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));

        var board = engine.BoardForTesting;

        // Находим Closed-клетку, которая НЕ мина, и ставим на неё флаг.
        CellPoint safeCellForFlag = default;
        foreach (var point in GetAllPoints(board))
        {
            if (!board[point].IsMine && board[point].State == CellState.Closed)
            {
                safeCellForFlag = point;
                break;
            }
        }

        // Ставим 1 флаг на безопасную Closed-клетку.
        engine.CycleMark(safeCellForFlag);
        Assert.Equal(9, engine.RemainingMines);

        // Act: играем до поражения (открываем мину, которая не Flagged).
        CellPoint mineCell = default;
        foreach (var point in GetAllPoints(board))
        {
            if (board[point].IsMine && board[point].State != CellState.Flagged)
            {
                mineCell = point;
                break;
            }
        }
        engine.OpenCell(mineCell);

        // Assert: состояние Lost, флаг стал WrongFlag, но счётчик не изменился.
        Assert.Equal(GameState.Lost, engine.State);
        Assert.Equal(CellState.WrongFlag, board[safeCellForFlag].State);
        Assert.Equal(9, engine.RemainingMines);
    }

    [Fact]
    public void RemainingMines_AfterLost_UnchangedByMineReveal()
    {
        // Arrange: ставим 2 правильных флага на мины, потом проигрываем.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));

        var board = engine.BoardForTesting;

        // Находим и помечаем 2 мины.
        int flaggedCount = 0;
        List<CellPoint> minePoints = new();
        foreach (var point in GetAllPoints(board))
        {
            if (board[point].IsMine && board[point].State == CellState.Closed)
            {
                engine.CycleMark(point);
                minePoints.Add(point);
                flaggedCount++;
                if (flaggedCount == 2) break;
            }
        }
        Assert.Equal(2, flaggedCount);
        Assert.Equal(8, engine.RemainingMines);

        // Act: играем до поражения.
        CellPoint mineCell = default;
        foreach (var point in GetAllPoints(board))
        {
            if (board[point].IsMine && board[point].State != CellState.Flagged)
            {
                mineCell = point;
                break;
            }
        }
        engine.OpenCell(mineCell);

        // Assert: правильно помеченные мины остались Flagged, счётчик не изменился.
        Assert.Equal(GameState.Lost, engine.State);
        foreach (var mp in minePoints)
            Assert.Equal(CellState.Flagged, board[mp].State);
        Assert.Equal(8, engine.RemainingMines);
    }

    [Fact]
    public void RemainingMines_OnlyChangesInCycleMark()
    {
        // Arrange: Beginner (9x9, 10 мин).
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));

        var board = engine.BoardForTesting;
        CellPoint closedCell = default;
        CellPoint mineCell = default;

        // Находим гарантированно Closed безопасную клетку для CycleMark.
        bool foundClosed = false;
        foreach (var point in GetAllPoints(board))
        {
            if (board[point].State == CellState.Closed && !board[point].IsMine)
            {
                closedCell = point;
                foundClosed = true;
                break;
            }
        }
        Assert.True(foundClosed, "Не нашли Closed-клетку для теста CycleMark.");

        // Находим мину для теста OpenCell на мину.
        foreach (var point in GetAllPoints(board))
        {
            if (board[point].IsMine)
            {
                mineCell = point;
                break;
            }
        }

        // 1) Правый клик на Closed → Flagged: уменьшает на 1.
        Assert.Equal(10, engine.RemainingMines);
        engine.CycleMark(closedCell);
        Assert.Equal(9, engine.RemainingMines);

        // 2) Второй правый клик (Flagged → Question): увеличивает на 1.
        engine.CycleMark(closedCell);
        Assert.Equal(10, engine.RemainingMines);

        // 3) OpenCell не меняет счётчик.
        int remainingBeforeOpen = engine.RemainingMines;
        CellPoint safeCell = default;
        bool foundSafe = false;
        foreach (var point in GetAllPoints(board))
        {
            if (!board[point].IsMine && board[point].State == CellState.Closed && point != mineCell)
            {
                safeCell = point;
                foundSafe = true;
                break;
            }
        }
        Assert.True(foundSafe, "Не нашли безопасную Closed-клетку для OpenCell.");
        engine.OpenCell(safeCell);
        Assert.Equal(remainingBeforeOpen, engine.RemainingMines);

        // 4) OpenCell на мину не меняет счётчик.
        remainingBeforeOpen = engine.RemainingMines;
        engine.OpenCell(mineCell);
        Assert.Equal(remainingBeforeOpen, engine.RemainingMines);

        // 5) CycleMark на Opened-клетке не меняет счётчик.
        remainingBeforeOpen = engine.RemainingMines;
        engine.CycleMark(safeCell);
        Assert.Equal(remainingBeforeOpen, engine.RemainingMines);
    }

    [Fact]
    public void RemainingMines_ResetOnStartNewGame()
    {
        // Arrange: победа с 3 флагами.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(42),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));

        var board = engine.BoardForTesting;

        // Ставим 3 флага.
        int flaggedCount = 0;
        foreach (var point in GetAllPoints(board))
        {
            if (board[point].IsMine && board[point].State == CellState.Closed && flaggedCount < 3)
            {
                engine.CycleMark(point);
                flaggedCount++;
            }
        }

        // Открываем все безопасные клетки для победы.
        CellPoint lastSafe = default;
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (!board[x, y].IsMine && board[x, y].State == CellState.Closed)
                    lastSafe = new CellPoint(x, y);

        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (!board[x, y].IsMine && !(x == lastSafe.X && y == lastSafe.Y))
                    board.Open(new CellPoint(x, y), CellContent.Empty);

        engine.OpenCell(lastSafe);

        // Assert: после победы с 3 флагами RemainingMines == 7.
        Assert.Equal(GameState.Won, engine.State);
        Assert.Equal(7, engine.RemainingMines);

        // Act: начинаем новую игру.
        engine.StartNewGame(DifficultyPresets.Beginner);

        // Assert: счётчик сброшен до MineCount.
        Assert.Equal(10, engine.RemainingMines);
    }

    private static IEnumerable<CellPoint> GetAllPoints(Board board)
    {
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                yield return new CellPoint(x, y);
    }
}
