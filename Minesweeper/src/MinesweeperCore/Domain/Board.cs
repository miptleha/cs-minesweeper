namespace MinesweeperCore.Domain;

/// <summary>
/// Сетка клеток. Валидирует координаты и предоставляет доступ по CellPoint или индексу.
/// </summary>
internal sealed class Board
{
    private readonly Cell[,] _cells;
    private readonly int _width;
    private readonly int _height;
    private int _flaggedCount;

    public Board(int width, int height)
    {
        _width = width;
        _height = height;
        // Cell — struct. default(Cell) = начальное состояние, поэтому new Cell[w, h]
        // даёт корректно инициализированную доску за одну аллокацию + memset.
        _cells = new Cell[width, height];
    }

    public int Width => _width;
    public int Height => _height;
    public int TotalCells => _width * _height;

    /// <summary>
    /// Количество открытых безопасных клеток. Используется для O(1) проверки победы.
    /// </summary>
    public int OpenedSafeCells { get; private set; }

    /// <summary>
    /// Количество флагов, поставленных игроком и ещё не снятых.
    /// Меняется только в CycleMark. Не меняется при Open/SetWrongFlag/Won/Lost —
    /// потому что игрок свой флаг не снимал. Используется для O(1)-расчёта
    /// GameEngine.RemainingMines.
    /// </summary>
    public int FlaggedCount => _flaggedCount;

    /// <summary>
    /// ref-доступ к клетке для модификации (Cell — struct).
    /// Использовать только внутри ядра и в тестах, где нужно изменить клетку in-place.
    /// Для чтения — this[x, y] (возвращает копию).
    /// ВНИМАНИЕ: если выставляете IsMine = true, AdjacentMines у соседей НЕ обновится.
    /// Используйте Board.PlaceMines(mines), чтобы поставить мины с корректным подсчётом.
    /// </summary>
    public ref Cell CellRef(int x, int y) => ref _cells[x, y];

    /// <summary>
    /// ref-доступ к клетке по CellPoint. См. CellRef(int, int).
    /// </summary>
    public ref Cell CellRef(CellPoint point) => ref _cells[point.X, point.Y];

    /// <summary>
    /// Копия клетки для чтения. Модификация через индексатор не изменит доску —
    /// для модификации используйте CellRef или методы Open/CycleMark/SetWrongFlag/PlaceMines.
    /// </summary>
    public Cell this[int x, int y] => _cells[x, y];

    /// <summary>
    /// Копия клетки по CellPoint. См. this[int, int].
    /// </summary>
    public Cell this[CellPoint point] => _cells[point.X, point.Y];

    /// <summary>
    /// Проверяет, что координаты находятся в пределах поля.
    /// </summary>
    public bool IsValid(CellPoint point) =>
        point.X >= 0 && point.X < _width &&
        point.Y >= 0 && point.Y < _height;

    /// <summary>
    /// Возвращает соседей точки, которые находятся в пределах поля.
    /// </summary>
    public IEnumerable<CellPoint> GetValidNeighbors(CellPoint point)
    {
        foreach (var neighbor in point.Neighbors())
        {
            if (IsValid(neighbor))
                yield return neighbor;
        }
    }

    private bool _minesPlaced;
    internal bool MinesPlaced => _minesPlaced;

    /// <summary>
    /// Расставляет мины и сразу считает AdjacentMines у их соседей.
    /// Обходит только мины (O(mineCount × 8)), а не всё поле (O(TotalCells × 8)) —
    /// для 100K мин на 1M клеток это ~800K операций вместо ~8M.
    /// Ожидает, что AdjacentMines у всех клеток = 0 (свежий Board или после Reset).
    /// </summary>
    internal void PlaceMines(ReadOnlySpan<CellPoint> mines)
    {
        if (_minesPlaced)
            throw new InvalidOperationException(
                "PlaceMines уже вызывался для этого Board. Ожидается свежий Board или Reset.");

        int w = _width, h = _height;

        foreach (var p in mines)
        {
            int x0 = p.X > 0 ? p.X - 1 : 0;
            int x1 = p.X < w - 1 ? p.X + 1 : w - 1;
            int y0 = p.Y > 0 ? p.Y - 1 : 0;
            int y1 = p.Y < h - 1 ? p.Y + 1 : h - 1;

            ref var mine = ref _cells[p.X, p.Y];

            // Контракт: span не должен содержать дубликатов. Иначе соседи получат
            // AdjacentMines += 2, а MineCount в Settings останется прежним —
            // победа станет недостижимой без единого исключения. Лучше упасть здесь.
            if (mine.IsMine)
                throw new InvalidOperationException(
                    $"Дубликат мины в ({p.X}, {p.Y}): PlaceMines ожидает уникальные клетки.");

            mine.IsMine = true;

            for (int dx = x0; dx <= x1; dx++)
            {
                for (int dy = y0; dy <= y1; dy++)
                {
                    if (dx == p.X && dy == p.Y)
                        continue;
                    ref var n = ref _cells[dx, dy];
                    n.AdjacentMines++;
                }
            }
        }

        _minesPlaced = true;
    }

    /// <summary>
    /// Открывает клетку через Board. Если клетка уже Opened — no-op;
    /// иначе открывает и, если клетка не мина, инкрементирует OpenedSafeCells.
    /// </summary>
    public void Open(CellPoint point, CellContent content)
    {
        ref var cell = ref _cells[point.X, point.Y];
        if (cell.State == CellState.Opened)
            return; // no-op

        cell.Open(content);
        if (!cell.IsMine)
            OpenedSafeCells++;
    }

    /// <summary>
    /// Циклически переключает метку клетки (Closed → Flagged → Question → Closed).
    /// Обновляет FlaggedCount по действию игрока.
    /// </summary>
    public void CycleMark(CellPoint point)
    {
        if (!IsValid(point))
            return;

        ref var cell = ref _cells[point.X, point.Y];
        var prev = cell.State;

        // Метку можно менять только на Closed/Flagged/Question.
        if (prev != CellState.Closed
            && prev != CellState.Flagged
            && prev != CellState.Question)
            return;

        cell.CycleMark();

        // FlaggedCount отражает действия игрока: +1 при постановке флага,
        // −1 при снятии. Question и Closed — без изменений.
        if (prev == CellState.Closed && cell.State == CellState.Flagged)
            _flaggedCount++;
        else if (prev == CellState.Flagged && cell.State == CellState.Question)
            _flaggedCount--;
    }

    /// <summary>
    /// Устанавливает состояние WrongFlag. FlaggedCount не трогает —
    /// игрок флаг не снимал, просто на Lost выяснилось, что он ошибочный.
    /// </summary>
    public void SetWrongFlag(int x, int y)
    {
        ref var cell = ref _cells[x, y];
        cell.SetWrongFlag();
    }

    /// <summary>
    /// Создаёт снимок клетки для UI.
    /// AdjacentMines отдаётся только для открытых клеток с цифрой, иначе 0.
    /// </summary>
    public CellInfo GetCellInfo(CellPoint point)
    {
        var cell = _cells[point.X, point.Y];
        // AdjacentMines отдаём только для открытых клеток с числом —
        // иначе UI получает скрытую информацию (цифры у закрытых клеток).
        int adjacentMines = (cell.State == CellState.Opened && cell.Content == CellContent.Number)
            ? cell.AdjacentMines
            : 0;

        return new CellInfo(point, cell.State, cell.Content, adjacentMines);
    }

    /// <summary>
    /// Создаёт снимки всех клеток для полной перерисовки.
    /// </summary>
    public IEnumerable<CellInfo> GetAllCellInfos()
    {
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                yield return GetCellInfo(new CellPoint(x, y));
            }
        }
    }

    /// <summary>
    /// Сбрасывает все клетки в начальное состояние: State = Closed, Content = Empty,
    /// IsMine = false, AdjacentMines = 0. Мины тоже снимаются —
    /// Cell — struct, Array.Clear заполняет default(Cell).
    /// </summary>
    public void Reset()
    {
        OpenedSafeCells = 0;
        _flaggedCount = 0;
        _minesPlaced = false;
        Array.Clear(_cells, 0, _cells.Length);
    }
}