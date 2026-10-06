# 16. Научить сервер читать и создавать циклы HMI Unified

Приоритет: низкий. Объём: малый, если Openness это позволяет. Просьба Biz, 06.10.2026.

## Цель

Циклы (`Cycles` в дереве HMI Unified: `T100ms` … `T10s` и свои, например `Custom cycle`) используют архивные теги
(`Cycle`), триггеры скриптов (`CustomCycle`) и опрос тегов (`AcquisitionCycle`). Сервер не умеет ни прочитать их
список, ни создать свой, ни проверить имя цикла.

## Что известно

- Список циклов по умолчанию — в [`unified-defaults.md`](../unified-defaults.md).
- На 06.10.2026 у `HmiUnified.HmiSoftware` нет свойства `Cycles` (свойства перечислены в
  `tools/openness-reflect.ps1 -Namespace 'HmiUnified'`); `Siemens.Engineering.Hmi.Cycle.CycleComposition`
  относится к классическому WinCC (`Siemens.Engineering.Hmi.HmiTarget`). Поиск по имени `Cycle` другого не нашёл.
- `HmiLoggingTag.Cycle` Openness проверяет (задача 03: принял `T100ms` … `T10s`, отклонил `T30s`);
  `Trigger.CustomDuration` скрипта не проверяет — принимает любое имя (задача 06).
- Тег: `HmiTag.AcquisitionCycle` — тоже имя цикла.

## Шаги

1. Искать пробой, не отражением: принимает ли `HmiLoggingTag.Cycle` пользовательский цикл (`Custom cycle`). Если да —
   имя цикла можно проверять косвенно: создать архивный тег в откатываемой транзакции с этим именем и посмотреть на
   ответ. Это дорого и хрупко; решить, нужно ли.
2. Поискать в `HmiRuntimeSetting` и в сборке `Siemens.Engineering.HmiUnified.dll` типы с `Cycle` в имени
   (`openness-reflect.ps1 -Name 'Cycle'` показал только классический).
3. Если API нет — записать это здесь как «невозможно» и оставить сообщение о непроверяемом имени в `notes`
   (уже есть для `CustomCycle`).

## Готово, когда

Либо есть инструмент `unified_get_cycles` / `unified_manage_cycles` с проверкой вживую, либо здесь записано, что API нет,
и что именно пробовали.
