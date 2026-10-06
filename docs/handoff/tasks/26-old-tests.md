# 26. Старые тесты, требующие TIA Portal

Исполнитель: Sonnet. Приоритет: низкий. Объём: небольшой. Риск для TIA Portal: нет.

## Проблема

`Test1Portal`, `Test2ProjectSession`, `Test3Devices`, `Test4Software`, `Test5McpServer`,
`Test6Diagnostics`, `Test21Project`, `Test22Session` требуют TIA Portal и проектов исходного
автора по путям `D:\Siemens\...` (`tests/TiaMcpServer.Test/Settings.cs`). С начала работ они
не запускались, о новых инструментах не знают. Эталонный проект решено не собирать
(`../decisions.md`). `tools/finish.ps1` их не запускает (фильтр `TestCategory=NoTia`).

## Что сделать

1. Прочитать каждый из восьми классов. Тесты, которые на деле TIA Portal не требуют (чистая
   логика, попавшая в «живой» класс), перенести в класс с `[TestCategory("NoTia")]`.
2. Остальным классам дать `[TestCategory("NeedsTia")]` и `[Ignore("Needs TIA Portal and the
   original author's projects; not maintained. Live checks: tools/smoke.ps1.")]` — так
   `dotnet test` без фильтра не падает на чужих путях.
3. `tests/TiaMcpServer.Test/README.md`: что запускается (`NoTia`), чем заменены живые тесты
   (`tools/smoke.ps1`), как при желании запустить старые (снять `Ignore`, поправить
   `Settings.cs`).
4. `AGENTS.md`, раздел о тестах: команда — `dotnet test --filter TestCategory=NoTia` либо
   `tools/finish.ps1`.

Удалять классы не нужно: это тесты исходного проекта, и решение об удалении — за Biz.

## Готово, когда

`dotnet test` без фильтра завершается без ошибок на машине без проектов автора; число тестов
`NoTia` не уменьшилось; строка в `../../PLAN.md` отмечена.
