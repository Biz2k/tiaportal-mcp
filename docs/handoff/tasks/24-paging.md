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
