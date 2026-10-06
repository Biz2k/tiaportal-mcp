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

## Результат (06.10.2026): через Openness невозможно

Biz: «давай 16». Инструмента `unified_get_cycles` / `unified_manage_cycles` нет: API для циклов Unified-HMI не существует.

Что проверено:
- Отражение `HmiUnified` (все типы): у `HmiSoftware` нет `Cycles`; в `RuntimeSettings` циклов тоже нет. Имя `Cycle` есть только у
  классического WinCC (`Siemens.Engineering.Hmi.Cycle.Cycle`, `CycleComposition`, `HmiTarget`) и у оборудования (`HW.*Cycle*`, к HMI
  отношения не имеют). Остальное с `Cycle` в имени — свойства `HmiLoggingTag.Cycle`, `HmiTag.AcquisitionCycle` и тип `Trigger`
  `CustomCycle` — только строки с именем.
- Проба на `HMI Unified/HMI_RT_3`, временные `MCPT_CT` (тег) и `MCPT_LT` (архивный тег `Cyclic` в `Data log_1`): имя цикла
  пользователя `Custom cycle` принято, `custom cycle` (другой регистр) принято, `Bogus` отклонено («Logging Cycle is invalid»;
  ошибка уже поясняется сервером). Значит Openness проверяет имя против циклов проекта, но перечислить их нельзя.
- Косвенная проверка имени (временный архивный тег с проверяемым именем) возможна, но хрупка и меняет проект — не добавлялась.
  Для `Trigger.CustomDuration` скрипта и `HmiTag.AcquisitionCycle` проверки нет и там; предупреждение в `notes` остаётся.

Сделано: сообщение об отклонённом цикле архивного тега теперь говорит, что цикл пользователя принимается по точному имени;
`unified-defaults.md` обновлён. Временные объекты удалены, проект сохранён.
