using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MinesweeperAvalonia.Windows;

/// <summary>
/// Диалог запроса имени игрока при новом рекорде.
/// </summary>
public partial class NamePromptWindow : Window
{
    /// <summary>
    /// Введённое имя игрока. По умолчанию — «Игрок».
    /// </summary>
    public string PlayerName { get; private set; } = "Игрок";

    public NamePromptWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Обработка кнопки OK: сохраняет имя и закрывает окно с результатом true.
    /// </summary>
    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        PlayerName = string.IsNullOrWhiteSpace(NameBox.Text) ? "Аноним" : NameBox.Text.Trim();
        Close(true); // Avalonia: Close(object?) вместо DialogResult = true
    }
}
