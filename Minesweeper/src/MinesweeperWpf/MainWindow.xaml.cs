using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using MinesweeperCore.Abstractions;
using MinesweeperCore.Domain;
using MinesweeperCore.Engine;
using MinesweeperCore.Events;
using MinesweeperCore.Infrastructure;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MinesweeperWpf;

/// <summary>
/// Кэш загруженных изображений для повторного использования.
/// Загружает каждый ассет один раз, замораживает для потокобезопасности.
/// </summary>
internal static class ImageCache
{
    private static readonly Dictionary<string, BitmapImage> _cache = new();

    public static BitmapImage Get(string name)
    {
        if (_cache.TryGetValue(name, out var cached)) return cached;

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri($"pack://application:,,,/Resources/{name}.png", UriKind.Absolute);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        _cache[name] = bmp;
        return bmp;
    }
}

/// <summary>
/// Представление одной клетки поля. Привязывается к UI через DataContext.
/// </summary>
public sealed class CellViewModel : INotifyPropertyChanged
{
    public CellPoint Position { get; }

    private ImageSource? _backgroundImage;
    public ImageSource? BackgroundImage { get => _backgroundImage; private set => SetField(ref _backgroundImage, value); }

    private ImageSource? _overlayImage;
    public ImageSource? OverlayImage { get => _overlayImage; private set => SetField(ref _overlayImage, value); }

    public CellViewModel(CellPoint position)
    {
        Position = position;
    }

    /// <summary>
    /// Обновляет отображение клетки по данным из ядра.
    /// Логика строго по таблице §16.3 docs/02-GAME-RULES.md.
    /// </summary>
    public void Update(CellInfo info, CellPoint? triggerCell, bool showFlagsOnClosed)
    {
        // Победа: все закрытые клетки с флагами показываются как мины (§16.1)
        if (showFlagsOnClosed && info.State is CellState.Closed or CellState.Flagged or CellState.Question)
        {
            BackgroundImage = ImageCache.Get("cell_closed");
            OverlayImage = ImageCache.Get("cell_flag");
            return;
        }

        switch (info.State)
        {
            case CellState.WrongFlag:
                BackgroundImage = ImageCache.Get("cell_wrong_flag");
                OverlayImage = null;
                break;

            case CellState.Opened when Position == triggerCell:
                // Триггерная мина — взрыв
                BackgroundImage = ImageCache.Get("cell_exploded");
                OverlayImage = null;
                break;

            case CellState.Opened when info.Content == CellContent.Mine:
                // Не-триггерная мина (открыта при проигрыше)
                BackgroundImage = ImageCache.Get("cell_empty");
                OverlayImage = ImageCache.Get("bomb");
                break;

            case CellState.Opened when info.Content == CellContent.Number:
                // Открытая клетка с цифрой
                BackgroundImage = ImageCache.Get("cell_empty");
                OverlayImage = ImageCache.Get($"digit_{info.AdjacentMines}");
                break;

            case CellState.Opened when info.Content == CellContent.Empty:
                // Открытая пустая клетка
                BackgroundImage = ImageCache.Get("cell_empty");
                OverlayImage = null;
                break;

            case CellState.Flagged:
                BackgroundImage = ImageCache.Get("cell_closed");
                OverlayImage = ImageCache.Get("cell_flag");
                break;

            case CellState.Question:
                BackgroundImage = ImageCache.Get("cell_closed");
                OverlayImage = ImageCache.Get("cell_question");
                break;

            case CellState.Closed:
            default:
                BackgroundImage = ImageCache.Get("cell_closed");
                OverlayImage = null;
                break;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Представление LED-дисплея (счётчик мин или таймер).
/// Хранит коллекцию изображений-цифр и обновляет их при изменении значения.
/// </summary>
public sealed class LedDisplayViewModel
{
    public ObservableCollection<ImageSource> Digits { get; } = new();

    /// <summary>
    /// Устанавливает отображаемое значение.
    /// При allowMinus=true и отрицательном значении показывает minus.png + 2 цифры.
    /// Иначе — 3 цифры с clamp [0..999].
    /// </summary>
    public void SetValue(int value, bool allowMinus)
    {
        Digits.Clear();
        if (allowMinus && value < 0)
        {
            Digits.Add(ImageCache.Get("minus"));
            var s = Math.Min(Math.Abs(value), 99).ToString("D2");
            foreach (var c in s)
                Digits.Add(ImageCache.Get($"timer_{c}"));
            return;
        }
        var str = Math.Clamp(value, 0, 999).ToString("D3");
        foreach (var c in str)
            Digits.Add(ImageCache.Get($"timer_{c}"));
    }
}

/// <summary>
/// Главная ViewModel: обёртка над GameEngine, подписки на события,
/// ObservableCollection клеток, LED-счётчики, таймер.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly GameEngine _engine;
    private readonly IRecordStore _recordStore;
    private CellPoint? _triggerCell;
    private readonly DispatcherTimer _timer;

    // Свойства для привязки к XAML
    public ObservableCollection<CellViewModel> Cells { get; } = new();
    public LedDisplayViewModel MineCounter { get; } = new();
    public LedDisplayViewModel TimerDisplay { get; } = new();

    public int Width => _engine.Width;
    public int Height => _engine.Height;
    public GameState State => _engine.State;
    public DifficultyLevel Difficulty => _engine.Difficulty;
    public double ElapsedSeconds => _engine.Elapsed.TotalSeconds;
    public IRecordStore RecordStore => _recordStore;
    public GameEngine Engine => _engine;
    public GameSettings Settings => _engine.Settings;
    public string PlayerName { get; set; } = string.Empty;

    private ImageSource? _smileyImage;
    public ImageSource SmileyImage { get => _smileyImage!; private set => SetField(ref _smileyImage, value); }

    // INotifyPropertyChanged для свойств VM
    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public MainViewModel()
    {
        _recordStore = new JsonRecordStore();
        _engine = new GameEngine(new SystemRandomProvider(), _recordStore);

        // Подписки на события ядра (обработчики будут в следующих шагах)
        _engine.CellChanged += OnCellChanged;
        _engine.CellsRevealed += OnCellsRevealed;
        _engine.BoardGenerated += OnBoardGenerated;
        _engine.GameStateChanged += OnGameStateChanged;

        // Таймер обновления дисплея (100 мс)
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (s, e) => UpdateTimer();
        _timer.Start();

        // Старт игры по умолчанию
        StartGame(DifficultyPresets.Beginner);
    }

    /// <summary>
    /// Запуск новой игры с заданными настройками.
    /// </summary>
    public void StartGame(GameSettings settings)
    {
        _triggerCell = null;
        _engine.StartNewGame(settings);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Width)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Height)));
    
        Cells.Clear();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var point = new CellPoint(x, y);
                var vm = new CellViewModel(point);
                // Инициализируем клетку — иначе BackgroundImage останется null,
                // и ItemsControl отрисует пустые Grid'ы: поле будет невидимым.
                vm.Update(_engine.GetCell(point), null, false);
                Cells.Add(vm);
            }
        }
        SetSmiley("smiley");
        UpdateMineCounter();
        UpdateTimer();
    }

    /// <summary>
    /// Устанавливает изображение смайлика.
    /// </summary>
    public void SetSmiley(string name)
    {
        SmileyImage = ImageCache.Get(name);
    }

    private void UpdateMineCounter() => MineCounter.SetValue(_engine.RemainingMines, allowMinus: true);
    private void UpdateTimer() => TimerDisplay.SetValue((int)_engine.Elapsed.TotalSeconds, allowMinus: false);

    // --- Обработчики событий (заполняются в шагах 15–18) ---

    /// <summary>
    /// Обработка изменения одной клетки (CellChanged).
    /// </summary>
    private void OnCellChanged(object? s, CellChangedEventArgs e)
    {
        var vm = Cells.FirstOrDefault(c => c.Position == e.Change.Position);
        vm?.Update(e.CellInfo, _triggerCell, _engine.State == GameState.Won);
        UpdateMineCounter();
    }

    /// <summary>
    /// Обработка батча открытых клеток (CellsRevealed).
    /// </summary>
    private void OnCellsRevealed(object? s, CellsRevealedEventArgs e)
    {
        foreach (var info in e.CellInfos)
        {
            var vm = Cells.FirstOrDefault(c => c.Position == info.Position);
            vm?.Update(info, _triggerCell, _engine.State == GameState.Won);
        }
        UpdateMineCounter();
    }

    /// <summary>
    /// Обработка генерации поля (после первого клика).
    /// </summary>
    private void OnBoardGenerated(object? s, BoardGeneratedEventArgs e)
    {
        UpdateMineCounter();
    }

    /// <summary>
    /// Обработка изменения состояния игры (Won/Lost).
    /// </summary>
    private void OnGameStateChanged(object? s, GameStateChangedEventArgs e)
    {
        switch (e.NewState)
        {
            case GameState.Won:
                SetSmiley("smiley_win");
                // Все закрытые клетки показываются как флаги (§16.1)
                foreach (var c in Cells)
                    c.Update(_engine.GetCell(c.Position), null, true);
                break;

            case GameState.Lost:
                SetSmiley("smiley_lose");
                _triggerCell = e.TriggerCell?.Position;
                // Все клетки перерисовываются с триггером
                foreach (var c in Cells)
                    c.Update(_engine.GetCell(c.Position), _triggerCell, false);
                break;
        }

        UpdateMineCounter();
        UpdateTimer();
    }

    public void Dispose()
    {
        _timer.Stop();

        // Отписка от событий
        _engine.CellChanged -= OnCellChanged;
        _engine.CellsRevealed -= OnCellsRevealed;
        _engine.BoardGenerated -= OnBoardGenerated;
        _engine.GameStateChanged -= OnGameStateChanged;

        _engine.Dispose();
    }
}

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    // Поля для обработки ввода (шаг 23)
    private bool _isLeftDown = false;
    private bool _isRightDown = false;
    private CellPoint? _chordCandidate = null;
    private bool _suppressNextMouseUp = false;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        // Шаг 30: обработка победы для показа диалогов имени и рекордов
        _vm.Engine.GameStateChanged += OnGameStateChangedDialog;

        // Шаг 26: освобождение ресурсов при закрытии окна
        Closed += (s, e) => _vm.Dispose();
    }

    /// <summary>
    /// Клик по смайлику — новая игра.
    /// </summary>
    private void OnSmileyClick(object sender, RoutedEventArgs e)
    {
        _vm.StartGame(_vm.Settings);
    }

    // --- Обработчики меню (пустые заглушки, заполняются в шаге 31) ---

    private void OnNewGameClick(object sender, RoutedEventArgs e)
        => _vm.StartGame(_vm.Settings);

    private void OnBeginnerClick(object sender, RoutedEventArgs e)
        => _vm.StartGame(DifficultyPresets.Beginner);

    private void OnIntermediateClick(object sender, RoutedEventArgs e)
        => _vm.StartGame(DifficultyPresets.Intermediate);

    private void OnExpertClick(object sender, RoutedEventArgs e)
        => _vm.StartGame(DifficultyPresets.Expert);

    private void OnExitClick(object sender, RoutedEventArgs e)
        => Close();

    private void OnRecordsClick(object sender, RoutedEventArgs e)
        => new RecordsWindow(_vm.RecordStore) { Owner = this }.ShowDialog();

    private void OnAboutClick(object sender, RoutedEventArgs e)
        => new AboutWindow { Owner = this }.ShowDialog();

    // --- Обработчики поля (шаги 23–25) ---

    /// <summary>
    /// Преобразует координаты мыши в CellPoint.
    /// </summary>
    private CellPoint? ToCellPoint(System.Windows.Point p)
    {
        int x = (int)(p.X / 24.0);
        int y = (int)(p.Y / 24.0);
        if (x < 0 || x >= _vm.Width || y < 0 || y >= _vm.Height) return null;
        return new CellPoint(x, y);
    }

    /// <summary>
    /// Сбрасывает смайлик в соответствии с текущим состоянием игры.
    /// </summary>
    private void ResetSmileyForCurrentState()
    {
        switch (_vm.State)
        {
            case GameState.NotStarted:
            case GameState.InProgress:
                _vm.SetSmiley("smiley");
                break;
            case GameState.Won:
                _vm.SetSmiley("smiley_win");
                break;
            case GameState.Lost:
                _vm.SetSmiley("smiley_lose");
                break;
        }
    }

    private void OnBoardMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Средняя кнопка — chord сразу
        if (e.ChangedButton == MouseButton.Middle)
        {
            var mp = ToCellPoint(e.GetPosition(Board));
            if (mp.HasValue) _vm.Engine.Chord(mp.Value);
            return;
        }

        if (e.ChangedButton == MouseButton.Left) _isLeftDown = true;
        if (e.ChangedButton == MouseButton.Right) _isRightDown = true;

        // Одновременное нажатие ЛКМ+ПКМ — кандидат на chord
        if (_isLeftDown && _isRightDown)
            _chordCandidate = ToCellPoint(e.GetPosition(Board));

        // Одиночная ЛКМ без chord-кандидата — смайлик "oh"
        if (e.ChangedButton == MouseButton.Left
            && _chordCandidate is null
            && (_vm.State == GameState.InProgress || _vm.State == GameState.NotStarted))
            _vm.SetSmiley("smiley_oh");
    }

    /// <summary>
    /// Обработка победы: показ диалога ввода имени, затем таблицы рекордов.
    /// Не показывается для кастомной сложности.
    /// </summary>
    private void OnGameStateChangedDialog(object? s, GameStateChangedEventArgs e)
    {
        if (e.NewState != GameState.Won) return;
        if (_vm.Difficulty == DifficultyLevel.Custom) return;

        // Проверяем, попал ли игрок в топ-5. 
        // Ядро уже сохранило рекорд (если он прошёл), поэтому мы ищем наше время в таблице.
        bool isNewRecord = _vm.Engine.Records.Any(r => Math.Abs(r.TimeSeconds - _vm.ElapsedSeconds) < 0.1);

        if (isNewRecord)
        {
            var prompt = new NamePromptWindow { Owner = this };
            if (prompt.ShowDialog() == true)
            {
                _vm.PlayerName = prompt.PlayerName;
                if (_vm.RecordStore is JsonRecordStore jsonStore)
                    jsonStore.RenameRecord(_vm.Difficulty, _vm.ElapsedSeconds, prompt.PlayerName);
            }
        }

        // В любом случае (попал или нет) показываем таблицу рекордов
        new RecordsWindow(_vm.RecordStore) { Owner = this }.ShowDialog();
    }

    private void OnBoardMouseUp(object sender, MouseButtonEventArgs e)
    {
        // Снять кнопку
        if (e.ChangedButton == MouseButton.Left) _isLeftDown = false;
        if (e.ChangedButton == MouseButton.Right) _isRightDown = false;

        // Suppress — отмена следующего MouseUp после chord
        if (_suppressNextMouseUp && _chordCandidate is null)
        {
            _suppressNextMouseUp = false;
            ResetSmileyForCurrentState();
            return;
        }

        // Chord candidate — chord
        if (_chordCandidate != null)
        {
            _vm.Engine.Chord(_chordCandidate.Value);
            _chordCandidate = null;
            _suppressNextMouseUp = true;
            ResetSmileyForCurrentState();
            return;
        }

        // Сброс смайлика
        ResetSmileyForCurrentState();

        var point = ToCellPoint(e.GetPosition(Board));
        if (!point.HasValue) return;

        // ==========================================
        // ОБРАБОТКА ПРАВОЙ КНОПКИ (ФЛАГИ)
        // ==========================================
        if (e.ChangedButton == MouseButton.Right)
        {
            _vm.Engine.CycleMark(point.Value);
            return;
        }

        // ==========================================
        // ОБРАБОТКА ЛЕВОЙ КНОПКИ (ОТКРЫТИЕ/АККОРД)
        // ==========================================
        if (e.ChangedButton == MouseButton.Left)
        {
            // Ctrl+ЛКМ — CycleMark
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                _vm.Engine.CycleMark(point.Value);
                return;
            }

            // Одиночный клик — OpenCell
            _vm.Engine.OpenCell(point.Value);
        }
    }
}