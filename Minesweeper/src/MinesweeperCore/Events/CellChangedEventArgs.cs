using MinesweeperCore.Domain;

namespace MinesweeperCore.Events;

/// <summary>
/// Одна клетка изменилась (открылась, флаг поставлен/снят).
/// </summary>
public sealed class CellChangedEventArgs : EventArgs
{
    public CellChange Change { get; }
    public CellInfo CellInfo { get; }

    public CellChangedEventArgs(CellChange change, CellInfo cellInfo)
    {
        Change = change;
        CellInfo = cellInfo;
    }
}
