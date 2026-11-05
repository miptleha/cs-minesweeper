# 05 — Roadmap

## Фазы разработки

1. **Phase 1: Core Domain.** Модели (CellPoint, Cell, Board), настройки сложности.
2. **Phase 2: Core Logic.** Генерация мин, каскадное открытие, проверка победы.
3. **Phase 3: Engine & Ports.** Фасад `GameEngine`, внедрение `IRandomProvider`, `IRecordStore` и `ITimeProvider`. Дефолтные реализации (`SystemRandomProvider`, `InMemoryRecordStore`, `SystemTimeProvider`) — в `Infrastructure/`.
4. **Phase 4: Core Testing.** Написание юнит-тестов для ядра.
5. **Phase 5: WinForms UI.** Подключение ядра к WinForms, отрисовка сетки, обработка кликов.
   Полная спецификация — `docs/09-PHASE-5-WINFROMS.md`.
6. **Phase 6: MonoGame UI.** Подключение ядра к MonoGame, работа с текстурами и спрайтами.
