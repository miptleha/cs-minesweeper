using MinesweeperCore.Abstractions;
using MinesweeperCore.Domain;
using MinesweeperCore.Records;

namespace MinesweeperCore.Infrastructure;

/// <summary>
/// Хранит рекорды в памяти. Используется в UI и тестах.
/// </summary>
public sealed class InMemoryRecordStore : IRecordStore
{
    private readonly Dictionary<DifficultyLevel, List<RecordEntry>> _data = new();

    public IEnumerable<RecordEntry> LoadRecords(DifficultyLevel level)
    {
        if (_data.TryGetValue(level, out var entries))
            return entries;

        return Enumerable.Empty<RecordEntry>();
    }

    /// <summary>
    /// Сохраняет рекорд. Возвращает true, если рекорд попал в топ-5.
    /// </summary>
    public bool SaveRecord(DifficultyLevel level, RecordEntry entry)
    {
        if (!_data.TryGetValue(level, out var entries))
        {
            entries = new List<RecordEntry>();
            _data[level] = entries;
        }

        entries.Add(entry);
        entries.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));

        // Проверяем попадание в топ-5 ДО обрезки — после обрезки entry уже может быть удалён
        bool madeTop5 = entries.IndexOf(entry) < 5;

        while (entries.Count > 5)
            entries.RemoveAt(5);

        return madeTop5;
    }
}
