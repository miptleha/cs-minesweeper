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

## 6. Ввод (Классическое поведение мыши + альтернативы)

Клетка **не открывается** по `MouseDown`. Используется классическое поведение
Windows XP: открытие и постановка метки происходят по `MouseUp`. Это нужно,
чтобы работало перетаскивание с зажатой кнопкой и чтобы отличить одиночный
клик от начала аккорда.

### 6.1. Итоговая раскладка ввода

| Действие | Событие / модификатор | Результат |
|---|---|---|
| ЛКМ по клетке | `MouseUp`, `Left` | `GameEngine.OpenCell` |
| ПКМ по клетке | `MouseUp`, `Right` | `GameEngine.CycleMark` |
| **Ctrl+ЛКМ** по клетке | `MouseUp`, `Left` + `Control` | `GameEngine.CycleMark` |
| ЛКМ+ПКМ одновременно | обе кнопки зажаты, отпускание любой | `GameEngine.Chord` |
| Средняя кнопка | `MouseDown`, `Middle` | `GameEngine.Chord` |
| ЛКМ по смайлику | `Button.Click` | `StartNewGame` |

**Зачем альтернативы:**

- **Ctrl+ЛКМ** — замена правого клика (`CycleMark`) для тачпадов, где ПКМ
  не работает или срабатывает нестабильно. Циклит метку
  `Closed → Flagged → Question → Closed`, счётчик мин обновляется.
- **Средняя кнопка** — на тачпадах обычно 3-finger tap. Аккорд срабатывает
  мгновенно, без ожидания второй кнопки.

Все пути ведут в одни и те же методы ядра — поведение и события идентичны.

### 6.2. Порядок обработки в `MouseUp`

```
1. Снять флаг «кнопка зажата» из _downButtons.
2. Если _suppressNextMouseUp — погасить второй MouseUp после аккорда, выход.
3. Если _chordCandidate задан — Chord, _suppressNextMouseUp = true, выход.
4. Left:
   a. ResetSmileyForCurrentState()
   b. point = ToCellPoint(e.X, e.Y); если null — выход.
   c. если Ctrl зажат                — CycleMark(point)   [аналог ПКМ]
      иначе                            — OpenCell(point)
   d. UpdateMineCounter()
5. Right:
   a. point = ToCellPoint(e.X, e.Y); если null — выход.
   b. CycleMark(point)
   c. UpdateMineCounter()
```

`Control.ModifierKeys` читается **в момент `MouseUp`**, а не `MouseDown`:
пользователь мог зажать Ctrl уже после нажатия кнопки мыши.

### 6.3. Смайлик-кнопка

- `MouseDown` (ЛКМ, состояние `NotStarted`/`InProgress`) → `smiley_oh.png`.
- `MouseUp` → `ResetSmileyForCurrentState()`: `smiley` / `smiley_win` / `smiley_lose`
  в зависимости от `GameState`.
- При аккорде `MouseDown` **не** включает `smiley_oh` — иначе смайлик мигал бы
  при каждом аккорде.

### 6.4. Защита от рассинхронизации кнопок

`_downButtons` может «застрять», если кнопку отпустили за пределами панели
(`MouseUp` не пришёл). В `BoardPanel_MouseEnter`:

```csharp
if (Control.MouseButtons == MouseButtons.None)
    _downButtons = MouseButtons.None;
```

В `StartGame` — полный сброс:

```csharp
_downButtons = MouseButtons.None;
_chordCandidate = null;
_suppressNextMouseUp = false;
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
