# 04 — Agent Guide

## Твой рабочий процесс (Workflow)

### Сценарий 1: Работа с Ядром (`MinesweeperCore`)

1.  **Архитектура:** Убедись, что используешь существующие порты (`IRandomProvider`, `IRecordStore`).
2.  **Код:** Пиши логику строго по `02-GAME-RULES.md`.
3.  **Тесты:** Каждый новый метод или правило должны быть покрыты тестами (см. `07-TESTING.md`).

### Сценарий 2: Работа с UI (`MinesweeperWinForms` / `MinesweeperMonoGame`)

**Полная спецификация WinForms-слоя** — `docs/09-PHASE-5-WINFROMS.md`.
Обязательна к прочтению перед любой правкой UI.

1.  **Инициализация:** Создай экземпляр `GameEngine`. Передай ему:
    - реализацию `IRandomProvider` — либо `SystemRandomProvider` из `MinesweeperCore` (тонкая обёртка над `System.Random`), либо свою;
    - реализацию `IRecordStore` — **обязательный** параметр (не опциональный). В `MinesweeperCore` есть `InMemoryRecordStore` как заглушка; для реального UI — файловый/реестровый.
2.  **Отрисовка:** В методе отрисовки (OnPaint или Draw) запрашивай `CellInfo` для каждой клетки и рисуй соответствующую картинку.
3.  **Ввод:** Обработчик клика мыши просто вызывает `GameEngine.OpenCell(point)` или `GameEngine.CycleMark(point)`.
4.  **События:** Подпишись на `CellChanged`, `CellsRevealed`, `GameStateChanged`. При их срабатывании вызывай `Invalidate()` (WinForms) или обновляй состояние (MonoGame). **Не пиши логику игры в UI!**

## Shared Assets (`assets/`)

Исходные изображения для всех UI-проектов лежат в корне solution — в папке `assets/`.
Файлы отсюда копируются в UI-проекты вручную (или агентом) и размещаются
согласно конвенциям конкретного фреймворка.

### Состав

Все ассеты готовы и лежат в `assets/`:

| Файл | Назначение |
|---|---|
| `bomb.png` | Иконка мины (на открытой клетке с миной) |
| `cell_closed.png` | Закрытая клетка |
| `cell_empty.png` | Открытая пустая клетка (0 мин вокруг) |
| `cell_flag.png` | Клетка с флагом (накладывается на `cell_closed.png`) |
| `cell_question.png` | Клетка со знаком вопроса (накладывается на `cell_closed.png`) |
| `cell_wrong_flag.png` | Клетка с ошибочно поставленным флагом (при `Lost`) |
| `cell_exploded.png` | Клетка с миной, на которую кликнули (взрыв) |
| `digit_1.png` .. `digit_8.png` | Цифры для открытых клеток с N > 0 |
| `smiley.png` | Смайлик в нормальном состоянии (кнопка «Новая игра») |
| `smiley_oh.png` | Смайлик «о» — пока зажата левая кнопка мыши |
| `smiley_win.png` | Смайлик в очках (победа) |
| `smiley_lose.png` | Смайлик с крестиками вместо глаз (поражение) |
| `minus.png` | Знак минус для счётчика мин (при отрицательных значениях) |
| `timer_0.png` .. `timer_9.png` | Цифры LED-счётчика (счётчик мин и таймер) |
| `icon.ico` | Иконка окна приложения (WinForms / MonoGame) |

### Куда копировать

- **WinForms:** `src/MinesweeperWinForms/Resources/` — через `Properties.Resources`
  (resx-файл) или `Image.FromFile` с относительным путём.
- **MonoGame:** `src/MinesweeperMonoGame/Content/` — и добавить файлы в `Content.mgcb`,
  чтобы MGCB build tool сгенерировал `.xnb`-ассеты.

**Имена файлов не менять** — код UI будет ссылаться на них по имени.

## Запреты

*   Не тяни зависимости UI (WinForms, MonoGame) в `MinesweeperCore`.
*   Не удаляй `CellInfo` или `IRecordStore` из ядра.
*   Не используй `DateTime.UtcNow`, `DateTime.Now`, `Environment.TickCount` или `Stopwatch` **внутри `MinesweeperCore`** — только через `ITimeProvider`.
    *Исключение — `Infrastructure/SystemTimeProvider` (использует `DateTime.UtcNow`). Это единственное место в `MinesweeperCore`, где допустимы прямые вызовы.*
*   Не используй `new Random()` / `Random.Shared` **внутри `MinesweeperCore`** — только через `IRandomProvider`.
    *Исключение — `Infrastructure/SystemRandomProvider` (использует `new Random()` / `Random.Shared`). Это единственное место в `MinesweeperCore`, где допустимы прямые вызовы.*
