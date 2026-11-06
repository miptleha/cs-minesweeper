# 06 — Changelog

Все заметные изменения в проекте. Формат основан на [Keep a Changelog](https://keepachangelog.com/ru/1.0.0/),
проект придерживается [Semantic Versioning](https://semver.org/lang/ru/).

## [0.2.1] - 2026-11-06

Патч-релиз, исправляющий отображение интерфейса на мониторах с высоким DPI (например, на ноутбуках с масштабированием 150%).

### Fixed

- **Масштабирование High-DPI в WinForms:** интерфейс больше не кажется крошечным на экранах с масштабированием (150% и выше).
- Добавлено свойство `AutoScaleMode.Dpi` и `AutoScaleDimensions` для `MainForm`, `RecordsForm`, `AboutForm` и `NamePromptForm`, чтобы шрифты и стандартные контролы (меню, кнопки) корректно увеличивались системой.
- Размер клеток игрового поля теперь рассчитывается динамически (`24 * DeviceDpi / 96f`) вместо жестко заданных 24 пикселей.
- Отрисовка поля, счётчиков мин/времени и смайлика теперь использует масштабированные координаты и размеры `Bitmap`, чтобы избежать размытия и обрезки краёв при высоком DPI.
- Добавлена обработка `OnDpiChanged`, чтобы при перетаскивании окна между мониторами с разным масштабом интерфейс перестраивался на лету.

---

## [0.2.0] - 2026-11-06

Релиз 0.2.0 знаменует собой завершение Phase 5 — появление полноценного и играбельного WinForms-интерфейса, а также укрепление контрактов ядра и расширение тестового покрытия.

### Added

**Ядро (MinesweeperCore):**

- `Board.MinesPlaced` (internal) и защита в `PlaceMines`: теперь метод бросает `InvalidOperationException` при попытке повторной расстановки мин или при наличии дубликатов в переданном `ReadOnlySpan<CellPoint>`. Это предотвращает молчаливое создание непроходимой партии.
- `GameEngine.OpenCell` теперь проверяет `Board.MinesPlaced` перед вызовом `PlaceMines`. Это позволяет тестам вручную расставлять мины до первого клика, не нарушая контракт `Board`.
- 6 новых тестов на события (всего 105): проверка payload (`CellChanged`, `TriggerCell`), порядок эмиссии (`BoardGenerated` -> `CellChanged` -> `CellsRevealed` -> `GameStateChanged`), отсутствие повторной эмиссии `BoardGenerated`.
- 4 новых теста в `BoardTests` на защиту от дубликатов и повторного вызова `PlaceMines`.

**WinForms UI (MinesweeperWinForms):**

- Полноценный интерфейс игры (`MainForm`), включая отрисовку поля, LED-счётчики таймера и мин, смайлик-кнопку.
- Классическое поведение мыши: открытие клетки по `MouseUp` (а не `MouseDown`), поддержка перетаскивания с зажатой кнопкой.
- `JsonRecordStore` — сохранение таблицы рекордов в `%LocalAppData%\Minesweeper\records.json` (безопасно, не требует прав администратора).
- Окно «Лучшие результаты» (`RecordsForm`) с тремя вкладками (Новичок, Любитель, Профессионал).
- Окно «О программе» (`AboutForm`) со ссылкой на GitHub.
- Диалог ввода имени (`NamePromptForm`), появляющийся при побитии рекорда.
- Встроенные ресурсы (Embedded Resources) для картинок: изображения теперь упакованы в `.dll` и не могут быть случайно удалены или изменены пользователем в папке с игрой.
- Закрытие дочерних окон по клавише `Esc`.

### Changed

**Ядро (MinesweeperCore):**

- `RecordTable` переведен в `internal`. Публичное API для работы с рекордами ограничено `RecordEntry` и `IRecordStore`.
- `RevealService.CascadeReveal` больше не возвращает дубликаты: `startPoint` добавляется один раз при инициализации, соседи — один раз при `Open`. Ранее Empty-клетки попадали в список дважды (на 9×9 с 1 миной — ~157 записей вместо 80).
- `GameEngine.OpenCell`: убрана избыточная проверка `AdjacentMines == 0` при определении каскада (достаточно `content == CellContent.Empty`).
- `SaveRecord`: сначала вызывается сохранение в стор, при `false` в память ничего не пишется (пятая запись не вытесняется).

**WinForms UI:**

- Переименование `Form1` в `MainForm`.
- `AutoScaleMode = AutoScaleMode.None` для идеально ровных клеток 24×24 на любых мониторах.
- Изменение логики `RenderLedCounter` и `UpdateMineCounter`: используется `InterpolationMode.NearestNeighbor` и явные `Rectangle` для предотвращения искажения пиксель-арта.
- Смайлик теперь представлен стандартной `Button` (размер 32×32), картинка масштабируется до 28×28 для идеальной посадки в рамку.

### Fixed

- **`GameEngine.RemainingMines`** больше не сканирует поле на каждое чтение (был O(N) через `CountFlagged`). Теперь O(1) через `Board.FlaggedCount`.
- **`GameEngine.OpenCell`** при каскаде строит `List<CellInfo>` только если есть подписчики на `CellsRevealed` (защита от лишних аллокаций).
- **`Board.Reset`**: исправлено описание (комментарий), теперь явно указано, что мины тоже снимаются.
- Добавлены тесты `Win_CorrectlyFlaggedMine_RemainsFlagged`, `Win_MineInQuestionState_RemainsQuestion`, `Lose_QuestionOnSafeCell_RemainsQuestion` — покрывают §16.1 и §16.2 `02-GAME-RULES.md`.
- `docs/02-GAME-RULES.md` §15: описана формула счётчика мин (не считает `Question`, поддерживает отрицательные значения).
- `docs/03-ARCHITECTURE.md`: упоминание `ToggleFlag` заменено на `CycleMark`, добавлено примечание про `JsonRecordStore`.
- `docs/09-PHASE-5-WINFROMS.md`: полностью переписан под актуальную архитектуру WinForms (встроенные ресурсы, `MainForm`, `JsonRecordStore`, классическое поведение мыши).

### Removed

- Отладочный `DebugFlagTests.cs`.
- `CellInfo.WasWrongFlag` (заменён на `State == CellState.WrongFlag`).
- `IRandomProvider.NextDouble` (не используется).
- `RecordTable.Remove` (не используется).
- Зависимость от копирования `*.png` в выходную папку (теперь только Embedded Resources).

---

## [0.1.0] - 2026-10-15

Первый релиз ядра `MinesweeperCore` (Phase 1–4). Полная бизнес-логика игры, порты для тестирования, базовая архитектура.

### Added

- Модели `CellPoint`, `Cell`, `Board`, `GameSettings`, `DifficultyPresets`.
- Генерация мин (ленивая, только после первого клика) с защитой первого клика.
- Каскадное открытие (Flood Fill) без рекурсии.
- Состояния: `Closed`, `Opened`, `Flagged`, `Question`, `WrongFlag`.
- Счётчик мин `RemainingMines`, таймер `Elapsed`, таблица рекордов (топ-5).
- Абстракции `IRandomProvider`, `IRecordStore`, `ITimeProvider`.
- 94 юнит-теста (включая тесты производительности).