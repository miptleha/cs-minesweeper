using MinesweeperCore.Domain;

namespace MinesweeperCore.Events;

/// <summary>
/// Каскадное открытие завершилось. Содержит список всех открытых клеток — чтобы UI перерисовал их за один проход.
/// </summary>
public sealed class CellsRevealedEventArgs : EventArgs
{
    public IEnumerable<CellInfo> CellInfos { get; }

    public CellsRevealedEventArgs(IEnumerable<CellInfo> cellInfos)
    {
        CellInfos = cellInfos;
    }
}
