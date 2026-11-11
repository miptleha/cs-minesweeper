using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

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

        // Перехватываем Esc в фазе туннелирования (до того, как элементы UI его получат)
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close();
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
