# 32. Строки таблиц наблюдения

Исполнитель: Sonnet. Приоритет: высокий. Объём: средний. Риск для TIA Portal: средний — новый
вид объектов для записи, сначала проба.

## Зачем

Сервер создаёт пустую таблицу наблюдения (`plc_create_watch_table`) и читает строки
(`plc_get_watch_table_info`), но наполнить таблицу не может. В `README.md` это стоит в «Known
limitations».

## Что известно

Справочник — [`../api/plc-watch-tables.txt`](../api/plc-watch-tables.txt) (снят 07.10.2026).

- `PlcWatchTable.Entries` — `PlcTableCommentEntryComposition`; у неё только `Create()` без
  параметров, возвращает базовый `PlcTableCommentEntry`. Что именно создаётся, не проверено.
- У `PlcWatchTableEntry` свойства `Address`, `Name`, `DisplayFormat`, `MonitorTrigger`,
  `ModifyTrigger`, `ModifyValue`, `ModifyIntention` по отражению **только для чтения**; есть
  `Delete()`.
- `PlcWatchTableComposition.Import(FileInfo, ImportOptions)` и `PlcWatchTable.Export(...)` —
  SimaticML. Старые инструменты экспорта и импорта закомментированы в `McpServer.Tags.cs`
  (`export_xml_watch_table`, `import_watch_table`); операция `ExportXmlWatchTable` — в
  `Portal.Tags.cs`.
- Таблица принудительных значений одна на ПЛК, не создаётся и не удаляется. В задачу не входит.

## Что сделать

1. Проба в откатываемой транзакции на временной таблице `MCPT_Watch` в `PLC (A0)`, по одному
   вызову с отметкой `TRY` перед каждым:
   - что создаёт `Entries.Create()`, какого типа объект; есть ли у композиции
     `GetCreationInfos()` или создание типизированной строки через общий интерфейс;
   - принимает ли строка `SetAttribute` для `Address`, `Name`, `DisplayFormat` вопреки отражению;
   - `Delete()` строки и порядок оставшихся.
2. Если атрибуты не пишутся — путь через SimaticML: экспорт таблицы, правка XML, `Import` с
   `ImportOptions.Override` в ту же группу. Образец XML строки снять с таблицы проверочного
   проекта, в которой строки есть (`plc_get_watch_tables`). Проверить, что импорт сохраняет имя
   и место таблицы.
3. Инструмент `plc_manage_watch_table_entries(softwarePath, watchTablePath, actions)` по образцу
   `plc_manage_tag_table_entries` (`McpServer.Tags.cs`, `Portal.Tags.cs`). Действия: `add`
   (`name` или `address`, `displayFormat`, `monitorTrigger`, `modifyValue`, `comment`; `index` —
   куда вставить, по умолчанию в конец), `delete` (по `index` или `name`), `clear`. Всё или
   ничего; неизвестное поле — отказ; формат и триггер — по перечислениям, в отказе допустимые.
4. Имя проверять до записи: тег ПЛК или член блока данных существует. Принимает ли Openness
   несуществующее имя молча — выяснить пробой и записать в заголовок файла.
5. Читать обратно: после записи сравнить строки с заданным.
6. `plc_get_watch_table_info` должен отдавать все поля строки, которые пишет новый инструмент.
7. Модульные тесты на разбор действий; вызовы в `tools/smoke/write.json` (таблица `MCPT_`,
   строки, очистка, удаление); убрать пункт из «Known limitations» обоих README.

## Готово, когда

В таблице, созданной сервером, есть строки с заданными тегом, форматом и триггером; экспорт
таблицы из TIA Portal их содержит; «дымовой» прогон оставляет состав проекта прежним.

## Если не выходит

Ни атрибуты, ни импорт не дают строк — записать в таблицу «Что невозможно» плана с точными
ответами Openness и закрыть задачу так.
