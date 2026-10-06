# 07. Остальные объекты WinCC Unified

Приоритет: низкий, по запросу Biz. Объём: каждый пункт небольшой и независим.

Набор `unified_*` покрывает экраны, элементы, события, фейсплейты, тренды, теги, таблицы
тегов, соединения, алармы, классы алармов, списки. Скрипты — задача 02, архивы — задача 03.
Ниже остаток по убыванию пользы. Перед началом любого пункта спросить Biz, нужен ли он.

## Пункты

- [ ] **Настройки runtime** — `HmiSoftware.RuntimeSettings`
      (`docs/handoff/api/unified-runtime-settings.txt`): языки и шрифты, OPC UA-сервер,
      блокировка входа, исключительное управление. Один объект с вложенными; подойдёт пара
      `unified_get_runtime_settings` / `unified_set_runtime_settings` с точечной записью
      свойств, как у классов алармов.
- [ ] **Системные теги** — `HmiSoftware.SystemTags`, только чтение (`Name`, `DataType`).
      Проще всего — параметр `includeSystem` у `unified_get_tags`.
- [ ] **OPC UA-алармы** — `HmiSoftware.OpcUaAlarmTypes`
      (`docs/handoff/api/unified-opcua-alarms.txt`): `Create(nodeId, connection, name)`.
      Нужны OPC UA-соединение и сервер с алармами — в тестовом проекте их нет.
- [ ] **Аудит** — `HmiAlarmAuditClass`, `AuditTrails` (`docs/handoff/api/unified-audit.txt`).
      Нужен GMP-проект; без запроса не делать.
- [ ] **Объекты установки** — `PlantObjectTags` (`docs/handoff/api/unified-plant-objects.txt`),
      технологическая иерархия. Без запроса не делать.
- [ ] **Окна экранов и всплывающие окна** — `HmiScreenWindow`
      (`docs/handoff/api/unified-screens.txt`) создаётся как обычный элемент экрана через
      `unified_manage_items` (`itemType: "HmiScreenWindow"`, свойство `Screen`). Проверить и
      описать в `README.md`; нового инструмента не нужно.

## Общее

Все новые виды объектов — через типизированные свойства и с пробой
(`docs/handoff/context.md`, правило 2). Образцы: `Portal.Unified.Alarms.cs` (объект с
вложенными объектами состояний), `Portal.Unified.Tags.cs` (пакетная операция).
