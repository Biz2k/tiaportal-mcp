# 21. Кросс-ссылки всего ПЛК

Исполнитель: Sonnet. Приоритет: высокий, дефект (найден независимым тестированием).
Объём: небольшой. Риск для TIA Portal: низкий — только чтение.

## Проблема

`plc_get_cross_references` с пустым `objectPath` отвечает `NotSupported`
(«... does not provide cross references»), хотя описание инструмента и комментарий параметра
(`Empty targets the whole PLC software`) обещают для пустого пути весь ПЛК. Воспроизведено на
`PLC (A0)` и `А1.2`, в том числе с `objectKind: "block"`. По одному блоку (`objectPath: "Main"`)
инструмент работает.

## Что известно

- Код: `src/TiaMcpServer/Siemens/Software/Portal.Software.CrossReferences.cs`
  (`GetCrossReferences`, `GetCrossReferenceSources`, `ResolveCrossReferenceProvider`) и
  `ModelContextProtocol/Software/McpServer.Software.CrossReferences.cs`.
- Комментарий в начале файла утверждает, что `CrossReferenceService` есть у программы ПЛК.
  На деле `GetService<CrossReferenceService>()` у `PlcSoftware` возвращает `null` — как и у
  пользовательской группы блоков.
- Для группы блоков ответ уже собирается из её блоков с пояснением в `note`
  (`GetCrossReferenceSources`, ветка `provider is PlcBlockGroup`).

## Шаги

1. Пробой (`tools/openness-probe.ps1`, `-ReadOnly`) выяснить, у чего в корне служба есть:
   `PlcSoftware`, `BlockGroup` (системная корневая группа), `TypeGroup`, `TagTableGroup`,
   отдельные типы, таблицы тегов, теги. Исправить комментарий в начале файла по факту.
2. Для пустого `objectPath` собирать ответ из объектов ПЛК: блоки всех групп, типы всех групп,
   таблицы тегов. `objectKind` при пустом пути работает как фильтр вида (`block`, `type`,
   `tagTable`; `auto` — все).
3. Ответ на ПЛК в сотню блоков большой. Добавить `limit` / `offset` по объектам-источникам
   через `ListPage<T>` (`ModelContextProtocol/ListPage.cs`), с `total`, `offset`, `truncated`
   в `meta` и фразой в сообщении, как у `plc_get_blocks`. Действующие `maxDepth` и `Truncated`
   не ломать.
4. Если какой-то вид объектов службу не даёт — сказать это в `note` ответа, а не общим
   `NotSupported`. `NotSupported` оставить только для того, у чего ссылок не бывает (таблицы
   наблюдения, принудительные значения, внешние исходники), и поправить текст ошибки.
5. Привести описание инструмента и параметров в соответствие с тем, что получилось.

## Готово, когда

- Пустой `objectPath` на `PLC (A0)` возвращает первую страницу ссылок, `offset` читает дальше,
  сумма страниц равна `total`.
- `objectKind: "block"` с пустым путём возвращает только блоки.
- Ответ по одному блоку и по группе не изменился.
- Вызов добавлен в `tools/smoke/read.json`; `tools/smoke.ps1` проходит.
- Модульный тест на чистую часть (сборка страниц), запись в `CHANGELOG.md`, строка в
  `../PLAN.md` отмечена.
