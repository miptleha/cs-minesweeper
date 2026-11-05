using System.Text.Json;
using MinesweeperCore.Abstractions;
using MinesweeperCore.Domain;
using MinesweeperCore.Records;

namespace MinesweeperWinForms;

public class JsonRecordStore : IRecordStore
{
    private readonly string _filePath;
    private readonly Dictionary<string, List<RecordEntry>> _data;

    public JsonRecordStore()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Minesweeper");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "records.json");
        _data = LoadFromFile();
    }

    public IEnumerable<RecordEntry> LoadRecords(DifficultyLevel level)
    {
        var key = level.ToString();
        if (_data.TryGetValue(key, out var entries))
            return entries;
        return Enumerable.Empty<RecordEntry>();
    }

    public bool SaveRecord(DifficultyLevel level, RecordEntry entry)
    {
        var key = level.ToString();
        if (!_data.TryGetValue(key, out var entries))
        {
            entries = new List<RecordEntry>();
            _data[key] = entries;
        }

        entries.Add(entry);
        entries.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
        bool madeTop5 = entries.IndexOf(entry) < 5;

        while (entries.Count > 5)
            entries.RemoveAt(5);

        SaveToFile();
        return madeTop5;
    }

    // Метод для перезаписи имени у только что сохраненного рекорда
    public void RenameRecord(DifficultyLevel level, double timeSeconds, string newName)
    {
        var key = level.ToString();
        if (_data.TryGetValue(key, out var entries))
        {
            // Ищем запись с таким же временем
            var entry = entries.FirstOrDefault(e => Math.Abs(e.TimeSeconds - timeSeconds) < 0.1);
            if (entry != null)
            {
                int index = entries.IndexOf(entry);
                entries[index] = new RecordEntry(newName, entry.TimeSeconds);
                SaveToFile();
            }
        }
    }

    private Dictionary<string, List<RecordEntry>> LoadFromFile()
    {
        if (!File.Exists(_filePath)) return new Dictionary<string, List<RecordEntry>>();
        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<Dictionary<string, List<RecordEntry>>>(json) ?? new Dictionary<string, List<RecordEntry>>();
        }
        catch { return new Dictionary<string, List<RecordEntry>>(); }
    }

    private void SaveToFile()
    {
        try
        {
            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch { }
    }
}