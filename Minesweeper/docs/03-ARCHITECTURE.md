# 03 — Архитектура

Этот документ описывает **архитектуру решения `cs-minesweeper`**: структуру проектов, правила зависимостей, внутреннее устройство ядра `MinesweeperCore` и то, как каждый UI-проект взаимодействует с ядром.

Здесь нет описания правил игры (см. `02-GAME-RULES.md`) и нет конвенций написания кода (см. `04-AGENT-GUIDE.md`).

---

## 1. Общая схема

Решение построено по принципу **слоистой архитектуры с чистым ядром** (по мотивам Clean Architecture и Ports & Adapters):

```
┌───────────────────────────────────────────────────────────────┐
│                        UI-проекты                             │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐       │
│  │ WinForms │  │ MonoGame │  │ Avalonia │  │   WPF    │       │
│  │ (MVP)    │  │(planned) │  │ (future) │  │ (future) │       │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘       │
└───────┼─────────────┼─────────────┼─────────────┼─────────────┘
        │             │             │             │
        └─────────────┴──────┬──────┴─────────────┘
                             │ depends on
                             ▼
┌───────────────────────────────────────────────────────────────┐
│                      MinesweeperCore                          │
│  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐   │
│  │     Domain     │  │     Engine     │  │  Abstractions  │   │
│  │  (модели,      │  │  (правила,     │  │  (порты:       │   │
│  │   enum-ы)      │  │   события)     │  │   IRandomProvider│ │
│  └────────────────┘  └────────────────┘  │   IRecordStore)│   │
│                                          └────────────────┘   │
└───────────────────────────────────────────────────────────────┘
```

**Направление зависимостей — всегда от UI к Core.** Core не знает о существовании UI-проектов.

---

## 2. Проекты в решении

| Проект | TFM | Назначение | Статус |
|--------|-----|------------|--------|
| `MinesweeperCore` | `net8.0` | Бизнес-логика игры, доменные модели, события, абстракции | ✅ MVP |
| `Minesweeper.WinForms` | `net8.0-windows` | UI на WinForms (использует `System.Drawing` для отрисовки) | ✅ MVP |
| `Minesweeper.MonoGame` | `net8.0` | UI на MonoGame DesktopGL | 🚧 planned |
| `Minesweeper.Avalonia` | `net8.0` | Кроссплатформенный UI | 💭 future |
| `Minesweeper.WPF` | `net8.0-windows` | UI на WPF | 💭 future |
| `MinesweeperCore.Tests` | `net8.0` | Модульные тесты ядра (xUnit) | 📝 not started |

> **Важно:** `MinesweeperCore` нацелен на `net8.0` **без суффикса `-windows`**. Это гарантирует, что Core не сможет случайно сослаться на `System.Windows.Forms`, `System.Drawing` (в части Windows-only API) или `Microsoft.Win32.Registry`.

---

## 3. Правила зависимостей

Соблюдение этих правил — критично для сохранения кроссплатформенности и тестируемости.

### 3.1. Разрешено

- UI-проекты **ссылаются** на `MinesweeperCore` (ProjectReference).
- `MinesweeperCore.Tests` ссылается на `MinesweeperCore`.
- Core использует **только BCL** (`System.*`) и абстракции, определённые внутри себя.

### 3.2. Запрещено

- ❌ `MinesweeperCore` ссылается на любой UI-проект.
- ❌ `MinesweeperCore` ссылается на UI-библиотеки: `System.Windows.Forms`, `Microsoft.Xna.Framework.*`, `Avalonia.*`, `PresentationCore`, `System.Drawing.Common` (в части `Bitmap`/`Graphics`).
- ❌ Core использует `System.Console` для вывода (логировать должен вызывающий код).
- ❌ Core пишет напрямую в реестр (`Microsoft.Win32`) или файловую систему — только через абстракции (`IRecordStore`).
- ❌ Один UI-проект ссылается на другой UI-проект.
- ❌ UI-проект напрямую мутирует состояние `Board` или `Cell` — только через методы `GameEngine`.

### 3.3. Автоматическая проверка

Правила проверяются двумя способами:

1. **Целевая платформа.** `net8.0` без `-windows` не даст сослаться на WinForms.
2. **Тест архитектуры** (добавить в `MinesweeperCore.Tests`): рефлексией проверить, что среди referenced assemblies `MinesweeperCore` нет UI-сборок. Пример — `NetArchTest.Rules` или простой reflection-тест на `typeof(GameEngine).Assembly.GetReferencedAssemblies()`.

---

## 4. Внутренняя структура `MinesweeperCore`

Папки внутри проекта:

```
MinesweeperCore/
├── Domain/              # Модели предметной области
│   ├── Cell.cs
│   ├── Board.cs
│   ├── CellState.cs     # enum: Closed, Open, Flagged
│   ├── CellContent.cs   # enum: Empty, Number, Mine
│   ├── GameState.cs     # enum: NotStarted, InProgress, Won, Lost
│   ├── GameSettings.cs
│   └── DifficultyLevel.cs  # enum: Beginner, Intermediate, Expert, Custom
├── Engine/              # Правила и управление игрой
│   ├── GameEngine.cs
│   ├── MinePlacement.cs       # генерация и переразмещение мин
│   ├── RevealService.cs       # каскадное открытие
│   └── WinCondition.cs        # проверка победы
├── Events/              # Аргументы событий
│   ├── CellChangedEventArgs.cs
│   ├── CellsRevealedEventArgs.cs
│   ├── GameStateChangedEventArgs.cs
│   └── BoardGeneratedEventArgs.cs
├── Abstractions/        # Порты (интерфейсы для внешнего мира)
│   ├── IRandomProvider.cs
│   └── IRecordStore.cs
└── Records/             # Доменные модели рекордов
    ├── RecordEntry.cs
    └── RecordTable.cs
```

### 4.1. Domain — модели предметной области

**Только данные и минимальное поведение, зависящее только от самих данных.**

- `Cell` — структура или класс с координатами, состоянием, содержимым. Иммутабельность по возможности.
- `Board` — содержит клетки, знает свои размеры, умеет отдавать соседей.
- `GameSettings` — `record` с `Width`, `Height`, `MineCount`, `DifficultyLevel`.
- Enum-ы — чистые перечисления без поведения.

**Не класть в Domain:** логику открытия клеток, работу с событиями, работу с UI-состоянием (например, «какая клетка под курсором»).

### 4.2. Engine — правила игры

- **`GameEngine`** — публичное API для UI. Хранит состояние игры, обрабатывает команды (`OpenCell`, `ToggleFlag`, `StartNewGame`), поднимает события.
- **`MinePlacement`** — внутренний сервис: размещение мин с гарантией безопасного первого хода (ленивое размещение при первом клике, либо переразмещение).
- **`RevealService`** — каскадное открытие клеток с `N = 0`.
- **`WinCondition`** — проверка «все безопасные клетки открыты».

**Engine не знает об UI.** Не хранит `Bitmap`, `Texture2D`, не форматирует строки для отображения, не измеряет время в миллисекундах для анимации.

### 4.3. Events — контракт оповещения

Полный список событий, на которые может подписаться UI:

| Событие | Когда поднимается | Аргументы |
|---------|-------------------|-----------|
| `BoardGenerated` | После создания/пересоздания поля (например, в конструкторе или `StartNewGame`) | `GameSettings` |
| `CellChanged` | Одиночное изменение клетки (открытие одной клетки, установка/снятие флага) | `X`, `Y`, `CellState`, `CellContent` |
| `CellsRevealed` | Массовое открытие (каскад или открытие всех мин при поражении) | `IReadOnlyList<CellChange>` |
| `GameStateChanged` | Переход между состояниями (`NotStarted → InProgress → Won/Lost`) | `OldState`, `NewState` |
| `TimerTick` | **Не является событием ядра.** UI сам опрашивает `GameEngine.Elapsed` по своему таймеру. | — |

**Почему `CellsRevealed` отдельно от `CellChanged`:** при каскаде может открыться 50+ клеток за один клик. Поднимать 50 событий — лишняя нагрузка и лишний шум для UI. Одно событие со списком эффективнее.

**Почему `TimerTick` — не в Core:** тиканье таймера — это UI-концерн. Core предоставляет `Elapsed` (через `TimeProvider`), а как часто обновлять отображение — решает UI.

### 4.4. Abstractions — порты наружу

Абстракции, которые Core требует от окружения:

**`IRandomProvider`** — генерация случайных чисел.

```csharp
public interface IRandomProvider
{
    int Next(int maxExclusive);
}
```

Реализации: `SystemRandomProvider` (в Core, обёртка над `Random`), `DeterministicRandomProvider` (в тестах, для воспроизводимости).

**`IRecordStore`** — хранилище таблицы рекордов.

```csharp
public interface IRecordStore
{
    IReadOnlyList<RecordEntry> GetRecords(DifficultyLevel level);
    void AddRecord(DifficultyLevel level, RecordEntry entry);
}
```

Реализации: `JsonFileRecordStore` в UI-проекте (или отдельной инфраструктурной сборке), `InMemoryRecordStore` в тестах.

**`TimeProvider`** — встроенный в .NET 8 (`System.TimeProvider`). Core использует его для `Elapsed`, а тесты подставляют `FakeTimeProvider` (пакет `Microsoft.Extensions.TimeProvider.Testing`).

### 4.5. Records — модель таблицы рекордов

Чистые данные:

```csharp
public sealed record RecordEntry(string PlayerName, TimeSpan Time, DateTime AchievedAt);
```

`RecordTable` — вспомогательный класс: сортировка по времени, отсечение до топ-5, вставка новой записи. Никакой работы с файлами — это делает `IRecordStore`.

---

## 5. Публичное API `GameEngine`

Основная точка входа для UI. Примерная форма:

```csharp
public sealed class GameEngine
{
    // Состояние (только для чтения)
    public GameSettings Settings { get; }
    public GameState State { get; }
    public IReadOnlyBoard Board { get; }
    public int FlagsPlaced { get; }
    public int MinesRemaining => Settings.MineCount - FlagsPlaced;
    public TimeSpan Elapsed { get; }

    // События
    public event EventHandler<BoardGeneratedEventArgs>? BoardGenerated;
    public event EventHandler<CellChangedEventArgs>? CellChanged;
    public event EventHandler<CellsRevealedEventArgs>? CellsRevealed;
    public event EventHandler<GameStateChangedEventArgs>? GameStateChanged;

    // Конструктор
    public GameEngine(GameSettings settings, IRandomProvider random, TimeProvider? time = null);

    // Команды
    public void StartNewGame(GameSettings settings);
    public void StartNewGame();                     // перезапуск с текущими настройками
    public void OpenCell(int x, int y);
    public void ToggleFlag(int x, int y);

    // Вспомогательное
    public CellInfo GetCell(int x, int y);          // снимок состояния клетки
}
```

### 5.1. Как UI читает состояние

UI **не должен** обращаться к внутренним коллекциям `Board` и мутировать их. Только:

- Чтение через `GetCell(x, y)` или `Board` (иммутабельный снимок).
- Реакция на события `CellChanged` / `CellsRevealed`.
- Управление через `OpenCell` / `ToggleFlag`.

### 5.2. Ленивое размещение мин

При создании `GameEngine` мины **не размещаются**. Они размещаются при первом вызове `OpenCell` — так, чтобы открываемая клетка гарантированно оказалась безопасной (см. правила, раздел 5).

Это упрощает реализацию и делает поведение детерминированным для тестов. С точки зрения игрока результат идентичен классическому «перемещению мины» после первого клика.

---

## 6. Как UI-проекты используют Core

### 6.1. WinForms

- **Отрисовка поля:** кастомный `Control` с переопределённым `OnPaint`, либо сетка `Button`-ов / `Label`-ов (для MVP — второе проще).
- **Ввод:** `MouseDown`, различает левую и правую кнопку → `OpenCell` / `ToggleFlag`.
- **Подписка на события:** подписывается на `CellChanged` и `CellsRevealed` для точечного `Invalidate(rect)` — перерисовывается только изменившаяся клетка, а не всё поле.
- **Таймер:** `System.Windows.Forms.Timer` (интервал 1 сек) обновляет подпись из `GameEngine.Elapsed`.
- **Ресурсы:** `Properties.Resources` или папка `Assets/` с PNG-файлами, копируемыми в output. Core об этих файлах не знает.
- **Таблица рекордов:** реализация `IRecordStore` через JSON-файл в `%APPDATA%\Minesweeper\records.json`. Открытие диалога Best Times — через `Form`.
- **«О программе»:** обычный `AboutBox`.

### 6.2. MonoGame

- **Отрисовка:** `SpriteBatch.Draw` для каждой клетки, спрайты из `Content/`.
- **Ввод:** `Mouse.GetState()` в `Update` → сравнение с предыдущим состоянием для детекта клика.
- **Подписка на события:** **не обязательна**. Можно проще: раз в кадр обходить видимое поле и рисовать по текущему состоянию из `GameEngine.GetCell`. События всё равно полезны для реакции на `GameStateChanged` (показать экран «Game Over»).
- **Таймер:** `GameTime.TotalGameTime` для отображения, либо `Elapsed` из Core.
- **Ресурсы:** пайплайн MonoGame Content Pipeline, `Content.mgcb`. Core об этих файлах не знает.
- **Таблица рекордов:** тот же `IRecordStore`, но JSON-файл рядом с exe.

### 6.3. Avalonia / WPF (future)

- Используют **MVVM**: слой ViewModel в UI-проекте оборачивает `GameEngine` и реализует `INotifyPropertyChanged`.
- **`INotifyPropertyChanged` не проникает в Core** — это чисто UI-паттерн.
- `GameEngine` остаётся event-based; ViewModel подписывается на события и транслирует их в `PropertyChanged`.

---

## 7. Ресурсы и Assets

**Правило:** все визуальные и звуковые ресурсы живут **только в UI-проектах**.

- Core не содержит путей к файлам, `Bitmap`, `Texture2D`, шрифтов.
- Core сообщает **что** показать через enum-ы (`CellContent.Mine`, `CellContent.Number`), а UI решает **как** — какой спрайт, какой цвет, какая анимация.
- Для чисел 1–8 UI использует собственные ресурсы (например, `number_1.png` … `number_8.png`), а Core лишь возвращает `int NeighborMines`.

Это делает Core полностью независимым от способа отрисовки и позволяет каждому UI-проекту иметь свой визуальный стиль.

---

## 8. Тестирование

- **`MinesweeperCore.Tests`** — xUnit.
- Покрытие фокусное: правила игры (открытие, каскад, победа, поражение, флаги, первый ход), `RecordTable`, `MinePlacement`, `RevealService`.
- **`IRandomProvider`** в тестах подменяется детерминированным генератором — тесты не должны зависеть от случайности.
- **`TimeProvider`** в тестах — `FakeTimeProvider`, чтобы `Elapsed` был предсказуем.
- **Архитектурные тесты** (reflection): проверить, что `MinesweeperCore` не ссылается на UI-сборки.
- UI-проекты тестами **не покрываются** на MVP — только ручная проверка и (опционально) smoke-тесты.

---

## 9. Чего в `MinesweeperCore` быть не должно

Контрольный список для code review и для AI-агента:

- ❌ `using System.Windows.Forms;`
- ❌ `using System.Drawing;`
- ❌ `using Microsoft.Xna.Framework;`
- ❌ `using Avalonia;`
- ❌ `Bitmap`, `Image`, `Texture2D`, `SpriteBatch`, `Graphics`, `Pen`, `Brush`
- ❌ `MessageBox`, `Form`, `Button`, `Control`
- ❌ `Console.WriteLine`, `Debug.WriteLine` (логирование — на стороне вызывающего)
- ❌ `DateTime.Now` напрямую — только через `TimeProvider`
- ❌ `Random` напрямую — только через `IRandomProvider`
- ❌ `File.WriteAllText`, `Registry.*` — только через `IRecordStore`
- ❌ Строки для отображения («Вы победили!», «Новая игра») — это UI
- ❌ Цвета, размеры в пикселях, координаты в пикселях — это UI
- ❌ `INotifyPropertyChanged` — это UI-паттерн
- ❌ `async`/`await` там, где это не нужно (игровой цикл синхронный; таймер — забота UI)

---

## 10. Потокобезопасность

На MVP **Core не является потокобезопасным** и не претендует на это. Предполагается, что все вызовы `GameEngine` происходят из UI-потока.

- WinForms: вызовы из обработчиков `MouseDown` — UI-поток.
- MonoGame: вызовы из `Update` — игровой поток (он же UI-поток для MonoGame).
- Если UI решит вынести что-то в фон (например, загрузку рекордов), синхронизация — ответственность UI.

События поднимаются **синхронно**, в том же потоке, из которого вызван метод `GameEngine`. Никакой внутренней очереди сообщений в Core нет.

---

## 11. Расширяемость

Точки расширения, заложенные в архитектуру:

- **Новые уровни сложности / пресеты** — через `GameSettings` + `DifficultyLevel`. Core не хардкодит 9×9/16×16/30×16 — эти значения передаются из UI или из фабрики пресетов.
- **Новые UI** — просто добавьте проект, зависящий от Core. Никаких изменений в Core не требуется.
- **Новые способы хранения рекордов** — новая реализация `IRecordStore`, без правки Core.
- **Темы оформления / скины** — целиком на стороне UI, Core не участвует.

Если для новой фичи требуется изменить Core так, чтобы он узнал о UI, — это **сигнал, что фича спроектирована неправильно**.