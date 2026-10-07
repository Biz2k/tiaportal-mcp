# MCP-сервер для TIA Portal

[English](README.md) | **Русский**

> Документ доступен на двух языках. Переключить язык можно по ссылкам выше.

MCP-сервер, через который ИИ-ассистент работает с Siemens TIA Portal по интерфейсу Openness:
читает проект, изменяет программу ПЛК, оборудование и HMI, выполняет загрузку в ПЛК.

## Возможности

- Подключение к запущенному TIA Portal, открытие проекта или локальной сессии
  многопользовательского проекта
- Чтение программы ПЛК: блоки, типы данных, теги и константы, таблицы наблюдения и
  принудительных значений, внешние исходные файлы, перекрёстные ссылки, исходный текст блоков
  и типов
- Создание, переименование, удаление, копирование, перемещение, импорт и компиляция объектов ПЛК
- Чтение аппаратной топологии, создание устройств, установка модулей, построение подсетей и
  систем PROFINET IO
- Чтение и изменение WinCC Unified: экраны, элементы экранов, события, теги, тренды, фейсплейты
- Загрузка конфигурации оборудования и программы в ПЛК или симулятор

Запуск PLCSIM и управление им намеренно не входят в этот сервер: для этого есть отдельный
MCP-сервер [plcsim-mcp](https://github.com/Biz2k/plcsim-mcp). Инструмент `download_to_plc`
рассчитывает, что цель загрузки уже запущена.

## Требования

- Windows с **.NET Framework 4.8**
- **Siemens TIA Portal V21**, установленный и **запущенный** (более ранние версии выбираются
  параметром `--tia-major-version`)
- Пользователь Windows входит в группу `Siemens TIA Openness`
- Пользовательская переменная среды `TiaPortalLocation` указывает на каталог установки, например
  `C:\Program Files\Siemens\Automation\Portal V21`

Проверить всё это можно, не запуская MCP-сервер:

```text
> TiaMcpServer.exe --doctor
Diagnose:
├─ Connected = False
├─ Project: No project open
├─ Active Version: V21
├─ Installed TIA Portal versions:
│  └─ V21: C:\Program Files\Siemens\Automation\Portal V21
│     ├─ Engineering: OK
│     └─ Portal:      OK
├─ User in 'Siemens TIA Openness' user group: True
└─ Write mode: enabled
```

Тот же отчёт клиент MCP получает инструментом `doctor`. Оба варианта только читают: они не
подключаются к TIA Portal, не открывают проект и не меняют членство в группах.

## Установка

Готовая сборка лежит в [`Install/TiaMcpServer`](Install/TiaMcpServer). Скопируйте папку в любое
место и укажите клиенту MCP путь к `TiaMcpServer.exe`. Пошаговые инструкции для Claude Code,
Claude Desktop и клиентов на базе VS Code — в [`Install/INSTALL_RU.md`](Install/INSTALL_RU.md)
([на английском](Install/INSTALL.md)).

Claude Code:

```bash
claude mcp add tia-mcp-server -- C:\path\to\TiaMcpServer\TiaMcpServer.exe
```

Клиенты с настройкой через JSON (в Claude Desktop раздел называется `mcpServers`, в VS Code —
`servers`):

```json
{
  "mcpServers": {
    "tia-mcp-server": {
      "command": "C:\\path\\to\\TiaMcpServer\\TiaMcpServer.exe",
      "args": []
    }
  }
}
```

Когда новая сборка впервые подключается к TIA Portal, он спрашивает, разрешить ли доступ
Openness. Подтвердите запрос в окне TIA Portal.

## Быстрый старт

1. Запустите TIA Portal. Сервер подключается к работающему экземпляру и сам его не запускает.
2. Вызовите `open_tia_project` с абсолютным путём к проекту `.apXX` или сессии `.alsXX`.
   Инструмент подключится, откроет проект и вернёт пути к программам ПЛК.
3. Осмотритесь с помощью `get_project_tree`, `plc_get_software_tree` и `hw_get_devices`.
4. Читайте и изменяйте объекты инструментами `plc_*`, `hw_*`, `net_*` и `unified_*`.
5. Сохраните изменения вызовом `save_project`. До этого они существуют только в памяти.

## Параметры командной строки

| Параметр                  | Назначение                                                                      |
| ------------------------- | ------------------------------------------------------------------------------- |
| `--tia-major-version <n>` | Версия TIA Portal, с которой работает сервер. По умолчанию `21`.                |
| `--read-only`             | Не регистрировать инструменты, изменяющие проект. См. ниже.                     |
| `--logging <1\|2\|3>`     | `1` — stderr, `2` — отладочный вывод, `3` — журнал событий Windows. Без параметра журнал не ведётся. |
| `--doctor`                | Вывести отчёт об окружении и завершиться, не запуская MCP-сервер.               |
| `--debug-tools`           | Зарегистрировать инструменты разработки сервера (`unified_debug_*`).                    |
| `--tools <области>`       | Зарегистрировать только названные области инструментов, например `--tools plc,unified`. См. ниже. |
| `--allow-write`           | Принимается для старых конфигураций; запись включена по умолчанию, параметр ничего не меняет. |

## Области инструментов

Сервер регистрирует около 140 инструментов, и клиент кладёт описание каждого в контекст своей модели.
`--tools plc,unified` регистрирует только названные области, через запятую без пробелов; без флага или с
`--tools all` регистрируются все. Неизвестная область останавливает запуск: в stderr печатается перечень допустимых,
код выхода 2.

| Область    | Инструменты области                                                                            | Число |
| ---------- | ---------------------------------------------------------------------------------------------- | ----- |
| `plc`      | `plc_*`                                                                                        | 66    |
| `hw`       | `hw_*` и `net_*`: устройства, оборудование, подсети, соединения                                | 16    |
| `unified`  | `unified_*`                                                                                    | 35    |
| `library`  | `get_libraries`, `get_library_types`, `get_master_copies`, `instantiate_master_copy`, `open_global_library` | 5 |
| `transfer` | `export_objects`, `import_objects`, `preview_import`                                           | 3     |
| `download` | `download_to_plc`, `get_download_targets`                                                      | 2     |

Всегда регистрируются, что бы ни стояло в `--tools` (12): `connect`, `disconnect`, `get_state`, `get_tia_instances`,
`doctor`, `open_tia_project`, `open_project`, `get_project`, `save_project`, `save_as_project`, `close_project`,
`get_project_tree`. В числах учтены инструменты записи; `--read-only` убирает часть из них из любой области.
Инструкция сервера называет зарегистрированные области, когда это не все, а `get_state` и `doctor` сообщают их как
`toolAreas`. Пример настройки — в `samples/`.

## Режим записи

Инструменты, изменяющие проект, доступны по умолчанию. Чтобы их убрать, запустите сервер с
параметром `--read-only`.

- С `--read-only` 69 изменяющих инструментов **не регистрируются вообще** и не появляются в
  `tools/list`. Модель не может вызвать то, чего не видит.
- Без параметра они регистрируются с пометкой `destructiveHint: true`, так что клиент может
  запрашивать подтверждение перед каждым вызовом.
- `get_state` и отчёт `--doctor` показывают `allowWrite`: клиент может понять, что инструментов
  нет из-за настройки, а не из-за версии.
- Операции записи меняют проект **только в памяти**. Об этом говорит каждый ответ на запись;
  изменения сохраняет `save_project` (для локальной сессии он сохраняет сессию).
- Каждая запись выполняется в транзакции TIA Portal, если он её предоставляет: неудачная запись
  откатывается, удачная становится одной записью в истории отмены.

`export_objects` и `plc_generate_sources` доступны всегда: они только пишут файлы на машине, где
работает сервер, и не изменяют проект.

## Инструменты

Точный список имён инструментов — в [`docs/tools-list.txt`](docs/tools-list.txt); тест падает,
если зарегистрированные инструменты расходятся с этим файлом. По умолчанию регистрируется
143 инструментов (72 с `--read-only`). Краткое описание каждого — в [`Implemented_Tools.md`](Implemented_Tools.md).

Доступны всегда (71):

| Область                          | Инструменты |
| -------------------------------- | ----------- |
| Подключение и состояние          | `connect`, `disconnect`, `get_state`, `get_tia_instances`, `doctor` |
| Проект и сессия                  | `open_tia_project`, `open_project`, `get_project`, `save_project`, `save_as_project`, `close_project` |
| Структура проекта                | `get_project_tree`, `hw_get_devices`, `hw_get_device_info`, `hw_get_device_item_info`, `hw_get_topology`, `hw_search_catalog`, `net_get_connections` |
| Программа ПЛК                    | `plc_get_summary`, `plc_get_software_info`, `plc_get_software_tree` |
| Блоки                            | `plc_get_blocks`, `plc_get_blocks_hierarchy`, `plc_get_block_info`, `plc_get_block_data`, `plc_get_block_interface`, `plc_get_block_source`, `plc_get_lad_networks`, `plc_get_lad_instructions` |
| Типы данных                      | `plc_get_types`, `plc_get_type_info`, `plc_get_type_source` |
| Теги и константы                 | `plc_get_tag_tables`, `plc_get_tag_table_info`, `plc_get_tags`, `plc_get_tag_info`, `plc_get_constants` |
| Таблицы наблюдения               | `plc_get_watch_tables`, `plc_get_watch_table_info`, `plc_get_force_tables` |
| Внешние исходные файлы           | `plc_get_external_sources`, `plc_get_external_source_info`, `plc_generate_sources` |
| Поиск и ссылки                   | `plc_resolve_object_path`, `plc_find_in_code`, `plc_where_used`, `plc_get_cross_references` |
| Экспорт и предпросмотр           | `export_objects`, `preview_import` |
| Библиотеки                       | `get_libraries`, `open_global_library`, `get_master_copies`, `get_library_types` |
| WinCC Unified                    | `unified_get_screens`, `unified_get_screen_groups`, `unified_get_scripts`, `unified_get_tag_table_groups`, `unified_get_logs`, `unified_get_logging_tags`, `unified_get_screen_items`, `unified_get_screen_item_properties`, `unified_get_tags`, `unified_get_tag_tables`, `unified_get_connections`, `unified_get_alarms`, `unified_get_alarm_classes`, `unified_get_text_lists`, `unified_get_graphic_lists`, `unified_get_runtime_settings`, `unified_get_system_tags` |
| Загрузка                         | `get_download_targets` |

Не регистрируются с `--read-only` (72):

| Область                          | Инструменты |
| -------------------------------- | ----------- |
| Импорт                           | `import_objects`, `instantiate_master_copy` |
| Группы блоков и типов            | `plc_create_block_group`, `plc_delete_block_group`, `plc_create_type_group`, `plc_delete_type_group` |
| Блоки                            | `plc_create_fb`, `plc_create_instance_db`, `plc_create_scl_block`, `plc_replace_source`, `plc_create_lad_block`, `plc_manage_lad_networks`, `plc_rename_block`, `plc_delete_block`, `plc_copy_block`, `plc_move_block`, `plc_compile_block`, `plc_compile_software` |
| Типы данных                      | `plc_rename_type`, `plc_delete_type`, `plc_copy_type`, `plc_move_type` |
| Таблицы тегов                    | `plc_create_tag_table`, `plc_rename_tag_table`, `plc_delete_tag_table`, `plc_create_tag_table_group`, `plc_delete_tag_table_group` |
| Теги и константы                 | `plc_create_tag`, `plc_update_tag`, `plc_delete_tag`, `plc_create_user_constant`, `plc_update_user_constant`, `plc_delete_user_constant`, `plc_manage_tag_table_entries` |
| Таблицы наблюдения               | `plc_create_watch_table`, `plc_rename_watch_table`, `plc_delete_watch_table`, `plc_create_watch_table_group`, `plc_delete_watch_table_group` |
| Внешние исходные файлы           | `plc_create_external_source`, `plc_delete_external_source`, `plc_create_external_source_group`, `plc_delete_external_source_group` |
| Оборудование                     | `hw_create_device`, `hw_plug_module`, `hw_delete_device` |
| Сеть                             | `net_connect_subnet`, `net_disconnect_subnet`, `net_create_io_system`, `net_connect_to_io_system`, `net_create_connection`, `net_delete_connection`, `net_delete_subnet` |
| WinCC Unified                    | `unified_create_screen`, `unified_delete_screen`, `unified_manage_items`, `unified_manage_faceplate`, `unified_compile`, `unified_configure_trend_control`, `unified_manage_tags`, `unified_manage_tag_tables`, `unified_manage_screen_groups`, `unified_manage_scripts`, `unified_manage_tag_table_groups`, `unified_manage_logs`, `unified_manage_logging_tags`, `unified_manage_connections`, `unified_manage_alarms`, `unified_manage_alarm_classes`, `unified_manage_lists`, `unified_set_runtime_settings` |
| Загрузка                         | `download_to_plc` |

`plc_get_software_tree` принимает параметр `sections` — любое подмножество
`blocks,types,tags,watch,sources` через запятую, по умолчанию `all`, — чтобы ответ оставался
небольшим на крупном ПЛК. С той же целью `plc_get_cross_references` принимает `maxDepth` (1–3,
по умолчанию 1).

## Пути

Пути указываются **от корня области**: `1_Tests/FC_Block_1`, а не
`Program blocks/1_Tests/FC_Block_1`. Узнать их можно через `get_project_tree` и
`plc_get_software_tree`; `plc_resolve_object_path` превращает имя объекта в путь.

TIA Portal допускает `/` внутри имени (группа блоков `Inputs/Outputs`, станция
`S7-1500/ET200MP station_1`). В пути такая косая черта записывается как `%2F`:
`Inputs%2FOutputs/AI_Handler`. В этой форме пути возвращают все листинги. Неэкранированная форма
`Inputs/Outputs/AI_Handler` тоже принимается; если существуют и группа `Inputs/Outputs`, и группа
`Inputs` с подгруппой `Outputs`, неэкранированная форма означает вложенную.

Устройство находится по пути из `hw_get_devices`, по имени в Openness или по имени его CPU, как оно
показано в дереве проекта (`PLC_1`). Если имя подходит нескольким устройствам, вызов отклоняется
со списком подходящих путей.

## Оборудование и сеть

- `hw_search_catalog` ищет по артикулу или названию идентификаторы типов, которые нужны
  `hw_create_device` и `hw_plug_module`.
- `hw_create_device` с идентификатором `OrderNumber:` или `GSD:` создаёт станцию вокруг этого
  головного модуля. С идентификатором `System:Device.` создаётся пустая станция: добавьте стойку
  через `hw_plug_module` с пустым `parentItemName`, затем установите головной модуль в стойку.
- Система PROFINET IO строится в фиксированном порядке, и каждый шаг отказывается выполняться
  раньше предыдущего: `net_connect_subnet` (интерфейс ПЛК) → `net_create_io_system` →
  `net_connect_subnet` (интерфейс IO-устройства, та же подсеть) → `net_connect_to_io_system`.

## Типы библиотеки

`get_library_types` перечисляет типы библиотеки проекта (или открытой глобальной библиотеки) с
версиями и сообщает, к какой системе относится каждый:

| `system`    | Значение                                                                         |
|-------------|----------------------------------------------------------------------------------|
| `unified`   | Тип WinCC Unified: фейсплейт, скрипт и подобное                                  |
| `classic`   | Тип WinCC Comfort / Advanced / Professional, например фейсплейт                  |
| `plc`       | Тип ПЛК: блок или тип данных                                                     |
| `universal` | Не привязан к системе, например иконки и рисунки                                 |

Параметр `system` оставляет типы только одной системы. В Openness нет атрибута, который разделяет
Comfort, Advanced и Professional, поэтому все три сообщаются вместе как `classic`. Тип, который
приходит как общий библиотечный тип, считается `unified`, если у него указана минимальная версия
устройства, и `universal`, если не указана.

## WinCC Unified

Инструменты `unified_*` работают с WinCC Unified — и на панели Unified, и на ПК-станции Unified.
`softwarePath` — имя устройства и элемент runtime, например `HMI_1/HMI_RT_1`.

### Элементы экранов

`unified_manage_items` создаёт, изменяет, создаёт-или-изменяет (`upsert`) и удаляет элементы на
экранах, сразу несколько за вызов. Каждому свойству задаётся либо статическое значение, либо
динамизация, а `events` назначает скрипты событиям:

```json
{
  "softwarePath": "HMI_1/HMI_RT_1",
  "actions": [
    {
      "action": "upsert", "screenName": "Screen_1", "itemName": "Speed", "itemType": "HmiIOField",
      "properties": {
        "Left": 100, "Width": 200,
        "IOFieldType": "Output",
        "BackColor": "#FFFFFF",
        "ProcessValue": { "tag": "Pump1_Speed" }
      }
    },
    {
      "action": "upsert", "screenName": "Screen_1", "itemName": "Start", "itemType": "HmiButton",
      "properties": { "Text": "Start" },
      "events": { "Tapped": "HMIRuntime.Tags.SysFct.SetTagValue('Pump1_Start', 1);" }
    },
    { "action": "delete", "screenName": "Screen_1", "itemName": "Old_Label" }
  ]
}
```

- Простое значение — это статическое значение того типа, который у свойства: число, логическое,
  строка, член перечисления по имени, цвет в виде `#RRGGBB` или по названию.
- `{ "resourceList": "...", "tag": "..." }` показывает запись текстового или графического списка,
  соответствующую тегу (см. ниже).
- `{ "tag": "..." }` привязывает свойство к HMI-тегу, `{ "script": "..." }` задаёт динамизацию
  скриптом, `{ "dynamization": "none" }` убирает динамизацию.
- Привязка к тегу принимает рядом с `tag` параметры: `"readOnly"`, `"indirect"` (тег типа String с именем читаемого
  тега) и либо `"formula"` (`"'Tag_1'*2+1"`), либо `"mapping"`: `{"type": "range", "entries": [{"from": 0, "to": 30,
  "value": "#00FF00"}, {"from": 31, "to": 70, "value": "Yellow", "flashing": true, "rate": "Fast", "alternate": "#808080"}]}`,
  `{"type": "singlebit", "entries": [{"bit": 0, "value": "Red"}, {"bit": 1, "value": "Green"}]}` или `{"type": "none"}`. Каждой строке диапазона нужны `from` и `to`: строки «до значения» и «от значения» создать
  нельзя (Openness отдаёт тип диапазона строки только для чтения) — для открытого конца берите очень большое `to`.
- Скрипт принимает `"async"`, `"globalDefinitions"` (область одна на все скрипт-динамизации экрана) и `"trigger"`:
  `"T1s"` (от `T100ms` до `T10s`), `"AutomaticTags"`, `"Disabled"`, `{"type": "Tags", "tags": ["Tag_1"]}` или
  `{"type": "CustomCycle", "cycle": "Имя цикла"}`. Тип можно не указывать: `{"tags": [...]}` — это триггер `Tags`, `{"cycle": "..."}` — `CustomCycle`.
- `{ "expression": "формула" }` задаёт свойству выражение, `{ "flashing": { "condition": "Always", "rate": "Fast",
  "color": "#FF0000", "alternateColor": "#0000FF" } }` включает мигание у свойства-цвета.
- После записи объект проверяет сам TIA Portal (`Validate()`): несуществующие экран, рисунок, тег, соединение,
  список или цикл, начальное значение не по типу данных и скрипт-динамизация, которую не запускает ни один тег,
  завершают действие ошибкой, хотя Openness сохранил их без сообщения. То, что было не в порядке до записи,
  возвращается в `notes`. В формуле проверяются теги (`'Tag_1'` в одинарных кавычках); синтаксис формулы и
  скрипта не проверяется — такие ошибки с экраном и элементом сообщает `unified_compile` (компиляция HMI).
- `events` принимает и `{ "script": "...", "async": true, "globalDefinitions": "..." }` на каждое событие; `propertyEvents`
  задаёт скрипт на изменение свойства (`{ "ProcessValue": "..." }`, `"ProcessValue.QualityCodeChange"` для кода качества,
  нужна привязка к тегу).
- Текст задаётся обычной строкой и сохраняется в формате WinCC Unified; строка задаёт текст для
  всех языков проекта, `{ "texts": { "en-US": "..." } }` — для отдельных.
- `events` сопоставляет имени события его скрипт; пустой скрипт удаляет обработчик. На
  неизвестное имя события в ответ перечисляются события этого элемента. Пустое `itemName`
  обращается к самому экрану.
- Набор типов элементов зависит от устройства: на ПК-станции, например, нет `HmiText`, и
  TIA Portal об этом сообщает.
- Вызов применяет **все действия или ни одного**. Если одно не удалось, ошибка называет действие
  и свойство, а проект остаётся прежним.

`unified_configure_trend_control` добавляет тренд в `HmiTrendControl` и привязывает его к
источнику данных. `HmiTrendCompanion` — обычный элемент: его свойство `SourceTrendControl`
называет элемент тренда.

### Теги, таблицы тегов и соединения

`unified_manage_tags` создаёт, изменяет, создаёт-или-изменяет и удаляет HMI-теги, сразу несколько
за вызов:

```json
{
  "softwarePath": "HMI_1/HMI_RT_1",
  "actions": [
    { "action": "create", "tagName": "Setpoint", "tagTable": "Internal",
      "properties": { "DataType": "Real", "InitialValue": 1.5, "Persistent": true } },
    { "action": "upsert", "tagName": "Pump1", "tagTable": "Pumps",
      "properties": { "Connection": "HMI_Connection_1", "PlcTag": "HMI.Pumps.CP_1" } },
    { "action": "upsert", "tagName": "Level",
      "properties": { "Connection": "HMI_Connection_1", "AccessMode": "AbsoluteAccess",
                      "DataType": "Int", "Address": "%MW100" } },
    { "action": "delete", "tagName": "Old_Tag" }
  ]
}
```

- Новый тег — внутренний, типа `Int`, в таблице тегов по умолчанию, если `tagTable` и
  `properties` не задают иное. Пустое `Connection` снова делает тег внутренним.
- Символьному тегу ПЛК нужны `Connection` и `PlcTag`; тип данных берётся от тега ПЛК.
  Абсолютному — `Connection`, `AccessMode`, `DataType` и `Address`. Порядок, в котором вы их
  пишете, не важен.
- `Name` переименовывает тег. `Comment` принимает строку или `{ "en-US": "..." }`.
- `DisplayName` отклоняется: его запись через Openness закрывает TIA Portal.
- Тег нельзя перенести в другую таблицу; удалите его и создайте там.

`unified_manage_tag_tables` создаёт, переименовывает и удаляет таблицы тегов. Удаление таблицы
удаляет её теги; таблицу по умолчанию удалить нельзя.

`unified_manage_screen_groups` создаёт, переименовывает и удаляет группы экранов (группа в группе
записывается `Родитель/Дочерняя`); удаление группы удаляет её экраны. `unified_create_screen`
принимает `group`, `unified_get_screens` возвращает группу каждого экрана и умеет фильтровать по
ней. Имена экранов уникальны во всём HMI, поэтому остальные инструменты находят экран по имени.

`unified_get_scripts` читает глобальные модули скриптов (глобальные определения, функции, список
экспортируемых функций); `unified_manage_scripts` создаёт или заменяет модуль **целиком** и читает его обратно.
TIA Portal часть кода, который не может выполнить, сохраняет искажённым (параметр по умолчанию
`f(a, b = 5)` превращается в `b___5`), поэтому такая запись отклоняется и откатывается. Обычную синтаксическую
ошибку (`return a *;`) он сохраняет как есть, и сервер её не видит: её с строкой и столбцом показывает
`unified_compile` с `pathFilter` `Scripts/<модуль>`. Модули нельзя удалить или переименовать через Openness.

`unified_get_logs` / `unified_manage_logs` читают и меняют архивы данных и архивы алармов (размер, длительность,
устройство хранения, сегмент, резервирование; настройки по именам вида `Settings.LogMaxSize`); журналы аудита
только читаются. `unified_get_logging_tags` / `unified_manage_logging_tags` записывают HMI-теги в архивы данных.
Тренд показывает архивный тег, если источник данных в `unified_configure_trend_control` — `<тег процесса>:<архивный тег>`.
У структурного тега архивные теги висят на его членах, поэтому тег процесса — путь вида
`AI_DB_CP10-U1.field_input_EUF`. Удаление архива оставляет архивные теги ссылаться на него (Openness их не трогает);
переименование архива переименовывает его и в них. `Cycle` называется `T500ms`, `T1s`, `T2s`, `T5s`, `T10s`;
циклический архивный тег не может быть быстрее 500 мс.

`unified_manage_connections` создаёт, изменяет и удаляет соединения. С параметром `partner` —
путём к ПЛК проекта — новое соединение получается **интегрированным**: HMI-теги на нём
ссылаются на теги ПЛК по именам:

```json
{
  "softwarePath": "HMI_1/HMI_RT_1",
  "actions": [
    { "action": "create", "connectionName": "HMI_Connection_1", "partner": "PLC_1" }
  ]
}
```

- У HMI и ПЛК должны быть интерфейсы в общей подсети (`net_connect_subnet`). Инструмент берёт
  первую такую пару; `localInterface` и `partnerInterface` выбирают другие.
- Без `partner` соединение неинтегрированное: задайте `CommunicationDriver` в `properties` и
  адрес в `driverProperties`, например `{ "Protocol.RemStAddress": "192.168.0.10" }` —
  параметры перечисляет `unified_get_connections`. Теги на нём используют абсолютные адреса.
- `properties` принимает также `Comment`, `DisabledAtStartup` и `Name`.
- Партнёра у существующего соединения изменить нельзя; удалите его и создайте заново.

Все три инструмента применяют все действия или ни одного. Тег или соединение, которые ещё
используются, TIA Portal удаляет без предупреждения.

### Алармы

`unified_manage_alarms` создаёт, изменяет, создаёт-или-изменяет и удаляет дискретные и
аналоговые алармы:

```json
{
  "softwarePath": "HMI_1/HMI_RT_1",
  "actions": [
    { "action": "upsert", "alarmName": "Pump1_Fault",
      "properties": { "RaisedStateTag": "Pump1_Status", "RaisedStateTagBitNumber": 3,
                      "AlarmClass": "Alarm", "EventText": "Pump 1 fault" } },
    { "action": "upsert", "alarmName": "Level_High", "type": "analog",
      "properties": { "RaisedStateTag": "Level", "Condition": "UpperLimit", "ConditionValue": 80.5,
                      "AlarmClass": "Warning", "EventText": { "en-US": "Level high" } } }
  ]
}
```

- Дискретный аларм срабатывает по биту HMI-тега (`RaisedStateTag`, `RaisedStateTagBitNumber`,
  `TriggerMode`), аналоговый — по пределу (`RaisedStateTag`, `Condition`, `ConditionValue`).
- `EventText`, `EventText1` .. `EventText9` и `InfoText` принимают строку для всех языков
  проекта или `{ "en-US": "..." }` для отдельных. `unified_get_alarms` возвращает их простым
  текстом.
- Тег и класс алармов должны существовать: сам Openness принял бы любое имя.
- `unified_manage_alarm_classes` задаёт `Priority`, `StateMachine`, `Log` и вид каждого
  состояния в форме `"RaisedState.BackColor": "#FFA500"` (состояния: `RaisedState`,
  `ClearedState`, `AcknowledgedState`, `AcknowledgedClearedState`; свойства: `BackColor`,
  `TextColor`, `Flashing`). Системные классы удалить нельзя.

### Текстовые и графические списки

В Openness нет объектов для записей списка: списки только экспортируются и импортируются в виде
файлов YAML. `unified_get_text_lists`, `unified_get_graphic_lists` и `unified_manage_lists` делают
это сами и показывают записи в простом виде:

```json
{
  "softwarePath": "HMI_1/HMI_RT_1",
  "actions": [
    { "action": "upsert", "listName": "Modes",
      "entries": [
        { "value": 0, "text": "Off" },
        { "value": 1, "text": { "en-US": "Auto", "de-DE": "Automatik" } },
        { "from": 10, "to": 20, "text": "Service" },
        { "from": 100, "text": "Fault" },
        { "default": true, "text": "?" }
      ] },
    { "action": "upsert", "listName": "Pump_Symbols", "kind": "graphic",
      "entries": [
        { "value": 1, "graphic": "Pump_On" },
        { "default": true, "graphic": "Pump_Off" }
      ] }
  ]
}
```

- Запись соответствует одному значению (`value`), диапазону (`from` и `to`), значению и выше
  (только `from`), значению и ниже (только `to`) либо является записью по умолчанию
  (`"default": true`) — она показывается для всех значений, не покрытых другими записями.
- Запись текстового списка несёт `text`, графического — `graphic`: имя рисунка из графики
  проекта. Это имя TIA Portal не проверяет.
- Список записывается **целиком**: `entries` заменяет всё, что в нём было.
- При импорте TIA Portal молча отбрасывает то, что не понял. Поэтому инструмент после записи
  читает список обратно и при любом расхождении завершает вызов ошибкой, отменяя его.
- Список, являющийся типом библиотеки, среди списков HMI не числится: прочитать и записать его
  нельзя.

К объекту экрана список привязывается через `unified_manage_items`: свойство показывает запись,
соответствующую значению тега, — текстовый список на текстовом свойстве, графический на
графическом.

```json
{ "action": "update", "screenName": "Screen_1", "itemName": "Mode_Text",
  "properties": { "Text": { "resourceList": "Modes", "tag": "Pump1_Mode" } } }
```
### Фейсплейты

У экземпляра фейсплейта свой инструмент, потому что набор его параметров не фиксирован: это
интерфейс типа фейсплейта. `get_library_types` перечисляет типы со значением
`ContainedType` каждой версии; `unified_manage_faceplate` создаёт или изменяет один экземпляр
и возвращает его интерфейс:

```json
{
  "softwarePath": "HMI_1/HMI_RT_1", "screenName": "Screen_1", "itemName": "Valve_1",
  "action": "upsert", "faceplateType": "V0.0.2\\HMI_Discret_Valve",
  "properties": { "Left": 50, "Top": 60 },
  "interfaceValues": {
    "Interface_Tag_1": "Valve1_Data",
    "Valve_Name": { "tag": "Valve1_Name" }
  }
}
```

- **Теговый интерфейс** принимает имя HMI-тега простым значением либо `{ "tagParameter": "..." }`.
- **Интерфейс-свойство** принимает статическое значение, `{ "tag": "..." }` или
  `{ "script": "..." }`.
- `{ "dynamization": "none" }` убирает динамизацию.
- Openness не сообщает, к какому из двух видов относится свойство интерфейса. Динамизация не
  того вида отклоняется с подсказкой, и ничего не меняется.
- Вызов с `action: "update"` без значений читает интерфейс существующего экземпляра.

## Загрузка в ПЛК

1. Запустите ПЛК или экземпляр PLCSIM сами и убедитесь, что его адрес совпадает с проектом.
2. `get_download_targets` перечисляет цели в виде `режим / интерфейс ПК / целевой интерфейс`.
3. `download_to_plc` принимает эти три значения, а также `hardware` и `software`.

CPU не останавливается и не запускается, пока вы не передадите `stopPlc` / `startPlc`; загрузка,
которой нужна остановка, отклоняется с этим объяснением. В ответе перечислены все шаги
конфигурации, которые задал TIA Portal, данный на каждый ответ и сообщения результата. Шаг, для
которого у сервера нет ответа, остаётся с предустановкой TIA Portal и помечается в ответе; чтобы
решить иначе, передайте `selections` (`ТипШага=Вариант`). Если в CPU данные управления
пользователями отличаются от проекта, решает `downloadUserManagement`: `keep` (по умолчанию) оставляет данные CPU,
`update` берёт пользователей проекта, но оставляет пароли CPU, `overwrite` заменяет всё и сбрасывает пароли. В ответе
сообщения верхнего уровня перечислены как `parts`, у каждого своё состояние и счётчики ошибок и предупреждений (аппаратная и
программная часть). Ответ для управления пользователями и `parts` **не проверены на настоящей загрузке** (ПЛК или PLCSIM
не было). Предварительного просмотра нет: вызов выполняет загрузку.

## Версии TIA Portal

- По умолчанию используется **V21**. Для более ранних версий нужен параметр `--tia-major-version`.
- Исходные документы (`.s7dcl` / `.s7res`) для блоков требуют TIA Portal V20 или новее.
- Исходные документы для типов данных ПЛК требуют **V21** или новее: методы
  `PlcType.ExportAsDocuments` и `PlcTypeComposition.ImportFromDocuments` появились в Openness
  только в V21.
- Сервер проверялся на V21. `plc_create_fb` для LAD, FBD и STL использует шаблон SimaticML,
  взятый из экспорта V21, и на более ранних версиях не проверялся.

## Исходные документы SIMATIC

Исходный документ — это читаемая форма объекта, пригодная для сравнения в git: `<Имя>.s7dcl`
содержит объявление и тело в виде текста SCL/LAD/STL, необязательный `<Имя>.s7res` — комментарии
и языковые ресурсы. Формат `xml` инструмента `export_objects` пишет вместо этого XML SimaticML,
который сравнивать неудобно.

Имена файлов задаёт TIA Portal, а не сервер: ответ на экспорт перечисляет файлы, которые были
записаны на самом деле. У таблиц тегов и таблиц наблюдения в Openness V21 нет интерфейса
документов, для них остаётся только XML.

Имя типа данных ПЛК уникально во всём ПЛК, а не только в своей группе. Поэтому импорт уже
существующего имени в *другую* группу завершается ошибкой даже с перезаписью; указывайте группу,
в которой тип уже находится.

## Длинные списки

Инструмент, который может вернуть сотни записей, принимает `limit` (наибольшее число записей на странице; 0 возвращает
все) и `offset` и сообщает в `meta`, какой длины список: `total`, `offset`, `truncated` и, если есть продолжение,
`nextOffset` — `offset` следующей страницы. Сообщение ответа говорит то же словами. Это инструменты: `plc_get_blocks`,
`plc_get_types`, `plc_get_tags`, `plc_get_cross_references`, `hw_get_devices`, `get_master_copies`,
`get_library_types`, `unified_get_screens`, `unified_get_screen_items`, `unified_get_tags`,
`unified_get_system_tags`, `unified_get_alarms`, `unified_get_logging_tags`, `unified_get_text_lists` и
`unified_get_graphic_lists`. `plc_find_in_code` и `plc_get_block_source` режут по объёму текста и тоже сообщают
`truncated` в `meta`.

## Проекты и экземпляры

- `connect` подключается к единственному экземпляру TIA Portal, у которого открыт проект; при нескольких задайте `processId` или
  `projectPath` (`get_tia_instances` их перечисляет). `get_state` подсказывает, что делать, если сервер не подключён.
- `save_as_project` принимает **папку** нового проекта без расширения (`C:\Projects\NewPlant`); TIA Portal создаёт в ней
  `NewPlant.apXX`, путь к файлу возвращается в ответе. После этого TIA Portal работает с копией. Родительская папка должна
  существовать, сама папка — не существовать или быть пустой.
- Сервер выполняет один вызов инструмента за раз, поэтому `close_project` не освобождает то, чем ещё пользуется чтение.

## Документация

- [`docs/tools/`](docs/tools/README.md) - каждый инструмент с параметрами, по странице на область (создаётся из описаний в коде скриптом `tools/make-tool-docs.ps1`, на английском).
- [`docs/recipes/`](docs/recipes/README.md) - сценарии из нескольких инструментов, каждый выполнен на настоящем проекте (на английском).
- [`docs/tools-list.txt`](docs/tools-list.txt) - имена всех инструментов; [`Implemented_Tools.md`](Implemented_Tools.md) - то же по строке на инструмент.
- [`docs/error-model.md`](docs/error-model.md) - как возникают и сообщаются ошибки.
- [`CHANGELOG.md`](CHANGELOG.md) - что изменилось, со списком переименованных инструментов.

## Известные ограничения

- Импорт блоков LAD из исходных документов требует, чтобы файл `.s7res` содержал записи en-US для
  всех элементов; иначе импорт может не пройти. Это ограничение TIA Portal Openness (замечено
  02.09.2025).
- **Сети LAD** читаются и меняются текстом (`plc_get_lad_networks`, `plc_manage_lad_networks`, TIA Portal V21);
  файл текстов `.s7res` инструмент ведёт сам. LAD-блок со встроенным технологическим объектом, у которого изменены
  начальные значения (экземпляр `PID_Compact`), получает отказ: текстовая форма блока этих значений не несёт, и
  изменение их потеряло бы. Блоки FBD, STL, GRAPH и CEM только читаются (`plc_get_block_source`).
- **Записи таблиц наблюдения** пока нельзя создавать и удалять через сервер.
- **Подсеть нельзя удалить** через сервер; `net_connect_subnet` создаёт её при необходимости.
- **Инструменты HMI — только для WinCC Unified.** Для WinCC Comfort, Advanced и Professional в
  Openness нет объектной модели экранов: экран нельзя создать, а его элементы — прочитать или
  изменить; возможен только экспорт и импорт в XML. Инструменты `unified_*` отказывают на таком
  HMI с этим объяснением. Что известно о классических системах, записано в
  `docs/hmi-classic-notes.md` — для отдельного набора инструментов позже.

Ограничения самого интерфейса Openness — их не обойти никакими параметрами:

- **Нет перемещения и копирования блоков и типов.** `plc_copy_block`, `plc_move_block`,
  `plc_copy_type` и `plc_move_type` собраны из экспорта и импорта. Отсюда следует:
  - Объект должен быть согласованным: несогласованный TIA Portal не экспортирует. Сначала
    скомпилируйте.
  - Имя блока, номер блока и имя типа уникальны в пределах ПЛК. Поэтому копии внутри того же ПЛК
    нужен `newName`, а скопированный блок получает первый свободный номер своего вида. Чтобы
    сохранить имя, копируйте в другой ПЛК через `targetSoftwarePath`.
  - Перемещение экспортирует объект, удаляет оригинал и импортирует его в целевую группу; имя и
    номер сохраняются. Если импорт не удался, объект импортируется обратно в исходную группу.
    Блоки, использующие перемещённый (его экземплярные DB, вызывающие блоки), становятся
    несогласованными до следующей компиляции ПЛК.
- **Нет универсального «создать блок».** `PlcBlockComposition.CreateFB` создаёт только блоки
  ProDiag. Поэтому `plc_create_fb` создаёт блок SCL из текста источника с одним блоком, а блоки
  LAD, FBD и STL — импортом минимального документа SimaticML; остальные языки (GRAPH и другие)
  отклоняются. Блок ProDiag приносит с собой экземплярный DB и `ProDiagOB`. Блоки всех прочих
  видов создаются через `plc_create_scl_block` или `import_objects`.
- **Номера блоков.** Openness принимает номер блока буквально даже при автонумерации:
  экземплярный DB, созданный с номером 0, действительно становится `DB0` и не компилируется.
  Сервер сам подбирает первый свободный номер и сообщает его в ответе.
- **Объекты только для чтения.** Системные константы нельзя создавать и изменять, таблицу
  принудительных значений — создавать и удалять, таблицу тегов по умолчанию — удалять, а
  системные группы (`Program blocks`, `PLC data types`, `PLC tags` и другие) — переименовывать и
  удалять. В этих случаях возвращается сообщение `NotSupported`, а не невнятная ошибка Openness.
- **Нет перекрёстных ссылок** для таблиц наблюдения, таблиц принудительных значений и внешних
  исходных файлов.
- **Программы безопасности.** F-блоки и теги безопасности отклоняют большинство изменений, иногда
  требуя пароль безопасности. Исходная ошибка передаётся с её собственным текстом.
- **Блоки и типы с защитой know-how** отклоняются до любого изменения, с просьбой сначала снять
  защиту в TIA Portal.
- **Нет предпросмотра загрузки.** Шаги конфигурации загрузки можно увидеть, только ответив на
  них, а ответ и запускает загрузку.

## Обработка ошибок

- Неудавшийся инструмент возвращает результат с `isError: true` и сообщением, по которому модель
  может действовать, а не ошибку JSON-RPC. Сообщение содержит причину от TIA Portal, код ошибки и
  пути, о которых шла речь, например
  `CreateFB failed: <текст Openness> [code: CreateFailed; softwarePath: 'PLC_1'; groupPath: 'Tests']`.
- Коды ошибок: `NotFound`, `InvalidParams`, `InvalidState`, `ExportFailed`, `ImportFailed`,
  `CreateFailed`, `DeleteFailed`, `RenameFailed`, `NotSupported`.
- TIA Portal не экспортирует несогласованные блоки и типы. Одиночный экспорт завершается
  сообщением о необходимости компиляции; пакетный пропускает несогласованные объекты и
  перечисляет их.
- Все вызовы Openness выполняются под одной блокировкой: объекты Openness не потокобезопасны, а
  SDK MCP может запускать вызовы инструментов параллельно.

Устройство модели ошибок описано в [`docs/error-model.md`](docs/error-model.md) (на английском).

## Протокол MCP и транспорт

- Сервер построен на .NET SDK
  [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) **2.2.0**. Версии
  протокола, согласуемые при `initialize`: `2024-11-05`, `2025-03-26`, `2025-06-18`, `2025-11-25`.
- У каждого инструмента есть читаемый `title` и аннотации поведения (`readOnlyHint`,
  `destructiveHint`, `idempotentHint`, `openWorldHint`). Большинство инструментов публикуют
  `outputSchema` и возвращают `structuredContent`.
- Подсказки (prompts) не регистрируются; то же самое сказано в описаниях инструментов.
- Транспорт — только **stdio**. Журнал при этом пишется в stderr, чтобы не повредить JSON-RPC.
- Streamable HTTP из этого процесса недоступен: SDK поставляет его для .NET 8+, а сервер собран
  под `net48`, как того требует TIA Openness. Понадобился бы отдельный процесс-посредник.

## Сборка и тестирование

```powershell
dotnet build TiaMcpServer.sln -c Release
```

Результат — в `src\TiaMcpServer\bin\Release\net48`.

Тесты, которым не нужен TIA Portal (регистрация инструментов, тексты ошибок, пути, перенос блоков,
аргументы загрузки, командная строка):

```powershell
dotnet test tests\TiaMcpServer.Test\TiaMcpServer.Test.csproj -c Release --filter "FullyQualifiedName~Test7|FullyQualifiedName~Test8|FullyQualifiedName~Test9|FullyQualifiedName~Test10|FullyQualifiedName~Test11|FullyQualifiedName~Test12"
```

Остальным тестам нужны запущенный TIA Portal и тестовый проект, описанные в
[`tests/TiaMcpServer.Test/README.md`](tests/TiaMcpServer.Test/README.md).

Если вы работаете с репозиторием вместе с ИИ-ассистентом, сначала прочитайте
[`AGENTS.md`](AGENTS.md): тесты и всё, что затрагивает TIA Portal, запускаются только после явного
подтверждения.

## Документы проекта

| Документ | Содержание |
| -------- | ---------- |
| [`CHANGELOG.md`](CHANGELOG.md) | Что изменилось в каждой версии (на английском) |
| [`docs/tools-list.txt`](docs/tools-list.txt) | Имена зарегистрированных инструментов |
| [`Implemented_Tools.md`](Implemented_Tools.md) | Описание каждого инструмента одной строкой |
| [`docs/error-model.md`](docs/error-model.md) | Как возникают и передаются ошибки (на английском) |
| [`docs/PLAN.md`](docs/PLAN.md) | Незавершённые работы и предложения |
| [`TODO.md`](TODO.md) | Долгосрочный список задач |
| [`docs/handoff/README.md`](docs/handoff/README.md) | Для разработчиков: контекст проекта, открытые задачи, справочники по API Openness |
| [`tools/README.md`](tools/README.md) | Скрипты разработки: проба Openness, вызов сервера, завершение изменения (на английском) |
| [`Install/INSTALL_RU.md`](Install/INSTALL_RU.md) | Установка и подключение к клиентам |

## Материалы

- [Документация TIA Portal Openness API](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows)
- [Документация Openness по экспорту и импорту](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/export/import)

## Происхождение и лицензия

Проект начинался как форк
[heilingbrunner/tiaportal-mcp](https://github.com/heilingbrunner/tiaportal-mcp) (автор —
J. Heilingbrunner) и теперь развивается самостоятельно. Распространяется по лицензии MIT, см.
[`LICENSE.txt`](LICENSE.txt).
