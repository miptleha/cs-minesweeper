namespace MinesweeperCore.Domain;

/// <summary>
/// Внутренняя модель клетки. Используется только внутри ядра.
/// Struct: на 1M клеток это ~16 MB и одна аллокация массива + memset вместо
/// 1M отдельных new Cell() с GC-давлением.
/// default(Cell) = { Closed, Empty, False, 0 } — это ровно начальное
/// состояние, поэтому new Cell[w, h] не требует инициализации.
/// Модификация — через ref (Board.CellRef); Board[x, y] возвращает копию для чтения.
/// </summary>
internal struct Cell
{
    public CellState State { get; private set; }
    public CellContent Content { get; internal set; }
    public bool IsMine { get; internal set; }
    public int AdjacentMines { get; internal set; }

    /// <summary>
    /// Сбрасывает клетку в начальное состояние.
    /// </summary>
    public void Reset()
    {
        State = CellState.Closed;
        Content = CellContent.Empty;
        IsMine = false;
        AdjacentMines = 0;
    }

    /// <summary>
    /// Устанавливает состояние WrongFlag (ошибочный флаг).
    /// </summary>
    public void SetWrongFlag()
    {
        State = CellState.WrongFlag;
    }

    /// <summary>
    /// Открывает клетку, устанавливая её содержимое.
    /// </summary>
    internal void Open(CellContent content)
    {
        State = CellState.Opened;
        Content = content;
    }

    /// <summary>
    /// Циклически переключает метку закрытой клетки:
    /// Closed → Flagged → Question → Closed.
    /// На клетках в других состояниях (Opened, WrongFlag) ничего не делает.
    /// </summary>
    public void CycleMark()
    {
        State = State switch
        {
            CellState.Closed => CellState.Flagged,
            CellState.Flagged => CellState.Question,
            CellState.Question => CellState.Closed,
            _ => State
        };
    }
}