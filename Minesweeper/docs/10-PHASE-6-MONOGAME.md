# 10 — Phase 6: MonoGame UI

Спецификация реализации MonoGame-фронтенда поверх ядра `MinesweeperCore`.
Ядро (`GameEngine`, `CellInfo`, события, порты) — **не меняется**. UI только
потребляет его API и отрисовывает.

## 1. Цель Phase 6

Запускаемое MonoGame-приложение, в котором можно сыграть полную партию:
- Поле рисуется по `CellInfo` и текстурам из Content Pipeline.
- ЛКМ / ПКМ / Ctrl+ЛКМ / двойной ЛКМ / ЛКМ+ПКМ / средняя кнопка — все пути
  открытия/метки/аккорда работают по правилам `02-GAME-RULES.md`.
- Смайлик-кнопка показывает состояние: `smiley` / `smiley_oh` / `smiley_win` / `smiley_lose`.
- Счётчик мин и таймер обновляются в реальном времени, отрицательный счётчик
  рисуется через `minus.png`.
- Меню `Игра` / `Справка`, экраны `Records` / `About` / `NamePrompt`.
- Сохранение рекордов — через `JsonRecordStore` из `MinesweeperCore.Infrastructure`
  (тот же `%LocalAppData%\Minesweeper\records.json`, что и в WinForms-версии).

**Вне scope Phase 6:** звуки, анимации, темы, мобильные платформы, геймпад.

**Ограничение по DPI:** в Phase 6 **нет** масштабирования. Размеры фиксированы
в пикселях (`CellSize = 24`). На 150% DPI окно будет визуально меньше — это
известное ограничение, скейлинг добавим отдельной задачей, если понадобится.

## 2. Архитектура UI

Одно окно `MinesweeperGame` (переименовать `Game1` → `MinesweeperGame`,
файл `Game1.cs` → `MinesweeperGame.cs`, в `Program.cs` — `new MinesweeperGame()`).
Внутри — машина экранов + рисованные меню и диалоги.

| `Screen` | Что рисуется |
|---|---|
| `Main` | Поле, счётчики, смайлик, шапка, меню |
| `Records` | Таблица топ-5 по трём уровням (3 колонки) |
| `About` | Название, версия, автор, ссылка (текст) |
| `NamePrompt` | Поле ввода имени + `OK` |

Поля `MinesweeperGame`:

```csharp
private GameEngine _engine;
private IRecordStore _recordStore;   // new JsonRecordStore()
private readonly Dictionary<string, Texture2D> _tex = new();
private SpriteFont _font;

private Screen _screen = Screen.Main;
private MenuKind _openMenu = MenuKind.None;
private int _openMenuX;
private int _menuHover = -1;

// Ввод
private MouseState _prevMouse;
private KeyboardState _prevKeyboard;
private bool _suppressNextMouseUp;
private CellPoint? _chordCell;
private CellPoint? _lastClickPoint;
private TimeSpan _lastClickTime;
private bool _wasInsideBoard;
private string _smiley = "smiley";
private CellPoint? _triggerCell;

// NamePrompt
private string _nameBuffer = "Игрок";
```

Константы:

```csharp
private const int CellSize        = 24;
private const int TopPanelHeight  = 48;
private const int MenuHeight      = 24;
private const int SmileySize      = 32;
private const int LedW            = 24;
private const int LedH            = 32;
private const int LedGap          = 10;
private const int PanelTopMargin  = 8;
private const int DoubleClickMs   = 500;   // как SystemInformation.DoubleClickTime по умолчанию
```

**Ключевое:** события ядра эмитятся **внутри `Update()`**. Их обработчики
**не должны** вызывать `SpriteBatch.Draw` — только менять поля UI. Отрисовка — в `Draw()`.

## 3. Ассеты (Content Pipeline)

### 3.1. Копирование PNG

Из `assets/` в `src/MinesweeperMonoGame/Content/` (имена не менять):

```
bomb.png
cell_closed.png, cell_empty.png, cell_flag.png
cell_question.png, cell_wrong_flag.png, cell_exploded.png
digit_1.png .. digit_8.png
smiley.png, smiley_oh.png, smiley_win.png, smiley_lose.png
minus.png
timer_0.png .. timer_9.png
```

### 3.2. Регистрация в `Content.mgcb`

`Content.mgcb` — обычный текстовый файл. MGCB build tool читает его при
`dotnet build` и для каждого `#begin ... /build:...` генерирует `.xnb` в
`Content/bin/<Platform>/`. В коде `Content.Load<T>("имя")` ищет `имя.xnb` без
расширения.

Для каждой PNG нужен блок. Пример для `cell_closed.png`:

```
#begin cell_closed.png
/importer:TextureImporter
/processor:TextureProcessor
/processorParam:ColorKeyColor=255,0,255,255
/processorParam:ColorKeyEnabled=False
/processorParam:GenerateMipmaps=False
/processorParam:PremultiplyAlpha=True
/processorParam:ResizeToPowerOfTwo=False
/processorParam:MakeSquare=False
/processorParam:TextureFormat=Color
/build:cell_closed.png
```

Остальные 29 PNG — **точно такой же блок**, меняется только имя файла в
`#begin` и `/build:`. Порядок блоков в файле не важен. `Content.mgcb` **коммитится**
(это исходник), а `Content/bin/` и `Content/obj/` — в `.gitignore` (уже есть).

### 3.3. Загрузка в коде

```csharp
protected override void LoadContent()
{
    _spriteBatch = new SpriteBatch(GraphicsDevice);

    _tex["bomb"]            = Content.Load<Texture2D>("bomb");
    _tex["cell_closed"]     = Content.Load<Texture2D>("cell_closed");
    _tex["cell_empty"]      = Content.Load<Texture2D>("cell_empty");
    _tex["cell_flag"]       = Content.Load<Texture2D>("cell_flag");
    _tex["cell_question"]   = Content.Load<Texture2D>("cell_question");
    _tex["cell_wrong_flag"] = Content.Load<Texture2D>("cell_wrong_flag");
    _tex["cell_exploded"]   = Content.Load<Texture2D>("cell_exploded");
    _tex["minus"]           = Content.Load<Texture2D>("minus");
    for (int i = 1; i <= 8; i++) _tex[$"digit_{i}"] = Content.Load<Texture2D>($"digit_{i}");
    for (int i = 0; i <= 9; i++) _tex[$"timer_{i}"] = Content.Load<Texture2D>($"timer_{i}");
    _tex["smiley"]          = Content.Load<Texture2D>("smiley");
    _tex["smiley_oh"]       = Content.Load<Texture2D>("smiley_oh");
    _tex["smiley_win"]      = Content.Load<Texture2D>("smiley_win");
    _tex["smiley_lose"]     = Content.Load<Texture2D>("smiley_lose");

    _font = Content.Load<SpriteFont>("DefaultFont");
}
```

### 3.4. Иконки (`Icon.ico`, `Icon.bmp`) и `bomb.png` — это разное

В `src/MinesweeperMonoGame/` уже есть `Icon.ico` и `Icon.bmp`, и `.csproj`
ссылается на них:

```xml
<ApplicationIcon>Icon.ico</ApplicationIcon>
...
<EmbeddedResource Include="Icon.ico"><LogicalName>Icon.ico</LogicalName></EmbeddedResource>
<EmbeddedResource Include="Icon.bmp"><LogicalName>Icon.bmp</LogicalName></EmbeddedResource>
```

| Файл | Назначение | Формат |
|---|---|---|
| `Icon.ico` | Иконка `.exe` (проводник, диспетчер задач) | **ICO** (Win32-ресурс) |
| `Icon.bmp` | Иконка окна (заголовок, панель задач во время работы) | **BMP** (SDL2 `SDL_SetWindowIcon`) |
| `bomb.png` | Спрайт мины на клетке | PNG (Content Pipeline) |

Визуально `Icon.ico` = `bomb.png` (та же картинка мины), но **форматы разные**.
Заменить PNG на ICO в `<ApplicationIcon>` нельзя — MSBuild требует именно
`.ico`. Для SDL-иконки нужен BMP, а не PNG.

**Что делать:**
1. Проверить, что `src/MinesweeperMonoGame/Icon.ico` совпадает с `assets/icon.ico`.
   Если нет — скопировать `assets/icon.ico` → `src/MinesweeperMonoGame/Icon.ico`.
2. `src/MinesweeperMonoGame/Icon.bmp` — это та же картинка в BMP (32×32).
   Если её нет или она устарела — сгенерировать из `icon.ico`
   (открыть в Paint / ImageMagick, сохранить как BMP). Файл нужен, иначе
   MonoGame DesktopGL не покажет иконку в заголовке окна.

### 3.5. `SpriteFont` — для текста в диалогах и меню

В MonoGame нет текстового рендеринга «из коробки». Чтобы нарисовать строку,
нужен `SpriteFont` — XML-описание шрифта, которое MGCB компилирует в `.xnb`.
Без него текст в `Records` / `About` / `NamePrompt` / меню не отрисовать.

Создать `src/MinesweeperMonoGame/Content/DefaultFont.spritefont`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<XnaContent xmlns:Graphics="Microsoft.Xna.Framework.Content.Pipeline.Graphics">
  <Asset Type="Graphics:FontDescription">
    <FontName>Segoe UI</FontName>
    <Size>14</Size>
    <Spacing>0</Spacing>
    <UseKerning>true</UseKerning>
    <Style>Regular</Style>
    <CharacterRegions>
      <!-- ASCII -->
      <CharacterRegion><Start>&#32;</Start><End>&#126;</End></CharacterRegion>
      <!-- Кириллица А-я, Ё, ё -->
      <CharacterRegion><Start>&#1040;</Start><End>&#1103;</End></CharacterRegion>
      <CharacterRegion><Start>&#1025;</Start><End>&#1025;</End></CharacterRegion>
      <CharacterRegion><Start>&#1105;</Start><End>&#1105;</End></CharacterRegion>
    </CharacterRegions>
  </Asset>
</XnaContent>
```

И зарегистрировать в `Content.mgcb`:

```
#begin DefaultFont.spritefont
/importer:FontDescriptionImporter
/processor:FontDescriptionProcessor
/processorParam:PremultiplyAlpha=True
/processorParam:TextureFormat=Compressed
/build:DefaultFont.spritefont
```

В коде — `Content.Load<SpriteFont>("DefaultFont")`. Рисовать —
`_spriteBatch.DrawString(_font, text, position, Color.Black)`.

**Важно:** `Segoe UI` есть на Windows, но может отсутствовать на Linux.
Если проект должен собираться на Linux — заменить на `Arial` (есть везде) или
положить `.ttf` в `Content/` и указать путь в `<FontName>`. Для MVP на Windows
`Segoe UI` подходит.

## 4. Окно и компоновка

### 4.1. Размер окна

```csharp
protected override void Initialize()
{
    ApplyWindowSize();
    base.Initialize();
}

private void ApplyWindowSize()
{
    _graphics.PreferredBackBufferWidth  = _engine.Width  * CellSize;
    _graphics.PreferredBackBufferHeight = _engine.Height * CellSize
                                        + TopPanelHeight
                                        + MenuHeight;
    _graphics.ApplyChanges();
}
```

`Window.AllowUserResizing = false`, `Window.Title = "Сапёр"`.

При смене сложности (`StartNewGame`) — снова `ApplyWindowSize()`, чтобы окно
подстроилось под новое поле.

### 4.2. Раскладка (все координаты в пикселях, без масштабирования)

```
┌──────────────────────────────────────────┐ ← MenuHeight = 24, SystemColors.Control
│ Игра  Справка                            │
├──────────────────────────────────────────┤ ← TopPanelHeight = 48, SystemColors.Control
│ ┌────────┐        ┌──┐        ┌────────┐ │
│ │ мины   │        │ 🙂 │        │ таймер │ │  ← LED 72×32, smiley 32×32, отступы 8/10
│ │  72×32 │        │32×32│       │  72×32 │ │
│ └────────┘        └──┘        └────────┘ │
├──────────────────────────────────────────┤
│                                          │ ← игровое поле, клетки 24×24
│                                          │
└──────────────────────────────────────────┘
```

Точные координаты:

- Счётчик мин: `X = 10`, `Y = MenuHeight + 8`, `W = 72`, `H = 32`, фон `Black`.
- Смайлик: `X = (WindowWidth - 32) / 2`, `Y = MenuHeight + 8`, `W = 32`, `H = 32`,
  картинка скейлится до `28×28` внутри (как в шаблоне MonoGame — иначе
  обрезается рамкой). Фон — `SystemColors.Control` (RGB 240,240,240),
  рамка 1 px `SystemColors.ControlDark`.
- Счётчик времени: `X = WindowWidth - 10 - 72`, `Y = MenuHeight + 8`, `W = 72`, `H = 32`,
  фон `Black`.
- Поле: `X = 0`, `Y = MenuHeight + TopPanelHeight`, `W = Width*CellSize`,
  `H = Height*CellSize`.

Цвета: `SystemColors.Control` = `new Color(240, 240, 240)`,
`SystemColors.ControlDark` = `new Color(160, 160, 160)`,
`SystemColors.MenuHighlight` = `new Color(0, 120, 215)`.

## 5. Отрисовка поля (по §16.3 `02-GAME-RULES.md`)

Один `SpriteBatch.Begin(samplerState: SamplerState.PointClamp)` на кадр.
`PointClamp` — обязателен, иначе пиксель-арт размывается.

Порядок:

```
1. GraphicsDevice.Clear(SystemColors.Control)
2. DrawMenuBar()
3. DrawTopPanel()       // LED мины + смайлик + LED таймер
4. DrawBoard()
5. DrawActiveScreen()   // Records / About / NamePrompt / dropdown
```

`DrawCell` — **строго по таблице §16.3**:

```csharp
private void DrawCell(CellInfo info)
{
    var rect = new Rectangle(
        info.Position.X * CellSize,
        MenuHeight + TopPanelHeight + info.Position.Y * CellSize,
        CellSize, CellSize);

    switch (info.State)
    {
        case CellState.Closed:
            Draw("cell_closed", rect);
            break;
        case CellState.Flagged:
            Draw("cell_closed", rect);
            Draw("cell_flag", rect);
            break;
        case CellState.Question:
            Draw("cell_closed", rect);
            Draw("cell_question", rect);
            break;
        case CellState.WrongFlag:
            Draw("cell_wrong_flag", rect);
            break;
        case CellState.Opened:
            if (info.Position == _triggerCell)
            {
                Draw("cell_exploded", rect);
            }
            else
            {
                Draw("cell_empty", rect);
                if (info.Content == CellContent.Number && info.AdjacentMines is >= 1 and <= 8)
                    Draw($"digit_{info.AdjacentMines}", rect);
                else if (info.Content == CellContent.Mine)
                    Draw("bomb", rect);
            }
            break;
    }
}
```

**При `Won`** (§16.1): ядро **не меняет** клетки. UI сам перерисовывает поле —
для каждой клетки, которая осталась `Closed` / `Flagged` / `Question`,
рисует `cell_flag.png` поверх. Это гарантированно мины (условие победы:
все безопасные уже `Opened`).

**При `Lost`** (§16.2): триггерная клетка — `cell_exploded.png`; остальные
мины — `cell_empty.png` + `bomb.png`; `WrongFlag` — `cell_wrong_flag.png`.
Правильно помеченные мины остаются `Flagged` (награда за догадку), их
**не** открываем и **не** перерисовываем.

`_triggerCell` обновляется из `GameStateChanged(Lost).TriggerCell?.Position`.
При `Won` — `null` (§16.1: `TriggerCell` при победе не передаётся).

## 6. Ввод (polling + edge-детекция)

В MonoGame нет событий мыши — только `Mouse.GetState()`. «Нажатие» и
«отпускание» определяем сравнением с `_prevMouse`.

### 6.1. Раскладка ввода

| Действие | Условие | Результат |
|---|---|---|
| ЛКМ по клетке | edge `Released ← Pressed` | `engine.OpenCell(point)` |
| ПКМ по клетке | edge `Released ← Pressed` | `engine.CycleMark(point)` |
| Ctrl+ЛКМ | ЛКМ-up + `Keyboard.IsKeyDown(LeftControl \| RightControl)` | `engine.CycleMark(point)` |
| Двойной ЛКМ | ЛКМ-up по той же клетке, `Δ ≤ 500 мс` | 2-й клик → `engine.Chord(point)` |
| ЛКМ+ПКМ | обе зажаты → отпускание любой | `engine.Chord(point)` |
| Средняя кнопка | edge `Pressed` (**down**, не up) | `engine.Chord(point)` |
| ЛКМ по смайлику | ЛКМ-up в `SmileyRect` | `StartNewGame(_engine.Settings)` |
| `Esc` | edge `Keyboard` | Закрывает выпадающее меню; возвращает с экрана (`NamePrompt` → `Records`, остальные → `Main`). Игру НЕ закрывает. |

**Почему средняя кнопка — на `Pressed`:** это «мгновенный» аккорд, без
ожидания отпускания. В WinForms-версии то же поведение.

**Про двойной клик:** в MonoGame DesktopGL **нет** встроенного события
`DoubleClick` (ни в `Mouse`, ни в `GameWindow`). Делаем вручную — та же
edge-детекция + константа `DoubleClickMs = 500`. Это не P/Invoke, всё в
управляемом коде.

### 6.2. `UpdateInput()`

```csharp
private void UpdateInput(GameTime gameTime)
{
    var m = Mouse.GetState();
    var pos = m.Position;

    // Средняя кнопка — аккорд сразу, на нажатии.
    if (m.MiddleButton == ButtonState.Pressed && _prevMouse.MiddleButton == ButtonState.Released)
    {
        var p = ToCellPoint(pos);
        if (p.HasValue) { _engine.Chord(p.Value); UpdateMineCounter(); }
        // Смайлик не трогаем — при аккорде он не должен мигать.
    }

    // ЛКМ+ПКМ: запоминаем клетку-кандидат на нажатии второй кнопки.
    bool bothPressed = m.LeftButton == ButtonState.Pressed && m.RightButton == ButtonState.Pressed;
    bool wasBoth = _prevMouse.LeftButton == ButtonState.Pressed && _prevMouse.RightButton == ButtonState.Pressed;
    if (bothPressed && !wasBoth)
        _chordCell = ToCellPoint(pos);

    // Смайлик "о" на нажатии ЛКМ (не в области смайлика, нет _chordCell).
    bool leftJustDown = m.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
    if (leftJustDown && _chordCell is null && !SmileyRect.Contains(pos)
        && (_engine.State == GameState.InProgress || _engine.State == GameState.NotStarted))
    {
        SetSmiley("smiley_oh");
    }

    // Аналог MouseEnter / MouseLeave: зажатая ЛКМ при входе на поле → "о";
    // уход с поля → обычный смайлик (но не при Won/Lost).
    bool insideBoard = BoardRect.Contains(pos);
    if (insideBoard && !_wasInsideBoard)
    {
        if (m.LeftButton == ButtonState.Released && m.RightButton == ButtonState.Released)
            _chordCell = null;
        if (m.LeftButton == ButtonState.Pressed
            && (_engine.State == GameState.InProgress || _engine.State == GameState.NotStarted))
            SetSmiley("smiley_oh");
    }
    if (!insideBoard && _wasInsideBoard)
    {
        if (_engine.State == GameState.InProgress || _engine.State == GameState.NotStarted)
            SetSmiley("smiley");
    }
    _wasInsideBoard = insideBoard;

    // ЛКМ up.
    if (m.LeftButton == ButtonState.Released && _prevMouse.LeftButton == ButtonState.Pressed)
        OnLeftUp(pos, gameTime);

    // ПКМ up.
    if (m.RightButton == ButtonState.Released && _prevMouse.RightButton == ButtonState.Pressed)
        OnRightUp(pos);

    // Esc.
    if (EscJustPressed())
    {
        if (_openMenu != MenuKind.None) _openMenu = MenuKind.None;
        else if (_screen != Screen.Main) _screen = Screen.Main;
        else Exit();
    }

    _prevMouse = m;
}
```

### 6.3. `OnLeftUp`

```csharp
private void OnLeftUp(Point pos, GameTime gameTime)
{
    // Погасить второй MouseUp после аккорда.
    if (_suppressNextMouseUp)
    {
        _suppressNextMouseUp = false;
        ResetSmileyForCurrentState();
        return;
    }

    // Клик по смайлику — новая игра.
    if (SmileyRect.Contains(pos))
    {
        StartGame(_engine.Settings);
        return;
    }

    // Аккорд по ЛКМ+ПКМ — на отпускании первой из двух.
    if (_chordCell is CellPoint chord)
    {
        _chordCell = null;
        _suppressNextMouseUp = true;
        _engine.Chord(chord);
        UpdateMineCounter();
        ResetSmileyForCurrentState();
        return;
    }

    ResetSmileyForCurrentState();

    var point = ToCellPoint(pos);
    if (!point.HasValue) return;

    if (IsControlDown())
    {
        // Ctrl+ЛКМ — аналог ПКМ (CycleMark).
        _engine.CycleMark(point.Value);
    }
    else
    {
        // Двойной клик: та же клетка + интервал ≤ 500 мс.
        bool isDouble = _lastClickPoint == point.Value
                     && (gameTime.TotalGameTime - _lastClickTime).TotalMilliseconds <= DoubleClickMs;

        if (isDouble)
        {
            _engine.Chord(point.Value);
            _lastClickPoint = null;   // тройной клик не должен дать второй аккорд
        }
        else
        {
            _engine.OpenCell(point.Value);
            _lastClickPoint = point.Value;
            _lastClickTime = gameTime.TotalGameTime;
        }
    }
    UpdateMineCounter();
}
```

### 6.4. `OnRightUp`

```csharp
private void OnRightUp(Point pos)
{
    if (_suppressNextMouseUp) { _suppressNextMouseUp = false; return; }

    if (_chordCell is CellPoint chord)
    {
        _chordCell = null;
        _suppressNextMouseUp = true;
        _engine.Chord(chord);
        UpdateMineCounter();
        return;
    }

    var point = ToCellPoint(pos);
    if (!point.HasValue) return;
    _engine.CycleMark(point.Value);
    UpdateMineCounter();
}
```

### 6.5. Сброс при `StartGame`

```csharp
private void StartGame(GameSettings settings)
{
    _triggerCell = null;
    _chordCell = null;
    _suppressNextMouseUp = false;
    _lastClickPoint = null;     // иначе первый клик новой игры может слиться с последним старой
    _wasInsideBoard = false;
    _screen = Screen.Main;
    _openMenu = MenuKind.None;

    _engine.StartNewGame(settings);
    ApplyWindowSize();
    SetSmiley("smiley");
    UpdateMineCounter();
    UpdateTimer();
}
```

## 7. Смайлик-кнопка

Прямоугольник:

```csharp
private Rectangle SmileyRect => new(
    (GraphicsDevice.Viewport.Width - SmileySize) / 2,
    MenuHeight + PanelTopMargin,
    SmileySize, SmileySize);
```

Состояния:

| Источник | Когда | Что |
|---|---|---|
| `UpdateInput` (left-down) | не в `SmileyRect`, нет `_chordCell`, `NotStarted`/`InProgress` | `smiley_oh` |
| Вход на поле с зажатой ЛКМ | `NotStarted`/`InProgress` | `smiley_oh` |
| Уход с поля | `NotStarted`/`InProgress` | `smiley` |
| `OnLeftUp` → `ResetSmileyForCurrentState` | после любого ЛКМ-up | `smiley` / `win` / `lose` |
| `GameStateChanged(Won)` | — | `smiley_win` |
| `GameStateChanged(Lost)` | — | `smiley_lose` |
| Аккорд (`_chordCell != null`) | `smiley_oh` **не** включается | — |

```csharp
private void ResetSmileyForCurrentState()
{
    SetSmiley(_engine.State switch
    {
        GameState.Won  => "smiley_win",
        GameState.Lost => "smiley_lose",
        _              => "smiley",
    });
}
```

Отрисовка: заливка `SystemColors.Control`, картинка `28×28` по центру,
рамка 1 px `SystemColors.ControlDark` (имитация стандартной кнопки).

## 8. LED-счётчики (по §15 `02-GAME-RULES.md`)

**Счётчик мин:** `engine.RemainingMines = MineCount − флаги`.
- `>= 0`: 3 цифры `PadLeft(3,'0')`.
- `< 0`: `minus.png` + 2 цифры `PadLeft(2,'0')` от `abs(value)`. Знак минуса
  занимает одну позицию, как в Windows XP.
- `> 999` / `abs > 99`: обрезаем (рисуем только 3/2 цифры) — как в WinForms-версии.

**Таймер:** `Math.Min((int)engine.Elapsed.TotalSeconds, 999)`, 3 цифры.

`Question` **не** считается флагом. При `Won`/`Lost` счётчик **не меняется** —
игрок своих действий не отменял (§15).

```csharp
private void DrawLed(int value, int x, int y, bool isMineCounter)
{
    if (isMineCounter && value < 0)
    {
        Draw("minus", new Rectangle(x, y, LedW, LedH));
        var s = Math.Abs(value).ToString().PadLeft(2, '0');
        for (int i = 0; i < 2; i++)
            Draw($"timer_{s[i]}", new Rectangle(x + LedW + i * LedW, y, LedW, LedH));
        return;
    }

    var str = value.ToString().PadLeft(3, '0');
    for (int i = 0; i < 3; i++)
        Draw($"timer_{str[i]}", new Rectangle(x + i * LedW, y, LedW, LedH));
}
```

Фон под LED — `Color.Black`. `PointClamp` + прямоугольники `(24, 32)` —
без размытия.

## 9. Меню и диалоги

### 9.1. Меню

```
Игра
├── Новая игра          (N)
├── ─────
├── Новичок             (1)
├── Любитель            (2)
├── Профессионал        (3)
├── ─────
└── Выход

Справка
├── Лучшие результаты   (F2)
├── ─────
└── О программе         (F1)
```

Рисуется полоса `MenuHeight = 24` сверху, фон `SystemColors.Control`.
Заголовки `Игра` / `Справка` — текст `SpriteFont`, при наведении/открытии —
подсветка `SystemColors.MenuHighlight` + белый текст.

Dropdown — при клике по заголовку: белый фон, 1 px рамка `ControlDark`,
пункты высотой `20` px, разделители — линия `ControlDark`, пункт под
курсором — подсветка `MenuHighlight`.

`Esc` / клик вне — закрыть dropdown.

**Горячие клавиши:** по решению пользователя `1`/`2`/`3`, `N`, `F1`, `F2`
**не реализуются** — полного меню + `Esc` достаточно. WinForms-версия
горячих клавиш тоже не имеет. Если в будущем понадобятся — добавлять
отдельной задачей с edge-детекцией `Keyboard.GetState()` в `UpdateInput`.

Esc не закрывает игру на главном экране, а только закрывает выпадающие меню и вспомогательные экраны (Records, About, NamePrompt)

### 9.2. `Records`

Три колонки: «Новичок», «Любитель», «Профессионал». Под каждой —
`store.LoadRecords(level)`, топ-5, формат `место | имя | время (F1) сек`.
Пустое имя → «Аноним» (как `string.IsNullOrWhiteSpace(PlayerName) ? "Аноним" : ...`).
`Esc` — назад в `Main`.

### 9.3. `About`

Иконка (`Icon.bmp` через `Content` или `smiley` как fallback), «Сапёр»
(bold, ~16pt), «Классическая игра «Сапёр».», «Автор: miptleha.»,
ссылка «Скачать последнюю версию с GitHub» (в MonoGame не кликабельна; при
желании — `Process.Start` через `System.Diagnostics.Process`), кнопка `OK`.
`Esc` / `OK` — назад.

### 9.4. `NamePrompt` (по §12 `02-GAME-RULES.md` и архитектурному компромиссу ядра)

Ядро `GameEngine` сохраняет рекорд (с пустым именем) **до** `GameStateChanged(Won)`.
Поэтому UI после победы:
1. `_screen = NamePrompt`.
2. Игрок вводит имя (по умолчанию `"Игрок"`), `Enter` — submit, `Esc` — cancel.
3. На submit:
   ```csharp
   _engine.PlayerName = _nameBuffer;
   if (_recordStore is JsonRecordStore json)
       json.RenameRecord(_engine.Difficulty, _engine.Elapsed.TotalSeconds, _nameBuffer);
   ```
   `RenameRecord` ищет запись с тем же временем (`Math.Abs(e.TimeSeconds - t) < 0.1`)
   и перезаписывает имя в JSON.
4. `_screen = Records` (и на submit, и на cancel — как в WinForms-версии).

Ввод текста: `Window.TextInput` (DesktopGL) — событие приходит с символом;
накапливаем в `_nameBuffer`, `Backspace` — удалить последний, `Enter` — submit.
Если `TextInput` недоступен — edge-детекция `Keyboard` для `A-Z`, `А-Я`, `0-9`, `пробел`.

## 10. События движка — что делает UI

| Событие | Действие UI |
|---|---|
| `CellChanged` | `UpdateMineCounter()` (перерисовка — каждый кадр в `Draw`) |
| `CellsRevealed` | — (перерисовка в `Draw`) |
| `BoardGenerated` | `UpdateMineCounter()` |
| `GameStateChanged(Won)` | `SetSmiley("smiley_win")`; если `Difficulty != Custom` → `_screen = NamePrompt`; `UpdateMineCounter()`, `UpdateTimer()` |
| `GameStateChanged(Lost)` | `SetSmiley("smiley_lose")`; `_triggerCell = e.TriggerCell?.Position`; `UpdateMineCounter()`, `UpdateTimer()` |

**Правило:** обработчики — только меняют поля. `SpriteBatch.Draw` вне `Draw` бросит
`InvalidOperationException`.

## 11. Сценарий `Won` (по §16.1 `02-GAME-RULES.md`)

1. `GameStateChanged(Won)` → `smiley_win`.
2. Если `Difficulty != Custom` — `_screen = NamePrompt` (см. §9.4). Иначе сразу `Records` или `Main`.
3. После `NamePrompt` — `_screen = Records`.
4. При отрисовке поля: все клетки, оставшиеся `Closed` / `Flagged` / `Question`,
   рисуются как `cell_closed.png` + `cell_flag.png` поверх. Это мины
   (условие победы: все безопасные `Opened`). `TriggerCell` при `Won` = `null`.

Счётчик мин при `Won` **не меняется** (§15).

## 12. Сценарий `Lost` (по §16.2 `02-GAME-RULES.md`)

1. `GameStateChanged(Lost)` → `smiley_lose`, `_triggerCell = e.TriggerCell?.Position`.
2. При отрисовке:
   - `info.Position == _triggerCell` → `cell_exploded.png`.
   - `State == Opened && Content == Mine` (не триггер) → `cell_empty.png` + `bomb.png`.
   - `State == WrongFlag` → `cell_wrong_flag.png`.
   - `State == Flagged` (правильный флаг на мине) → остаётся `cell_closed.png` + `cell_flag.png`.
   - `State == Question` на безопасной клетке → остаётся `cell_closed.png` + `cell_question.png`.
3. Счётчик мин **не меняется** (§15).

## 13. Антипаттерны (не делать)

- **Не коммитить `Content/bin/`, `Content/obj/`.** Только `Content.mgcb` и PNG.
- **Не вызывать `SpriteBatch.Draw` из обработчиков событий.** События — в `Update`,
  `SpriteBatch` там не активен.
- **Не открывать клетку на `LeftButton == Pressed`.** Только edge `Released ← Pressed`.
- **Не делать `Chord` на среднюю кнопку по `Released`.** Только на `Pressed` (мгновенный аккорд).
- **Не включать `smiley_oh` при аккорде** (`_chordCell != null`).
- **Не менять смайлик на `smiley` при уходе с поля, если `State == Won/Lost`.**
- **Не рисовать `cell_exploded` для всех мин.** Только для `_triggerCell`.
- **Не использовать `SamplerState.LinearClamp`.** Только `PointClamp`.
- **Не масштабировать `timer_N` нецелочисленно.** Рисовать в `(24, 32)`.
- **Не забывать `_lastClickPoint = null` в `StartGame`.** Иначе первый клик новой
  игры сольётся с последним старой в «двойной».
- **Не забывать `ApplyWindowSize()` при смене сложности.**
- **Не ссылаться в этом документе на `09-PHASE-5-WINFROMS.md`.** Документ
  самодостаточен: всё поведение — из `02-GAME-RULES.md`.
  Не закрывать игру по Esc на главном экране. Esc закрывает только меню и диалоги (как в WinForms-версии)

## 14. Что нужно доработать в репозитории до первой сборки

1. **Переименовать `Game1` → `MinesweeperGame`** (класс + файл `Game1.cs` → `MinesweeperGame.cs`),
   обновить `Program.cs`.
2. **Скопировать ассеты** `assets/*.png` → `src/MinesweeperMonoGame/Content/` (см. §3.1).
3. **Заполнить `Content.mgcb`** ~30 блоками (см. §3.2). Сейчас файл пуст.
4. **Создать `DefaultFont.spritefont`** и зарегистрировать в `Content.mgcb` (см. §3.5).
5. **Проверить `Icon.ico` / `Icon.bmp`** в `src/MinesweeperMonoGame/` — заменить
   на актуальные из `assets/icon.ico` (и сгенерировать `Icon.bmp`, если нужно).
6. **Убедиться, что `JsonRecordStore`** доступен из `MinesweeperCore.Infrastructure`
   (он уже там, `MinesweeperMonoGame` ссылается на Core — ничего делать не надо).

## 15. Явные ограничения Phase 6

1. **Нет DPI-масштабирования.** На 150% DPI окно визуально меньше, чем на 100%.
   Скейлинг — отдельная задача после MVP.
2. **`SpriteFont` vs GDI+.** Метрики шрифта в MonoGame отличаются от `Segoe UI 9pt`
   в WinForms. Размеры прямоугольников сохраняются, текст может выглядеть чуть иначе.
3. **Кнопка смайлика** — имитация стандартной кнопки (1 px рамка `ControlDark`),
   не нативный рендер.
4. **Dropdown-меню** — рисованное. Если делать полный паритет с `MenuStrip` долго,
   MVP — только заголовки + горячие клавиши.
5. **Ссылка в `About` не кликабельна** (или `Process.Start`, если решим добавить).
