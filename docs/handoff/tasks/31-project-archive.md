# 31. Архив проекта: архивировать и разархивировать

Исполнитель: Sonnet. Приоритет: высокий. Объём: небольшой. Риск для TIA Portal: средний —
разархивирование открывает другой проект.

## Зачем

Резервная копия перед большими изменениями агента: один файл `.zap21`, который TIA Portal
разворачивает сам. Сейчас сервер умеет только `save_as_project` (копия папкой, и TIA Portal
после неё работает в копии).

## Что известно (по отражению, вживую не вызывалось)

- `Project.Archive(DirectoryInfo targetDirectory, string targetName, ProjectArchivationMode mode)`;
  режимы `ProjectArchivationMode`: `None`, `Compressed`, `DiscardRestorableData`,
  `DiscardRestorableDataAndCompressed`. Переснять: `tools/openness-reflect.ps1 -Name Archiv`.
- `ProjectComposition.Retrieve(FileInfo sourcePath, DirectoryInfo targetDirectory)` и
  `RetrieveWithUpgrade(...)` возвращают открытый проект.
- TIA Portal держит один проект: `create_project` при открытом проекте отказывает — так же
  должен вести себя инструмент разархивирования (образец — `CreateProject` в `Siemens/Portal.cs`).
- Правила пути — класс `ProjectPathRules` (`findings.md`, раздел 22).

## Что сделать

1. Проба (`tools/openness-probe.ps1`, проект сохранён): `Archive` проверочного проекта во
   временную папку. Выяснить: требует ли сохранённого проекта, сколько длится (проект 473 МБ),
   меняет ли `isModified`, какое расширение получает файл при `targetName` с расширением и без.
2. `archive_project(targetDirectory, name, mode = "compressed")` — изменяющий (пишет файл),
   группа `project_write`, вне транзакции (`GuardedNoTransaction`, как `save_project`). Отказы до
   обращения к Openness: папки нет, файл уже есть, неизвестный режим (перечислить допустимые),
   проект с несохранёнными изменениями — если проба покажет, что архив их не содержит. В ответе
   полный путь файла и его размер.
3. `retrieve_project(archivePath, targetDirectory)` — отказ, если открыт проект (назвать его и
   `close_project`), файла нет, целевая папка не пуста. После успеха проект открыт: в ответе путь
   его файла. `RetrieveWithUpgrade` не делать; архив старой версии — отказ с причиной TIA Portal.
4. Оба — в список «всегда» в `ToolSets.cs` (как остальные операции проекта), в
   `docs/tools-list.txt`, изменяющий — в `Test_703`.
5. Живая проверка: архив проверочного проекта → `close_project` → `retrieve_project` в
   `C:\Users\Biz\Desktop\MCPT_Retrieve` → `get_project` показывает копию → `close_project` →
   `open_project` проверочного → удалить временную папку и архив.
6. `tools/smoke/write.json`: `archive_project` в `{WORK}`. `retrieve_project` — в
   `tools/smoke/project.json` (выполняется вручную) и в пропуски `smoke.ps1`, как `download_to_plc`.
7. Навык (`skills/tia-portal-mcp/SKILL.md`, раздел 3): одна строка — перед большим изменением
   предложить пользователю `archive_project`. Навык не должен вырасти: убрать строку взамен.

## Готово, когда

Архив проверочного проекта разворачивается сервером в рабочий проект; после проверки в TIA
Portal снова открыт проверочный проект без изменений; отказы «открыт проект» и «файл уже есть»
проверены вживую.

## Риски

После `retrieve_project` в TIA Portal открыт **другой** проект. Никаких изменяющих вызовов, пока
не возвращён проверочный; перед каждым — `get_project`.
