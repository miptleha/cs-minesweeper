namespace MinesweeperCore.Domain;

/// <summary>
/// Снимок клетки для отправки UI. Не позволяет изменять состояние.
/// </summary>
public sealed record CellInfo
{
    public CellPoint Position { get; }
    public CellState State { get; }
    public CellContent Content { get; }
    public int AdjacentMines { get; }

    public CellInfo(CellPoint position, CellState state, CellContent content, int adjacentMines)
    {
        Position = position;
        State = state;
        Content = content;
        AdjacentMines = adjacentMines;
    }
}
