using MinesweeperCore.Abstractions;
using MinesweeperCore.Domain;
using MinesweeperCore.Events;
using MinesweeperCore.Records;

namespace MinesweeperCore.Engine;

/// <summary>
/// Публичный фасад ядра. Единственный класс, с которым работает UI.
/// Хранит состояние партии и вызывает остальные сервисы.
/// </summary>
public sealed class GameEngine : IDisposable
{
    private Board _board = null!;
    private readonly MinePlacement _minePlacement;
    private readonly RevealService _revealService;
    private readonly IRecordStore _recordStore;
    private readonly ITimeProvider _timeProvider;

    private GameState _state = GameState.NotStarted;
    private RecordTable _records = RecordTable.Empty();
    private DateTime? _startTime;
    private TimeSpan _elapsed;
    private bool _disposed;

    /// <summary>
    /// Имя игрока. Используется при сохранении рекорда.
    /// </summary>
    public string PlayerName { get; set; } = string.Empty;

    // --- Events ---

    /// <summary>
    /// Срабатывает при изменении состояния одной клетки.
    /// </summary>
    public event EventHandler<CellChangedEventArgs>? CellChanged;

    /// <summary>
    /// Срабатывает после завершения каскадного открытия.
    /// </summary>
    public event EventHandler<CellsRevealedEventArgs>? CellsRevealed;

    /// <summary>
    /// Срабатывает при переходе игры в состояние Победа или Поражение.
    /// </summary>
    public event EventHandler<GameStateChangedEventArgs>? GameStateChanged;

    /// <summary>
    /// Срабатывает при расстановке мин (после первого клика).
    /// </summary>
    public event EventHandler<BoardGeneratedEventArgs>? BoardGenerated;

    // --- Public Properties ---

    /// <summary>
    /// Текущее состояние игры.
    /// </summary>
    public GameState State => _state;

    /// <summary>
    /// Параметры текущей игры.
    /// </summary>
    public GameSettings Settings { get; private set; }

    /// <summary>
    /// Прошедшее время с начала игры (или с паузы).
    /// </summary>
    public TimeSpan Elapsed => _state == GameState.InProgress
        ? _elapsed + (_timeProvider.GetUtcNow() - _startTime!.Value)
        : _elapsed;

    /// <summary>
    /// Уровень сложности текущей игры.
    /// </summary>
    public DifficultyLevel Difficulty => DifficultyPresets.GetLevel(Settings);

    /// <summary>
    /// Топ-5 рекордов для текущего уровня сложности.
    /// </summary>
    public IEnumerable<RecordEntry> Records => _records.Entries;

    /// <summary>
    /// Количество мин на поле.
    /// </summary>
    public int MineCount => Settings.MineCount;

    /// <summary>
    /// Ширина поля.
    /// </summary>
    public int Width => Settings.Width;

    /// <summary>
    /// Высота поля.
    /// </summary>
    public int Height => Settings.Height;

    /// <summary>
    /// Счётчик мин для отображения в UI (LED-индикатор): сколько флагов
    /// осталось проставить. Значение информационное и может быть ложным —
    /// если игрок ставит флаги не на мины, счётчик всё равно уменьшается.
    /// Может быть отрицательным, если флагов больше, чем мин.
    /// Считается в O(1) по Board.FlaggedCount. При Won/Lost не меняется.
    /// </summary>
    public int RemainingMines => MineCount - _board.FlaggedCount;

    public GameEngine(IRandomProvider random, IRecordStore recordStore, ITimeProvider? timeProvider = null)
    {
        _recordStore = recordStore ?? throw new ArgumentNullException(nameof(recordStore));
        _timeProvider = timeProvider ?? new Infrastructure.SystemTimeProvider();
        _minePlacement = new MinePlacement(random ?? throw new ArgumentNullException(nameof(random)));
        _revealService = new RevealService();
        Settings = DifficultyPresets.Beginner;
        _board = new Board(Settings.Width, Settings.Height);
    }

    /// <summary>
    /// Запускает новую игру с заданными настройками.
    /// Сбрасывает всё: поле, мины, состояние, таймер, рекорды.
    /// </summary>
    public void StartNewGame(GameSettings settings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Settings = settings;
        _board = new Board(settings.Width, settings.Height);
        _state = GameState.NotStarted;
        _startTime = null;
        _elapsed = TimeSpan.Zero;
        _records = RecordTable.Empty();

        // Загружаем рекорды для текущего уровня сложности.
        var entries = _recordStore.LoadRecords(Difficulty);
        foreach (var entry in entries)
            _records.AddRecord(entry);
    }

    /// <summary>
    /// Открывает клетку левым кликом.
    /// Порядок проверок:
    /// 1. Dispose → ObjectDisposedException
    /// 2. Won/Lost → return
    /// 3. NotStarted: если клетка Flagged → return (без PlaceMines/BoardGenerated);
    ///    иначе → PlaceMines, InProgress, BoardGenerated
    /// 4. Не Valid → return
    /// 5. Closed/Question → открыть; Flagged/Opened/WrongFlag → игнор.
    /// </summary>
    public void OpenCell(CellPoint point)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_state == GameState.Won || _state == GameState.Lost)
            return;

        // Если игра не начата — первый клик запускает игру
        if (_state == GameState.NotStarted)
        {
            // Первый клик вне поля — игнорируем полностью, игру не запускаем.
            if (!_board.IsValid(point))
                return;

            // Если клетка Flagged — не запускаем игру, не ставим мины, не эмитим BoardGenerated.
            if (_board[point.X, point.Y].State == CellState.Flagged)
                return;

            // Иначе — ленивая генерация мин.
            // Проверка MinesPlaced нужна для тестов, которые ставят мины
            // вручную через BoardForTesting до первого клика.
            if (!_board.MinesPlaced)
            {
                _minePlacement.PlaceMines(_board, point, Settings.MineCount);
            }

            _state = GameState.InProgress;
            _startTime = _timeProvider.GetUtcNow();

            BoardGenerated?.Invoke(this, new BoardGeneratedEventArgs());
        }

        // Проверяем валидность точки (для NotStarted уже проверено выше, но для InProgress нужно).
        if (!_board.IsValid(point))
            return;

        // Игнорируем клик по открытой или помеченной флагом клетке.
        // Question — «мягкая» метка, разрешаем открытие.
        var cell = _board[point.X, point.Y];
        if (cell.State != CellState.Closed && cell.State != CellState.Question)
            return;

        var change = _revealService.RevealCell(_board, point, out var content);
        if (change == null)
            return;

        var cellInfo = _board.GetCellInfo(point);

        // Каскад или одиночное открытие?
        // RevealCell выставляет content = Empty только при AdjacentMines == 0,
        // поэтому достаточно одной проверки.
        bool isCascade = content == CellContent.Empty;

        if (isCascade)
        {
            var revealedPoints = _revealService.CascadeReveal(_board, point);
            // Собираем CellInfo только если кто-то подписан: UI перерисовывает всё
            // по CellChanged/GameStateChanged, а CellsRevealed для неё — просто триггер
            // Invalidate(). На каскаде в сотни тысяч клеток List<CellInfo> — это десятки
            // MB аллокаций, которых можно избежать, если подписчиков нет.
            if (CellsRevealed != null)
            {
                var revealedInfos = new List<CellInfo>(revealedPoints.Count);
                foreach (var p in revealedPoints)
                    revealedInfos.Add(_board.GetCellInfo(p));
                CellsRevealed.Invoke(this, new CellsRevealedEventArgs(revealedInfos));
            }
        }
        else
        {
            // Одиночное открытие: эмитим CellChanged.
            CellChanged?.Invoke(this, new CellChangedEventArgs(change, cellInfo));
        }

        // Попали на мину — поражение
        if (content == CellContent.Mine)
        {
            // Открываем все мины и неверные флаги через RevealService.
            var revealedInfos = _revealService.RevealAll(_board);
            CellsRevealed?.Invoke(this, new CellsRevealedEventArgs(revealedInfos));
            _elapsed = Elapsed;
            _state = GameState.Lost;
            GameStateChanged?.Invoke(this, new GameStateChangedEventArgs(GameState.Lost, cellInfo));
            return;
        }

        // Проверка победы
        if (WinCondition.IsWon(_board, Settings.MineCount))
        {
            _elapsed = Elapsed;
            _state = GameState.Won;
            SaveRecord();
            GameStateChanged?.Invoke(this, new GameStateChangedEventArgs(GameState.Won));
        }
    }

    /// <summary>
    /// Циклически переключает метку клетки правым кликом:
    /// Closed → Flagged → Question → Closed.
    /// Клик по Opened/WrongFlag игнорируется.
    /// Разрешён в состоянии NotStarted (меняет метку, эмитит CellChanged,
    /// GameState остаётся NotStarted).
    /// </summary>
    public void CycleMark(CellPoint point)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_state == GameState.Won || _state == GameState.Lost)
            return;

        if (!_board.IsValid(point))
            return;

        var cell = _board[point.X, point.Y];

        // Метку можно менять только на закрытых клетках (в любом из трёх состояний).
        if (cell.State != CellState.Closed
            && cell.State != CellState.Flagged
            && cell.State != CellState.Question)
            return;

        var previousState = cell.State;
        _board.CycleMark(point);
        var newState = _board[point.X, point.Y].State;

        var change = new CellChange(point, previousState, newState);
        var cellInfo = _board.GetCellInfo(point);

        CellChanged?.Invoke(this, new CellChangedEventArgs(change, cellInfo));
    }

    /// <summary>
    /// Внутреннее поле для тестов. Не использовать в UI.
    /// </summary>
    internal Board BoardForTesting => _board;

    /// <summary>
    /// Получает снимок клетки для отрисовки UI.
    /// После Dispose бросает ObjectDisposedException.
    /// </summary>
    public CellInfo GetCell(CellPoint point)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_board.IsValid(point))
            return new CellInfo(point, CellState.Closed, CellContent.Empty, 0);

        return _board.GetCellInfo(point);
    }

    /// <summary>
    /// Получает снимки всех клеток для полной перерисовки.
    /// После Dispose бросает ObjectDisposedException.
    /// </summary>
    public IEnumerable<CellInfo> GetAllCells()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _board.GetAllCellInfos();
    }

    // --- Private helpers ---

    /// <summary>
    /// Сохраняет рекорд при победе на фиксированном уровне.
    /// </summary>
    private void SaveRecord()
    {
        if (Difficulty == DifficultyLevel.Custom)
            return;

        var timeSeconds = Elapsed.TotalSeconds;

        if (!_records.WouldQualify(timeSeconds))
            return;

        var entry = new RecordEntry(PlayerName, timeSeconds);

        // Сначала сохраняем в стор — только он решает, попал ли рекорд в топ-5.
        // Если стор отверг, память не трогаем: иначе AddRecord вытеснит пятую
        // запись, и откат её не восстановит.
        if (!_recordStore.SaveRecord(Difficulty, entry))
            return;

        // Стор принял — зеркалим в память.
        _records.AddRecord(entry);
    }

    /// <summary>
    /// Аккорд: открывает всех закрытых соседей клетки, если число флагов вокруг
    /// равно цифре на клетке. Классическое поведение Windows XP.
    /// Требует: State == InProgress, клетка Opened + Number, flags == AdjacentMines.
    /// При неверных флагах — поражение (как при клике в мину).
    /// Question-соседи открываются, Flagged — остаются.
    /// </summary>
    public void Chord(CellPoint point)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_state != GameState.InProgress)
            return;

        if (!_board.IsValid(point))
            return;

        var center = _board[point.X, point.Y];
        if (center.State != CellState.Opened || center.Content != CellContent.Number)
            return;

        // Флаги вокруг (Question — не флаг).
        int flags = 0;
        foreach (var n in _board.GetValidNeighbors(point))
            if (_board[n.X, n.Y].State == CellState.Flagged)
                flags++;

        // Классика: аккорд срабатывает только на точное равенство.
        // Если флагов больше, чем цифра — ничего не делаем (игрок ошибся, пусть думает).
        if (flags != center.AdjacentMines)
            return;

        // Соседи, которые будут открыты: Closed и Question (Question — «мягкая» метка,
        // не блокирует открытие, см. §7 02-GAME-RULES.md).
        var toOpen = new List<CellPoint>();
        foreach (var n in _board.GetValidNeighbors(point))
        {
            var nc = _board[n.X, n.Y];
            if (nc.State == CellState.Closed || nc.State == CellState.Question)
                toOpen.Add(n);
        }

        if (toOpen.Count == 0)
            return;

        // Проверка на мину. Если есть — поражение сразу, остальные безопасные
        // соседи НЕ открываются (классическое поведение: показан только взрыв + все мины).
        CellPoint? triggeredMine = null;
        foreach (var n in toOpen)
        {
            if (_board[n.X, n.Y].IsMine)
            {
                triggeredMine = n;
                break;
            }
        }

        if (triggeredMine is CellPoint mine)
        {
            // Открываем триггерную мину, эмитим CellChanged — как в OpenCell при клике в мину.
            var change = _revealService.RevealCell(_board, mine, out _);
            if (change == null) return; // защита; не должно случиться
            var cellInfo = _board.GetCellInfo(mine);
            CellChanged?.Invoke(this, new CellChangedEventArgs(change, cellInfo));

            // RevealAll открывает все остальные непомеченные мины и помечает WrongFlag.
            var revealedInfos = _revealService.RevealAll(_board);
            CellsRevealed?.Invoke(this, new CellsRevealedEventArgs(revealedInfos));

            _elapsed = Elapsed;
            _state = GameState.Lost;
            GameStateChanged?.Invoke(this, new GameStateChangedEventArgs(GameState.Lost, cellInfo));
            return;
        }

        // Безопасно: открываем всех соседей. Empty — каскад; Number — одиночное.
        // HashSet вместо List — каскады могут пересекаться, дедуп бесплатный.
        var revealedSet = new HashSet<CellPoint>();
        foreach (var n in toOpen)
        {
            var nc = _board[n.X, n.Y];
            if (nc.State != CellState.Closed && nc.State != CellState.Question)
                continue; // сосед уже открыт предыдущим каскадом в этом же аккорде

            var change = _revealService.RevealCell(_board, n, out var content);
            if (change == null) continue;

            if (content == CellContent.Empty)
            {
                // CascadeReveal включает стартовую точку в результат.
                foreach (var cp in _revealService.CascadeReveal(_board, n))
                    revealedSet.Add(cp);
            }
            else
            {
                revealedSet.Add(n);
            }
        }

        // Аккорд — «пакетное» действие, эмитим одно CellsRevealed (как каскад).
        // UI перерисует всё поле по одному событию.
        if (revealedSet.Count > 0 && CellsRevealed != null)
        {
            var infos = new List<CellInfo>(revealedSet.Count);
            foreach (var p in revealedSet)
                infos.Add(_board.GetCellInfo(p));
            CellsRevealed?.Invoke(this, new CellsRevealedEventArgs(infos));
        }

        // Проверка победы: последняя безопасная могла открыться именно аккордом.
        if (WinCondition.IsWon(_board, Settings.MineCount))
        {
            _elapsed = Elapsed;
            _state = GameState.Won;
            SaveRecord();
            GameStateChanged?.Invoke(this, new GameStateChangedEventArgs(GameState.Won));
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
