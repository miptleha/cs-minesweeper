# 09 — Phase 5: WinForms UI

Спецификация реализации WinForms-фронтенда поверх готового ядра `MinesweeperCore`.
Стабильное API ядра (105 тестов) — UI его **только потребляет**.

## 1. Цель Phase 5

Запускаемое WinForms-приложение, в котором можно сыграть полную партию:
- Поле рисуется по `CellInfo` и встроенным ассетам.
- Левый клик открывает клетку (по отпусканию), правый — циклически меняет метку.
- Смайлик-кнопка показывает состояние: `smiley` / `smiley_oh` / `smiley_win` / `smiley_lose`.
- Счётчик мин и таймер обновляются в реальном времени.
- Работают меню настроек сложности, окно «О программе» и таблица рекордов с сохранением в файл.

**Вне scope Phase 5:** звуки, анимации, темы оформления.

## 2. Архитектура UI

WinForms-проект разбит на несколько файлов (все в `src/MinesweeperWinForms/`):
- `MainForm.cs` — главная форма, отрисовка поля и игровой цикл.
- `RecordsForm.cs` — окно таблицы рекордов (вкладки для уровней).
- `AboutForm.cs` — окно «О программе».
- `NamePromptForm.cs` — диалог ввода имени при установлении рекорда.
- `JsonRecordStore.cs` — реализация `IRecordStore` (сохранение в JSON).

Главная форма (`MainForm`) — **тонкий слой**. Вся игровая логика — в `GameEngine`. UI подписывается на события и вызывает `Invalidate()`.

## 3. Ассеты (Встроенные ресурсы)

Картинки **не копируются** в папку с `.exe`, чтобы пользователь не мог их случайно удалить или испортить. Они встраиваются в `MinesweeperWinForms.dll`.

### 3.1. Подключение в `.csproj`
```xml
<ItemGroup>
  <EmbeddedResource Include="Resources\*.png" LogicalName="Resources.%(Filename)%(Extension)" />
</ItemGroup>
<PropertyGroup>
  <ApplicationIcon>Resources\icon.ico</ApplicationIcon>
</PropertyGroup>
```

### 3.2. Загрузка в коде
Кэш `_imageCache` предотвращает утечки памяти и повторное чтение ресурсов. 
*Внимание:* `Bitmap(stream)` держит поток открытым, поэтому создаётся копия через `new Bitmap(temp)`.

```csharp
private static Image LoadImage(string name)
{
    if (_imageCache.TryGetValue(name, out var cached)) return cached;
    var asm = typeof(MainForm).Assembly;
    using var stream = asm.GetManifestResourceStream($"Resources.{name}");
    if (stream == null) return new Bitmap(24, 24);
    using var temp = new Bitmap(stream);
    var img = new Bitmap(temp);
    _imageCache[name] = img;
    return img;
}
```

## 4. Компоновка формы и DPI

- `AutoScaleMode = AutoScaleMode.None` — **критически важно**. Отключает DPI-масштабирование Windows, чтобы клетки всегда оставались ровно 24×24 пикселя.
- `FormBorderStyle = FixedSingle`, `MaximizeBox = false`.
- `TopPanel` (`Dock = Top`): высота **48 px** (не 64, чтобы не было лишних отступов).
- `BoardPanel` (`Dock = Fill`): кастомный класс `BufferedPanel : Panel` с `DoubleBuffered = true`.
- Смайлик и таймеры центрируются в `TopPanel` через событие `Resize`.

## 5. Отрисовка поля

`_boardPanel.Paint += BoardPanel_Paint;`
Внутри цикла запрашивается `_engine.GetCell(...)` и вызывается `DrawCell`.

**Отрисовка триггерной мины:**
При `Lost` триггерная мина имеет `State == Opened` и `Content == Mine`. Чтобы нарисовать `cell_exploded.png` вместо обычного `bomb.png`, `MainForm` хранит `_triggerCell = e.TriggerCell?.Position` из события `GameStateChanged`. В `DrawCell` проверяется `info.Position == _triggerCell`.

## 6. Ввод (Классическое поведение мыши)

В отличие от старой спецификации, клетка **не открывается** по `MouseDown`. Используется классическое поведение Windows XP:
1. `MouseDown` (ЛКМ) — смайлик меняется на `smiley_oh.png`.
2. `MouseUp` (ЛКМ) — смайлик возвращается в норму, и **только тогда** открывается клетка под курсором (может быть уже другая клетка, если игрок вел мышь с зажатой кнопкой).
3. `MouseEnter` / `MouseLeave` — корректно обрабатывают зажатие и отпускание кнопки за пределами поля.

```csharp
private void BoardPanel_MouseUp(object? sender, MouseEventArgs e)
{
    if (e.Button == MouseButtons.Left)
    {
        if (_engine.State == GameState.InProgress || _engine.State == GameState.NotStarted)
            SetSmiley("smiley.png");

        int x = e.X / CellSize, y = e.Y / CellSize;
        if (x >= 0 && x < _engine.Width && y >= 0 && y < _engine.Height)
            _engine.OpenCell(new CellPoint(x, y));
    }
}
```

## 7. Кнопка смайлика

- Используется стандартная `Button` (`FlatStyle.Standard`, размер `32x32`).
- Чтобы картинка не обрезалась рамками кнопки, она предварительно скейлится до `28x28` и кэшируется в `_smileyCache`.
- Таймер и счётчик мин имеют высоту `32`, поэтому кнопка идеально выровнена по ним.

## 8. Таймер и Счётчик мин

`RenderLedCounter(int value, int digits)` — склеивает картинки `timer_N.png` горизонтально.
- **Обязательно:** `g.InterpolationMode = NearestNeighbor` и явное указание `Rectangle(i * 24, 0, 24, 32)`. Без этого GDI+ может исказить пиксель-арт.
- Старые `Bitmap` **обязательно** освобождаются через `Dispose()`, чтобы не текла память GDI+.
- Отрицательный счётчик мин рисует `minus.png` и две цифры.

## 9. Таблица рекордов и диалоги

### 9.1. `JsonRecordStore`
Рекорды сохраняются в `%LocalAppData%\Minesweeper\records.json`. Это предотвращает потерю файлов при переустановке игры. Сохранение происходит через `System.Text.Json`.

### 9.2. Окна (`RecordsForm`, `AboutForm`)
- Открываются через `ShowDialog(this)`.
- Закрываются по клавише `Esc` (перехват через `ProcessCmdKey`).
- В `RecordsForm` отображается топ-5 для трёх уровней. Если имя пустое — пишется «Аноним».

### 9.3. Ввод имени (`NamePromptForm`)
Архитектурный компромисс: ядро `GameEngine` сохраняет рекорд (с пустым именем) **до** того, как вызовет событие `GameStateChanged(Won)`. 
Поэтому `MainForm` делает следующее:
1. Ловит `GameStateChanged(Won)`.
2. Показывает `NamePromptForm`.
3. Если игрок ввел имя, вызывает `jsonStore.RenameRecord(...)`.
4. Метод `RenameRecord` ищет запись с таким же временем (`Math.Abs(e.TimeSeconds - timeSeconds) < 0.1`) и перезаписывает имя в JSON.
5. Сразу открывает `RecordsForm`, чтобы игрок увидел свой результат.

## 10. Меню

```
Игра
├── Новая игра
├── ─────
├── Новичок
├── Любитель
├── Профессионал
├── ─────
└── Выход

Справка
├── Лучшие результаты
├── ─────
└── О программе
```

## 11. События движка — что делает UI

| Событие | Действие UI |
|---|---|
| `CellChanged` | `_boardPanel.Invalidate()`, `UpdateMineCounter()` |
| `CellsRevealed` | `_boardPanel.Invalidate()` |
| `BoardGenerated` | `UpdateMineCounter()` |
| `GameStateChanged` | Смена смайлика. При `Lost` — сохранение `_triggerCell`. При `Won` — запрос имени и открытие таблицы рекордов. |

## 12. Антипаттерны (не делать)

- **Не копировать `*.png` в output.** Только `EmbeddedResource`.
- **Не открывать клетку на `MouseDown`.** Только на `MouseUp`, чтобы работало перетаскивание.
- **Не хранить копию `Board` в UI.** Единственный источник — `engine.GetCell(point)`.
- **Не рисовать `cell_exploded` для всех мин.** Только для `_triggerCell`.
- **Не забывать `Dispose()` для старых `Bitmap` счётчиков.**
- **Не включать `AutoScaleMode.Font` или `Dpi`.** Только `None`, иначе поле «поплывет».
