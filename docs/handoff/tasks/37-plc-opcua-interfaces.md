# 37. OPC UA-интерфейсы сервера ПЛК

Исполнитель: Sonnet. Приоритет: средний. Объём: средний. Риск для TIA Portal: средний — вызовы
у нас не проверялись ни разу.

## Зачем

OPC UA-сервер CPU отдаёт клиентам то, что описано в его серверных интерфейсах. Сервер умеет
включить OPC UA и завести пользователей (`sec_manage_opcua_users`), но интерфейсы не видит.

## Что известно

Справочник — [`../api/plc-opcua.txt`](../api/plc-opcua.txt) (снят 07.10.2026). Образец кода —
репозиторий Siemens `tia-addin-opc-ua-modelled-interface` (V21),
<https://github.com/tia-portal-applications>.

- Вход: `OpcUaProvider` (служба ПО ПЛК; как её получить — `GetService<OpcUaProvider>()` у
  `PlcSoftware`, уточнить пробой) → `CommunicationGroup` → `ServerInterfaceGroup`.
- `ServerInterfaceGroup`: `ServerInterfaces` (`Create(name)`; у интерфейса `Enabled*`, `Author*`,
  `Comment`, `Import(FileInfo)`, `Export(FileInfo)`, `Delete()`), `SimaticInterfaces`
  (`Create(name)`; `Name*`, `Enabled*`, `UseStringNodeIds*`, `Export`, `Delete`),
  `ReferenceNamespaces` (`Create(name, FileInfo xml)`; `Enabled*`, `GenerateNodes*`, ...),
  `AccessControl` (роли и права пространств имён).
- Узлы интерфейса через объектную модель не видны: содержимое — только файл XML
  (`Export` / `Import`).
- OPC UA-сервер CPU включается атрибутом `OpcUaServer` элемента `OPC UA_1`
  (`hw_set_device_item_attributes`); нужен ли он включённым для создания интерфейса — выяснить.

## Что сделать

1. Проба на временной станции `MCPT_` с CPU 1511 V2.9 или новее (проект сохранён, запись в
   откатываемой транзакции, по одному вызову с отметкой `TRY`): получение службы, чтение трёх
   коллекций, `Create`, запись `Enabled`, `Export` пустого и непустого интерфейса, `Import`
   экспортированного файла обратно, `Delete`. Записать, что из этого требует включённого
   сервера OPC UA, лицензии или компиляции.
2. `plc_get_opcua_interfaces(softwarePath)` — чтение: серверные и SIMATIC-интерфейсы и
   ссылочные пространства имён с признаками (`Enabled`, автор, время изменения).
3. `plc_manage_opcua_interfaces(softwarePath, actions)` — действия `create`, `update`
   (`enabled`, `author`, `comment`), `delete`, `import` (файл XML в интерфейс), `export` (в
   файл); `kind`: `server`, `simatic`, `namespace`. Всё или ничего. Экспорт пишет файл и проект
   не меняет — если удобнее, вынести его в читающий инструмент `plc_export_opcua_interface`.
4. Каркас — как у остальных: `Operation.Run`, `PortalException`, `Guarded`; новые файлы
   `Portal.OpcUa.cs` и `McpServer.OpcUa.cs` с заголовком-находками (`recipes.md`).
5. `AccessControl` (роли OPC UA и права пространств имён) в задачу **не входит**: это защита,
   её ведёт Opus. Только упомянуть в заголовке файла, что она есть.
6. Тесты, `docs/tools-list.txt`, `Test_703`, вызовы в `tools/smoke/write.json`, README (оба),
   `CHANGELOG.md`.

## Готово, когда

На временном ПЛК интерфейс создаётся, экспортируется, импортируется обратно и удаляется через
сервер; `plc_get_opcua_interfaces` на трёх ПЛК проверочного проекта отвечает (пустым списком
либо интерфейсами) без ошибки.

## Риски

Если проба оборвалась после `TRY` и TIA Portal закрылся — вызов в таблицу опасных
(`context.md`), `tools/start-tia.ps1`, сказать Biz. Этот вызов не повторять.
