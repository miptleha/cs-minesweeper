using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace MinesweeperAvalonia.Windows;

/// <summary>
/// Окно «Управление»: описание трёх основных действий и их клавиш.
/// </summary>
public partial class ControlsWindow : Window
{
    public ControlsWindow()
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
}
