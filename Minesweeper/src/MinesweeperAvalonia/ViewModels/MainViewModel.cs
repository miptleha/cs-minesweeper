using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Threading;
using MinesweeperCore.Abstractions;
using MinesweeperCore.Domain;
using MinesweeperCore.Engine;
using MinesweeperCore.Events;
using MinesweeperCore.Infrastructure;
using MinesweeperAvalonia.Services;

namespace MinesweeperAvalonia.ViewModels;

/// <summary>
/// Главный ViewModel приложения.
/// Обёртка над <see cref="GameEngine"/>: подписки на события, коллекция клеток,
/// LED-счётчики, смайлик, таймер.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly GameEngine _engine;
    private readonly IRecordStore _recordStore;
    private readonly DispatcherTimer _timer;
    private CellPoint? _triggerCell;

    // --- Коллекции и подчинённые ViewModel ---

    /// <summary>
    /// Коллекция ViewModel клеток поля. Пересоздаётся при StartGame.
    /// </summary>
    public ObservableCollection<CellViewModel> Cells { get; } = new();

    /// <summary>
    /// LED-дисплей счётчика мин.
    /// </summary>
    public LedDisplayViewModel MineCounter { get; }

    /// <summary>
    /// LED-дисплей таймера.
    /// </summary>
    public LedDisplayViewModel TimerDisplay { get; }

    // --- Свойства из движка (прокси) ---

    /// <summary>Ширина поля.</summary>
    public int Width => _engine.Width;

    /// <summary>Высота поля.</summary>
    public int Height => _engine.Height;

    /// <summary>Текущее состояние игры.</summary>
    public GameState State => _engine.State;

    /// <summary>Уровень сложности текущей игры.</summary>
    public DifficultyLevel Difficulty => _engine.Difficulty;

    /// <summary>Прошедшее время в секундах (для диалогов / победы).</summary>
    public double ElapsedSeconds => _engine.Elapsed.TotalSeconds;

    /// <summary>Хранилище рекордов (нужно для окон Records / NamePrompt).</summary>
    public IRecordStore RecordStore => _recordStore;

    /// <summary>Игровой движок (нужен MainWindow для подписки на GameStateChanged).</summary>
    public GameEngine Engine => _engine;

    /// <summary>Параметры текущей игры.</summary>
    public GameSettings Settings => _engine.Settings;

    /// <summary>Имя игрока (для сохранения рекорда).</summary>
    public string PlayerName { get; set; } = string.Empty;

    // --- Смайлик ---

    private IImage? _smileyImage;

    /// <summary>
    /// Картинка смайлика на кнопке.
    /// Меняется через <see cref="SetSmiley"/>, эмитит PropertyChanged.
    /// </summary>
    public IImage? SmileyImage
    {
        get => _smileyImage;
        private set => SetField(ref _smileyImage, value);
    }

    // --- INotifyPropertyChanged ---

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // --- Конструктор ---

    public MainViewModel()
    {
        _recordStore = new JsonRecordStore();
        _engine = new GameEngine(new SystemRandomProvider(), _recordStore);

        MineCounter = new LedDisplayViewModel();
        TimerDisplay = new LedDisplayViewModel();

        // Подписки на события ядра.
        _engine.CellChanged += OnCellChanged;
        _engine.CellsRevealed += OnCellsRevealed;
        _engine.BoardGenerated += OnBoardGenerated;
        _engine.GameStateChanged += OnGameStateChanged;

        // Таймер обновляет дисплей раз в 100 мс. Запуск — в конструкторе,
        // чтобы счётчик тикал даже до первого клика (пока 0).
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => UpdateTimer();
        _timer.Start();

        // Старт игры по умолчанию — Beginner. Реальная инициализация полей
        // произойдёт в Группе 2 (сейчас StartGame — заглушка).
        StartGame(DifficultyPresets.Beginner);
    }

    // --- Публичные методы ---

    /// <summary>
    /// Запускает новую игру с заданными настройками.
    /// Сбрасывает поле, пересоздаёт коллекцию клеток, обновляет дисплеи.
    /// </summary>
    public void StartGame(GameSettings settings)
    {
        _triggerCell = null;
        _engine.StartNewGame(settings);

        // Width/Height меняются — вручную эмитим PropertyChanged (computed-свойства).
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Width)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Height)));

        Cells.Clear();
        for (int y = 0; y < _engine.Height; y++)
        {
            for (int x = 0; x < _engine.Width; x++)
            {
                var point = new CellPoint(x, y);
                var vm = new CellViewModel(point);
                vm.Update(_engine.GetCell(point), null, showFlagsOnClosed: false);
                Cells.Add(vm);
            }
        }

        SetSmiley("smiley");
        UpdateMineCounter();
        UpdateTimer();
    }

    /// <summary>
    /// Открывает клетку левым кликом.
    /// </summary>
    public void OpenCell(CellPoint p) => _engine.OpenCell(p);

    /// <summary>
    /// Циклически переключает метку правым кликом.
    /// </summary>
    public void CycleMark(CellPoint p) => _engine.CycleMark(p);

    /// <summary>
    /// Аккорд — открыть соседей, если флаги совпадают с цифрой.
    /// </summary>
    public void Chord(CellPoint p) => _engine.Chord(p);

    /// <summary>
    /// Устанавливает картинку смайлика по имени ассета (без расширения).
    /// </summary>
    public void SetSmiley(string name) => SmileyImage = ImageCache.Get(name);

    // --- Обновление дисплеев (заглушки — Группа 2, шаг 9) ---

    /// <summary>
    /// Обновляет счётчик мин из движка.
    /// </summary>
    public void UpdateMineCounter()
    {
        MineCounter.SetValue(_engine.RemainingMines, allowMinus: true);
    }

    /// <summary>
    /// Обновляет дисплей таймера.
    /// </summary>
    public void UpdateTimer()
    {
        TimerDisplay.SetValue((int)_engine.Elapsed.TotalSeconds, allowMinus: false);
    }

    // --- Обработчики событий ядра (Группа 4) ---

    /// <summary>
    /// Реагирует на изменение одной клетки (открытие, флаг).
    /// Обновляет VM клетки и пересчитывает счётчик мин.
    /// </summary>
    private void OnCellChanged(object? sender, CellChangedEventArgs e)
    {
        var vm = Cells.FirstOrDefault(c => c.Position == e.Change.Position);
        vm?.Update(e.CellInfo, _triggerCell, State == GameState.Won);
        UpdateMineCounter();
    }

    /// <summary>
    /// Реагирует на пакетное открытие клеток (каскад, раскрытие мин при поражении).
    /// Обновляет каждую клетку и пересчитывает счётчик мин.
    /// </summary>
    private void OnCellsRevealed(object? sender, CellsRevealedEventArgs e)
    {
        foreach (var info in e.CellInfos)
        {
            var vm = Cells.FirstOrDefault(c => c.Position == info.Position);
            vm?.Update(info, _triggerCell, State == GameState.Won);
        }
        UpdateMineCounter();
    }

    /// <summary>
    /// Реагирует на генерацию поля (после первого клика).
    /// Пересчитывает счётчик мин.
    /// </summary>
    private void OnBoardGenerated(object? sender, BoardGeneratedEventArgs e)
    {
        UpdateMineCounter();
    }

    /// <summary>
    /// Реагирует на изменение состояния игры (Won/Lost).
    /// Меняет смайлик, перерисовывает все клетки, обновляет таймер.
    /// </summary>
    private void OnGameStateChanged(object? sender, GameStateChangedEventArgs e)
    {
        switch (e.NewState)
        {
            case GameState.Won:
                SetSmiley("smiley_win");
                foreach (var c in Cells)
                    c.Update(_engine.GetCell(c.Position), null, showFlagsOnClosed: true);
                break;

            case GameState.Lost:
                SetSmiley("smiley_lose");
                _triggerCell = e.TriggerCell?.Position;
                foreach (var c in Cells)
                    c.Update(_engine.GetCell(c.Position), _triggerCell, showFlagsOnClosed: false);
                break;
        }

        UpdateMineCounter();
        UpdateTimer();
    }

    // --- IDisposable ---

    public void Dispose()
    {
        _timer.Stop();

        _engine.CellChanged -= OnCellChanged;
        _engine.CellsRevealed -= OnCellsRevealed;
        _engine.BoardGenerated -= OnBoardGenerated;
        _engine.GameStateChanged -= OnGameStateChanged;

        _engine.Dispose();
    }
}
