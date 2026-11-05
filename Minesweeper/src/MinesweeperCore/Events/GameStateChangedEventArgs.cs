using MinesweeperCore.Domain;

namespace MinesweeperCore.Events;

/// <summary>
/// Игра перешла в состояние Победа или Поражение.
/// </summary>
public sealed class GameStateChangedEventArgs : EventArgs
{
    public GameState NewState { get; }
    public CellInfo? TriggerCell { get; }

    public GameStateChangedEventArgs(GameState newState, CellInfo? triggerCell = null)
    {
        NewState = newState;
        TriggerCell = triggerCell;
    }
}
