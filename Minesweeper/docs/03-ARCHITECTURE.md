# 03 — Architecture

## Проекты в решении

- `src/MinesweeperCore` — библиотека с игровой логикой (.NET 8, без UI).
- `src/MinesweeperWinForms` — приложение на WinForms. Подключает `MinesweeperCore`.
- `src/MinesweeperMonoGame` — приложение на MonoGame. Подключает `MinesweeperCore`.
- `tests/MinesweeperCore.Tests` — тесты для ядра (xUnit).

Ядро ничего не знает про UI. UI зависит от ядра, а не наоборот.

## Что лежит в `MinesweeperCore`

### `Domain/`

| Класс | Роль |
|---|---|
| `CellPoint` | Координаты клетки (x, y). |
| `Board` | Сетка клеток, валидация координат, доступ по `CellPoint` или индексу. Хранит `OpenedSafeCells` (O(1) win condition) и `FlaggedCount` (O(1) `RemainingMines`). `PlaceMines(ReadOnlySpan<CellPoint>)` — расстановка мин + подсчёт `AdjacentMines` за O(mineCount × 8). `CellRef`/`SetWrongFlag` — mutation-доступ для struct-клеток. |
| `Cell` | Внутренняя модель клетки. **`struct`** (16 байт): на 1000×1000 это ~16 MB и одна аллокация вместо 1M `new Cell()`. `default(Cell)` = { Closed, Empty, !IsMine, 0 }. Модификация — через `Board.CellRef`; индексатор `Board[x, y]` возвращает копию для чтения. |
| `CellInfo` | Снимок клетки для UI. Отдаётся наружу вместо `Cell`, чтобы UI не мог изменить состояние. |
| `GameSettings` | Размер поля и количество мин. Валидирует параметры. |
| `DifficultyPresets` | Готовые настройки для Beginner / Intermediate / Expert. |
| `CellState`, `CellContent`, `GameState`, `DifficultyLevel` | Перечисления. |

### `Abstractions/`

| Интерфейс | Роль |
|---|---|
| `IRandomProvider` | Даёт случайные числа. Нужен, чтобы в тестах подменять `Random` на детерминированный фейк. |
| `IRecordStore` | Сохраняет и загружает таблицу рекордов. Ядро не знает, куда именно (файл, БД, реестр). |
| `ITimeProvider` | Даёт текущее время (`DateTime UtcNow`). Нужен, чтобы в тестах подменять реальные часы на управляемые. |

### Дефолтные реализации портов

В `MinesweeperCore` (в подпапке `Infrastructure/`) лежат готовые реализации портов, чтобы UI не писал их с нуля:

| Класс | Реализует | Роль |
|---|---|---|
| `SystemRandomProvider` | `IRandomProvider` | Обёртка над `System.Random`. В тестах подменяется на детерминированный фейк. |
| `SystemTimeProvider` | `ITimeProvider` | Обёртка над `DateTime.UtcNow`. В тестах подменяется на ручные часы. |
| `InMemoryRecordStore` | `IRecordStore` | Хранит рекорды в памяти. Для тестов и как заглушка в UI. |

Для реального UI файловый `IRecordStore` (JSON/реестр) реализуется **вне** `MinesweeperCore` — в соответствующем UI-проекте или отдельной библиотеке.

### `Events/`

| Класс | Роль |
|---|---|
| `BoardGeneratedEventArgs` | Сообщает, что мины расставлены (после первого клика). |
| `CellChange` | Пара «координата + новое состояние клетки». Используется внутри `CellChangedEventArgs`. |
| `CellChangedEventArgs` | Одна клетка изменилась (открылась, флаг поставлен/снят). |
| `CellsRevealedEventArgs` | Каскадное открытие завершилось. Содержит список всех открытых клеток — чтобы UI перерисовал их за один проход, а не по одному. |
| `GameStateChangedEventArgs` | Игра перешла в состояние Победа или Поражение. |

### `Records/`

| Класс | Роль |
|---|---|
| `RecordEntry` | Одна запись рекорда: время и имя игрока. |
| `RecordTable` | Логика таблицы: сортировка, обрезка до топ-5, проверка, попадает ли новое время в таблицу. |

### `Engine/`

| Класс | Роль |
|---|---|
| `GameEngine` | Публичный фасад. Единственный класс, с которым работает UI. Хранит состояние партии и вызывает остальные сервисы. `RemainingMines` — O(1) через `Board.FlaggedCount`. При каскаде `List<CellInfo>` для `CellsRevealed` строится только если есть подписчики. |
| `MinePlacement` | Расставляет мины при первом клике (лениво). Классическое поведение: только клетка клика безопасна, соседи могут содержать мины. Partial Fisher-Yates (только `mineCount` swaps), `CellPoint[]` вместо `List<CellPoint>`, прямое сравнение с `safePoint`. Делегирует подсчёт `AdjacentMines` в `Board.PlaceMines`. |
| `RevealService` | Открывает клетки и делает каскадное открытие без рекурсии. `CascadeReveal` использует `bool[] visited` вместо `HashSet<CellPoint>` и инлайн-обход соседей без `yield return` — критично на больших полях. Возвращает `List<CellPoint>`. |
| `WinCondition` | Проверяет, все ли безопасные клетки открыты. Вызывается после каждого открытия. |

## Как UI работает с ядром

1. UI создаёт `GameEngine`, передавая ему `IRandomProvider` и `IRecordStore`.
2. Вызывает `StartNewGame(settings)` при старте или новой игре.
3. Для отрисовки запрашивает `GetCell(point)` — получает `CellInfo` (координаты, состояние, содержимое, число соседних мин) и рисует по нему.
4. На клик вызывает `OpenCell(point)` (левая кнопка) или `CycleMark(point)`
    (правая кнопка — циклически переключает метку Closed → Flagged → Question → Closed).
5. Подписывается на события (`CellChanged`, `CellsRevealed`, `GameStateChanged`) и по ним обновляет экран.

## Правила для UI

- Игровая логика — только в ядре. В UI её нет.
- UI не считает мины вокруг клетки, не делает каскадное открытие, не решает, победил игрок или нет — всё это делает `GameEngine`.
- UI работает только с `CellInfo`, `GameEngine` и аргументами событий. Внутренние модели (`Cell`, `Board`) UI не трогает.
- UI не ссылается на WinForms / MonoGame из ядра — наоборот, не бывает.

## Правила для ядра

- Ядро не ссылается на WinForms, MonoGame, WPF и любой другой UI.
- Всё, что зависит от среды (случайность, файлы, время), — через интерфейсы из `Abstractions/`. Это нужно, чтобы тесты работали без реального `Random` и без файловой системы.
- Публичное API ядра: `GameEngine`, `GameSettings`, `DifficultyPresets`, `DifficultyLevel`, `CellPoint`, `CellInfo`, `CellState`, `CellContent`, `GameState`, `RecordEntry`, `IRandomProvider`, `IRecordStore`, `ITimeProvider`, `SystemRandomProvider`, `SystemTimeProvider`, `InMemoryRecordStore`, все `*EventArgs`, `CellChange`.
- `Board`, `Cell`, `MinePlacement`, `RevealService`, `WinCondition` — детали реализации.

## Производительность

Скорость клика не зависит от размера поля. Горячие пути:

| Метод | Сложность | Частота |
|---|---|---|
| `GameEngine.OpenCell` (не каскад) | O(1) | каждый клик |
| `GameEngine.CycleMark` | O(1) | каждый правый клик |
| `GameEngine.RemainingMines` | O(1) | каждое чтение |
| `GameEngine.GetCell` | O(1) | в `OnPaint` для каждой клетки |
| `WinCondition.IsWon` | O(1) | после каждого открытия |
| `RevealService.CascadeReveal` | O(открываемых) | один раз на каскад |
| `MinePlacement.PlaceMines` | O(mineCount × 8) | **один раз за партию** |
| `RevealService.RevealAll` | O(TotalCells) | **один раз за партию** (`Lost`) |
| `Board.Reset` / `new Board(...)` | O(TotalCells) | **один раз за партию** (`StartNewGame`) |

Ключевые решения:

- `Cell` — `struct`, а не `class`. `new Cell[w, h]` — одна аллокация + memset.
- `Board.PlaceMines` обходит только мины (O(mineCount × 8)), а не всё поле.
- `MinePlacement` — partial Fisher-Yates: только `mineCount` swaps.
- `CascadeReveal` — `bool[] visited` вместо `HashSet<CellPoint>`, инлайн-обход соседей без `IEnumerator` allocs.
- `AdjacentMines` считается один раз в `PlaceMines` и не пересчитывается.
- `WinCondition.IsWon` — O(1) через `OpenedSafeCells`. Флаги в условии победы не участвуют.
