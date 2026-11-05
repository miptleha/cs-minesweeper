namespace MinesweeperCore.Records;

/// <summary>
/// Логика таблицы рекордов: сортировка, обрезка до топ-5, проверка попадания.
/// </summary>
internal sealed class RecordTable
{
    private const int MaxRecords = 5;
    private readonly List<RecordEntry> _entries = new();

    public IEnumerable<RecordEntry> Entries => _entries.AsReadOnly();
    public int Count => _entries.Count;

    /// <summary>
    /// Проверяет, попадает ли новое время в топ-5.
    /// </summary>
    public bool WouldQualify(double timeSeconds)
    {
        if (_entries.Count < MaxRecords)
            return true;

        return timeSeconds < _entries.Last().TimeSeconds;
    }

    /// <summary>
    /// Добавляет новый рекорд, сортирует и обрезает до топ-5.
    /// </summary>
    public void AddRecord(RecordEntry entry)
    {
        _entries.Add(entry);
        _entries.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));

        while (_entries.Count > MaxRecords)
            _entries.RemoveAt(MaxRecords);
    }

    /// <summary>
    /// Создаёт пустую таблицу рекордов.
    /// </summary>
    public static RecordTable Empty() => new();
}
