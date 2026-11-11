using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MinesweeperAvalonia.Services;

namespace MinesweeperAvalonia.ViewModels;

/// <summary>
/// ViewModel LED-дисплея (счётчик мин или таймер).
/// Представляет число как последовательность картинок-цифр.
/// </summary>
public sealed class LedDisplayViewModel
{
    /// <summary>
    /// Последовательность картинок-цифр для отображения текущего значения.
    /// </summary>
    public ObservableCollection<IImage> Digits { get; } = new();

    /// <summary>
    /// Устанавливает отображаемое значение.
    /// </summary>
    /// <param name="value">Число для отображения.</param>
    /// <param name="allowMinus">Если true и значение отрицательное — показать минус.</param>
    public void SetValue(int value, bool allowMinus)
    {
        Digits.Clear();

        if (allowMinus && value < 0)
        {
            // Знак минуса + 2 цифры (abs, максимум 99)
            Digits.Add(ImageCache.Get("minus"));
            int digits = Math.Min(Math.Abs(value), 99);
            Digits.Add(ImageCache.Get($"timer_{digits / 10}"));
            Digits.Add(ImageCache.Get($"timer_{digits % 10}"));
        }
        else
        {
            // 3 цифры (clamp от 0 до 999)
            int digits = Math.Clamp(value, 0, 999);
            Digits.Add(ImageCache.Get($"timer_{digits / 100}"));
            Digits.Add(ImageCache.Get($"timer_{(digits % 100) / 10}"));
            Digits.Add(ImageCache.Get($"timer_{digits % 10}"));
        }
    }
}
