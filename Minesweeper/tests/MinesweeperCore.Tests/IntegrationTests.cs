using MinesweeperCore.Domain;
using MinesweeperCore.Engine;
using MinesweeperCore.Infrastructure;
using MinesweeperCore.Tests.Fakes;
using Xunit;

namespace MinesweeperCore.Tests;

/// <summary>
/// Интеграционные тесты: полная партия через GameEngine —
/// клики, метки, поражение, финальная проверка всех состояний клеток.
/// </summary>
public class IntegrationTests
{
    [Fact]
    public void FullGame_PlayerHitsMine_AllStatesCorrectlyResolved()
    {
        // Arrange: Beginner (9×9, 10 мин). Seed 1 — детерминированная расстановка.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);

        // Игрок начинает: клик в (4,4) — безопасный первый клик.
        engine.OpenCell(new CellPoint(4, 4));

        var board = engine.BoardForTesting;

        // Ищем на поле клетки для разных сценариев:
        // - 3 мины (Closed — можно поставить метку или кликнуть)
        // - 2 безопасные клетки (не Opened — можно поставить метку)
        // После каскада от (4,4) много safe-клеток Opened, поэтому safe
        // ищем среди Closed/Flagged/Question, а не среди всех.
        CellPoint correctMine = default, wrongSafeCell = default;
        CellPoint questionMine = default, questionSafeCell = default;
        CellPoint triggerMine = default;
        int foundMines = 0;
        int foundSafe = 0;
        bool foundCorrect = false, foundQuestion = false, foundTrigger = false;
        bool foundWrongSafe = false, foundQuestionSafe = false;

        // Шаг 1: ищем 3 Closed мины.
        for (int x = 0; x < board.Width && foundMines < 3; x++)
            for (int y = 0; y < board.Height && foundMines < 3; y++)
            {
                var cell = board[x, y];
                var p = new CellPoint(x, y);
                if (!cell.IsMine || cell.State != CellState.Closed) continue;
                if (p == new CellPoint(4, 4)) continue; // первый клик безопасен

                if (!foundCorrect)
                { correctMine = p; foundCorrect = true; foundMines++; }
                else if (!foundQuestion)
                { questionMine = p; foundQuestion = true; foundMines++; }
                else if (!foundTrigger)
                { triggerMine = p; foundTrigger = true; foundMines++; }
            }

        // Шаг 2: ищем 2 safe-клетки (не Opened — можно поставить метку).
        for (int x = 0; x < board.Width && foundSafe < 2; x++)
            for (int y = 0; y < board.Height && foundSafe < 2; y++)
            {
                var cell = board[x, y];
                var p = new CellPoint(x, y);
                if (cell.IsMine) continue;
                // CycleMark игнорирует Opened — ищем Closed/Flagged/Question.
                if (cell.State == CellState.Opened) continue;

                if (!foundWrongSafe)
                { wrongSafeCell = p; foundWrongSafe = true; foundSafe++; }
                else if (!foundQuestionSafe)
                { questionSafeCell = p; foundQuestionSafe = true; foundSafe++; }
            }

        Assert.Equal(3, foundMines);
        Assert.Equal(2, foundSafe);

        // Игрок ставит 4 метки через engine:
        engine.CycleMark(correctMine);   // Closed → Flagged (правильный флаг на мине)
        engine.CycleMark(wrongSafeCell); // Closed → Flagged (ошибочный флаг на safe)
        engine.CycleMark(questionMine);  // Closed → Flagged
        engine.CycleMark(questionMine);  // Flagged → Question
        engine.CycleMark(questionSafeCell);  // Closed → Flagged
        engine.CycleMark(questionSafeCell);  // Flagged → Question

        // Проверяем промежуточное состояние до клика в мину.
        Assert.Equal(CellState.Flagged, engine.GetCell(correctMine).State);
        Assert.Equal(CellState.Flagged, engine.GetCell(wrongSafeCell).State);
        Assert.Equal(CellState.Question, engine.GetCell(questionMine).State);
        Assert.Equal(CellState.Question, engine.GetCell(questionSafeCell).State);

        // Act: игрок кликает в триггерную мину — поражение.
        engine.OpenCell(triggerMine);

        // Assert: игра Lost, все состояния корректны.
        Assert.Equal(GameState.Lost, engine.State);

        // 1) Триггерная мина: Opened + Mine.
        var triggerCell = engine.GetCell(triggerMine);
        Assert.Equal(CellState.Opened, triggerCell.State);
        Assert.Equal(CellContent.Mine, triggerCell.Content);

        // 2) Правильно помеченная мина: осталась Flagged, Content=Empty.
        var correctCell = engine.GetCell(correctMine);
        Assert.Equal(CellState.Flagged, correctCell.State);
        Assert.Equal(CellContent.Empty, correctCell.Content);

        // 3) Ошибочный флаг: стал WrongFlag.
        var wrongCell = engine.GetCell(wrongSafeCell);
        Assert.Equal(CellState.WrongFlag, wrongCell.State);

        // 4) Вопрос на мине: открылся как Opened + Mine.
        var qMineCell = engine.GetCell(questionMine);
        Assert.Equal(CellState.Opened, qMineCell.State);
        Assert.Equal(CellContent.Mine, qMineCell.Content);

        // 5) Вопрос на безопасной: остался Question без изменений.
        var qSafeCell = engine.GetCell(questionSafeCell);
        Assert.Equal(CellState.Question, qSafeCell.State);
        Assert.Equal(CellContent.Empty, qSafeCell.Content);

        // 6) Все остальные не-помеченные мины тоже открыты.
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                var cell = board[x, y];
                var p = new CellPoint(x, y);
                if (!cell.IsMine) continue;
                if (p == correctMine) continue;  // правильный флаг — исключение
                Assert.Equal(CellState.Opened, cell.State);
                Assert.Equal(CellContent.Mine, cell.Content);
            }
    }

    [Fact]
    public void FullGame_PlayerWins_MinesCorrectlyFlagged()
    {
        // Arrange: Beginner (9×9, 10 мин). Seed 42.
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(42),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });
        engine.StartNewGame(DifficultyPresets.Beginner);

        // Игрок: первый клик, потом отмечает все мины флагами через engine,
        // затем открывает все безопасные.
        engine.OpenCell(new CellPoint(0, 0));

        var board = engine.BoardForTesting;

        // Ставим флаги на все мины через CycleMark.
        int flaggedCount = 0;
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (board[x, y].IsMine && board[x, y].State == CellState.Closed)
                {
                    engine.CycleMark(new CellPoint(x, y));
                    flaggedCount++;
                }
        Assert.Equal(10, flaggedCount);

        // Открываем все безопасные клетки — победа.
        CellPoint lastSafe = default;
        bool found = false;
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (!board[x, y].IsMine && board[x, y].State == CellState.Closed)
                { lastSafe = new CellPoint(x, y); found = true; }
        Assert.True(found, "Не нашли безопасную Closed-клетку для победы.");

        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (!board[x, y].IsMine && !(x == lastSafe.X && y == lastSafe.Y))
                    board.Open(new CellPoint(x, y), CellContent.Empty);

        // Act: последний клик — победа.
        engine.OpenCell(lastSafe);

        // Assert: Won, все мины остались Flagged.
        Assert.Equal(GameState.Won, engine.State);
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (board[x, y].IsMine)
                {
                    var cell = engine.GetCell(new CellPoint(x, y));
                    Assert.Equal(CellState.Flagged, cell.State);
                    Assert.Equal(CellContent.Empty, cell.Content);
                }
    }
}
