using MinesweeperCore.Domain;
using MinesweeperCore.Events;

namespace MinesweeperCore.Engine;

/// <summary>
/// Открывает клетки и делает каскадное открытие без рекурсии (итеративный Flood Fill).
/// </summary>
internal sealed class RevealService
{
    /// <summary>
    /// Открывает одну клетку.
    /// </summary>
    /// <returns>
    /// `CellChange` если клетка изменила состояние, null если действие игнорируется.
    /// </returns>
    public CellChange? RevealCell(Board board, CellPoint point, out CellContent content)
    {
        content = CellContent.Empty;
        if (!board.IsValid(point)) return null;

        var cell = board[point];
        if (cell.State != CellState.Closed && cell.State != CellState.Question)
            return null;

        var previousState = cell.State;

        if (cell.IsMine) content = CellContent.Mine;
        else if (cell.AdjacentMines == 0) content = CellContent.Empty;
        else content = CellContent.Number;

        board.Open(point, content);
        return new CellChange(point, previousState, board[point].State);
    }

    /// <summary>
    /// При поражении: открывает все мины и помечает неверные флаги как WrongFlag.
    /// </summary>
    public IReadOnlyList<CellInfo> RevealAll(Board board)
    {
        var revealedInfos = new List<CellInfo>();

        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                var cell = board[x, y];
                if (cell.IsMine && cell.State != CellState.Opened && cell.State != CellState.Flagged)
                {
                    board.Open(new CellPoint(x, y), CellContent.Mine);
                    revealedInfos.Add(board.GetCellInfo(new CellPoint(x, y)));
                }
            }

        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                var cell = board[x, y];
                if (!cell.IsMine && cell.State == CellState.Flagged)
                {
                    board.SetWrongFlag(x, y);
                    revealedInfos.Add(board.GetCellInfo(new CellPoint(x, y)));
                }
            }

        return revealedInfos;
    }

    /// <summary>
    /// Каскадное открытие (Flood Fill) для пустой клетки.
    /// Использует Queue — без StackOverflow. visited — flat bool[] вместо
    /// HashSet<CellPoint> (меньше памяти, прямая индексация). Обход соседей инлайн —
    /// без yield return и аллокаций IEnumerator на каждом шаге.
    /// </summary>
    /// <returns>Список всех открытых клеток.</returns>
    public List<CellPoint> CascadeReveal(Board board, CellPoint startPoint)
    {
        // startPoint уже в списке (см. инициализацию). Соседи добавляются при Open.
        // Повторный revealed.Add(current) здесь давал дубликаты Empty-клеток:
        // Empty попадала в список при Open у родителя и ещё раз на своём Dequeue.
        var revealed = new List<CellPoint> { startPoint };
        int w = board.Width, h = board.Height;
        var visited = new bool[w * h];
        var queue = new Queue<CellPoint>();

        int startFlat = startPoint.X * h + startPoint.Y;
        visited[startFlat] = true;
        queue.Enqueue(startPoint);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            int cx = current.X, cy = current.Y;
            var cell = board[current];

            if (cell.State != CellState.Opened || cell.Content != CellContent.Empty)
                continue;

            int x0 = cx > 0 ? cx - 1 : 0;
            int x1 = cx < w - 1 ? cx + 1 : w - 1;
            int y0 = cy > 0 ? cy - 1 : 0;
            int y1 = cy < h - 1 ? cy + 1 : h - 1;

            for (int dx = x0; dx <= x1; dx++)
            {
                for (int dy = y0; dy <= y1; dy++)
                {
                    if (dx == cx && dy == cy) continue;
                    int flat = dx * h + dy;
                    if (visited[flat]) continue;
                    visited[flat] = true;

                    var n = board[dx, dy];
                    if (n.State == CellState.Opened || n.State == CellState.Flagged) continue;
                    if (n.IsMine) continue;

                    CellContent neighborContent = n.AdjacentMines == 0
                        ? CellContent.Empty
                        : CellContent.Number;
                    board.Open(new CellPoint(dx, dy), neighborContent);
                    revealed.Add(new CellPoint(dx, dy));

                    if (neighborContent == CellContent.Empty)
                        queue.Enqueue(new CellPoint(dx, dy));
                }
            }
        }

        return revealed;
    }
}