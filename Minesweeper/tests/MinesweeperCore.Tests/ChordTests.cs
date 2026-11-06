using MinesweeperCore.Domain;
using MinesweeperCore.Engine;
using MinesweeperCore.Infrastructure;
using MinesweeperCore.Tests.Fakes;
using Xunit;

namespace MinesweeperCore.Tests;

/// <summary>
/// Тесты аккорда (Chord) — открытие соседей при совпадении флагов.
/// </summary>
public class ChordTests
{
    /// <summary>
    /// Создаёт движок 5×5 с 1 миной в (0,0). 
    /// Вручную ставим мину через PlaceMines, чтобы не зависеть от Random.
    /// </summary>
    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(
            FakeRandomProvider.WithSeed(1),
            new InMemoryRecordStore(),
            new FakeTimeProvider { UtcNow = new DateTime(2024, 1, 1) });

        // 1 мина в настройках, чтобы WinCondition работал корректно (25 клеток, 1 мина = 24 безопасных).
        engine.StartNewGame(new GameSettings(5, 5, 1));
        engine.BoardForTesting.PlaceMines(new[] { new CellPoint(0, 0) });
        return engine;
    }

    [Fact]
    public void Chord_FlagsMatch_OpensAllClosedNeighbors()
    {
        // Arrange: открываем (1,1). Она рядом с миной (0,0), поэтому открывается как Number (1).
        // Каскада нет. Ставим флаг на мину (0,0).
        var engine = CreateEngine();
        engine.OpenCell(new CellPoint(1, 1));
        engine.CycleMark(new CellPoint(0, 0));

        // Act: аккорд на (1,1). У неё 1 флаг вокруг и цифра 1.
        engine.Chord(new CellPoint(1, 1));

        // Assert: открылись все безопасные клетки, победа (24 из 25).
        Assert.Equal(GameState.Won, engine.State);
        Assert.Equal(CellState.Opened, engine.GetCell(new CellPoint(0, 1)).State);
        Assert.Equal(CellState.Opened, engine.GetCell(new CellPoint(1, 0)).State);
    }

    [Fact]
    public void Chord_FlagsMismatch_DoesNothing()
    {
        // Arrange: открываем (1,1). Флагов вокруг нет (0).
        var engine = CreateEngine();
        engine.OpenCell(new CellPoint(1, 1));

        int cellChangedCount = 0;
        int cellsRevealedCount = 0;
        engine.CellChanged += (s, e) => cellChangedCount++;
        engine.CellsRevealed += (s, e) => cellsRevealedCount++;

        // Act: аккорд на (1,1). Цифра 1, флагов 0 — несовпадение.
        engine.Chord(new CellPoint(1, 1));

        // Assert: ноль событий, соседи закрыты.
        Assert.Equal(0, cellChangedCount);
        Assert.Equal(0, cellsRevealedCount);
        Assert.Equal(CellState.Closed, engine.GetCell(new CellPoint(0, 1)).State);
    }

    [Fact]
    public void Chord_WithUnflaggedMine_Loses()
    {
        // Arrange: открываем (1,1). Ставим ошибочный флаг на безопасную (2,2).
        // Вокруг (1,1) теперь 1 флаг (ошибочный) и цифра 1 (настоящая мина в 0,0 не помечена).
        var engine = CreateEngine();
        engine.OpenCell(new CellPoint(1, 1));
        engine.CycleMark(new CellPoint(2, 2));

        // Act: аккорд на (1,1) открывает соседей, среди которых настоящая мина (0,0).
        engine.Chord(new CellPoint(1, 1));

        // Assert: поражение, триггерная мина открыта.
        Assert.Equal(GameState.Lost, engine.State);
        var mineCell = engine.GetCell(new CellPoint(0, 0));
        Assert.Equal(CellState.Opened, mineCell.State);
        Assert.Equal(CellContent.Mine, mineCell.Content);
    }

    [Fact]
    public void Chord_QuestionNeighbors_AreOpened()
    {
        // Arrange: открываем (1,1). Ставим Question на (2,2) и флаг на мину (0,0).
        var engine = CreateEngine();
        engine.OpenCell(new CellPoint(1, 1));
        engine.CycleMark(new CellPoint(2, 2));
        engine.CycleMark(new CellPoint(2, 2)); // → Question
        engine.CycleMark(new CellPoint(0, 0)); // → Flag

        // Act: аккорд на (1,1) — цифра 1, флагов 1.
        engine.Chord(new CellPoint(1, 1));

        // Assert: Question-клетка открылась (мягкая метка не блокирует аккорд).
        Assert.Equal(CellState.Opened, engine.GetCell(new CellPoint(2, 2)).State);
    }

    [Fact]
    public void Chord_OnClosedCell_DoesNothing()
    {
        // Arrange: открываем (1,1), чтобы запустить игру. Клетка (1,2) остаётся Closed.
        var engine = CreateEngine();
        engine.OpenCell(new CellPoint(1, 1));

        // Act: пытаемся сделать аккорд на закрытой клетке (1,2).
        engine.Chord(new CellPoint(1, 2));

        // Assert: ничего не произошло, клетка закрыта.
        Assert.Equal(CellState.Closed, engine.GetCell(new CellPoint(1, 2)).State);
    }
}
