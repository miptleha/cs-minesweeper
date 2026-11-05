namespace MinesweeperCore.Domain;

/// <summary>
/// Готовые пресеты для фиксированных уровней сложности.
/// </summary>
public static class DifficultyPresets
{
    public static GameSettings Beginner => new(9, 9, 10);
    public static GameSettings Intermediate => new(16, 16, 40);
    public static GameSettings Expert => new(30, 16, 99);

    public static DifficultyLevel GetLevel(GameSettings settings)
    {
        if (settings.Width == 9 && settings.Height == 9 && settings.MineCount == 10)
            return DifficultyLevel.Beginner;

        if (settings.Width == 16 && settings.Height == 16 && settings.MineCount == 40)
            return DifficultyLevel.Intermediate;

        if (settings.Width == 30 && settings.Height == 16 && settings.MineCount == 99)
            return DifficultyLevel.Expert;

        return DifficultyLevel.Custom;
    }
}
