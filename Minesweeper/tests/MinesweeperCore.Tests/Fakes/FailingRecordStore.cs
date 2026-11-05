using MinesweeperCore.Abstractions;
using MinesweeperCore.Domain;
using MinesweeperCore.Records;

namespace MinesweeperCore.Tests.Fakes;

/// <summary>
/// Фейковый рекорд-стор, который всегда возвращает false.
/// Используется для проверки отката при неудачном сохранении.
/// </summary>
public sealed class FailingRecordStore : IRecordStore
{
    public IEnumerable<RecordEntry> LoadRecords(DifficultyLevel level)
    {
        return Enumerable.Empty<RecordEntry>();
    }

    public bool SaveRecord(DifficultyLevel level, RecordEntry entry)
    {
        // Всегда возвращаем false — симулируем ошибку сохранения
        return false;
    }
}
