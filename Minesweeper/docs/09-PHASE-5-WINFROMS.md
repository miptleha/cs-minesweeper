# 09 — Phase 5: WinForms UI

Спецификация реализации WinForms-фронтенда поверх готового ядра `MinesweeperCore`.
Стабильное API (94 теста, 100% покрытие правил `02-GAME-RULES.md`) — UI его **только потребляет**.

## 1. Цель Phase 5

Запускаемое WinForms-приложение, в котором можно сыграть полную партию:

- Поле рисуется по `CellInfo` и ассетам из `assets/`.
- Левый клик открывает клетку, правый — циклически меняет метку.
- Смайлик-кнопка показывает состояние: `smiley` / `smiley_oh` / `smiley_win` / `smiley_lose`.
- Счётчик мин и таймер обновляются в реальном времени.
- Меню позволяет начать новую игру на Beginner / Intermediate / Expert / Custom.

**Вне scope Phase 5:** таблица рекордов (сохраним на Phase 7), звуки, анимации, темы оформления.

## 2. Архитектура UI

WinForms-проект — **тонкий слой**. Вся логика — в `GameEngine`. UI:

1. Подписывается на события движка (`CellChanged`, `CellsRevealed`, `GameStateChanged`).
2. По событию вызывает `Invalidate()` — перерисовывает поле.
3. В `OnPaint` запрашивает `engine.GetCell(point)` и рисует по таблице §16.3 `02-GAME-RULES.md`.
4. В обработчиках мыши вызывает `engine.OpenCell(point)` / `engine.CycleMark(point)`.
5. В `Timer.Tick` читает `engine.Elapsed` и обновляет `Label`.

**Запрещено** в UI-проекте:
- Считать `AdjacentMines`, `IsMine`, проверять победу/поражение.
- Менять `CellState` напрямую.
- Хранить копию состояния поля (только `engine.GetAllCells()` при перерисовке).

## 3. Ассеты

### 3.1. Копирование

Все файлы из `assets/*.png` копируются в `src/MinesweeperWinForms/Resources/`.
`assets/icon.ico` — в `src/MinesweeperWinForms/Resources/icon.ico`.

**Не менять имена** — код ссылается по имени.

### 3.2. Включение в сборку

В `MinesweeperWinForms.csproj` добавить:

```xml
<PropertyGroup>
  <ApplicationIcon>Resources\icon.ico</ApplicationIcon>
</PropertyGroup>

<ItemGroup>
  <Content Include="Resources\*.png">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

В рантайме ассеты лежат в `{AppContext.BaseDirectory}/Resources/`.

### 3.3. Загрузка в коде

Один статический кэш `ImageCache` (внутри `Form1.cs` или отдельный класс):

```csharp
private static readonly Dictionary<string, Image> _imageCache = new();

private static Image LoadImage(string name)
{
    if (_imageCache.TryGetValue(name, out var cached))
        return cached;
    var path = Path.Combine(AppContext.BaseDirectory, "Resources", name);
    var img = Image.FromFile(path);
    _imageCache[name] = img;
    return img;
}
```

`Image.FromFile` держит файл залоченным — это нормально для игры, файлы не меняются.

## 4. Инициализация `GameEngine`

В `Form1`:

```csharp
private readonly GameEngine _engine;

public Form1()
{
    InitializeComponent();

    _engine = new GameEngine(
        new SystemRandomProvider(),
        new InMemoryRecordStore());

    _engine.CellChanged += (s, e) => Invalidate();
    _engine.CellsRevealed += (s, e) => Invalidate();
    _engine.GameStateChanged += OnGameStateChanged;
    _engine.BoardGenerated += (s, e) => UpdateMineCounter();

    _engine.StartNewGame(DifficultyPresets.Beginner);
    ApplyLayout();
}
```

**`IRecordStore`:** в Phase 5 используем `InMemoryRecordStore` (заглушка). Файловый сторож — Phase 7.

## 5. Форма: layout

Структура формы:

```
┌─────────────────────────────────────┐
│  [007]      [🙂]        [042]        │  ← TopPanel (высота 64)
├─────────────────────────────────────┤
│                                     │
│         [Поле W × H клеток]         │  ← BoardPanel (занимает всё остальное)
│                                     │
└─────────────────────────────────────┘
```

- **`TopPanel`** — `Panel` высотой 64 px, `Dock = Top`. Внутри три элемента:
  - `_mineCounter` — `PictureBox` слева, 3 LED-цифры (`timer_N.png` + `minus.png`).
  - `_smileyButton` — `Button` 40×40 по центру, `Image = smiley.png`. По клику → `StartNewGame(_engine.Settings)`.
  - `_timerDisplay` — `PictureBox` справа, 3 LED-цифры.
- **`BoardPanel`** — `Panel`, `Dock = Fill`, `DoubleBuffered = true`. Все клики и отрисовка — здесь.

Клетка — **24×24 px** (ассеты это поддерживают). Размер `BoardPanel` = `Width × 24` на `Height × 24`.

Пересчёт размеров окна при `StartNewGame`:

```csharp
private void ApplyLayout()
{
    int boardW = _engine.Width * CellSize;
    int boardH = _engine.Height * CellSize;
    _boardPanel.Size = new Size(boardW, boardH);
    ClientSize = new Size(boardW, boardH + TopPanelHeight);
    _boardPanel.Invalidate();
    UpdateMineCounter();
    UpdateTimer();
}
```

## 6. Отрисовка поля

`_boardPanel.Paint += BoardPanel_Paint;`

```csharp
private const int CellSize = 24;

private void BoardPanel_Paint(object sender, PaintEventArgs e)
{
    for (int x = 0; x < _engine.Width; x++)
        for (int y = 0; y < _engine.Height; y++)
        {
            var info = _engine.GetCell(new CellPoint(x, y));
            var rect = new Rectangle(x * CellSize, y * CellSize, CellSize, CellSize);
            DrawCell(e.Graphics, rect, info);
        }
}

private void DrawCell(Graphics g, Rectangle rect, CellInfo info)
{
    switch (info.State)
    {
        case CellState.Closed:
            g.DrawImage(LoadImage("cell_closed.png"), rect);
            break;

        case CellState.Flagged:
            g.DrawImage(LoadImage("cell_closed.png"), rect);
            g.DrawImage(LoadImage("cell_flag.png"), rect);
            break;

        case CellState.Question:
            g.DrawImage(LoadImage("cell_closed.png"), rect);
            g.DrawImage(LoadImage("cell_question.png"), rect);
            break;

        case CellState.WrongFlag:
            g.DrawImage(LoadImage("cell_wrong_flag.png"), rect);
            break;

        case CellState.Opened:
            g.DrawImage(LoadImage("cell_empty.png"), rect);
            if (info.Content == CellContent.Number && info.AdjacentMines >= 1 && info.AdjacentMines <= 8)
                g.DrawImage(LoadImage($"digit_{info.AdjacentMines}.png"), rect);
            else if (info.Content == CellContent.Mine)
                g.DrawImage(LoadImage("bomb.png"), rect);
            break;
    }
}
```

**Взрыв (триггерная клетка).** Триггерная мина при `Lost` — это `Opened + Mine`. По §16.2 `02-GAME-RULES.md` её нужно рисовать как `cell_exploded.png`, а не `cell_empty + bomb`. Как отличить:

- UI хранит поле `CellPoint? _triggerCell`, заполняемое из `GameStateChangedEventArgs.TriggerCell?.Position` при `Lost`.
- В `DrawCell`, если `info.Position == _triggerCell` → `cell_exploded.png`.

Иначе при `Lost` рисуем всё как обычно (`cell_empty + bomb`).

## 7. Ввод

`_boardPanel.MouseDown += BoardPanel_MouseDown;`

**Правый клик до первого открытия** ставит метку, игра остаётся `NotStarted`.
Это корректно: `CycleMark` разрешён в `NotStarted` (§7 `02-GAME-RULES.md`).

```csharp
private void BoardPanel_MouseDown(object sender, MouseEventArgs e)
{
    int x = e.X / CellSize;
    int y = e.Y / CellSize;
    if (x < 0 || x >= _engine.Width || y < 0 || y >= _engine.Height)
        return;

    var point = new CellPoint(x, y);

    if (e.Button == MouseButtons.Left)
    {
        _smileyButton.Image = LoadImage("smiley_oh.png");
        _engine.OpenCell(point);
    }
    else if (e.Button == MouseButtons.Right)
    {
        _engine.CycleMark(point);
        UpdateMineCounter();
    }
}
```

`_boardPanel.MouseUp += (s, e) =>`:
- Если `GameState == InProgress` / `NotStarted` — вернуть `smiley.png`.
- Если `GameState == Won` / `Lost` — **не** сбрасывать смайлик (он уже `smiley_win.png` / `smiley_lose.png`).

**Важно:** `MouseUp` не должен менять состояние поля. Только визуально возвращает смайлик.

## 8. Таймер

`System.Windows.Forms.Timer` в `Form1`:

```csharp
private readonly System.Windows.Forms.Timer _timer = new() { Interval = 100 };

// В конструкторе:
_timer.Tick += (s, e) => UpdateTimer();
_timer.Start();

private void UpdateTimer()
{
    var seconds = (int)_engine.Elapsed.TotalSeconds;
    if (seconds > 999) seconds = 999;
    _timerDisplay.Image = RenderLedCounter(seconds, 3);
}
```

`RenderLedCounter(int value, int digits)` — склеивает `digits` картинок `timer_N.png` горизонтально в один `Bitmap`:

```csharp
private Image RenderLedCounter(int value, int digits)
{
    var bmp = new Bitmap(digits * 24, 32);
    using var g = Graphics.FromImage(bmp);
    var s = value.ToString().PadLeft(digits, '0');
    for (int i = 0; i < digits; i++)
        g.DrawImage(LoadImage($"timer_{s[i]}.png"), i * 24, 0);
    return bmp;
}
```

Таймер работает всегда — `engine.Elapsed` сам возвращает фиксированное значение при `Won`/`Lost` (§14 `02-GAME-RULES.md`), UI не нужно останавливать `_timer`.

## 9. Счётчик мин

```csharp
private void UpdateMineCounter()
{
    int remaining = _engine.RemainingMines;

    if (remaining >= 0)
    {
        _mineCounter.Image = RenderLedCounter(remaining, 3);
    }
    else
    {
        // Отрицательное значение: minus.png + цифры модуля (§15 02-GAME-RULES.md).
        int abs = Math.Abs(remaining);
        var bmp = new Bitmap(3 * 24, 32);
        using var g = Graphics.FromImage(bmp);
        g.DrawImage(LoadImage("minus.png"), 0, 0);
        var s = abs.ToString().PadLeft(2, '0');
        g.DrawImage(LoadImage($"timer_{s[0]}.png"), 24, 0);
        g.DrawImage(LoadImage($"timer_{s[1]}.png"), 48, 0);
        _mineCounter.Image = bmp;
    }
}
```

`RemainingMines` — O(1) (`MineCount − Board.FlaggedCount`). **Не обнуляется** при `Won`/`Lost`. Может быть отрицательным. Если игрок выиграл без флагов, счётчик показывает `MineCount`.

**`Question` НЕ считается флагом** (см. §15 `02-GAME-RULES.md`). Только `Flagged`.

**Освобождение старых Bitmap:** перед заменой `_mineCounter.Image` / `_timerDisplay.Image` проверять, что старое значение — `Bitmap`, и вызывать `Dispose()`, чтобы не утекать GDI-ресурсы.

## 10. Смайлик-кнопка

- `smiley.png` — по умолчанию, во время `InProgress` и `NotStarted`.
- `smiley_oh.png` — пока зажата левая кнопка мыши на поле (см. §7).
- `smiley_win.png` — после `GameStateChanged(Won)`.
- `smiley_lose.png` — после `GameStateChanged(Lost)`.

```csharp
private void OnGameStateChanged(object? sender, GameStateChangedEventArgs e)
{
    if (e.NewState == GameState.Won)
        _smileyButton.Image = LoadImage("smiley_win.png");
    else if (e.NewState == GameState.Lost)
    {
        _smileyButton.Image = LoadImage("smiley_lose.png");
        // Запомнить триггерную клетку для отрисовки cell_exploded
        _triggerCell = e.TriggerCell?.Position; // CellInfo.Position, не Point
    }

    UpdateMineCounter();
    UpdateTimer();
    _boardPanel.Invalidate();
}
```

По клику на смайлик — `StartNewGame(_engine.Settings)` + сброс `_triggerCell = null`.

## 11. Меню

`MenuStrip` сверху формы:

```
Игра
├── Новая (F2)
├── ─────
├── Новичок
├── Любитель
├── Профессионал
├── ─────
└── Пользовательский...
```

Обработчики:

```csharp
private void StartGame(GameSettings settings)
{
    _triggerCell = null;
    _engine.StartNewGame(settings);
    _smileyButton.Image = LoadImage("smiley.png");
    ApplyLayout();
}

private void OnBeginner(object s, EventArgs e) => StartGame(DifficultyPresets.Beginner);
private void OnIntermediate(object s, EventArgs e) => StartGame(DifficultyPresets.Intermediate);
private void OnExpert(object s, EventArgs e) => StartGame(DifficultyPresets.Expert);
```

**Custom-диалог:** в Phase 5 — минимальный: `Form` с тремя `NumericUpDown`
(Width, Height, MineCount) и кнопкой OK. **Границы в UI — классические:**
`Width 9..30`, `Height 9..24`, `MineCount` по формуле §12.2 `02-GAME-RULES.md`.
Ядро технически допускает `3..1000 × 3..1000`, но UI не должен это разрешать
игроку. Валидация — при создании `GameSettings` (выбросит
`ArgumentOutOfRangeException`, показать `MessageBox`).

## 12. События движка — какие и зачем

| Событие | Что делает UI |
|---|---|
| `CellChanged` | `_boardPanel.Invalidate()`, `UpdateMineCounter()` |
| `CellsRevealed` | `_boardPanel.Invalidate()` |
| `GameStateChanged` | смена смайлика, запоминание `TriggerCell` при `Lost`, финальный `Invalidate()` |
| `BoardGenerated` | `UpdateMineCounter()` (после первого клика) |

**`BoardGenerated`** важен: до первого клика `MineCount` известен, но `Flagged = 0`, и счётчик уже показывает `MineCount`. Не критично, но не помешает.

## 13. Acceptance — визуальные критерии (ручное тестирование)

После реализации агент **не может** проверить это юнит-тестами. Проверяет человек.

- [ ] Запуск: окно с полем 9×9 Beginner, таймер `000`, счётчик `010`.
- [ ] Первый клик: клетка открывается (или каскад), таймер тикает.
- [ ] Правый клик циклически: флаг → вопрос → пусто → флаг.
- [ ] Правый клик на мине при победе: правильный флаг остаётся.
- [ ] Клик в мину: поле показывает `cell_exploded` на триггере, `bomb` на остальных минах, `cell_wrong_flag` на ошибочных флагах, смайлик → `smiley_lose`.
- [ ] Все безопасные открыты: все мины в флагах (`cell_flag`), смайлик → `smiley_win`. Счётчик при этом показывает `MineCount − поставленные_флаги` (не обязательно `0`).
- [ ] Счётчик: после каждого правого клика пересчитывается (`Flagged` уменьшает).
- [ ] Счётчик отрицательный: если поставить 11 флагов на 10 мин, показано `-01` (`minus.png` + цифры).
- [ ] Смена сложности в меню: новое поле, счётчики сброшены, таймер `000`.
- [ ] Кнопка «Новая» / клик по смайлику: `StartNewGame` с текущими `Settings`, всё сброшено.
- [ ] Изменение размера окна: `FormBorderStyle.FixedSingle` — нельзя, поле фиксировано.

## 14. Антипаттерны (не делать)

- **Не хранить `Board` копию в UI.** Единственный источник — `engine.GetCell(point)` в `OnPaint`.
- **Не считать `AdjacentMines` в UI.** Ядро отдаёт `CellInfo.AdjacentMines`.
- **Не менять `CellState`** из обработчиков. Только через `engine.OpenCell` / `engine.CycleMark`.
- **Не реализовывать каскад в UI.** Ядро эмитит `CellsRevealed` с готовым списком.
- **Не рисовать `cell_exploded` для всех мин при `Lost`.** Только для триггерной (`TriggerCell` из события). Остальные — `cell_empty + bomb` (§16.2 `02-GAME-RULES.md`).
- **Не считать `Question` за флаг в счётчике.** Только `Flagged` (§15).
- **Не останавливать таймер вручную при `Won`/`Lost`.** `engine.Elapsed` уже фиксирован (§14)
