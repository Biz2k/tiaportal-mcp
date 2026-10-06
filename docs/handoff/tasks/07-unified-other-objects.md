# 07. Остальные объекты WinCC Unified

Приоритет: низкий, по запросу Biz. Объём: каждый пункт небольшой и независим.

Набор `unified_*` покрывает экраны, элементы, события, фейсплейты, тренды, теги, таблицы
тегов, соединения, алармы, классы алармов, списки. Скрипты — задача 02, архивы — задача 03.
Ниже остаток по убыванию пользы. Перед началом любого пункта спросить Biz, нужен ли он.

## Пункты

- [x] **Настройки runtime** — `HmiSoftware.RuntimeSettings`
      (`docs/handoff/api/unified-runtime-settings.txt`): языки и шрифты, OPC UA-сервер,
      блокировка входа, исключительное управление. Один объект с вложенными; подойдёт пара
      `unified_get_runtime_settings` / `unified_set_runtime_settings` с точечной записью
      свойств, как у классов алармов.
- [x] **Системные теги** — `HmiSoftware.SystemTags`, только чтение (`Name`, `DataType`).
      Проще всего — параметр `includeSystem` у `unified_get_tags`.
- [ ] **OPC UA-алармы** — `HmiSoftware.OpcUaAlarmTypes`
      (`docs/handoff/api/unified-opcua-alarms.txt`): `Create(nodeId, connection, name)`.
      Нужны OPC UA-соединение и сервер с алармами — в тестовом проекте их нет.
- [ ] **Аудит** — `HmiAlarmAuditClass`, `AuditTrails` (`docs/handoff/api/unified-audit.txt`).
      Нужен GMP-проект; без запроса не делать.
- [ ] **Объекты установки** — `PlantObjectTags` (`docs/handoff/api/unified-plant-objects.txt`),
      технологическая иерархия. Без запроса не делать.
- [x] **Окна экранов и всплывающие окна** — `HmiScreenWindow`
      (`docs/handoff/api/unified-screens.txt`) создаётся как обычный элемент экрана через
      `unified_manage_items` (`itemType: "HmiScreenWindow"`, свойство `Screen`). Проверить и
      описать в `README.md`; нового инструмента не нужно.

## Общее

Все новые виды объектов — через типизированные свойства и с пробой
(`docs/handoff/context.md`, правило 2). Образцы: `Portal.Unified.Alarms.cs` (объект с
вложенными объектами состояний), `Portal.Unified.Tags.cs` (пакетная операция).

## Результат (06.10.2026)

Biz: «давай 07». Сделаны три пункта, остальные не брались: OPC UA-алармы (в проекте нет OPC UA-сервера с алармами), аудит
(нужен GMP-проект), объекты установки — только по запросу.

- **Настройки runtime:** `unified_get_runtime_settings`, `unified_set_runtime_settings` (`Portal.Unified.Runtime.cs`). Обход идёт по
  типизированным свойствам, поэтому новые настройки Openness подхватываются сами. Чтение: вложенные объекты — вложенные словари,
  языки — по имени (`English (United States)` и т.п.). Свойство, которого нет в версии устройства (`TagOptimizationActive`,
  `Order` у языка на HMI_RT_3), бросает исключение при чтении; оно попадает в `NotAvailable`. Запись: имя через точку, всё или ничего.
  Живьём на `HMI Unified/HMI_RT_3`: `MaxLoginErrors` 20→21→20, `EnableLockAfterNumberOfAttempts`, язык `Russian (Russia)`
  `Enable` вкл/выкл — записано и возвращено, проект сохранён. **Зависимость:** `MaxLoginErrors` отказывает, пока
  `EnableLockAfterNumberOfAttempts` выключен (причины Openness не сообщает) — отказ теперь поясняет это.
- **Системные теги:** отдельный инструмент `unified_get_system_tags` (12 тегов на RT_3, имя и тип), не параметр `unified_get_tags`.
- **Окно экрана:** `unified_manage_items` с `HmiScreenWindow` и `Screen` работает без правок кода; на временных экранах `MCPT_S1`
  и `MCPT_S2` окно создано, `Screen` и `ScreenName` прочитаны, экраны удалены. Описано в README.
