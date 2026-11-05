using MinesweeperCore.Abstractions;
using MinesweeperCore.Domain;
using MinesweeperCore.Records;

namespace MinesweeperCore.Tests.Fakes;

/// <summary>
/// Фейковый стор: LoadRecords возвращает заранее заданные записи,
/// SaveRecord всегда возвращает false (симулирует отказ).
/// </summary>
public sealed class PreloadedFailingStore : IRecordStore
{
    private readonly IReadOnlyList<RecordEntry> _preloaded;

    public PreloadedFailingStore(IReadOnlyList<RecordEntry> preloaded)
    {
        _preloaded = preloaded;
    }

    public IEnumerable<RecordEntry> LoadRecords(DifficultyLevel level) => _preloaded;

    public bool SaveRecord(DifficultyLevel level, RecordEntry entry) => false;
}
