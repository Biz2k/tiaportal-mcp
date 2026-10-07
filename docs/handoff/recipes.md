# Рецепты: как добавить инструмент

Образцы взяты из работающего кода. Прежде чем писать своё, открой названный файл-образец:
стиль, плотность комментариев и обороты должны совпадать с соседним кодом.

## Имена

- Инструменты: `snake_case` с префиксом области — `plc_`, `hw_`, `net_`, `unified_`, `drive_`,
  `sec_`; общие для проекта и библиотеки без префикса (`get_library_types`, `save_project`).
- По имени сервер относит инструмент к области (`ToolSets.cs`, флаг `--tools`) и к групповому
  инструменту (`ToolGroups.cs`, `GroupOf`): `plc_` с `_delete_` в имени — группа `plc_delete`,
  остальные изменяющие `plc_` — `plc_write`; `hw_` и `net_` с `_delete_` или `_disconnect_` —
  `hw_delete`; всё читающее — `tia_read`. Имя выбирать так, чтобы группа вышла верной.
- Инструмент для одного языка несёт язык в имени: `plc_get_lad_networks`, `plc_create_scl_block`.
- Чтение: `<область>_get_<что>`. Запись: `<область>_manage_<что>` для пакетных инструментов
  (create / update / upsert / delete в одном вызове), иначе `<область>_<глагол>_<что>`.
- Переименование или удаление инструмента ломает клиентов. Оно допустимо, но должно быть видно:
  `docs/tools-list.txt`, запись «Breaking» в `CHANGELOG.md`.

## Чек-лист нового инструмента

1. Отражение и проба (см. «Рабочий цикл» в `context.md`).
2. Типы запроса и ответа — в `ModelContextProtocol/` рядом с типами той же области. У каждого
   свойства запроса `[Description("...")]`: это единственная документация, которую видит
   модель-клиент (в виде по умолчанию — через `tia_help`).
3. Операция — `Siemens/Portal.<Область>.cs`, в `Operation.Run`.
4. Инструмент — `ModelContextProtocol/McpServer.<Область>.cs`.
5. Модульные тесты на всё, что не требует TIA (разбор, преобразования, проверки входа) —
   `tests/TiaMcpServer.Test/Test<N><Что>.cs`, с `[TestCategory("NoTia")]`.
6. Имя в `docs/tools-list.txt` (по алфавиту, порядок `Ordinal`); изменяющий инструмент — ещё и
   в список `edits` теста `Test_703` в `Test7ToolRegistration.cs`. Область и группа — см.
   «Имена»; `Test28ToolSets` и `Test33ToolGroups` назовут инструмент, который никуда не попал.
7. Живая проверка: при разработке — `tools/inproc-call.ps1`, в конце задачи — установленный
   сервер (`tools/mcp-call.ps1 -Grouped`). См. «Живая проверка» ниже.
8. Вызов в `tools/smoke/read.json` (читающий) или `tools/smoke/write.json` (изменяющий, с
   уборкой за собой) — прогон называет изменяющий инструмент без вызова.
9. Документация и коммит (шаги 7–8 рабочего цикла): `CHANGELOG.md`, оба README (и числа
   инструментов в них), `Implemented_Tools.md`, `tools/make-tool-docs.ps1`.

Чувствительное (пароли, защита, пользователи) — только отдельными инструментами области
`security`, по одному на вид изменения: владелец решает в клиенте, что разрешить. Эту область
ведёт Opus.

## Заголовок файла

Каждый новый файл начинается с комментария: что это, кто вызывает, какие файлы читает и пишет,
и что выяснено про поведение Openness. Образец — начало `Siemens/Portal.Unified.Tags.cs` и
`Siemens/Portal.Unified.Lists.cs`. Находки про Openness записывать именно туда, с версией TIA
и датой: следующий разработчик прочтёт файл, а не историю чата.

## Инструмент чтения

```csharp
[McpServerTool(Name = "unified_get_tag_tables", Title = "Get WinCC Unified tag tables", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
 Description("List the tag tables of a WinCC Unified HMI with the number of tags in each")]
public static ResponseUnifiedTagTables GetUnifiedTagTables(
    [Description(UnifiedPath)] string softwarePath)
{
    try
    {
        var tables = Portal.GetUnifiedTagTables(softwarePath);

        return new ResponseUnifiedTagTables
        {
            Message = $"{tables.Count} tag table(s) in '{softwarePath}'",
            Items = tables,
            Meta = ReadMeta()
        };
    }
    catch (Exception ex)
    {
        throw ToolError(ex);
    }
}
```

- Ответ — типизированный класс, наследник `ResponseMessage`: из него SDK строит `outputSchema`.
  Анонимные объекты и `object` не возвращать.
- Любой выход с ошибкой — через `ToolError(ex)`: SDK передаёт клиенту только текст
  `McpException`.
- У списков, которые бывают большими (теги, алармы), сразу делать фильтр (`nameFilter` и т. п.).

## Изменяющий инструмент

```csharp
[WriteTool]
[McpServerTool(Name = "unified_manage_tags", Title = "Manage WinCC Unified tags", Destructive = true, OpenWorld = false, UseStructuredContent = true),
 Description("Create, update, upsert or delete HMI tags of a WinCC Unified HMI, several at once. ... A call applies all of its actions or none.")]
public static ResponseUnifiedActions ManageUnifiedTags(
    [Description(UnifiedPath)] string softwarePath,
    [Description("actions: the changes to make, applied in order")] List<UnifiedTagAction> actions)
{
    return Guarded(nameof(ManageUnifiedTags), () => UnifiedActions(Portal.ManageUnifiedTags(softwarePath, actions)));
}
```

- `[WriteTool]` — инструмент не регистрируется при `--read-only`.
- `Guarded` — проверка режима записи, транзакция TIA Portal, перевод ошибок. Без транзакции —
  `GuardedNoTransaction`: загрузка в ПЛК, компиляция, операции с проектом, а также случаи, когда
  нужно несколько попыток — после исключения Openness транзакция уже не фиксируется, поэтому
  каждая попытка идёт в своей `Portal.InTransaction` (образец — `plc_create_technology_object`,
  перебор версий в `Portal.TechnologyObjects.cs`).
- `UnifiedActions(results)` собирает ответ пакетного инструмента.

## Пакетная операция «всё или ничего»

Общий каркас — `RunUnifiedBatch` в `Portal.Unified.Tags.cs`. Он обходит действия, собирает
результат каждого и, если хоть одно не удалось, бросает исключение — это и откатывает транзакцию.

```csharp
public List<UnifiedActionResult> ManageUnifiedTagTables(string softwarePath, IList<UnifiedTagTableAction>? actions)
{
    return RunUnifiedBatch(nameof(ManageUnifiedTagTables), softwarePath, actions,
        "{ \"action\": \"create\", \"tableName\": \"Pumps\" }",      // пример для сообщения «No actions given»
        a => a.TableName,                                              // имя объекта для отчёта
        (software, action, verb, result) =>
        {
            var name = RequireName(action.TableName, "tableName");

            switch (verb)                                              // уже в нижнем регистре
            {
                case "create":
                    if (FindTagTable(software, name) != null)          // проверка ДО вызова Openness
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, $"Tag table '{name}' already exists.");
                    }

                    software.TagTables.Create(name);
                    break;
                ...
                default:
                    throw new PortalException(PortalErrorCode.InvalidParams,
                        $"Unknown action '{action.Action}'. Use 'create', 'rename' or 'delete'.");
            }
        });
}
```

- У класса действия должно быть свойство `Action` — каркас читает его отражением.
- `result.Applied` — что задано; `result.Notes` — что стоит знать, но ошибкой не является
  («теги таблицы удалены вместе с ней», «имя списка не найдено среди списков HMI»).
- Глаголы: `create` (ошибка, если есть), `update` (ошибка, если нет), `upsert`, `delete`.

## Задание свойств объекта

Три готовых способа, по убыванию предпочтения для нового вида объектов:

| Способ | Где | Когда |
|---|---|---|
| Типизированные свойства: `FindTypedProperty` + `SetTypedProperty` | `Portal.Unified.Alarms.cs` | Новый вид объектов. Не трогает общий доступ к атрибутам |
| `SetUnifiedAttributes(target, what, properties, order, result)` | `Portal.Unified.Tags.cs` | Объект, на котором `SetAttribute` уже проверен (теги, соединения) |
| `SetHmiItemProperty` | `Portal.Unified.Items.cs` | Элементы экрана: статическое значение или динамизация |

Преобразование значения JSON в тип свойства — всегда `ConvertHmiValue(value, type, name)`:
перечисление по имени или числу, цвет `#RRGGBB` или по названию, `uint`/`byte`, `bool`. Тип
`object` у свойства передавать как `null` — тогда берётся тип самого значения JSON.

Порядок свойств бывает важен (тегу сначала `Connection`, потом `PlcTag`) — задавать его в коде,
а не требовать от вызывающего; `Name` (переименование) — последним.

## Тексты

- Тексты на элементах экрана и тексты алармов `EventText*` хранятся как документ
  `<body><p>…</p></body>`; голую строку Openness отклоняет. Запись — `FormatUnifiedText`,
  чтение — `PlainUnifiedText`.
- Комментарии, `InfoText` аларма — обычный текст.
- Соглашение для входа: строка — на все языки проекта; объект `{"en-US": "..."}` — на
  отдельные. Неизвестный язык — ошибка с перечнем языков проекта.

## Объекты, доступные только через файлы

У списков и скриптов Unified объектной модели содержимого нет — только `Export` / `Import`.
Образец — `Portal.Unified.Lists.cs`:

- `WithListFolder` — своя временная папка на операцию, удаляется в `finally`;
- файл собирается в том виде, в каком его пишет сам TIA Portal;
- после импорта — экспорт и сравнение, расхождение = ошибка (и откат);
- разбор формата — отдельные `internal static` методы без обращения к TIA, покрытые тестами
  на настоящих образцах экспорта.

## Сообщения об ошибках

Сообщение читает модель-клиент, и по нему она должна суметь исправиться:

- назвать, что не так, и на каком объекте;
- при `NotFound` — перечислить существующее или назвать инструмент, который перечислит;
- при неизвестном значении — перечислить допустимые;
- если Openness отвечает невнятно («The property cannot be set») — перехватить и объяснить
  (пример — `CommunicationDriver` в `SetUnifiedAttributes`).

## Живая проверка

Файл вызовов удобнее всего собирать в PowerShell:

```powershell
$h = 'HMI Unified/HMI_RT_3'
$calls = @(
  @{ name = 'connect' },
  @{ name = 'unified_manage_tag_tables'; args = @{ softwarePath = $h; actions = @(,@{ action = 'create'; tableName = 'MCPT_Tbl' }) } },
  @{ name = 'unified_get_tag_tables';    args = @{ softwarePath = $h } },
  @{ name = 'unified_manage_tag_tables'; args = @{ softwarePath = $h; actions = @(,@{ action = 'delete'; tableName = 'MCPT_Tbl' }) } },
  @{ name = 'disconnect' })
$calls | ConvertTo-Json -Depth 10 | Set-Content "$scratch\calls.json" -Encoding UTF8
powershell -NoProfile -ExecutionPolicy Bypass -File tools\inproc-call.ps1 -Calls "$scratch\calls.json"
```

Один файл вызовов годится трём скриптам:

| Скрипт | Что это | Запрос «Openness access» |
|---|---|---|
| `tools\inproc-call.ps1` | методы инструментов сборки внутри PowerShell | нет — им и вести разработку |
| `tools\mcp-call.ps1` | настоящий сервер по stdio, инструменты по одному (`--full`) | да, на каждую новую сборку |
| `tools\mcp-call.ps1 -Grouped` | то же в виде по умолчанию: вызовы идут через групповые инструменты | да |

`inproc-call` не проверяет то, что делает сам сервер: схему параметров, очередь вызовов,
регистрацию, группы. Поэтому в конце задачи — один прогон настоящим сервером, предупредив Biz:
запрос доступа подтверждает только он, а при свёрнутом TIA Portal запроса не видно и вызов
просто ждёт.

Набор для каждого нового инструмента: удачный путь → чтение результата → каждая ветка ошибки →
повторный вызов (идемпотентность `upsert`) → удаление своих объектов → чтение, что чисто.
Порядок ключей в `@{}` не сохраняется; где порядок свойств важен для проверки — `[ordered]@{}`.

## Сообщение коммита

Английский, по образцу `git log`: заголовок `feat:` / `fix:` / `docs:` / `chore:` (`feat!:` при
ломающем изменении), в теле — что и почему, что выяснено про Openness, чем проверено. В конце
строка `Co-Authored-By` из системного напоминания текущей сессии.
