using System.Windows;

namespace MinesweeperWpf;

/// <summary>
/// Interaction logic for NamePromptWindow.xaml
/// Диалог ввода имени игрока при достижении рекорда.
/// </summary>
public partial class NamePromptWindow : Window
{
    /// <summary>
    /// Введённое имя игрока. По умолчанию «Игрок».
    /// </summary>
    public string PlayerName { get; private set; } = "Игрок";

    public NamePromptWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Обработка кнопки OK: сохраняет имя или «Аноним» если пусто.
    /// </summary>
    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        PlayerName = string.IsNullOrWhiteSpace(NameBox.Text) ? "Аноним" : NameBox.Text.Trim();

        // Обязательно закрываем окно с положительным результатом,
        // иначе ShowDialog() в MainWindow будет висеть.
        DialogResult = true;
    }
}
