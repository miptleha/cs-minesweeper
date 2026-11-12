# 11 — Phase 7: WPF UI

Спецификация реализации WPF-фронтенда поверх готового ядра `MinesweeperCore`.
Стабильное API ядра (ядро не меняется) — UI его **только потребляет**, как и в
Phase 5/6.

## 1. Цель Phase 7

Запускаемое WPF-приложение, в котором можно сыграть полную партию:

- Поле рисуется по `CellInfo` и ассетам из `assets/`, встроенным в DLL как `Resource`.
- Левый клик открывает клетку (по отпусканию), правый — циклически меняет метку,
  Ctrl+ЛКМ = `CycleMark`, средняя кнопка и ЛКМ+ПКМ = `Chord`.
- Смайлик-кнопка показывает состояние: `smiley` / `smiley_oh` / `smiley_win` / `smiley_lose`.
- LED-счётчик мин и таймер обновляются в реальном времени; отрицательный счётчик
  рисуется через `minus.png` (по §15 `02-GAME-RULES.md`).
- Меню `Игра` / `Справка` — **нативное WPF-меню** (не рисованное).
- Окна `RecordsWindow` / `AboutWindow` / `NamePromptWindow` — нативные `Window`
  с `ShowDialog()`.
- Сохранение рекордов — через `JsonRecordStore` из `MinesweeperCore.Infrastructure`
  (тот же `%LocalAppData%\Minesweeper\records.json`, что и в WinForms/MonoGame).

**Вне scope Phase 7:** звуки, анимации, темы оформления, MVVM-фреймворки
(CommunityToolkit.Mvvm и т.п.) — только `INotifyPropertyChanged` вручную.

**Главная цель эксперимента:** показать декларативный XAML и MVVM (Data Binding,
`ObservableCollection`, `DataTemplate`) в противовес императивному WinForms.

## 2. Архитектура UI (MVVM + code-behind для ввода)

### 2.1. Структура проекта

```
src/MinesweeperWpf/
├── MinesweeperWpf.csproj
├── App.xaml
├── App.xaml.cs
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── ViewModels/
│   ├── MainViewModel.cs
│   ├── CellViewModel.cs
│   └── LedDisplayViewModel.cs
├── Windows/
│   ├── RecordsWindow.xaml(.cs)
│   ├── AboutWindow.xaml(.cs)
│   └── NamePromptWindow.xaml(.cs)
├── Services/
│   └── ImageCache.cs
└── Resources/
    ├── *.png (скопированы из assets/)
    └── icon.ico
```

### 2.2. Роли слоёв

| Слой | Что содержит |
|---|---|
| `MainWindow.xaml` | Layout, `DataTemplate` клетки, `ItemsControl` поля, `Menu`, смайлик, LED. |
| `MainWindow.xaml.cs` | Только mouse-обработчики (Down/Up/Move) и вызовы `ViewModel.OpenCell` / `CycleMark` / `Chord`. |
| `MainViewModel` | Обёртка над `GameEngine`: подписки на события, `ObservableCollection<CellViewModel>`, `RemainingMines`, `ElapsedSeconds`, `SmileyImage`. |
| `CellViewModel` | `INotifyPropertyChanged`: `Position`, `BackgroundImage`, `OverlayImage`. |
| `LedDisplayViewModel` | `ObservableCollection<ImageSource> Digits`; `SetValue(int, bool allowMinus)`. |

### 2.3. Ключевой принцип

**Вся игровая логика — в ядре.** ViewModel только:

1. Вызывает `_engine.OpenCell` / `CycleMark` / `Chord`.
2. По событиям ядра пересчитывает свойства (меняет `Cells[i].BackgroundImage` и т.п.).
3. Держит `GameState` через `_engine.State`.

**Code-behind для ввода** — это норма для игровых WPF-приложений: клетки в
`ItemsControl` — это сгенерированные `ContentPresenter`, навешивать на каждую
команду через `DataTemplate.Triggers` громоздко. Mouse-обработчики на
`ItemsControl` + расчёт `CellPoint` по координате работают так же надёжно, как в
WinForms (см. `09-PHASE-5-WINFROMS.md` §6).

## 3. Ассеты (WPF Resource)

Картинки **встраиваются в DLL** через `Resource` (не `Content`). Это делает
`.exe` самодостаточным, как и WinForms-версия (`EmbeddedResource`).

### 3.1. `.csproj`

```xml
<ItemGroup>
  <Resource Include="Resources\*.png" />
  <Resource Include="Resources\icon.ico" />
</ItemGroup>
<PropertyGroup>
  <ApplicationIcon>Resources\icon.ico</ApplicationIcon>
</PropertyGroup>
```

### 3.2. Доступ в XAML и коде

Pack URI: `pack://application:,,,/Resources/cell_closed.png` (или сокращённо
`/Resources/cell_closed.png`). Для `Image.Source` в `DataTemplate` —
сокращённый вид.

Для `ImageSource` из ViewModel — централизованный кэш `ImageCache`:

```csharp
internal static class ImageCache
{
    private static readonly Dictionary<string, BitmapImage> _cache = new();

    public static BitmapImage Get(string name)
    {
        if (_cache.TryGetValue(name, out var cached)) return cached;

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri($"pack://application:,,,/Resources/{name}.png", UriKind.Absolute);
        bmp.CacheOption = BitmapCacheOption.OnLoad; // без утечек GDI+
        bmp.EndInit();
        bmp.Freeze(); // можно использовать из любого потока
        _cache[name] = bmp;
        return bmp;
    }
}
```

### 3.3. Масштабирование пиксель-арта

WPF по умолчанию применяет `Linear`-фильтрацию. Для пиксель-арта — отключить:

```xml
<Window ... RenderOptions.BitmapScalingMode="NearestNeighbor">
```

Исключение — смайлик (32×32 → 28×28): для него `BitmapScalingMode="HighQuality"`
на конкретном `Image`, чтобы не было «лесенки».

## 4. Компоновка окна и DPI

### 4.1. Размер окна

`ResizeMode="NoResize"`, `SizeToContent="WidthAndHeight"`,
`WindowStartupLocation="CenterScreen"`, `Title="Сапёр"`, иконка — из
`Resources/icon.ico`.

### 4.2. Layout

```
┌──────────────────────────────────────┐ ← Menu (Auto)
│ Игра  Справка                        │
├──────────────────────────────────────┤ ← TopPanel (48 DIP)
│ [LED]          [🙂]          [LED]   │
├──────────────────────────────────────┤
│                                      │ ← Board (Auto, Width*24 × Height*24)
│                                      │
└──────────────────────────────────────┘
```

XAML-скелет:

```xml
<Grid>
  <Grid.RowDefinitions>
    <RowDefinition Height="Auto" />  <!-- Menu -->
    <RowDefinition Height="48" />    <!-- TopPanel -->
    <RowDefinition Height="Auto" />  <!-- Board -->
  </Grid.RowDefinitions>

  <Menu Grid.Row="0"> ... </Menu>

  <Grid Grid.Row="1" Background="#F0F0F0">
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="Auto" />
      <ColumnDefinition Width="*" />
      <ColumnDefinition Width="Auto" />
    </Grid.ColumnDefinitions>
    <ItemsControl Grid.Column="0" ItemsSource="{Binding MineCounter.Digits}" Margin="10,8,0,0" />
    <Button     Grid.Column="1" Width="32" Height="32" HorizontalAlignment="Center" Margin="0,8,0,0" ...>
      <Image Source="{Binding SmileyImage}" Width="28" Height="28" RenderOptions.BitmapScalingMode="HighQuality" />
    </Button>
    <ItemsControl Grid.Column="2" ItemsSource="{Binding TimerDisplay.Digits}" Margin="0,8,10,0" />
  </Grid>

  <ItemsControl Grid.Row="2"
                x:Name="Board"
                ItemsSource="{Binding Cells}"
                MouseLeftButtonDown="OnBoardMouseDown"
                MouseLeftButtonUp="OnBoardMouseUp"
                MouseRightButtonDown="OnBoardMouseDown"
                MouseRightButtonUp="OnBoardMouseUp">
    <ItemsControl.ItemsPanel>
      <ItemsPanelTemplate>
        <UniformGrid Rows="{Binding Height}" Columns="{Binding Width}" />
      </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
    <ItemsControl.ItemTemplate>
      <DataTemplate>
        <Grid Width="24" Height="24">
          <Image Source="{Binding BackgroundImage}" Stretch="None" />
          <Image Source="{Binding OverlayImage}" Stretch="None" />
        </Grid>
      </DataTemplate>
    </ItemsControl.ItemTemplate>
  </ItemsControl>
</Grid>
```

### 4.3. DPI

WPF **сам** DPI-осведомлён (per-monitor v2). Ничего специально делать **не нужно**:
24 DIP = 24 px при 100%, 36 px при 150%. Ассеты — 24×24 native, растянутся
`NearestNeighbor`'ом — выглядит корректно, как в WinForms.

**Не** ставьте `AutoScaleMode` (это WinForms-свойство) и **не** отключайте
DPI-awareness — в отличие от WinForms, здесь отключение только испортит.

## 5. Отрисовка поля (`ItemsControl` + `UniformGrid` + `DataTemplate`)

### 5.1. `CellViewModel`

```csharp
public sealed class CellViewModel : INotifyPropertyChanged
{
    public CellPoint Position { get; }

    private ImageSource? _background;
    public ImageSource? BackgroundImage { get => _background; private set => SetField(ref _background, value); }

    private ImageSource? _overlay;
    public ImageSource? OverlayImage { get => _overlay; private set => SetField(ref _overlay, value); }

    public void Update(CellInfo info, CellPoint? triggerCell, bool showFlagsOnClosed)
    {
        // строго по §16.3, см. ниже
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void SetField<T>(ref T field, T value, [CallerMemberName] string? p = null)
    { if (Equals(field, value)) return; field = value; PropertyChanged?.Invoke(this, new(p)); }
}
```

### 5.2. Логика `Update` (по таблице §16.3)

```
if State == WrongFlag:
    Background = cell_wrong_flag; Overlay = null;

else if State == Opened:
    if Position == triggerCell:
        Background = cell_exploded; Overlay = null;
    else:
        Background = cell_empty;
        Overlay = Content switch {
            Number when 1 <= AdjacentMines <= 8 => digit_{AdjacentMines},
            Mine => bomb,
            _ => null
        };

else if showFlagsOnClosed && State in {Closed, Flagged, Question}:
    // Это Won: все такие клетки — мины (§16.1)
    Background = cell_closed; Overlay = cell_flag;

else if State == Flagged:
    Background = cell_closed; Overlay = cell_flag;

else if State == Question:
    Background = cell_closed; Overlay = cell_question;

else: // Closed
    Background = cell_closed; Overlay = null;
```

Параметр `showFlagsOnClosed = (engine.State == GameState.Won)` — по §16.1.
`triggerCell` = `_engine` не даёт триггер при Won, но даёт при Lost — храним в
`MainViewModel._triggerCell`.

### 5.3. Обновление клеток

| Источник | Что обновляем |
|---|---|
| `CellChanged` | `Cells[Change.Position]` — одна клетка. |
| `CellsRevealed` | Каждую `CellInfo` из батча (Lost / каскад / аккорд). |
| `GameStateChanged(Won)` | **Все** клетки — чтобы нарисовать флаги на закрытых. |
| `GameStateChanged(Lost)` | **Все** клетки — для `WrongFlag` + `bomb` + `cell_exploded`. |
| `StartNewGame` | Пересоздать `ObservableCollection<CellViewModel>` под новый размер. |

## 6. Ввод

### 6.1. Раскладка (идентична `09` §6.1)

| Действие | WPF-условие | Результат |
|---|---|---|
| ЛКМ по клетке | `MouseUp`, `Left` | `OpenCell` |
| ПКМ по клетке | `MouseUp`, `Right` | `CycleMark` |
| Ctrl+ЛКМ | `MouseUp`, `Left` + `Keyboard.IsKeyDown(Key.LeftCtrl)` | `CycleMark` |
| ЛКМ+ПКМ | обе зажаты → отпускание любой | `Chord` |
| Средняя кнопка | `MouseDown`, `Middle` | `Chord` |
| ЛКМ по смайлику | `Button.Click` | `StartNewGame` |

### 6.2. Порядок в `OnBoardMouseUp` (по образцу `09` §6.2)

```
1. Снять флаг кнопки из _downButtons.
2. Если _suppressNextMouseUp → погасить, выход.
3. Если _chordCandidate != null:
       Chord(candidate), _suppressNextMouseUp = true, выход.
4. Left:
   a. ResetSmileyForCurrentState()
   b. point = ToCellPoint(e.GetPosition(Board))
   c. если Ctrl — CycleMark
      иначе OpenCell
5. Right: CycleMark
```

`ToCellPoint`:

```csharp
private CellPoint? ToCellPoint(Point p)
{
    int x = (int)(p.X / 24.0);
    int y = (int)(p.Y / 24.0);
    if (x < 0 || x >= _vm.Width || y < 0 || y >= _vm.Height) return null;
    return new CellPoint(x, y);
}
```

`24.0` — размер клетки в **DIP**. WPF сам переводит mouse-координаты в DIP.

### 6.3. Средняя кнопка

`MouseDown` с `e.ChangedButton == MouseButton.Middle` → `Chord` сразу. Смайлик
`smiley_oh` **не** показывается (как в WinForms/MonoGame).

## 7. Смайлик-кнопка

```xml
<Button x:Name="SmileyButton" Width="32" Height="32" Focusable="False"
        Cursor="Hand" Click="OnSmileyClick">
  <Image Source="{Binding SmileyImage}" Width="28" Height="28"
         RenderOptions.BitmapScalingMode="HighQuality" />
</Button>
```

`SmileyImage` — `ImageSource` (не строка). `MainViewModel.SetSmiley(string name)`
внутри делает `ImageCache.Get(name)`.

| Источник | Когда | Что |
|---|---|---|
| `OnBoardMouseDown` (Left) | не в области смайлика, нет `_chordCandidate`, `NotStarted`/`InProgress` | `smiley_oh` |
| `OnBoardMouseUp` → `ResetSmileyForCurrentState()` | после любого ЛКМ-up | `smiley` / `smiley_win` / `smiley_lose` |
| `GameStateChanged(Won)` | — | `smiley_win` |
| `GameStateChanged(Lost)` | — | `smiley_lose` |
| `StartGame` | — | `smiley` |

## 8. LED-счётчики (`ObservableCollection<ImageSource>`)

`LedDisplayViewModel`:

```csharp
public sealed class LedDisplayViewModel
{
    public ObservableCollection<ImageSource> Digits { get; } = new();

    public void SetValue(int value, bool allowMinus)
    {
        Digits.Clear();
        if (allowMinus && value < 0)
        {
            Digits.Add(ImageCache.Get("minus"));
            var s = Math.Min(Math.Abs(value), 99).ToString("D2");
            foreach (var c in s) Digits.Add(ImageCache.Get($"timer_{c}"));
            return;
        }
        var str = Math.Clamp(value, 0, 999).ToString("D3");
        foreach (var c in str) Digits.Add(ImageCache.Get($"timer_{c}"));
    }
}
```

- **Счётчик мин** (`allowMinus: true`): `engine.RemainingMines`. При `>= 0` —
  три цифры; при `< 0` — `minus.png` + две цифры (знак занимает одну позицию,
  как в Windows XP, см. §15 `02-GAME-RULES.md`).
- **Таймер** (`allowMinus: false`): `Math.Min((int)engine.Elapsed.TotalSeconds, 999)`.
- **`Question` не считается флагом.** При `Won`/`Lost` счётчик **не меняется**.

Обновление — из `MainViewModel`:

```csharp
private void UpdateMineCounter() => MineCounter.SetValue(_engine.RemainingMines, allowMinus: true);
private void UpdateTimer()       => TimerDisplay.SetValue((int)_engine.Elapsed.TotalSeconds, allowMinus: false);
```

`DispatcherTimer` с интервалом 100 мс → `UpdateTimer()`.

## 9. Диалоги

Три окна, каждое — `Window` с XAML.

### 9.1. `RecordsWindow`

- Три вкладки `TabControl`: `Новичок` / `Любитель` / `Профессионал`.
- В каждой — `ListView` (`GridView`) с колонками `#`, `Имя`, `Время`.
- Данные — `_recordStore.LoadRecords(level)`; пустое имя → `«Аноним»`.
- `Esc` закрывает (`KeyDown` → `Close()`).

### 9.2. `AboutWindow`

- Иконка, «Сапёр», версия (`Assembly.GetExecutingAssembly().GetName().Version`),
  «Автор: miptleha.», `TextBlock` + `Hyperlink` (WPF-нативный, `Process.Start`),
  кнопка `OK` (`IsDefault="True"`, `IsCancel="True"`).
- `Esc` закрывает автоматически через `IsCancel="True"`.

### 9.3. `NamePromptWindow`

- `Label` «Введите ваше имя:», `TextBox` (по умолчанию `«Игрок»`),
  `Button` OK (`IsDefault="True"`) и Cancel (`IsCancel="True"`).
- После `OK` — `PlayerName` из `TextBox.Text`.
- В `MainViewModel` после `GameStateChanged(Won)`:
  1. Открыть `NamePromptWindow.ShowDialog()`.
  2. При `DialogResult == true`: `_engine.PlayerName = vm.PlayerName;`
     `if (_recordStore is JsonRecordStore j) j.RenameRecord(engine.Difficulty, engine.Elapsed.TotalSeconds, playerName);`
  3. Открыть `RecordsWindow.ShowDialog()`.
- `Custom`-уровень: `NamePromptWindow` **не показывается**, рекорд не сохраняется.

## 10. Меню (WPF `Menu`)

Используется **нативный** `Menu`, не рисованный:

```xml
<Menu>
  <MenuItem Header="Игра">
    <MenuItem Header="Новая игра" Click="OnNewGameClick" />
    <Separator />
    <MenuItem Header="Новичок"      Click="OnBeginnerClick" />
    <MenuItem Header="Любитель"     Click="OnIntermediateClick" />
    <MenuItem Header="Профессионал" Click="OnExpertClick" />
    <Separator />
    <MenuItem Header="Выход" Click="OnExitClick" />
  </MenuItem>
  <MenuItem Header="Справка">
    <MenuItem Header="Лучшие результаты" Click="OnRecordsClick" />
    <Separator />
    <MenuItem Header="О программе" Click="OnAboutClick" />
  </MenuItem>
</Menu>
```

**Горячие клавиши** (`1`/`2`/`3`, `N`, `F1`, `F2`) — по решению пользователя
**не реализуются**, как и в WinForms/MonoGame. Управление через меню.

## 11. События движка — что делает ViewModel

| Событие | Действие |
|---|---|
| `CellChanged` | `Cells[Change.Position].Update(...)`; `UpdateMineCounter()`. |
| `CellsRevealed` | Для каждого `CellInfo` — `Cells[Position].Update(...)`; `UpdateMineCounter()`. |
| `BoardGenerated` | `UpdateMineCounter()` (после первого клика `RemainingMines` уже верный). |
| `GameStateChanged(Won)` | `SetSmiley("smiley_win")`; пересчитать **все** клетки (флаги); `UpdateMineCounter()`; `UpdateTimer()`. |
| `GameStateChanged(Lost)` | `SetSmiley("smiley_lose")`; `_triggerCell = e.TriggerCell?.Position`; пересчитать **все** клетки; `UpdateMineCounter()`; `UpdateTimer()`. |

**Правило:** обработчики не вызывают ничего, что трогает `UIElement` напрямую —
только `INotifyPropertyChanged` через VM, WPF сам перерисует.

## 12. ViewModel и data binding — ключевые свойства

```csharp
public sealed class MainViewModel : INotifyPropertyChanged
{
    public ObservableCollection<CellViewModel> Cells { get; } = new();
    public LedDisplayViewModel MineCounter { get; } = new();
    public LedDisplayViewModel TimerDisplay { get; } = new();

    public int Width  => _engine.Width;
    public int Height => _engine.Height;

    private ImageSource _smileyImage = ImageCache.Get("smiley");
    public ImageSource SmileyImage { get => _smileyImage; private set { ... } }

    private CellPoint? _triggerCell;

    public void StartGame(GameSettings settings) { /* rebuild Cells, subscribe to events */ }
    public void OpenCell(CellPoint p)  => _engine.OpenCell(p);
    public void CycleMark(CellPoint p) => _engine.CycleMark(p);
    public void Chord(CellPoint p)     => _engine.Chord(p);
    public GameState State             => _engine.State;
}
```

На `StartGame` старые подписки **отписываются** (`-=`) перед новым `StartNewGame`,
иначе утечка и дублирующиеся обновления.

## 13. Антипаттерны (не делать)

- **Не вызывать `_engine.OpenCell` в `MouseDown`.** Только в `MouseUp` — иначе
  ломается drag-and-click и chord-детекция (см. `09` §6).
- **Не менять `Cells` in-place при `StartNewGame`** — только пересоздавать
  `ObservableCollection`, иначе WPF не увидит полной замены.
- **Не забывать `-=` при переподписке** на события `GameEngine`.
- **Не использовать `Bitmap` из `System.Drawing`** — только `ImageSource` /
  `BitmapImage` (WPF).
- **Не ставить `BitmapScalingMode="Linear"` на поле** — пиксель-арт размажется.
- **Не использовать `AutoScaleMode`** — это свойство WinForms, в WPF нет.
- **Не тянуть `MinesweeperCore.Domain.Board` в UI** — работать только с
  `GameEngine`, `CellInfo`, аргументами событий.
- **Не рисовать `cell_exploded` для всех мин** — только для `_triggerCell`.
- **Не забывать `Freeze()` у `BitmapImage`** — иначе утечка GDI+ и
  «Cannot access a frozen object».
- **Не реализовывать горячие клавиши `1`/`2`/`3`/`N`/`F1`/`F2`** — по решению
  пользователя их нет ни в одном UI.

## 14. Что нужно доработать в репозитории до первой сборки

1. **Создать `src/MinesweeperWpf/`** — проект WPF (`net8.0-windows`,
   `<UseWPF>true</UseWPF>`, `<OutputType>WinExe</OutputType>`).
2. **Скопировать ассеты** `assets/*.png` + `assets/icon.ico` →
   `src/MinesweeperWpf/Resources/`. Имена **не менять**.
3. **Добавить `<ProjectReference>`** на `MinesweeperCore.csproj`.
4. **Добавить проект** в `Minesweeper.slnx` под `/src/`.
5. **Release-конфигурация:** `SelfContained=true`, `RuntimeIdentifier=win-x64`,
   `PublishSingleFile=true`, `PublishDir=..\..\dist\MinesweeperWpf\`
   (по образцу `MinesweeperWinForms.csproj`).
6. **`DispatcherTimer`** для таймера (интервал 100 мс) — заводить в
   `MainViewModel` или `MainWindow`? Рекомендуется в VM, чтобы `TimerDisplay`
   обновлялся из VM без code-behind.

## 15. Явные ограничения Phase 7

1. **MVP без визуальных плюшек.** Стандартный вид кнопки смайлика, стандартный
   `ListView` в `RecordsWindow`, стандартное меню. Стилизация — отдельной задачей.
2. **Нет кастомных шрифтов/LED-эффектов**, кроме `timer_N.png`.
3. **Нет горячих клавиш** (см. §13).
4. **MVVM реализован вручную** (`INotifyPropertyChanged` + `ObservableCollection`),
   без `CommunityToolkit.Mvvm` и без source generators. Цель — показать «голый»
   WPF-паттерн, а не MVVM-фреймворк.
5. **Code-behind для ввода** — сознательный компромисс. Полный MVVM потребовал
   бы attached behaviors, что усложнило бы код ради чистоты паттерна.
6. **DPI** — WPF сам разбирается, специальных действий не требуется
   (в отличие от WinForms §4).
7. **`PublishSingleFile` в WPF** поддерживается с .NET 5+, но с оговорками
   (WPF resource assembly грузится из single-file bundle). Если возникнут
   проблемы при публикации — fallback на «папку с файлами» (как у MonoGame).
   Это не блокирует саму игру — только дистрибутив.
   