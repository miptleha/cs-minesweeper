namespace MinesweeperCore.Domain;

/// <summary>
/// Состояние клетки на игровом поле.
/// </summary>
public enum CellState
{
    /// <summary>Клетка закрыта и содержимое неизвестно.</summary>
    Closed,

    /// <summary>Клетка открыта, содержимое видно.</summary>
    Opened,

    /// <summary>Клетка закрыта и помечена флагом «мина».</summary>
    Flagged,

    /// <summary>Клетка закрыта и помечена знаком вопроса «возможно, мина».</summary>
    Question,

    /// <summary>Клетка закрыта, помечена флагом, но мины нет (ошибочный флаг).</summary>
    WrongFlag
}
