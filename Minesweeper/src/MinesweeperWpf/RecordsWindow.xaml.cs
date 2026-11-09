using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MinesweeperCore.Abstractions;
using MinesweeperCore.Domain;

namespace MinesweeperWpf;

/// <summary>
/// Interaction logic for RecordsWindow.xaml
/// Отображает лучшие результаты по трём уровням сложности.
/// </summary>
public partial class RecordsWindow : Window
{
    /// <summary>
    /// Представление одной записи о рекорде.
    /// </summary>
    private sealed class RecordDisplay
    {
        public int Rank { get; set; }
        public string PlayerName { get; set; } = "Аноним";
        public string TimeDisplay { get; set; } = string.Empty;
    }

    public RecordsWindow(IRecordStore store)
    {
        InitializeComponent();
        LoadTab(DifficultyLevel.Beginner, BeginnerList, store);
        LoadTab(DifficultyLevel.Intermediate, IntermediateList, store);
        LoadTab(DifficultyLevel.Expert, ExpertList, store);
    }

    /// <summary>
    /// Загружает рекорды для заданного уровня сложности в ListView.
    /// </summary>
    private void LoadTab(DifficultyLevel level, DataGrid listView, IRecordStore store)
    {
        var records = store.LoadRecords(level).ToList();
        var display = new ObservableCollection<RecordDisplay>();
        for (int i = 0; i < records.Count; i++)
        {
            display.Add(new RecordDisplay
            {
                Rank = i + 1,
                PlayerName = string.IsNullOrWhiteSpace(records[i].PlayerName) ? "Аноним" : records[i].PlayerName,
                TimeDisplay = $"{records[i].TimeSeconds:F1} сек"
            });
        }
        listView.ItemsSource = display;
    }

    /// <summary>
    /// Обработка нажатия клавиш (Escape для закрытия).
    /// </summary>
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }
}
