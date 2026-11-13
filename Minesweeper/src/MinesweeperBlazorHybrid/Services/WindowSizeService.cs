namespace MinesweeperBlazorHybrid.Services;

/// <summary>
/// Позволяет Blazor-компоненту попросить хост-форму WinForms
/// изменить размер окна под текущее игровое поле.
/// </summary>
public sealed class WindowSizeService
{
    /// <summary>Событие: (ширина, высота) поля в пикселях.</summary>
    public event Action<int, int>? SizeRequested;

    public void RequestSize(int boardWidth, int boardHeight)
        => SizeRequested?.Invoke(boardWidth, boardHeight);
}