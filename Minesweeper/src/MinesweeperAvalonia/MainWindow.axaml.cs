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
    private CellPoint? _chordCandidate = null;
    private bool _suppressNextMouseUp = false;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        // При закрытии окна освобождаем ресурсы ViewModel.
        Closed += (s, e) => _vm.Dispose();

        // Подписка на GameStateChanged для показа диалогов при победе.
        _vm.Engine.GameStateChanged += OnGameStateChangedDialog;
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

        // 1. Средняя кнопка — аккорд сразу.
        if (props.PointerUpdateKind == PointerUpdateKind.MiddleButtonPressed)
        {
            var mp = ToCellPoint(e.GetPosition(BoardControl));
            if (mp.HasValue) _vm.Chord(mp.Value);
            return;
        }

        _suppressNextMouseUp = false;

        // 2. Обновляем флаги нажатия
        if (props.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed) _isLeftDown = true;

        // 3. Если обе кнопки зажаты — это аккорд.
        if (props.IsLeftButtonPressed && props.IsRightButtonPressed)
        {
            var point = ToCellPoint(e.GetPosition(BoardControl));
            if (point.HasValue) _chordCandidate = point;
            return;
        }

        // 4. Обычный клик — показываем смайлик «О»
        if (_isLeftDown && _chordCandidate is null
            && (_vm.State == GameState.InProgress || _vm.State == GameState.NotStarted))
        {
            _vm.SetSmiley("smiley_oh");
        }
    }

    // ВАЖНО: Новый обработчик для отслеживания второй кнопки
    private void OnBoardPointerMoved(object? sender, PointerEventArgs e)
    {
        var props = e.GetCurrentPoint(BoardControl).Properties;

        // Если ЛКМ зажата, и мы нажимаем ПКМ, PointerPressed может не сработать из-за захвата.
        // Ловим это здесь.
        if (props.IsLeftButtonPressed && props.IsRightButtonPressed && !_chordCandidate.HasValue)
        {
            var point = ToCellPoint(e.GetPosition(BoardControl));
            if (point.HasValue) _chordCandidate = point;
        }
    }

    private void OnBoardPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var props = e.GetCurrentPoint(BoardControl).Properties;

        // 1. Снимаем флаги
        if (props.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased) _isLeftDown = false;

        // 2. Если это второе отпускание после аккорда — игнорируем
        if (_suppressNextMouseUp)
        {
            _suppressNextMouseUp = false;
            _chordCandidate = null;
            ResetSmileyForCurrentState();
            return;
        }

        // 3. Если был аккорд — выполняем его
        if (_chordCandidate.HasValue)
        {
            _vm.Chord(_chordCandidate.Value);
            _chordCandidate = null;
            _suppressNextMouseUp = true;
            ResetSmileyForCurrentState();
            return;
        }

        // 4. Обычное отпускание
        ResetSmileyForCurrentState();

        var point = ToCellPoint(e.GetPosition(BoardControl));
        if (!point.HasValue) return;

        if (props.PointerUpdateKind == PointerUpdateKind.RightButtonReleased)
        {
            _vm.CycleMark(point.Value);
        }
        else if (props.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                _vm.CycleMark(point.Value);
            }
            else
            {
                _vm.OpenCell(point.Value);
            }
        }
    }

    private void OnBoardPointerEntered(object? sender, PointerEventArgs e)
    {
        var props = e.GetCurrentPoint(BoardControl).Properties;
        if (!props.IsLeftButtonPressed && !props.IsRightButtonPressed)
        {
            _chordCandidate = null;
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

}
