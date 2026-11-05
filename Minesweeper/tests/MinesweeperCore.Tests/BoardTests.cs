using MinesweeperCore.Domain;
using Xunit;

namespace MinesweeperCore.Tests;

/// <summary>
/// Тесты класса Board — сетка клеток, валидация координат, доступ.
/// </summary>
public class BoardTests
{
    [Fact]
    public void IsValid_ReturnsTrue_ForValidCellPoint()
    {
        // Arrange: поле 9×9, проверяем углы и центр.
        var board = new Board(9, 9);

        // Act & Assert: все координаты в пределах поля.
        Assert.True(board.IsValid(new CellPoint(0, 0)));
        Assert.True(board.IsValid(new CellPoint(8, 8)));
        Assert.True(board.IsValid(new CellPoint(4, 4)));
    }

    [Fact]
    public void IsValid_ReturnsFalse_ForInvalidCellPoint()
    {
        // Arrange: поле 9×9, проверяем координаты за пределами.
        var board = new Board(9, 9);

        // Act & Assert: отрицательные и за пределами — false.
        Assert.False(board.IsValid(new CellPoint(-1, 0)));
        Assert.False(board.IsValid(new CellPoint(9, 0)));
        Assert.False(board.IsValid(new CellPoint(0, -1)));
        Assert.False(board.IsValid(new CellPoint(0, 9)));
    }

    [Fact]
    public void TotalCells_ReturnsWidthTimesHeight()
    {
        // Arrange: поля разных размеров.
        var small = new Board(9, 9);
        var big = new Board(30, 16);

        // Act & Assert: 9×9=81, 30×16=480.
        Assert.Equal(81, small.TotalCells);
        Assert.Equal(480, big.TotalCells);
    }

    [Fact]
    public void GetCellInfo_ReturnsCorrectInitialState()
    {
        // Arrange: поле 9×9, клетка в левом верхнем углу.
        var board = new Board(9, 9);

        // Act: получаем снимок клетки.
        var info = board.GetCellInfo(new CellPoint(0, 0));

        // Assert: начальное состояние — закрыта, пусто, 0 мин.
        Assert.Equal(new CellPoint(0, 0), info.Position);
        Assert.Equal(CellState.Closed, info.State);
        Assert.Equal(CellContent.Empty, info.Content);
        Assert.Equal(0, info.AdjacentMines);
    }

    [Fact]
    public void GetAllCellInfos_ReturnsAllCells()
    {
        // Arrange: поле 9×9 — 81 клетка.
        var board = new Board(9, 9);

        // Act: получаем снимки всех клеток.
        var infos = board.GetAllCellInfos().ToList();

        // Assert: возвращены все 81 клетка.
        Assert.Equal(81, infos.Count);
    }

    [Fact]
    public void Reset_SetsAllCellsToInitialState()
    {
        // Arrange: поле, модифицируем одну клетку через ref (Cell — struct).
        var board = new Board(9, 9);
        board.CellRef(0, 0).IsMine = true;
        board.CellRef(0, 0).AdjacentMines = 3;
        board.Open(new CellPoint(0, 0), CellContent.Number);

        // Act: сбрасываем всё поле.
        board.Reset();

        // Assert: клетка вернулась в начальное состояние — читаем через копию.
        Assert.Equal(CellState.Closed, board[0, 0].State);
        Assert.Equal(CellContent.Empty, board[0, 0].Content);
        Assert.False(board[0, 0].IsMine);
        Assert.Equal(0, board[0, 0].AdjacentMines);
    }

    // --- FlaggedCount tests (T44.3) ---

    [Fact]
    public void FlaggedCount_IncrementsOnClosedToFlagged()
    {
        // Arrange: поле 9×9, начальное состояние — Closed.
        var board = new Board(9, 9);
        Assert.Equal(0, board.FlaggedCount);

        // Act: CycleMark на Closed → Flagged.
        board.CycleMark(new CellPoint(0, 0));

        // Assert: счётчик увеличился на 1.
        Assert.Equal(1, board.FlaggedCount);
    }

    [Fact]
    public void FlaggedCount_DecrementsOnFlaggedToQuestion()
    {
        // Arrange: CycleMark × 2 (Closed → Flagged → Question).
        var board = new Board(9, 9);
        board.CycleMark(new CellPoint(0, 0));
        Assert.Equal(1, board.FlaggedCount);

        // Act: CycleMark на Flagged → Question.
        board.CycleMark(new CellPoint(0, 0));

        // Assert: счётчик уменьшился на 1.
        Assert.Equal(0, board.FlaggedCount);
    }

    [Fact]
    public void FlaggedCount_UnchangedOnQuestionToClosed()
    {
        // Arrange: CycleMark × 3 (Closed → Flagged → Question → Closed).
        var board = new Board(9, 9);
        board.CycleMark(new CellPoint(0, 0));
        board.CycleMark(new CellPoint(0, 0));
        board.CycleMark(new CellPoint(0, 0));

        // Assert: после полного цикла счётчик = 0.
        Assert.Equal(0, board.FlaggedCount);
    }

    [Fact]
    public void FlaggedCount_UnchangedOnSetWrongFlag()
    {
        // Arrange: ставим флаг (Flagged, count = 1).
        var board = new Board(9, 9);
        board.CycleMark(new CellPoint(0, 0));
        Assert.Equal(1, board.FlaggedCount);

        // Act: SetWrongFlag на той же клетке (игрок флаг не снимал).
        board.SetWrongFlag(0, 0);

        // Assert: счётчик не изменился.
        Assert.Equal(1, board.FlaggedCount);
    }

    [Fact]
    public void FlaggedCount_UnchangedOnOpen()
    {
        // Arrange: поле 9×9, ни одного флага.
        var board = new Board(9, 9);

        // Act: открываем клетку.
        board.Open(new CellPoint(0, 0), CellContent.Empty);

        // Assert: FlaggedCount остаётся 0.
        Assert.Equal(0, board.FlaggedCount);
    }

    [Fact]
    public void FlaggedCount_ResetsToZero()
    {
        // Arrange: ставим флаг.
        var board = new Board(9, 9);
        board.CycleMark(new CellPoint(0, 0));
        Assert.Equal(1, board.FlaggedCount);

        // Act: сбрасываем поле.
        board.Reset();

        // Assert: счётчик сброшен.
        Assert.Equal(0, board.FlaggedCount);
    }

    [Fact]
    public void PlaceMines_DuplicateCellInSpan_Throws()
    {
        // Arrange: свежая доска, span с дубликатом.
        var board = new Board(9, 9);
        var mines = new[] { new CellPoint(0, 0), new CellPoint(0, 0) };

        // Act + Assert: без throw соседи получили бы AdjacentMines += 2,
        // а MineCount остался бы прежним — победа стала бы недостижимой молча.
        Assert.Throws<InvalidOperationException>(() => board.PlaceMines(mines));
    }

    [Fact]
    public void PlaceMines_SameMinesTwice_Throws()
    {
        // Arrange: первый вызов легален.
        var board = new Board(9, 9);
        board.PlaceMines(new[] { new CellPoint(0, 0), new CellPoint(5, 5) });

        // Act + Assert: второй вызов без Reset — нарушение контракта «один раз per Board».
        Assert.Throws<InvalidOperationException>(() =>
            board.PlaceMines(new[] { new CellPoint(0, 0), new CellPoint(5, 5) }));
    }

    [Fact]
    public void PlaceMines_DifferentMinesTwice_Throws()
    {
        // Arrange: первый вызов легален.
        var board = new Board(9, 9);
        board.PlaceMines(new[] { new CellPoint(0, 0) });

        // Act + Assert: второй вызов с ДРУГИМИ минами тоже должен падать —
        // иначе на доске окажется 2 мины при MineCount = 1, и победа станет недостижимой.
        Assert.Throws<InvalidOperationException>(() =>
            board.PlaceMines(new[] { new CellPoint(5, 5) }));
    }

    [Fact]
    public void PlaceMines_AfterReset_Allowed()
    {
        // Arrange: первый вызов, затем сброс.
        var board = new Board(9, 9);
        board.PlaceMines(new[] { new CellPoint(0, 0) });
        board.Reset();

        // Act + Assert: после Reset контракт снова выполняется — не бросает.
        board.PlaceMines(new[] { new CellPoint(5, 5) });
        Assert.True(board.CellRef(5, 5).IsMine);
        Assert.False(board.CellRef(0, 0).IsMine);
    }
}
