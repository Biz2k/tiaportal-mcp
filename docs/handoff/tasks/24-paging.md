# 24. Единый вид постраничных ответов

Исполнитель: Sonnet. Приоритет: средний. Объём: средний, однообразный. Риск: низкий — чтение.
Делать после задачи 21: она переводит `plc_get_cross_references` на тот же помощник.

## Цель

Любой инструмент, который может вернуть сотни записей, принимает `limit` и `offset` и
сообщает `total`, `offset`, `truncated`, `nextOffset` — одинаково везде.

## Что известно

- Общий помощник — `ModelContextProtocol/ListPage.cs`: `ListPage<T>.Of(all, limit, offset)`,
  `Note(narrowBy)`, `Meta(meta)`; предел по умолчанию 500.
- Уже на нём: `plc_get_blocks`, `plc_get_types`, `plc_get_tags`, `unified_get_tags`,
  `unified_get_system_tags`, `unified_get_alarms`.
- Свои признаки усечения: `plc_get_block_source` и `plc_find_in_code` (`Truncated`,
  `maxResults`), `unified_get_logging_tags`, `plc_get_cross_references`.

## Шаги

1. В `ListPage.Meta` добавить `nextOffset` (число, когда `truncated`; иначе ключа нет). В
   типизированных ответах, где есть поля `Total` / `Offset` / `Truncated`, добавить
   `NextOffset`. Тест — `Test25ResponseSize.cs`.
2. Перевести на `ListPage`: `unified_get_screens`, `unified_get_screen_items`,
   `unified_get_logging_tags`, `unified_get_text_lists`, `unified_get_graphic_lists`,
   `get_library_types`, `get_master_copies`, `hw_get_devices`. Перед каждым проверить на
   проверочном проекте, сколько записей он возвращает; инструмент, у которого больше 500 не
   бывает (группы экранов, соединения, классы алармов), не трогать.
3. `plc_find_in_code` и `plc_get_block_source` оставить со своими параметрами (там усечение
   по объёму текста, а не по числу записей), но имя признака в `meta` сделать тем же —
   `truncated`.
4. Описание параметров `limit` и `offset` — одной и той же фразой во всех инструментах.

## Готово, когда

- `tools/smoke.ps1` проходит; для каждого переведённого инструмента вживую прочитаны две
  страницы подряд, и вместе они дают `total`.
- Поведение по умолчанию не изменилось для ответов короче 500 записей.
- `docs/tools/*.md` пересобраны (`tools/make-tool-docs.ps1`), запись в `CHANGELOG.md`, строка
  в `../../PLAN.md` отмечена.

## Результат (06.10.2026)

Проверено вживую на проверочном проекте (`pages.py` — страницы подряд, сумма равна `total`, `nextOffset` равен `offset + размер`):
`unified_get_screens` 74 (30+30+14), `unified_get_screen_items` 25 (экран `0_Main`), `unified_get_logging_tags` 279 (100+100+79),
`unified_get_text_lists` 4, `hw_get_devices` 23, `get_library_types` 33, `get_master_copies` 4 (глобальная библиотека
`Test_mcp_Library`, по одной), `unified_get_graphic_lists` 1, а также прежние `plc_get_blocks` 102, `plc_get_types` 7,
`plc_get_tags` 2150, `unified_get_tags` 2556, `unified_get_system_tags` 12, `plc_get_cross_references` 7. `unified_get_alarms`
в проекте пуст — страницы вживую не прочитаны (код тот же). Больше 500 записей в проверочном проекте у переведённых
инструментов не бывает (больше всего у `unified_get_tags`), поэтому предел 500 вживую не срабатывал; страницы меньше предела
проверены явным `limit`.

- `ListPage.Meta` добавляет `nextOffset` только при усечении; `Paging` (`ListPage.cs`) держит одни слова для `limit` и `offset`
  и `Paging.Page`. Тесты `Test_2508`, `Test_2509`.
- `unified_get_logging_tags`: портал теперь отдаёт все записи, страница режется в инструменте; признак `Truncated` в ответе оставлен.
- **Найден дефект задачи 21/10:** `plc_get_types` клал `total`, `offset`, `truncated` только в сообщение, а не в `meta` (в задаче 10 это
  было записано как сделанное). Исправлено.
- Шаг 3: `plc_find_in_code` и `plc_get_block_source` уже клали `truncated` в `meta` — менять нечего.
- Не тронуты, как велено: группы экранов, соединения, классы алармов (больше 500 не бывает).
