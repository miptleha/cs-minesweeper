using MinesweeperCore.Records;

namespace MinesweeperCore.Abstractions;

/// <summary>
/// Сохраняет и загружает таблицу рекордов.
/// Ядро не знает, куда именно (файл, БД, реестр).
/// </summary>
public interface IRecordStore
{
    /// <summary>
    /// Загружает топ-5 рекордов для указанного уровня сложности.
    /// </summary>
    IEnumerable<RecordEntry> LoadRecords(Domain.DifficultyLevel level);

    /// <summary>
    /// Сохраняет новый рекорд для указанного уровня сложности.
    /// Возвращает true, если рекорд попал в топ-5.
    /// </summary>
    bool SaveRecord(Domain.DifficultyLevel level, RecordEntry entry);
}
