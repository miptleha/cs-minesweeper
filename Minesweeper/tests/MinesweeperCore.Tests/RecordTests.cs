using MinesweeperCore.Abstractions;
using MinesweeperCore.Domain;
using MinesweeperCore.Engine;
using MinesweeperCore.Infrastructure;
using MinesweeperCore.Records;
using MinesweeperCore.Tests.Fakes;
using Xunit;

namespace MinesweeperCore.Tests;

/// <summary>
/// Тесты таблицы рекордов — InMemoryRecordStore и откат при ошибке сохранения.
/// </summary>
public class RecordTests
{
    [Fact]
    public void Records_BeforeStartNewGame_ReturnsEmpty()
    {
        // Arrange: создаём движок, но не вызываем StartNewGame.
        var random = FakeRandomProvider.WithSeed(1);
        var store = new InMemoryRecordStore();
        var engine = new GameEngine(random, store);

        // Act: читаем Records до старта игры.

        // Assert: не бросает NullReferenceException, коллекция пуста.
        Assert.NotNull(engine.Records);
        Assert.Empty(engine.Records);
    }

    [Fact]
    public void InMemoryRecordStore_SixthRecord_ReturnsFalse()
    {
        // Arrange: создаём InMemoryRecordStore и сохраняем 5 записей с временами 1..5.
        // Шестая запись со временем 6 не попадёт в топ-5.
        IRecordStore store = new InMemoryRecordStore();

        for (int i = 1; i <= 5; i++)
        {
            store.SaveRecord(DifficultyLevel.Beginner, new RecordEntry($"player{i}", i));
        }

        // Act: сохраняем шестую запись со временем 6 — она хуже пятой.
        bool result = store.SaveRecord(DifficultyLevel.Beginner, new RecordEntry("player6", 6));

        // Assert: SaveRecord возвращает false, в LoadRecords только 5 записей, шестой нет.
        Assert.False(result);
        var records = store.LoadRecords(DifficultyLevel.Beginner).ToList();
        Assert.Equal(5, records.Count);
        Assert.DoesNotContain(records, r => r.PlayerName == "player6");
    }

    [Fact]
    public void SaveRecord_StoreReturnsFalse_RollsBackInMemory()
    {
        // Arrange: создаём GameEngine с фейковым стором, который всегда возвращает false.
        // Seed 42 — детерминированная расстановка мин.
        var random = FakeRandomProvider.WithSeed(42);
        var failingStore = new FailingRecordStore();
        var timeProvider = new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) };
        var engine = new GameEngine(random, failingStore, timeProvider);

        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0)); // Запускаем игру

        // Находим последнюю безопасную закрытую клетку динамически.
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

        // Act: открываем последнюю безопасную клетку — это вызовет победу и SaveRecord.
        // FailingRecordStore вернёт false, и рекорд должен быть откатан.
        engine.OpenCell(lastSafe);

        // Assert: сначала убеждаемся, что игра выиграна (не поражена по ошибке).
        Assert.Equal(GameState.Won, engine.State);
        // Поскольку стор всегда возвращает false, рекорд должен быть откатан.
        Assert.Empty(engine.Records);
    }

    [Fact]
    public void SaveRecord_StoreReturnsFalse_KeepsFifthRecord()
    {
        // Arrange: стор уже содержит 5 записей 1..5, SaveRecord отвергает всё.
        var preloaded = new List<RecordEntry>
        {
            new("p1", 1), new("p2", 2), new("p3", 3), new("p4", 4), new("p5", 5),
        };
        var failingStore = new PreloadedFailingStore(preloaded);
        var random = FakeRandomProvider.WithSeed(42);
        var timeProvider = new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) };
        var engine = new GameEngine(random, failingStore, timeProvider);

        engine.StartNewGame(DifficultyPresets.Beginner);
        engine.OpenCell(new CellPoint(0, 0));

        // Открываем все безопасные клетки, кроме последней.
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

        // Act: победа за 3 секунды — рекорд должен попасть в топ-5,
        // но стор отвергает сохранение.
        engine.OpenCell(lastSafe);

        // Assert: игра выиграна, в памяти остались все 5 старых записей
        // (новый рекорд не добавлен, пятая не вытеснена).
        Assert.Equal(GameState.Won, engine.State);
        var records = engine.Records.ToList();
        Assert.Equal(5, records.Count);
        Assert.Contains(records, r => r.PlayerName == "p5");
    }

    [Fact]
    public void StartNewGame_ResetsEverything()
    {
        // Arrange: играем, побеждаем, у нас ненулевой Elapsed и записи.
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

        // Act: начинаем новую игру.
        engine.StartNewGame(DifficultyPresets.Beginner);

        // Assert: состояние NotStarted, Elapsed = 0, все клетки Closed — правило §11.
        Assert.Equal(GameState.NotStarted, engine.State);
        Assert.Equal(TimeSpan.Zero, engine.Elapsed);
        Assert.All(engine.GetAllCells(), c => Assert.Equal(CellState.Closed, c.State));
    }

    [Fact]
    public void SaveRecord_GameStateChangedWon_RecordAlreadyVisible()
    {
        // Arrange: побеждаем, подписываемся на GameStateChanged.
        // Проверяем, что в обработчике Won engine.Records уже содержит новую запись.
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

        bool recordAlreadyInList = false;

        // Act: победа — подписываемся на событие и проверяем Records.
        engine.GameStateChanged += (s, e) =>
        {
            if (e.NewState == GameState.Won)
                recordAlreadyInList = engine.Records.Any();
        };
        engine.OpenCell(lastSafe);

        // Assert: в обработчике Won рекорд уже в engine.Records.
        Assert.True(recordAlreadyInList, "В обработчике Won engine.Records пуст — SaveRecord должен сработать до GameStateChanged.");
    }

    [Fact]
    public void PlayerName_SavedInRecord()
    {
        // Arrange: устанавливаем PlayerName, побеждаем.
        var timeProvider = new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) };
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(42),
            new InMemoryRecordStore(),
            timeProvider);
        engine.PlayerName = "TestPlayer";
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

        // Act: победа.
        engine.OpenCell(lastSafe);
        Assert.Equal(GameState.Won, engine.State);

        // Assert: рекорд содержит PlayerName == "TestPlayer".
        var record = Assert.Single(engine.Records);
        Assert.Equal("TestPlayer", record.PlayerName);
    }

    [Fact]
    public void Difficulty_ForPresets_ReturnsCorrectLevel()
    {
        // Arrange: движок.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore());

        // Act + Assert: каждый пресет даёт свой уровень.
        engine.StartNewGame(DifficultyPresets.Beginner);
        Assert.Equal(DifficultyLevel.Beginner, engine.Difficulty);

        engine.StartNewGame(DifficultyPresets.Intermediate);
        Assert.Equal(DifficultyLevel.Intermediate, engine.Difficulty);

        engine.StartNewGame(DifficultyPresets.Expert);
        Assert.Equal(DifficultyLevel.Expert, engine.Difficulty);

        engine.StartNewGame(new GameSettings(10, 10, 15));
        Assert.Equal(DifficultyLevel.Custom, engine.Difficulty);
    }
}
