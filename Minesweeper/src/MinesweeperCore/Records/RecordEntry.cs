namespace MinesweeperCore.Records;

/// <summary>
/// Одна запись рекорда: время (в секундах) и имя игрока.
/// </summary>
public sealed record RecordEntry(string PlayerName, double TimeSeconds);
