using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MinesweeperCore.Domain;
using MinesweeperAvalonia.ViewModels;
using MinesweeperAvalonia.Windows;
using MinesweeperCore.Infrastructure;
using MinesweeperCore.Events;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace MinesweeperAvalonia;

/// <summary>
/// Главное окно приложения «Сапёр».
/// Содержит меню, LED-дисплеи, поле и обработчики ввода.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    // --- Поля для отслеживания ввода ---
    private bool _isLeftDown = false;
    private bool _suppressNextMouseUp = false;

    // Долгое нажатие (~0,5 с) — метка, как по ПКМ (тачпад/тачскрин).
    private readonly Avalonia.Threading.DispatcherTimer _longPressTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(500),
    };
    private CellPoint? _longPressCell = null;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        // При закрытии окна освобождаем ресурсы ViewModel.
        Closed += (s, e) => _vm.Dispose();

        // Подписка на GameStateChanged для показа диалогов при победе.
        _vm.Engine.GameStateChanged += OnGameStateChangedDialog;

        // Долгое нажатие (~0,5 с) — метка, как по ПКМ (тачпад/тачскрин).
        _longPressTimer.Tick += OnLongPressTick;
    }

    // --- Вспомогательные методы (Группа 3) ---

    /// <summary>
    /// Преобразует координаты точки в координаты клетки на поле.
    /// Размер клетки — 24 DIP.
    /// </summary>
    private CellPoint? ToCellPoint(Point p)
    {
        var x = (int)(p.X / 24.0);
        var y = (int)(p.Y / 24.0);
        if (x < 0 || x >= _vm.Width || y < 0 || y >= _vm.Height)
            return null;
        return new CellPoint(x, y);
    }

    /// <summary>
    /// Возвращает смайлик в состояние, соответствующее текущему GameState.
    /// </summary>
    private void ResetSmileyForCurrentState()
    {
        _vm.SetSmiley(_vm.State switch
        {
            GameState.Won => "smiley_win",
            GameState.Lost => "smiley_lose",
            _ => "smiley"
        });
    }

    // --- Обработчики меню (Группа 2) ---

    private void OnNewGameClick(object? sender, RoutedEventArgs e)
    {
        _vm.StartGame(_vm.Settings);
    }

    private void OnBeginnerClick(object? sender, RoutedEventArgs e)
    {
        _vm.StartGame(DifficultyPresets.Beginner);
    }

    private void OnIntermediateClick(object? sender, RoutedEventArgs e)
    {
        _vm.StartGame(DifficultyPresets.Intermediate);
    }

    private void OnExpertClick(object? sender, RoutedEventArgs e)
    {
        _vm.StartGame(DifficultyPresets.Expert);
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnRecordsClick(object? sender, RoutedEventArgs e)
    {
        var records = new RecordsWindow(_vm.RecordStore);
        records.ShowDialog(this);
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var about = new AboutWindow();
        about.ShowDialog(this);
    }

    private void OnControlsClick(object? sender, RoutedEventArgs e)
    {
        var controls = new ControlsWindow();
        controls.ShowDialog(this);
    }

    /// <summary>
    /// Обработка победы: запрос имени (если новый рекорд) + показ рекордов.
    /// </summary>
    private async void OnGameStateChangedDialog(object? s, GameStateChangedEventArgs e)
    {
        if (e.NewState != GameState.Won)
            return;

        // На Custom уровне рекорды не сохраняются — диалоги не показываем.
        if (_vm.Difficulty == DifficultyLevel.Custom)
            return;

        // Проверяем, попал ли рекорд в топ-5.
        bool isNewRecord = _vm.Engine.Records.Any(r => Math.Abs(r.TimeSeconds - _vm.ElapsedSeconds) < 0.1);

        if (isNewRecord)
        {
            var prompt = new NamePromptWindow();
            var ok = await prompt.ShowDialog<bool?>(this);
            if (ok == true)
            {
                _vm.PlayerName = prompt.PlayerName;
                if (_vm.RecordStore is JsonRecordStore json)
                    json.RenameRecord(_vm.Difficulty, _vm.ElapsedSeconds, prompt.PlayerName);
            }
        }

        var records = new RecordsWindow(_vm.RecordStore);
        await records.ShowDialog(this);
    }

    // --- Обработчик смайлика (Группа 2) ---

    private void OnSmileyClick(object? sender, RoutedEventArgs e)
    {
        _vm.StartGame(_vm.Settings);
    }

    // --- Обработчики поля (Группа 3) ---

    private void OnBoardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(BoardControl).Properties;
        if (props.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
            return;

        _isLeftDown = true;
        _longPressCell = ToCellPoint(e.GetPosition(BoardControl));
        StartLongPressTimer();

        // Смайлик «О» — только для ЛКМ по закрытой клетке
        // (по открытой цифре ЛКМ — аккорд, мигать не должен).
        if (_vm.State == GameState.InProgress || _vm.State == GameState.NotStarted)
        {
            var p = ToCellPoint(e.GetPosition(BoardControl));
            if (p.HasValue && _vm.Engine.GetCell(p.Value).State != CellState.Opened)
                _vm.SetSmiley("smiley_oh");
        }
    }

    private void OnBoardPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var props = e.GetCurrentPoint(BoardControl).Properties;
        var kind = props.PointerUpdateKind;

        // 1. Снимаем флаги
        if (kind == PointerUpdateKind.LeftButtonReleased)
        {
            _isLeftDown = false;
            StopLongPressTimer();
        }

        // 2. Если сработал long-press (метка уже поставлена) — игнорируем.
        if (_suppressNextMouseUp)
        {
            _suppressNextMouseUp = false;
            ResetSmileyForCurrentState();
            return;
        }

        // 3. Обычное отпускание
        ResetSmileyForCurrentState();

        var point = ToCellPoint(e.GetPosition(BoardControl));
        if (!point.HasValue) return;

        if (kind == PointerUpdateKind.RightButtonReleased)
        {
            // ПКМ — метка (флаг → вопрос → пусто). По открытой клетке ядро игнорирует.
            _vm.CycleMark(point.Value);
        }
        else if (kind == PointerUpdateKind.LeftButtonReleased)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                // Ctrl+ЛКМ — то же, что ПКМ (метка).
                _vm.CycleMark(point.Value);
            }
            else
            {
                // ЛКМ по открытой цифре — аккорд, иначе — открытие клетки.
                if (_vm.Engine.GetCell(point.Value).State == CellState.Opened)
                    _vm.Chord(point.Value);
                else
                    _vm.OpenCell(point.Value);
            }
        }
    }

    private void OnBoardPointerEntered(object? sender, PointerEventArgs e)
    {
        var props = e.GetCurrentPoint(BoardControl).Properties;
        if (!props.IsLeftButtonPressed && !props.IsRightButtonPressed)
        {
            _longPressCell = null;
            _isLeftDown = false;
        }
    }

    private void OnBoardPointerExited(object? sender, PointerEventArgs e)
    {
        if (_vm.State == GameState.InProgress || _vm.State == GameState.NotStarted)
        {
            _vm.SetSmiley("smiley");
        }
    }

    // --- Долгое нажатие (~0,5 с) — метка, как по ПКМ (тачпад/тачскрин) ---

    private void StartLongPressTimer()
    {
        if (!_longPressTimer.IsEnabled)
            _longPressTimer.Start();
    }

    private void StopLongPressTimer()
    {
        _longPressTimer.Stop();
    }

    private void OnLongPressTick(object? sender, EventArgs e)
    {
        _longPressTimer.Stop();

        if (_longPressCell is not CellPoint cell)
            return;

        if (_isLeftDown
            && (_vm.State == GameState.InProgress || _vm.State == GameState.NotStarted))
        {
            // Долгое нажатие сработало — подавляем последующий PointerReleased,
            // чтобы клетка не открылась.
            _suppressNextMouseUp = true;
            ResetSmileyForCurrentState();
            _vm.CycleMark(cell);
        }
    }

}
