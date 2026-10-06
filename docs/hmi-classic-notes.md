# Классический WinCC (Comfort / Advanced / Professional): отложенные наработки

Статус на 06.10.2026: инструментов для классического WinCC в сервере **нет**. Все HMI-инструменты
переименованы в `unified_*` и работают только с WinCC Unified; на классическом HMI они отказывают
с объяснением. Здесь собрано то, что уже выяснено, чтобы не выяснять заново, когда дойдёт очередь
до отдельного набора инструментов.

## Почему отдельно

Первые HMI-инструменты пытались обслуживать Unified и классические системы одними и теми же
методами. Модели в Openness разные:

| | WinCC Unified | Классический WinCC |
|---|---|---|
| Класс программы | `Siemens.Engineering.HmiUnified.HmiSoftware` | `Siemens.Engineering.Hmi.HmiTarget` |
| Экраны | `Screens`, плоский список, есть `Create` | `ScreenFolder.Screens` + вложенные `Folders`, `Create` нет |
| Элементы экрана | `ScreenItems`, свойства, динамизации, события | объектной модели нет |
| Теги | `Tags`, плоский список | `TagFolder.TagTables[].Tags` + вложенные папки |
| Изменение | через объекты | только экспорт в XML → правка → импорт |

Поэтому у каждого метода была вторая ветка, которая для классики не могла сделать работу. При
переименовании в `unified_*` эти ветки убраны.

## Что проверено

WinCC Advanced V21, устройство `HMI_1` (05.10.2026):

- Чтение списка экранов и списка тегов работает.
- Чтение соединений работает.
- Создать экран нельзя: у `ScreenComposition` нет метода `Create`.
- Прочитать или изменить элементы экрана нельзя: у экрана нет коллекции элементов.

WinCC Professional: **не проверялся**. В проверочном проекте у ПК-станции с Professional
программа HMI через `SoftwareContainer` не нашлась — либо она лежит на другом элементе
устройства, либо доступна через другой сервис. С этого и надо начинать.

Экспорт и импорт экранов в XML (`Export` / `Import` у экранов, тегов, соединений): не проверялся.
По документации Openness это единственный путь изменения классического HMI.

## Что сохранено в коде

`src/TiaMcpServer/Siemens/Classic/Portal.HmiClassic.cs` — методы без инструментов:

- `RequireClassicHmi(softwarePath)` — программа HMI по пути, с проверкой, что это `HmiTarget`.
- `GetClassicHmiScreens(softwarePath)` — имена экранов, включая вложенные папки.
- `FindClassicScreen(target, screenName)` — экран по имени.
- `GetClassicHmiTags(softwarePath)` — теги из всех таблиц и папок.
- `GetClassicFaceplateTypes()` — типы фейсплейтов классического WinCC в библиотеке проекта.

Поиск устройства и программы по пути (`RequireHmiContainer`) общий с Unified.

## С чего начать, когда вернёмся

1. Решить, как называть инструменты. Набор для Unified называется `unified_*`; по той же логике —
   отдельные префиксы для систем (`advanced_*`, `professional_*`) либо общий `classic_*`, если
   окажется, что API у Comfort / Advanced / Professional один.
2. Найти программу WinCC Professional в ПК-станции.
3. Проверить вживую экспорт экрана в XML и импорт обратно без изменений.
4. Инструменты чтения: экраны, теги, соединения — на готовых методах.
5. Инструмент изменения — через XML: экспорт, правка, импорт. Формат XML экрана придётся
   разбирать отдельно; это главный объём работы.

## Убранные инструменты

Для справки, если встретятся в старых заметках или промптах:

| Было | Стало |
|---|---|
| `hmi_get_screens`, `hmi_get_screen_items`, `hmi_get_screen_item_properties`, `hmi_get_tags`, `hmi_get_connections`, `hmi_get_library_types` | то же с префиксом `unified_` |
| `hmi_create_screen`, `hmi_delete_screen`, `hmi_manage_items` | то же с префиксом `unified_` |
| `hmi_create_screen_item`, `hmi_delete_screen_item` | `unified_manage_items` (`create` / `delete`) |
| `hmi_set_unified_screen_item_event` | `unified_manage_items`, поле `events` |
| `hmi_create_faceplate_instance`, `hmi_manage_unified_faceplate` | `unified_manage_faceplate` |
| `hmi_get_library_faceplates` | `unified_get_library_types` |
| `hmi_configure_unified_trend_control` | `unified_configure_trend_control` |
| `hmi_configure_unified_trend_companion` | `unified_manage_items`, свойство `SourceTrendControl` |
| `hmi_debug_reflect`, `hmi_debug_screen_item` | `unified_debug_reflect`, `unified_debug_screen_item` |
| `hmi_test_faceplate` | убран |
