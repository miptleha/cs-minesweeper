namespace MinesweeperCore.Domain;

/// <summary>
/// Параметры настройки игры: размер поля и количество мин.
/// Валидирует допустимые диапазоны.
/// </summary>
public sealed class GameSettings
{
    private const int MinWidth = 3;
    private const int MaxWidth = 1000;
    private const int MinHeight = 3;
    private const int MaxHeight = 1000;

    public GameSettings(int width, int height, int mineCount)
    {
        ValidateDimensions(width, height);
        ValidateMineCount(width, height, mineCount);

        Width = width;
        Height = height;
        MineCount = mineCount;
    }

    public int Width { get; }
    public int Height { get; }
    public int MineCount { get; }

    private static void ValidateDimensions(int width, int height)
    {
        if (width < MinWidth || width > MaxWidth)
            throw new ArgumentOutOfRangeException(nameof(width), $"Ширина должна быть от {MinWidth} до {MaxWidth}.");

        if (height < MinHeight || height > MaxHeight)
            throw new ArgumentOutOfRangeException(nameof(height), $"Высота должна быть от {MinHeight} до {MaxHeight}.");
    }

    private static void ValidateMineCount(int width, int height, int mineCount)
    {
        int maxMines = (width - 1) * (height - 1);
        if (mineCount < 0 || mineCount > maxMines)
            throw new ArgumentOutOfRangeException(
                nameof(mineCount),
                $"Количество мин должно быть от 0 до {maxMines} для поля {width}x{height}.");
    }
}
