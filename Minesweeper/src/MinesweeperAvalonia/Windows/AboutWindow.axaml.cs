using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;

namespace MinesweeperAvalonia.Windows;

/// <summary>
/// Окно «О программе».
/// Содержит иконку, описание, ссылку на GitHub и кнопку OK.
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Обработка клика по ссылке на GitHub.
    /// </summary>
    private void OnLinkPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/miptleha/cs-minesweeper")
        {
            UseShellExecute = true
        });
    }
}
