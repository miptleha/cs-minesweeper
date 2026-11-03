# 04 — Agent Guide

Практическое руководство для AI-агента (GigaCode, Copilot, Cursor и др.) по работе с репозиторием **`cs-minesweeper`**.

Этот файл дополняет `AGENTS.md` (точка входа) и **`03-ARCHITECTURE.md`** (архитектура). Здесь — только то, что нельзя вывести из кода автоматически: команды, жёсткие ограничения, конвенции и типичные ловушки.

> **Главный принцип для агента:** если правило можно проверить компилятором или тестом — оно должно быть проверено, а не написано в этом файле. Всё, что ниже, либо **неочевидно**, либо **критично** для сохранения архитектуры.

Задачи по тестам — см. `07-TESTING.md`

---

## 1. Быстрые команды

Выполнять из корня решения.

| Задача | Команда |
|--------|---------|
| Собрать всё решение | `dotnet build` |
| Запустить все тесты | `dotnet test` |
| Запустить только тесты Core | `dotnet test tests/MinesweeperCore.Tests` |
| Запустить один тест (xUnit v2) | `dotnet test --filter "FullyQualifiedName~GameEngineTests.OpenCell_FirstClick_IsNeverMine"` |
| Запустить WinForms-версию | `dotnet run --project src/Minesweeper.WinForms` |
| Запустить MonoGame-версию | `dotnet run --project src/Minesweeper.MonoGame` |
| Форматирование | `dotnet format` |
| Проверить архитектурные тесты | `dotnet test --filter "Category=Architecture"` |

**Не использовать** `msbuild`, `nuget restore` напрямую, `dotnet publish` без явной просьбы — стандартный цикл через `dotnet build` / `dotnet test`.

---

## 2. Жёсткие архитектурные ограничения

Соблюдение этих правил **важнее** скорости выполнения задачи. Нарушение = неправильное решение, даже если код компилируется.

### 2.1. `MinesweeperCore` — стерильная зона

В проекте `MinesweeperCore` **запрещено**:

| Запрет | Почему |
|--------|--------|
| `using System.Windows.Forms;` | Core не должен знать о WinForms |
| `using System.Drawing;` | `Bitmap`/`Graphics` — это UI |
| `using Microsoft.Xna.Framework.*;` | Core не должен знать о MonoGame |
| `using Avalonia.*;` / `using System.Windows.*;` | Будущие UI не должны просачиваться в Core |
| `using Microsoft.Win32;` | Реестр — только через `IRecordStore` в UI |
| `Console.WriteLine` / `Debug.WriteLine` | Логирование — ответственность вызывающего кода |
| `DateTime.Now` / `DateTime.UtcNow` напрямую | Только через `TimeProvider` |
| `new Random()` напрямую | Только через `IRandomProvider` |
| `File.*` / `Directory.*` | Только через `IRecordStore` |
| `INotifyPropertyChanged` | Это UI-паттерн, не бизнес-логика |
| `async`/`await` без необходимости | Игровой цикл синхронный; таймер — забота UI |
| Строки вида `"Вы победили!"` | Текст для игрока — это UI |
| Цвета, размеры в пикселях, координаты в пикселях | Визуальные параметры — это UI |
| Пути к PNG-файлам, имена ассетов | Ресурсы — это UI (см. раздел 6) |

**Проверка:** перед завершением задачи агент обязан выполнить `dotnet build` и (если есть) архитектурные тесты. Если агент добавил `using` из списка выше — решение неверное, даже если сборка прошла.

### 2.2. Направление зависимостей

```
UI-проекты  ──►  MinesweeperCore
UI-проекты  ──✕──►  другой UI-проект
MinesweeperCore  ──✕──►  любой UI-проект
```

Любая ссылка (ProjectReference или PackageReference), нарушающая эту схему, — ошибка. Единственное исключение: `MinesweeperCore.Tests` ссылается на `MinesweeperCore`.

### 2.3. UI не мутирует состояние Core напрямую

UI **не должен**:

- Изменять свойства `Cell`, `Board` напрямую.
- Хранить `GameEngine` внутри `Cell`-подобных структур.
- «Дорисовывать» состояние, которого нет в `GameEngine` (например, самостоятельно решать, что клетка открыта).

UI **должен**:

- Вызывать `GameEngine.OpenCell(x, y)` и `GameEngine.ToggleFlag(x, y)`.
- Читать состояние через `GameEngine.GetCell(x, y)` или события.
- Реагировать на `CellChanged` / `CellsRevealed` / `GameStateChanged`.

---

## 3. Конвенции кода (C# / .NET 8)

Эти конвенции уже заложены в `.editorconfig` — агент должен следовать им без напоминаний.

| Правило | Пример |
|---------|--------|
| Файловые namespace (file-scoped) | `namespace MinesweeperCore.Engine;` |
| `System`-директивы первыми, затем остальные | `using System.Linq;` → `using MinesweeperCore.Domain;` |
| `var` — когда тип очевиден из правой части | `var engine = new GameEngine(...);` |
| Имена приватных полей — `_camelCase` | `private readonly IRandomProvider _random;` |
| Имена интерфейсов — `I` + PascalCase | `IRandomProvider`, `IRecordStore` |
| `record` для неизменяемых DTO и доменных снимков | `public sealed record CellInfo(int X, int Y, CellState State, int NeighborMines);` |
| `readonly struct` для маленьких value-объектов | `public readonly record struct Point(int X, int Y);` |
| Nullable reference types включены | `string?` вместо `string` там, где возможно `null` |
| Expression-bodied члены — для однострочных | `public int MinesRemaining => Settings.MineCount - FlagsPlaced;` |
| Никаких `#region` | — |
| Никаких «магических чисел» | `const int BeginnerSize = 9;` в `DifficultyPresets` |

**Особенно важно для этого проекта:**

- **Иммутабельность по умолчанию.** Модели в `Domain` не должны иметь публичных сеттеров. Изменение состояния — только через методы `GameEngine`.
- **`sealed` для классов без явного наследования.** `GameEngine`, `Board`, `Cell` — `sealed` или `readonly record struct`, если возможно.
- **События — `EventHandler<T>` с `EventArgs`-наследниками.** Не изобретать `delegate void CellChangedDelegate(...)`.

---

## 4. События: правила подписки и генерации

### 4.1. В Core

- События поднимаются **синхронно**, в том же потоке, что и вызов метода.
- При каскадном открытии **не генерировать `CellChanged` на каждую клетку** — использовать одно событие `CellsRevealed` со списком изменений.
- `CellChanged` — только для одиночных изменений: один клик открыл одну клетку, один клик поставил/снял флаг.
- Не поднимать события в конструкторе (`BoardGenerated` — исключение, но его лучше поднимать после полной инициализации).

### 4.2. В UI

- Подписываться на события `GameEngine` при создании формы/контрола.
- **Отписываться** при закрытии/уничтожении, чтобы не держать ссылки (утечки в WinForms — реальная проблема).
- Если UI-проект на MVVM (Avalonia/WPF), ViewModel подписывается на события `GameEngine` и транслирует их в `PropertyChanged`. **Сам `GameEngine` не реализует `INotifyPropertyChanged`.**

---

## 5. Инструкция: добавление нового UI-проекта

Если задача — «добавь UI на [фреймворк]», агент выполняет шаги:

1. **Создать проект** в `src/Minesweeper.<Framework>`.
2. **TFM**:
   - Кроссплатформенный фреймворк (Avalonia, MonoGame) → `net8.0`.
   - Windows-only (WPF) → `net8.0-windows`.
3. **Добавить `ProjectReference`** на `MinesweeperCore`.
4. **Не добавлять** ссылки на другие UI-проекты.
5. **Скопировать нужные PNG** из корневой `assets/` в проект согласно конвенциям фреймворка (см. раздел 6).
6. **Реализовать цикл «ввод → GameEngine → события → отрисовка»**:
   - Создать `GameEngine` с настройками по умолчанию (или `DifficultyPresets.Beginner`).
   - Подписаться на `CellChanged`, `CellsRevealed`, `GameStateChanged`.
   - На клик мыши вызывать `OpenCell` / `ToggleFlag`.
   - На события — перерисовывать затронутые клетки.
7. **Реализовать `IRecordStore`** в этом же UI-проекте (JSON-файл в `%APPDATA%` для Windows или рядом с exe для портативных).
8. **Добавить проект в решение** (`dotnet sln add`).
9. **Проверить:** `dotnet build` проходит, Core не изменился (кроме случаев, когда задача явно требует правки Core).

**Не делать** при добавлении UI: не менять `MinesweeperCore`, не добавлять в Core абстракции «на будущее» под конкретный фреймворк, не копировать код из существующего UI-проекта «как есть» без адаптации к API нового фреймворка.

---

## 6. Shared Assets: работа с общими картинками

В корне решения есть папка **`assets/`** — склад **исходных изображений** (PNG) для всех UI-проектов. См. `assets/README.md`.

**Ключевой принцип:** `assets/` — это **склад исходников, а не папка рантайма**. Файлы в ней **не участвуют в сборке** и никуда автоматически не копируются.

При создании или доработке UI-проекта агент **копирует** нужные PNG из `assets/` в проект и размещает их согласно конвенциям конкретного фреймворка. Из кода **никогда** не ссылаться на корневую `assets/` — только на файлы, лежащие внутри проекта.

### 6.1. Таблица размещения по фреймворкам

| Фреймворк | Куда копировать PNG | Настройка csproj / Build Action | Как загружать в коде |
|-----------|---------------------|--------------------------------|----------------------|
| **WinForms** | `Assets/` внутри проекта | `<Content Include="Assets\**" CopyToOutputDirectory="PreserveNewest" />` | `Path.Combine(AppContext.BaseDirectory, "Assets", "mine.png")` → `Image.FromFile(...)` |
| **WinForms (альтернатива)** | `Properties/Resources.resx` | через конструктор ресурсов VS | `Properties.Resources.mine` |
| **MonoGame** | `Content/` внутри проекта | через `Content.mgcb` (MGCB Editor) | `Content.Load<Texture2D>("mine")` |
| **Avalonia** | `Assets/` внутри проекта | `<AvaloniaResource Include="Assets\**" />` | `avares://MyApp/Assets/mine.png` → `new Bitmap(...)` |
| **WPF** | `Assets/` внутри проекта | `<Resource Include="Assets\**" />` | `new BitmapImage(new Uri("pack://application:,,,/Assets/mine.png"))` |

### 6.2. Пошагово для агента

При задаче «создай UI-проект на <фреймворк>»:

1. **Не трогать** `MinesweeperCore`.
2. Создать проект `src/Minesweeper.<Framework>`.
3. **Скопировать** нужные PNG из корневой `assets/` в подпапку проекта:
   - WinForms → `src/Minesweeper.WinForms/Assets/`
   - MonoGame → `src/Minesweeper.MonoGame/Content/`
   - Avalonia → `src/Minesweeper.Avalonia/Assets/`
   - WPF → `src/Minesweeper.WPF/Assets/`
4. **Настроить csproj** согласно таблице выше (Build Action).
5. **Загружать** картинки по пути, соответствующему фреймворку (см. последнюю колонку таблицы).
6. **Не** добавлять `ProjectReference` на `assets/` и **не** настраивать копирование `assets/` в `Directory.Build.props`.

### 6.3. Обновление картинок

Если в `assets/` обновился PNG (например, `mine.png`), его нужно **вручную скопировать** в каждый UI-проект, который его использует. Автоматической синхронизации нет — это осознанное решение, потому что разные фреймворки требуют разной обработки ресурсов (см. 6.4).

### 6.4. Почему не «копировать всё в output через Directory.Build.props»

- **MonoGame** требует Content Pipeline (`Content.mgcb`) — PNG должен быть внутри `Content/` проекта, иначе он не обработается.
- **Avalonia** использует схему URI `avares://`, которая работает только с ресурсами, встроенными в сборку (`AvaloniaResource`).
- **WPF** использует `pack://` URI, аналогично.
- **WinForms** может работать через `Image.FromFile`, но для одиночного exe удобнее `Properties.Resources`.

Единая стратегия «положить в output через `Directory.Build.props`» **не работает** для MonoGame, Avalonia и WPF. Поэтому каждый UI-проект забирает картинки к себе и распоряжается ими по-своему.

### 6.5. Стандартный набор файлов в `assets/`

Ожидаемый набор (уточняется по мере разработки):

| Файл | Назначение |
|------|------------|
| `cell_closed.png` | Закрытая клетка |
| `cell_flagged.png` | Клетка с флагом |
| `mine.png` | Мина (открытая) |
| `mine_wrong.png` | Перечёркнутый флаг (ошибка при поражении) |
| `number_1.png` … `number_8.png` | Цифры 1–8 |
| `smile_normal.png` | Смайлик «игра идёт» |
| `smile_win.png` | Смайлик «победа» |
| `smile_lose.png` | Смайлик «поражение» |

Если какого-то файла не хватает — **не рисовать** его кодом в Core, а либо использовать заглушку в UI, либо сообщить пользователю.

---

## 7. Инструкция: изменение правил игры

Если задача меняет **поведение** (а не только UI):

1. **Источник истины — `02-GAME-RULES.md`.** Если правило меняется, сначала обновить этот документ (или убедиться, что задача уже его обновила).
2. **Изменения в `Domain`** (новые поля, enum-значения) — минимальны и иммутабельны.
3. **Изменения в `Engine`** — покрыть тестом **до** реализации (или одновременно). Тест должен воспроизводить именно то правило, которое меняется.
4. **События** — если меняется контракт (`EventArgs`), проверить всех подписчиков во всех UI-проектах. Это **breaking change** для UI.
5. **Не менять правила «попутно»**, если задача про другое. Агент не должен «улучшать» логику, которую не просили трогать.

---

## 8. Тестирование

### 8.1. Что и как тестировать

- **Только `MinesweeperCore`.** UI-проекты на MVP не покрываются автотестами.
- Фреймворк — **xUnit** (v2, если доступен).
- Паттерн — **AAA** (Arrange / Act / Assert).
- Имена тестов: `MethodName_Scenario_ExpectedBehavior`, например:
  `OpenCell_FirstClickOnMine_RelocatesMineToTopLeft`.
- **`IRandomProvider`** в тестах — детерминированная реализация (не `Random`). Тест не должен зависеть от случайности.
- **`TimeProvider`** в тестах — `FakeTimeProvider` (пакет `Microsoft.Extensions.TimeProvider.Testing`). Не использовать `Thread.Sleep` для проверки `Elapsed`.

### 8.2. Обязательные тестовые сценарии (минимум)

| Сценарий | Что проверяет |
|----------|---------------|
| Первый клик никогда не мина | Правило раздела 5 `02-GAME-RULES.md` |
| Каскадное открытие | Клетки с `N=0` открывают соседей, флаги не трогаются |
| Победа при открытии всех безопасных | `GameState.Won` |
| Поражение при открытии мины | `GameState.Lost`, все мины открыты |
| Установка/снятие флага | `ToggleFlag` меняет состояние |
| Флаг блокирует прямой клик | `OpenCell` по флагу ничего не делает |
| Каскад не трогает флаги | Помеченные клетки остаются закрытыми |
| `RecordTable` хранит топ-5 | Сортировка и отсечение |
| Архитектурный тест: Core не ссылается на UI | Reflection / NetArchTest |

### 8.3. Архитектурные тесты

Добавлять в `MinesweeperCore.Tests` (или отдельный `ArchitectureTests`):

```csharp
[Fact]
public void Core_ShouldNotReference_UI_Assemblies()
{
    var uiAssemblies = new[] { "System.Windows.Forms", "Microsoft.Xna.Framework", "Avalonia" };
    var referenced = typeof(GameEngine).Assembly.GetReferencedAssemblies().Select(a => a.Name);
    Assert.DoesNotContain(referenced, name => uiAssemblies.Any(ui => name?.StartsWith(ui) == true));
}
```

Или через **NetArchTest** — предпочтительнее для читаемости и расширяемости.

---

## 9. Типичные ошибки агента и как их избежать

| Ошибка | Как избежать |
|--------|--------------|
| Добавил `using System.Drawing;` в Core «для удобства» | Перед коммитом проверить `git diff` по `MinesweeperCore/` на запрещённые `using` |
| Сгенерировал 50 событий `CellChanged` при каскаде | Использовать `CellsRevealed` со списком; `CellChanged` — только для одиночных изменений |
| Использовал `DateTime.Now` в `Elapsed` | Только `TimeProvider.GetUtcNow()` или `TimeProvider.GetTimestamp()` |
| Создал `new Random()` в `MinePlacement` | Внедрить `IRandomProvider` через конструктор |
| Изменил `Board` напрямую из UI-проекта | Только через `GameEngine`; если не хватает API — добавить метод в `GameEngine`, а не «достучаться» до `Board` |
| «Улучшил» правила игры, которые не просили | Сверить diff с `02-GAME-RULES.md`; если правило не менялось — откатить |
| Добавил `INotifyPropertyChanged` в Core | Это UI-паттерн; в Core — только события |
| Написал строку `"Game Over"` в Core | Строки — в UI; Core возвращает `GameState.Lost` |
| Публичный сеттер в `Cell` | `Cell` иммутабелен; изменение — через `GameEngine` |
| Не отписался от события в WinForms | `FormClosed` / `Dispose` → отписка; утечки памяти |
| Скопировал PNG в Core «на всякий случай» | Ресурсы — только в UI (см. раздел 6) |
| Настроил копирование `assets/` в `Directory.Build.props` | `assets/` — склад исходников; каждый UI-проект копирует нужное к себе (см. 6.4) |
| Ссылается из кода UI на `../../assets/mine.png` | Недопустимо; PNG должен быть внутри UI-проекта |
| Нарисовал спрайт кодом в Core, потому что «PNG не было» | Сообщить пользователю; не тащить ресурсы в Core |

---

## 10. Чек-лист перед завершением задачи

Агент обязан мысленно (или явно в ответе) пройти чек-лист:

- [ ] `dotnet build` проходит без warnings (или warnings объяснены).
- [ ] `dotnet test` проходит (если тесты есть).
- [ ] В `MinesweeperCore` нет запрещённых `using` / API из раздела 2.1.
- [ ] Направление зависимостей не нарушено (Core не ссылается на UI).
- [ ] Если менялись правила — обновлён `02-GAME-RULES.md` и добавлены/изменены тесты.
- [ ] Если добавлялся UI — он ссылается только на `MinesweeperCore`, не на другие UI.
- [ ] Если добавлялся UI — PNG-файлы скопированы из `assets/` в проект и размещены по конвенции фреймворка (раздел 6).
- [ ] Если менялись события — проверены все подписчики во всех UI-проектах.
- [ ] Нет `Console.WriteLine`, `DateTime.Now`, `new Random()`, `File.*` в Core.
- [ ] Новые публичные типы в Core имеют XML-документацию (`///`), если они часть API для UI.
- [ ] Diff не содержит «попутных» изменений, не связанных с задачей.
- [ ] Запись добавлена в `[Unreleased]` в `06-CHANGELOG.md`, если изменилось поведение или публичный API.

---

## 11. Ссылки

- **`AGENTS.md`** — точка входа, краткий бриф.
- **`01-OVERVIEW.md`** — обзор проекта.
- **`02-GAME-RULES.md`** — правила игры (источник истины для поведения).
- **`03-ARCHITECTURE.md`** — архитектура, слои, API `GameEngine`.
- **`05-ROADMAP.md`** — план этапов (что делать дальше).
- **`06-CHANGELOG.md`** — история версий.
- **`07-TESTING.md`** — инструкция по написанию тестовых сценариев.
- **`assets/README.md`** — что такое склад ассетов и как с ним работать.

Если задача противоречит этим документам — **сначала уточнить у человека**, а не менять архитектуру «по своему усмотрению».
