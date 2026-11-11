using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MinesweeperCore.Abstractions;
using MinesweeperCore.Domain;
using MinesweeperCore.Infrastructure;

namespace MinesweeperAvalonia.Windows;

/// <summary>
/// Строка рекорда: ранг, имя, время.
/// </summary>
public sealed class RecordRow
{
    public int Rank { get; }
    public string PlayerName { get; }
    public string TimeDisplay { get; }

    public RecordRow(int rank, string playerName, string timeDisplay)
    {
        Rank = rank;
        PlayerName = playerName;
        TimeDisplay = timeDisplay;
    }
}

/// <summary>
/// Окно «Лучшие результаты» — 3 вкладки (Новичок, Любитель, Профессионал).
/// </summary>
public partial class RecordsWindow : Window
{
    private readonly ObservableCollection<RecordRow> _beginnerRecords = new();
    private readonly ObservableCollection<RecordRow> _intermediateRecords = new();
    private readonly ObservableCollection<RecordRow> _expertRecords = new();

    public ObservableCollection<RecordRow> BeginnerRecords => _beginnerRecords;
    public ObservableCollection<RecordRow> IntermediateRecords => _intermediateRecords;
    public ObservableCollection<RecordRow> ExpertRecords => _expertRecords;

    public RecordsWindow() : this(new JsonRecordStore())
    {
    }
    
    public RecordsWindow(IRecordStore store)
    {
        InitializeComponent();
        DataContext = this;

        LoadRecords(store, DifficultyLevel.Beginner, _beginnerRecords);
        LoadRecords(store, DifficultyLevel.Intermediate, _intermediateRecords);
        LoadRecords(store, DifficultyLevel.Expert, _expertRecords);

        // Перехватываем Esc в фазе туннелирования (до того, как DataGrid его съест)
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Загружает рекорды из хранилища и заполняет ObservableCollection.
    /// Пустое имя → «Аноним», время с суффиксом «сек» — как в WinForms.
    /// </summary>
    private static void LoadRecords(IRecordStore store, DifficultyLevel level, ObservableCollection<RecordRow> collection)
    {
        var records = store.LoadRecords(level).ToList();
        for (int i = 0; i < records.Count; i++)
        {
            var r = records[i];
            var name = string.IsNullOrWhiteSpace(r.PlayerName) ? "Аноним" : r.PlayerName;
            var time = $"{r.TimeSeconds:F1} сек";
            collection.Add(new RecordRow(i + 1, name, time));
        }
    }

    /// <summary>
    /// Обработка Escape для закрытия окна.
    /// </summary>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
