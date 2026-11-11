using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MinesweeperCore.Domain;
using MinesweeperAvalonia.Services;

namespace MinesweeperAvalonia.ViewModels;

/// <summary>
/// ViewModel одной клетки поля.
/// Содержит координаты и два слоя картинок: фон и оверлей.
/// </summary>
public sealed class CellViewModel : INotifyPropertyChanged
{
    public CellPoint Position { get; }

    private IImage? _backgroundImage;
    public IImage? BackgroundImage
    {
        get => _backgroundImage;
        private set => SetField(ref _backgroundImage, value);
    }

    private IImage? _overlayImage;
    public IImage? OverlayImage
    {
        get => _overlayImage;
        private set => SetField(ref _overlayImage, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CellViewModel(CellPoint position)
    {
        Position = position;
    }

    /// <summary>
    /// Обновляет визуальное состояние клетки по данным из ядра.
    /// Строго по таблице §16.3 docs/02-GAME-RULES.md.
    /// </summary>
    public void Update(CellInfo info, CellPoint? triggerCell, bool showFlagsOnClosed)
    {
        // Если показываем флаги на закрытых (победа) — рисуем cell_flag поверх всех Closed/Flagged/Question.
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
                // Триггерная мина — взорванная клетка.
                BackgroundImage = ImageCache.Get("cell_exploded");
                OverlayImage = null;
                break;

            case CellState.Opened when info.Content == CellContent.Mine:
                // Мина, открытая при поражении (не триггер).
                BackgroundImage = ImageCache.Get("cell_empty");
                OverlayImage = ImageCache.Get("bomb");
                break;

            case CellState.Opened when info.Content == CellContent.Number:
                // Цифра — количество мин вокруг.
                BackgroundImage = ImageCache.Get("cell_empty");
                OverlayImage = ImageCache.Get($"digit_{info.AdjacentMines}");
                break;

            case CellState.Opened when info.Content == CellContent.Empty:
                // Пустая открытая клетка.
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

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}