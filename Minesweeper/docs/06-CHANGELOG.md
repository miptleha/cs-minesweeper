# Changelog

Все значимые изменения проекта **`cs-minesweeper`** документируются в этом файле.

Формат основан на [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),  
проект придерживается [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> **Правило для агента:** при завершении задачи, которая меняет **поведение игры, публичный API Core или пользовательский сценарий UI**, необходимо добавить запись в раздел `[Unreleased]` (см. `04-AGENT-GUIDE.md`). Чисто инфраструктурные изменения (CI, документация, тесты без изменения поведения) в changelog не попадают.

## [Unreleased]

### Added
- Базовая структура решения: `MinesweeperCore`, `Minesweeper.WinForms`, `Minesweeper.MonoGame`, `MinesweeperCore.Tests`.
- Документация проекта в `docs/` и точки входа для AI-агентов (`GIGACODE.md`, `AGENTS.md`).

### Changed
- _пока пусто_

### Fixed
- _пока пусто_

### Removed
- _пока пусто_

## [0.1.0] — YYYY-MM-DD

Первый играбельный MVP (WinForms).

### Added
- `MinesweeperCore`: доменные модели, генерация поля, `GameEngine`, события, `IRandomProvider`, `IRecordStore`.
- `Minesweeper.WinForms`: игровое поле, меню, таймер, счётчик мин, таблица рекордов, «О программе».
- `MinesweeperCore.Tests`: тесты правил игры и архитектурный тест (Core не ссылается на UI).

[Unreleased]: https://github.com/miptleha/cs-minesweeper/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/miptleha/cs-minesweeper/releases/tag/v0.1.0