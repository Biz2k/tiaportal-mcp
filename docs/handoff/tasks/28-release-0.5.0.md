# 28. Выпуск версии 0.5.0

Исполнитель: Sonnet. Приоритет: средний. Объём: небольшой. Риск для TIA Portal: нет.

**Последняя задача этапа 2**: после задач 21–27 и после этапа 1 Opus (`../../PLAN.md`).

## Что сделать

1. Версия сборки `0.4.0` → `0.5.0`: найти все места (`*.csproj`, `Program.cs` — имя и версия
   сервера в ответе `initialize`, `README`), проверить `doctor` / `get_state`.
2. `CHANGELOG.md`: раздел `[Unreleased]` закрыть как `[0.5.0] - <дата>`, сверху оставить
   пустой `[Unreleased]`. Раздел велик (сотни строк): содержание не сокращать, но в его начале
   собрать:
   - таблицу «было → стало» по всем переименованным и удалённым инструментам (`hmi_*` →
     `unified_*`; `get_devices` → `hw_get_devices`, `get_device_item_info` →
     `hw_get_device_item_info`, `get_hardware_topology` → `hw_get_topology`,
     `get_block_interface` → `plc_get_block_interface`, `get_plc_summary` → `plc_get_summary`;
     `unified_get_library_types` → `get_library_types`; `hmi_create_screen_item` → вошёл в
     `unified_manage_items`; удалённые подсказки `McpPrompts`). Сверить с `git log` и
     `docs/tools-list.txt`, а не с этим перечнем: он может быть неполон;
   - изменения поведения, заметные вызывающему: запись включена по умолчанию, `connect` не
     запускает TIA Portal сам, `connect` без параметров при нескольких экземплярах (задача 22),
     `save_as_project` отклоняет путь с расширением (задача 22).
3. Документы, где названо число инструментов и тестов (`README.md`, `README_ru.md`,
   `Implemented_Tools.md`, `context.md`, `../PLAN.md`), привести к фактическим числам;
   `docs/tools/` пересобрать (`tools/make-tool-docs.ps1`).
4. `tools/finish.ps1 -Install`, коммит `release: 0.5.0`, тег `v0.5.0`, `git push origin main
   --tags`. GitHub Releases не используются (`../decisions.md`).

## Готово, когда

`get_state` установленного сервера показывает 0.5.0; тег есть на GitHub; в `CHANGELOG.md`
таблица переименований полна (каждое старое имя из `git log -S` по `docs/tools-list.txt`
либо есть в таблице, либо существует сейчас).

## Результат (07.10.2026)

- Версия сборки 0.5.0 (`TiaMcpServer.csproj`; `initialize` берёт её из сборки). `get_state` и `doctor` получили `serverVersion`
  (`Test_2902`).
- `CHANGELOG.md`: `[Unreleased]` пуст, раздел `[0.5.0] - 2026-10-07` начинается блоком «Upgrading from 0.3.0»: изменения поведения и
  таблица «было → стало» по всем 99 инструментам 0.3.0 (составлена по исходникам коммита `9c1883c`, каждый новый адрес проверен по
  `docs/tools-list.txt`) и по `hmi_*` сборки 0.4.0. Остальное содержание не сокращалось.
- Числа приведены в `README.md`, `README_ru.md` (две таблицы «Доступны всегда» / «Не регистрируются с `--read-only`» сверены с
  сервером: 70 и 69), `Implemented_Tools.md`, `src/TiaMcpServer/README.md`, `context.md`, `PLAN.md`; `docs/tools/` пересобран.
- Выпуск: коммит `release: 0.5.0`, тег `v0.5.0`, `git push origin main --tags`.
