# 03. Архивы WinCC Unified: архивы данных, архивы алармов, архивные теги

Статус: **сделана 06.10.2026** (см. «Что выяснено» в конце).

Приоритет: высокий (запрошено Biz). Объём: большой — можно делить на три коммита.

## Цель

1. Архивы данных (`DataLogs`) и архивы алармов (`AlarmLogs`): чтение, создание, изменение,
   удаление.
2. Архивные теги (`HmiTag.LoggingTags`): что из тега пишется в какой архив и как.
3. Привязка тренда к архивному тегу — закрыть непроверенную ветку
   `unified_configure_trend_control`.

## Что известно

Справочники: `docs/handoff/api/unified-logging.txt`, `docs/handoff/api/unified-logging-tags.txt`.

Объектная модель полноценная, файлы не нужны.

- `HmiSoftware.DataLogs` / `AlarmLogs`: `Create(name)`, `Find(name)`; у архива `Name*`,
  `Delete()`, и три вложенных объекта:
  - `Settings`: `LogMaxSize*`, `StorageDevice*` (перечисление `DeviceNode`: `Default`, `Local`,
    `SDX51`, `USBX61`, `USBX62`...), `StorageFolder*`, `LogTimePeriod` (`Days*`, `Hours*`,
    `Minutes*`, `Seconds*`, `Ticks*`, метод `SetLogDuration(...)`);
  - `Segment`: `SegmentMaxSize*`, `SegmentStartTime*`, `SegmentTimePeriod` (то же устройство);
  - `Backup`: `BackupMode*` (`NoBackup`, `PrimaryPath`), `PrimaryPath*`.
- `HmiSoftware.AuditTrails` — только чтение (нет `Create`).
- `HmiTag.LoggingTags`: `Create(name)`, `Find(name)`; у архивного тега `Name*`, `DataLog*`
  (имя архива), `LoggingMode*` (`Cyclic`, `OnDemand`, `OnChange`), `Cycle*`, `CycleFactor*`,
  `AggregationMode*`, `SmoothingMode*` и параметры сглаживания, `LimitScope*`, `HighLimit*`,
  `LowLimit*`, `TriggerMode*`, `TriggerTag*`, `TriggerTagBitNumber*`, `Source*`, `Delete()`.

Проверено чтением 06.10.2026 (типизированные свойства, TIA не падал):

- панель: 1 архив данных `Data log_1` — `LogMaxSize=12000`, `StorageDevice=USBX61`, период 7
  дней, сегмент 100 / 1 день, `BackupMode=NoBackup`; архивных тегов нет;
- ПК-станция: 10 архивов данных, 3 архива алармов.
- В откатываемой транзакции `DataLogs.Create('MCPT_Log')`, `Tags.Create`, затем
  `LoggingTags.Create('...')` и `DataLog = 'MCPT_Log'` прошли без ошибки.

Не проверено: `GetAttributeInfos` на этих объектах — и не нужно, работать через
типизированные свойства (`FindTypedProperty` / `SetTypedProperty` из `Portal.Unified.Alarms.cs`).

## Шаги

1. Проба чтения всех типизированных свойств архивного тега и архива алармов на ПК-станции
   (там они есть): по одному свойству, как в `Show-Typed`.
2. Проба записи в откатываемой транзакции: каждое свойство `Settings`/`Segment`/`Backup`;
   длительности — через свойства `Days`.. и через `SetLogDuration`; `StorageDevice` значениями,
   не подходящими устройству (панель и ПК различаются). Принимает ли Openness несуществующее
   имя архива в `LoggingTag.DataLog` — скорее всего да; тогда проверять самому.
3. Инструменты:
   - `unified_get_logs(softwarePath, type = "")` — архивы данных и алармов с настройками;
   - `unified_manage_logs(softwarePath, actions)` — `type`: `data` | `alarm`; свойства
     вложенных объектов задавать точечной записью, как у классов алармов:
     `"Settings.LogMaxSize": 5000`, `"Segment.SegmentMaxSize": 100`, а длительность —
     понятной формой, например `"Settings.LogTimePeriod": {"days": 7}`;
   - архивные теги: либо отдельный `unified_manage_logging_tags`, либо поле `loggingTags` в
     действии `unified_manage_tags`. Отдельный инструмент проще описать и проверить;
   - чтение архивных тегов: фильтр по архиву и по тегу — тегов тысячи.
4. Удаление архива, на который ссылаются архивные теги: выяснить, что происходит, и сообщать
   в `Notes`, как при удалении соединения с тегами.
5. Тренд из архива: в `unified_configure_trend_control` параметр `dataSource` сейчас проверен
   только с HMI-тегом. Выяснить, как Unified записывает источник «архивный тег»
   (`DataSourceY` — посмотреть на тренде, настроенном вручную, или в
   `docs/handoff/api/unified-parts.txt` по слову `Trend`), и поддержать.

## Готово, когда

- Архив данных и архив алармов создаются, читаются, изменяются, удаляются — на панели и ПК.
- Архивный тег создаётся на теге, привязывается к архиву, читается, удаляется.
- Тренд показывает архивный тег.
- Ошибочные случаи: неизвестный архив, неизвестное значение перечисления, устройство хранения,
  не подходящее HMI.

## Риски

- `SegmentStartTime` — `DateTime`; `ConvertHmiValue` этот тип не знает, добавить разбор
  ISO-строки и тест.
- `AggregationDelay`, `SmoothingMinTime`, `SmoothingMaxTime` — `TimeSpan`, то же самое.
- На ПК-станции архивы рабочие — свои объекты только с префиксом `MCPT_`.

## Что выяснено

Инструменты: `unified_get_logs`, `unified_manage_logs`, `unified_get_logging_tags`,
`unified_manage_logging_tags`; тренд — проверка источника в `unified_configure_trend_control`. Находки про
Openness — в заголовке `Portal.Unified.Logs.cs`.

- Имена архивов уникальны среди архивов данных И алармов. Журналы аудита только читаются.
- Длительности пишутся целиком через `SetLogDuration` / `SetSegmentDuration`; `SegmentStartTime` — ISO.
- `StorageDevice`: новый архив на панели получает `SDX51`, который панель сама отвергает; принимается только носитель
  уже существующих архивов (`USBX61`). ПК-станция принимает `Default` и `Local`. Сообщения Openness содержат
  внутренний текст `ResourceID` — сервер его заменяет.
- Переименование архива переименовывает `DataLog` у архивных тегов; **удаление не трогает их** — ссылки остаются
  висеть (сообщается в `Notes`).
- Архивный тег: имя уникально в пределах HMI-тега; `DataLog`, `TriggerTag` проверяются сервером заранее (иначе
  исключение Openness); `SmoothingMinTime` не больше `SmoothingMaxTime` (максимум ставится первым).
- Тренд читает архивный тег как источник `<HMI-тег>:<архивный тег>` (так настроены тренды ПК-станции).
- **`Cycle`** — имя цикла проекта: `T100ms`, `T250ms`, `T500ms`, `T1s`, `T2s`, `T5s`, `T10s` принимаются (регистр не
  важен), `T30s`, `T1min` и обычное время — нет. В режиме `Cyclic` всё быстрее 500 мс отвергается («LoggingCycleConsistency
  check failed») и на панели, и на ПК-станции; в других режимах короткие значения принимаются. Образцы Biz (06.10.2026):
  `AI_DB_CP10-ШК-U1/U2` Cyclic `T5s`/`T500ms`, `U10..U12` OnDemand с триггерами `xReset`, `"HMI_Analog_Valves_VM-16".CMD_Mode`,
  `HMI_Pumps_CP_1.CMD_Start_Anti_Cond`.
- **Архивные теги структурных тегов висят на их членах** (`HmiTag.Members`, рекурсивно): у `AI_DB_CP10-ШК-U1` нет своих, у
  члена `field_input_EUF` есть, с именем по самому тегу. Первая версия смотрела только верхний уровень и видела 0 из 279.
  Исправлено: `tagName` — путь `Тег.Член`, источник тренда — `Тег.Член:АрхивныйТег`, `TriggerTag` — путь (в кавычках для
  имён со спецсимволами).
- Проверено вживую на панели (архив, архивный тег, тренд, ошибки) и на ПК-станции (архивы данных и алармов, чтение
  176 архивных тегов). Архивные теги на ПК-станции не создавались. Запись архивных тегов проверена на члене `AI_DB_CP10-ШК-U3.field_input` (создание, Cyclic/OnDemand, триггеры, тренд, удаление).
