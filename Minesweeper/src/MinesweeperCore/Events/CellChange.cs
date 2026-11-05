using MinesweeperCore.Domain;

namespace MinesweeperCore.Events;

/// <summary>
/// Пара «координата + новое состояние клетки». Используется внутри CellChangedEventArgs.
/// </summary>
public sealed class CellChange
{
    public CellPoint Position { get; }
    public CellState PreviousState { get; }
    public CellState NewState { get; }

    public CellChange(CellPoint position, CellState previousState, CellState newState)
    {
        Position = position;
        PreviousState = previousState;
        NewState = newState;
    }
}
