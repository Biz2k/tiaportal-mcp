# 01. Экраны Unified в группах и свойства экрана

Статус: **сделана 06.10.2026** (см. «Что выяснено» в конце).

Приоритет: **высокий** — это дефект, а не новая возможность. Объём: средний.

## Проблема

Экраны WinCC Unified могут лежать в группах экранов. `HmiSoftware.Screens` содержит только
экраны верхнего уровня; экраны групп находятся в `HmiSoftware.ScreenGroups[].Screens` (и в
`Groups` вложенных групп). Все инструменты сервера ищут экран через `software.Screens`, поэтому
экраны в группах для них **не существуют**.

Проверено 06.10.2026 на `HMI Unified/HMI_RT_3`: `software.Screens.Count` = 14, а в четырёх
группах (`6_AI`, `3_Pumps`, `5_DI`, `ScreenWindow`) ещё 60 экранов. Экран, созданный через
`group.Screens.Create(...)`, через `software.Screens.Find(...)` не находится.

Затронуты: `unified_get_screens`, `unified_get_screen_items`,
`unified_get_screen_item_properties`, `unified_manage_items`, `unified_manage_faceplate`,
`unified_configure_trend_control`, `unified_delete_screen`, отладочные инструменты.

## Что сделать

1. Общий обход экранов по образцу `EnumerateTagTables` в `Portal.Unified.Tags.cs` (таблицы
   тегов в группах уже обходятся так): верхний уровень + группы рекурсивно, с путём группы.
2. `FindUnifiedScreen` / `RequireUnifiedScreen` в `Portal.Unified.cs` перевести на этот обход.
   Имена экранов в HMI уникальны (проверить пробой: создать в группе экран с именем
   существующего экрана верхнего уровня) — если уникальны, искать по имени, как сейчас.
3. `unified_get_screens`: добавить в ответ группу экрана (поле `group`, `/`-разделённый путь,
   пусто на верхнем уровне) и параметр-фильтр по группе.
4. `unified_create_screen`: необязательный параметр `group` — создать экран в группе. Решить,
   создавать ли отсутствующую группу или отвечать ошибкой с перечнем групп (предпочтительно
   ошибка: молчаливое создание групп по опечатке хуже).
5. Группы экранов: чтение и создание / переименование / удаление. Либо отдельный небольшой
   инструмент `unified_manage_screen_groups` по образцу `unified_manage_tag_tables`, либо
   действия в существующем. Удаление группы, вероятно, удаляет её экраны — выяснить пробой и
   сообщать в `Notes`, как это сделано для таблиц тегов.
6. Свойства самого экрана. Сейчас `unified_manage_items` с пустым `itemName` обращается к
   экрану; проверено только назначение события. Проверить и задокументировать запись
   `Width`, `Height`, `BackColor`, `ScreenNumber`, `BackGraphic`. **`DisplayName` экрана — это
   `MultilingualText`; запись `DisplayName` HMI-тега закрывает TIA Portal.** Запись
   `DisplayName` экрана пробовать только отдельным скриптом, последней, на сохранённом проекте;
   если закрывает — отклонять до обращения к Openness, как у тегов.

## API

`docs/handoff/api/unified-screens.txt`, `docs/handoff/api/unified-screen-groups.txt`.

- `HmiScreenGroup`: `Name*`, `Screens` (`HmiScreenComposition` с `Create(name)`, `Find(name)`),
  `Groups` (вложенные группы), `Delete()`.
- `HmiScreenGroupComposition`: `Create(name)`, `Find(name)`.
- `HmiScreen`: `Width*`, `Height*`, `BackColor*`, `ScreenNumber*`, `BackGraphic*`,
  `DisplayName` (MultilingualText), `ResizeScreen()`, `Delete()`.

## Где в коде

- `src/TiaMcpServer/Siemens/Portal.Unified.cs` — `FindUnifiedScreen`, `RequireUnifiedScreen`,
  `GetUnifiedScreens`, `CreateUnifiedScreen`, `DeleteUnifiedScreen`, тип `HmiScreenInfo`.
- `src/TiaMcpServer/ModelContextProtocol/McpServer.Unified.cs` — инструменты.
- Образец обхода групп: `EnumerateTagTables` в `Portal.Unified.Tags.cs`.

## Готово, когда

- `unified_get_screens` на `HMI Unified/HMI_RT_3` возвращает 74 экрана с группами.
- `unified_get_screen_items` и `unified_manage_items` работают на экране из группы (например,
  `3_0_CP_1` в группе `3_Pumps`): создать `MCPT_`-элемент, прочитать, удалить.
- Экран создаётся в группе и удаляется из неё.
- Проверено на ПК-станции `АРМ Unified/HMI_RT_1` (2 группы).
- Ошибочные случаи: неизвестная группа, дубль имени.
- Документация обновлена; в `CHANGELOG.md` запись в раздел `Fixed`.

## Риски

- Запись `DisplayName` экрана — см. выше.
- Экраны в группе `ScreenWindow` у Biz — содержимое всплывающих окон; свои тестовые объекты
  создавать на отдельном экране `MCPT_...`, а не в рабочих экранах.

## Что выяснено

Проверено вживую на `HMI Unified/HMI_RT_3` (74 экрана, 4 группы) и `АРМ Unified/HMI_RT_1` (87 экранов,
10 групп; чтение). Находки — в заголовке `Portal.Unified.ScreenGroups.cs`.

- Имена экранов уникальны во всём HMI, включая группы (`ValueIsNotUnique` при дубле в той же или
  другой группе), поэтому экран по-прежнему ищется по имени.
- Группы: инструменты `unified_get_screen_groups` и `unified_manage_screen_groups` (create / rename /
  delete, вложенные — `Родитель/Дочерняя`). Создание в несуществующей группе и группы в
  несуществующем родителе — ошибка с перечнем групп. Удаление группы удаляет её экраны (note).
- `unified_create_screen` получил `group`, `unified_get_screens` — `group` в ответе и в фильтре.
- Свойства экрана: `Width`, `Height`, `ScreenNumber`, `BackColor`, `BackGraphic` пишутся;
  `DisplayName` экрана пишется (`{"texts": {...}}`) и **не закрывает TIA**, в отличие от
  `HmiTag.DisplayName`. Голая строка в `MultilingualTextItem.Text` отклоняется — нужен
  `<body><p>…</p></body>`. `BackGraphic` принимает несуществующее имя.
- Не проверено: запись на ПК-станции (там только чтение); вложенные группы проверены записью на панели.
