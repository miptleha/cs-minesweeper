# 06 — Changelog

## [Unreleased]

### Added

- `Board.FlaggedCount` — инкрементальный счётчик флагов, поставленных игроком. `GameEngine.RemainingMines` стал O(1) (`MineCount − FlaggedCount`).
- `ITimeProvider` (порт времени) + `SystemTimeProvider` (реализация) + `FakeTimeProvider` (тестовая).
- `SystemRandomProvider(int? seed = null)` — конструктор с seed.
- `InMemoryRecordStore` — заглушка для тестов и UI.
- `CellState.WrongFlag` + `CellInfo.WasWrongFlag` — ошибочные флаги отличимы от обычных Closed.
- `RevealService.RevealAll(Board)` — батчинг всех мин и ошибочных флагов в один `CellsRevealed` при поражении.
- `RecordTable.Remove(RecordEntry)` — удаление записи по ссылке.
- `GameEngine.BoardForTesting` — internal-свойство для тестов (через `[InternalsVisibleTo]`).
- 12 новых тестов (30 → 42): идемпотентность кликов, поведение после Win/Lose, сброс таймера, `Difficulty`, `GetCell` out-of-bounds, `StartNewGame` reset.
- `CellState.Question` — новое состояние клетки с меткой «возможно, мина».
- Ассеты `cell_question.png`, `cell_wrong_flag.png`, `minus.png`, `icon.ico`,
  `smiley_win.png`, `smiley_lose.png`, `smiley_oh.png` — все готовы.
- `CellInfo.Position`, `GameEngine.PlayerName`, `GameEngine.RemainingMines`, `Board.OpenedSafeCells`, `Board.Open(CellPoint, CellContent)`.
- 15 новых тестов (55 → 70): `RemainingMines`, `PlayerName`, `Speed`, `Cascade_5x5`, `AllCellsFlagged`, `AllCellsQuestioned`, `Dispose`, `InvalidPoint`, `FirstClickOnFlagged/Question`.
- `Board.PlaceMines(ReadOnlySpan<CellPoint>)` — расстановка мин + подсчёт `AdjacentMines` за O(mineCount × 8) вместо O(TotalCells × 8).
- `Board.CellRef(int, int)` / `Board.CellRef(CellPoint)` — ref-доступ к клетке (struct).
- `Board.SetWrongFlag(int, int)` — установка `WrongFlag` через `Board`.
- `PerformanceTests` — 3 теста производительности: `Speed_30x24WithManyMines_...` (< 1 с), `Speed_30x24WithFewMines_...` (< 1 с), `Stress_1000x1000With100kMines_...` (< 5 с).
- `GameSettingsTests` — валидация границ `GameSettings` (3×3 .. 1000×1000, `MineCount` 0..(W-1)*(H-1)).
- `CascadeTests.Cascade_3x3WithZeroMines_CornerClickOpensAllAndWins` — тест минимального поля 3×3.
- 1 новый тест (93 → 94): `Cascade_9x9WithOneMine_CellsRevealedHasExactly80UniqueCells`.

### Changed

- **`GameEngine.RemainingMines`** больше не обнуляется при `Won`. Раньше при победе UI видел `0`; теперь — `MineCount − поставленные_флаги`. Значение информационное и может быть ложным.
- **Первый клик: классическое поведение Windows XP** — из расстановки мин исключается только клетка клика; соседи могут содержать мины. Первый клик больше не гарантирует каскадное открытие. Затрагивает `MinePlacement.PlaceMines`, тесты `FirstClickSafetyTests`, `CascadeTests`, `FlagTests`.
- **Каскад больше не эмитит `CellChanged`** — только одно `CellsRevealed`.
- **`RevealService.RevealAll`** — вместо N `CellChanged` (по одному на каждую мину) один `CellsRevealed`.
- **`SaveRecord`** — сначала сохранение в стор, при `false` в память не пишем.
    `AddRecord` в память вызывается только после успешного `SaveRecord`; при false пятая запись остаётся на месте.
- **`Elapsed`** фиксируется до смены `GameState` при `Won`/`Lost`.
- **Первый клик вне поля** не запускает игру (ни `PlaceMines`, ни `BoardGenerated`, ни таймер).
- **`InMemoryRecordStore.SaveRecord`** — честно возвращает `bool`: `true` только если рекорд попал в топ-5 до обрезки.
- **`CellInfo`, `RecordEntry`** — `sealed record` (было `class`).
- **Кастомный делегат `GameStateChangedEventHandler`** заменён на `EventHandler<GameStateChangedEventArgs>`.
- **Правый клик циклически переключает метку**: `Closed → Flagged →
  Question → Closed`. Метод `GameEngine.ToggleFlag` переименован в
  `GameEngine.CycleMark`; `Cell.ToggleFlag` → `Cell.CycleMark`.
- **Правильно помеченные мины при `Lost` остаются `Flagged`** (не
  открываются). Раньше открывались как обычные мины — теперь сохраняются
  как награда за правильную догадку.
- **`Question` не блокирует открытие**: клетку в состоянии вопроса можно
  открыть левым кликом, и каскад её не пропускает (только `Flagged`
  блокирует).
- **`Point` → `CellPoint`**, `CellInfo.Point` → `Position`, `CellChange.Point` → `Position`.
- **Порядок `OpenCell` при `NotStarted`**: `Flagged` не запускает игру.
- **`CycleMark` разрешён в `NotStarted`**: меняет метку, эмитит `CellChanged`, но `GameState` остаётся `NotStarted`.
- **`WinCondition.IsWon`** — O(1) через `OpenedSafeCells`.
- **`GetCell`/`GetAllCells` после `Dispose`** — `ObjectDisposedException`.
- **`GameSettings`** — сообщения исключений на русском.
- **`Cell` — `struct` вместо `class`.** На 1000×1000 это ~16 MB и одна аллокация вместо 1M `new Cell()`. Модификация — через `Board.CellRef`, чтение — копия через `Board[x, y]`.
- **`MinePlacement.PlaceMines`** — partial Fisher-Yates (только `mineCount` swaps вместо полного shuffle), `CellPoint[]` вместо `List<CellPoint>`, прямое сравнение с `safePoint`. Удалён `CalculateAdjacentMines` — логика перенесена в `Board.PlaceMines`.
- **`RevealService.CascadeReveal`** — `bool[] visited` вместо `HashSet<CellPoint>`, инлайн-обход соседей без `yield return`. Возвращает `List<CellPoint>` вместо `IEnumerable<CellPoint>`.
- **`GameEngine.OpenCell`** — при каскаде `List<CellInfo>` для `CellsRevealed` строится только если есть подписчики.
- **`GameSettings`** — границы валидации: `MinWidth = MinHeight = 3`, `MaxWidth = MaxHeight = 1000` (было 9..30 / 9..24). Классические `9..30 / 9..24` остаются рекомендацией для UI.

### Fixed

- **`GameEngine.RemainingMines`** больше не сканирует поле на каждое чтение (был O(N) через `CountFlagged`). Теперь O(1) через `Board.FlaggedCount`.
- `docs/03-ARCHITECTURE.md`: упоминание `ToggleFlag` заменено на `CycleMark`.
- `docs/02-GAME-RULES.md` §16.1: добавлено упоминание `smiley_win.png` на победе.
- `docs/02-GAME-RULES.md` §15: описана формула счётчика мин (не считает `Question`, поддерживает отрицательные значения через `minus.png`).
- Добавлены тесты `Win_CorrectlyFlaggedMine_RemainsFlagged`,
  `Win_MineInQuestionState_RemainsQuestion`,
  `Lose_QuestionOnSafeCell_RemainsQuestion` — покрывают §16.1 и §16.2,
  которые раньше не были проверены (47 → 50 тестов).
- **`RevealService.CascadeReveal`** больше не возвращает дубликаты: `startPoint`
    добавляется один раз при инициализации, соседи — один раз при `Open`. Раньше
    Empty-клетки попадали в список дважды (на 9×9 с 1 миной — ~157 записей вместо 80).
- **`CellRef(x, y).IsMine = true` больше не используется в тестах** — заменён на `Board.PlaceMines(...)`, который корректно обновляет `AdjacentMines` у соседей. Раньше такой обход `PlaceMines` оставлял соседей с `AdjacentMines = 0`, и клетки открывались как `Empty` вместо `Number`.

### Removed

- Поле `_random` в `GameEngine` (передаётся напрямую в `MinePlacement`).
- Метод `RevealAllMines` из `GameEngine` (перенесён в `RevealService` как `RevealAll`).
- Метод `ITimeProvider.Sleep` (не относится к абстракции времени).
- Отладочный `DebugFlagTests.cs`.
- `CellInfo.WasWrongFlag` (заменён на `State == CellState.WrongFlag`).
- `IRandomProvider.NextDouble` (не используется).
- `RecordTable.Remove` (не используется).
