# TIA Portal MCP Server

**English** | [Русский](README_ru.md)

> This document is available in two languages. Use the links above to switch.

An MCP server that lets an AI assistant work with Siemens TIA Portal through the Openness API:
read a project, edit PLC software, hardware and HMI, and download to a PLC.

## Features

- Connect to a running TIA Portal and open a project or a multiuser local session
- Read the PLC software: program blocks, PLC data types, tags and constants, watch and force
  tables, external source files, cross references, and the source text of blocks and types
- Create, rename, delete, copy, move, import and compile PLC objects
- Read the hardware topology, create devices, plug modules, build subnets and PROFINET IO systems
- Read and edit WinCC Unified: screens, screen items, events, tags, trends and faceplate instances
- Download hardware and software to a PLC or a simulated PLC

Starting and controlling PLCSIM is deliberately not part of this server; it lives in a separate
MCP server, [plcsim-mcp](https://github.com/Biz2k/plcsim-mcp). `download_to_plc` expects the target
to be running already.

## Requirements

- Windows with **.NET Framework 4.8**
- **Siemens TIA Portal V21** installed and **running** (earlier versions are selected with `--tia-major-version`)
- The Windows user is a member of the group `Siemens TIA Openness`
- The user environment variable `TiaPortalLocation` points at the installation, for example
  `C:\Program Files\Siemens\Automation\Portal V21`

Check all of this without starting the MCP server:

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

The same report is available to MCP clients through the `doctor` tool. Both are read-only: they
never connect to TIA Portal, open a project, or change group membership.

## Installation

A ready-to-run build is in [`Install/TiaMcpServer`](Install/TiaMcpServer). Copy the folder anywhere
and point your MCP client at `TiaMcpServer.exe`. Step-by-step instructions for Claude Code,
Claude Desktop and VS Code-style clients are in [`Install/INSTALL.md`](Install/INSTALL.md)
([Russian](Install/INSTALL_RU.md)).

Claude Code:

```bash
claude mcp add tia-mcp-server -- C:\path\to\TiaMcpServer\TiaMcpServer.exe
```

Clients configured with JSON (Claude Desktop uses `mcpServers`, VS Code uses `servers`):

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

The first time a new build attaches to TIA Portal, TIA Portal asks whether to grant Openness
access. Confirm it in the TIA Portal window.

## Quick start

1. Start TIA Portal. The server attaches to a running instance and does not start one on its own.
2. Call `open_tia_project` with the absolute path of a `.apXX` project or `.alsXX` session. It
   connects, opens the project and returns the PLC software paths.
3. Explore with `get_project_tree`, `plc_get_software_tree` and `hw_get_devices`.
4. Read or change objects with the `plc_*`, `hw_*`, `net_*` and `unified_*` tools.
5. Call `save_project` to keep the changes. Until then they exist only in memory.

## Command line arguments

| Argument                  | Description                                                                    |
| ------------------------- | ------------------------------------------------------------------------------ |
| `--tia-major-version <n>` | TIA Portal major version to bind against. Default `21`.                        |
| `--read-only`             | Do not register the tools that change the project. See below.                  |
| `--logging <1\|2\|3>`     | `1` stderr, `2` debug output, `3` Windows event log. Omit for no logging.      |
| `--doctor`                | Print the environment report and exit without starting the MCP server.         |
| `--debug-tools`           | Register the server-development tools (`unified_debug_*`).                      |
| `--allow-write`           | Accepted for older configurations; writing is on by default, so it is a no-op. |

## Write mode

The tools that change the project are available by default. Start the server with `--read-only`
to leave them out.

- With `--read-only` the 57 project-changing tools are **not registered at all**, so they never
  appear in `tools/list`. A model cannot call what it cannot see.
- Without it they are registered and annotated `destructiveHint: true`, so a client can still
  prompt before each call.
- `get_state` and the `--doctor` report both expose `allowWrite`, so a client can tell whether the
  tools are missing by configuration rather than by version.
- Write operations change the project **in memory only**. Every write response says so;
  `save_project` persists the changes (it saves the session when a multiuser local session is
  open).
- Each write runs inside a TIA Portal transaction when TIA Portal grants one, so a failed write
  rolls back and a successful one is a single entry in the undo stack.

`export_objects` and `plc_generate_sources` are not gated: they only write files on the machine
running the server and never modify the project.

## Tools

The authoritative list of tool names is [`docs/tools-list.txt`](docs/tools-list.txt); a test fails
when the registered tools and that file disagree. 114 tools are registered by default. A short
description of each, in Russian, is in [`Implemented_Tools.md`](Implemented_Tools.md).

Always available (57):

| Area                    | Tools |
| ----------------------- | ----- |
| Portal and state        | `connect`, `disconnect`, `get_state`, `doctor` |
| Project and session     | `open_tia_project`, `open_project`, `get_project`, `save_project`, `save_as_project`, `close_project` |
| Project structure       | `get_project_tree`, `hw_get_devices`, `hw_get_device_info`, `hw_get_device_item_info`, `hw_get_topology`, `hw_search_catalog` |
| PLC software            | `plc_get_summary`, `plc_get_software_info`, `plc_get_software_tree`, `plc_compile_software` |
| Blocks                  | `plc_get_blocks`, `plc_get_blocks_hierarchy`, `plc_get_block_info`, `plc_get_block_data`, `plc_get_block_interface`, `plc_get_block_source` |
| Types                   | `plc_get_types`, `plc_get_type_info`, `plc_get_type_source` |
| Tags and constants      | `plc_get_tag_tables`, `plc_get_tag_table_info`, `plc_get_tags`, `plc_get_tag_info`, `plc_get_constants` |
| Watch and force tables  | `plc_get_watch_tables`, `plc_get_watch_table_info`, `plc_get_force_tables` |
| External sources        | `plc_get_external_sources`, `plc_get_external_source_info`, `plc_generate_sources` |
| Search and references   | `plc_resolve_object_path`, `plc_find_in_code`, `plc_where_used`, `plc_get_cross_references` |
| Export and preview      | `export_objects`, `preview_import` |
| Libraries               | `get_libraries`, `open_global_library`, `get_master_copies`, `get_library_types` |
| WinCC Unified           | `unified_get_screens`, `unified_get_screen_groups`, `unified_get_scripts`, `unified_get_tag_table_groups`, `unified_get_logs`, `unified_get_logging_tags`, `unified_get_screen_items`, `unified_get_screen_item_properties`, `unified_get_tags`, `unified_get_tag_tables`, `unified_get_connections`, `unified_get_alarms`, `unified_get_alarm_classes`, `unified_get_text_lists`, `unified_get_graphic_lists` |
| Download                | `get_download_targets` |

Left out with `--read-only` (57):

| Area                    | Tools |
| ----------------------- | ----- |
| Import                  | `import_objects`, `instantiate_master_copy` |
| Block and type groups   | `plc_create_block_group`, `plc_delete_block_group`, `plc_create_type_group`, `plc_delete_type_group` |
| Blocks                  | `plc_create_fb`, `plc_create_instance_db`, `plc_create_scl_block`, `plc_replace_source`, `plc_rename_block`, `plc_delete_block`, `plc_copy_block`, `plc_move_block`, `plc_compile_block` |
| Types                   | `plc_rename_type`, `plc_delete_type`, `plc_copy_type`, `plc_move_type` |
| Tag tables              | `plc_create_tag_table`, `plc_rename_tag_table`, `plc_delete_tag_table`, `plc_create_tag_table_group`, `plc_delete_tag_table_group` |
| Tags and constants      | `plc_create_tag`, `plc_update_tag`, `plc_delete_tag`, `plc_create_user_constant`, `plc_update_user_constant`, `plc_delete_user_constant`, `plc_manage_tag_table_entries` |
| Watch tables            | `plc_create_watch_table`, `plc_rename_watch_table`, `plc_delete_watch_table`, `plc_create_watch_table_group`, `plc_delete_watch_table_group` |
| External sources        | `plc_create_external_source`, `plc_delete_external_source`, `plc_create_external_source_group`, `plc_delete_external_source_group` |
| Hardware                | `hw_create_device`, `hw_plug_module`, `hw_delete_device` |
| Network                 | `net_connect_subnet`, `net_disconnect_subnet`, `net_delete_subnet`, `net_create_io_system`, `net_connect_to_io_system`, `net_get_connections`, `net_create_connection`, `net_delete_connection` |
| WinCC Unified           | `unified_create_screen`, `unified_delete_screen`, `unified_manage_items`, `unified_manage_faceplate`, `unified_compile`, `unified_configure_trend_control`, `unified_manage_tags`, `unified_manage_tag_tables`, `unified_manage_screen_groups`, `unified_manage_scripts`, `unified_manage_tag_table_groups`, `unified_manage_logs`, `unified_manage_logging_tags`, `unified_manage_connections`, `unified_manage_alarms`, `unified_manage_alarm_classes`, `unified_manage_lists`, `unified_get_runtime_settings`, `unified_set_runtime_settings`, `unified_get_system_tags` |
| Download                | `download_to_plc` |

`plc_get_software_tree` accepts a `sections` argument - any comma separated subset of
`blocks,types,tags,watch,sources`, default `all` - to keep the output small on a large PLC.
`plc_get_cross_references` accepts `maxDepth` (1-3, default 1) for the same reason.

## Paths

Paths are **root-relative**: `1_Tests/FC_Block_1`, not `Program blocks/1_Tests/FC_Block_1`. Use
`get_project_tree` and `plc_get_software_tree` to discover them; `plc_resolve_object_path` turns a
bare name into a path.

TIA Portal allows `/` inside a name (a block group `Inputs/Outputs`, a station
`S7-1500/ET200MP station_1`). In a path such a slash is written `%2F`:
`Inputs%2FOutputs/AI_Handler`. Listings return paths in this form. The unescaped form
`Inputs/Outputs/AI_Handler` is accepted as well; if both a group `Inputs/Outputs` and a group
`Inputs` with a subgroup `Outputs` exist, the unescaped form means the nested one.

A device is found by its path from `hw_get_devices`, by its Openness name, or by the name of its CPU
as the project tree shows it (`PLC_1`). A name that fits several devices is rejected with the
candidate paths.

## Hardware and network

- `hw_search_catalog` finds the type identifiers that `hw_create_device` and `hw_plug_module` need,
  by article number or product name.
- `hw_create_device` with an `OrderNumber:` or `GSD:` identifier creates a station around that head
  module. With a `System:Device.` identifier it creates an empty station: add the rack with
  `hw_plug_module` and an empty `parentItemName`, then plug the head module into the rack.
- A PROFINET IO system is built in a fixed order, and each step refuses to run before the previous
  one: `net_connect_subnet` (PLC interface) → `net_create_io_system` → `net_connect_subnet` (IO
  device interface, same subnet) → `net_connect_to_io_system`.

## Library types

`get_library_types` lists the types of the project library (or of an opened global library) with
their versions and says which system each one belongs to:

| `system`    | Meaning                                                                          |
|-------------|----------------------------------------------------------------------------------|
| `unified`   | WinCC Unified type: faceplate, script and the like                               |
| `classic`   | WinCC Comfort / Advanced / Professional type, e.g. a faceplate                   |
| `plc`       | PLC type: block or data type                                                     |
| `universal` | Not tied to a system, e.g. icons and graphics                                    |

Pass `system` to get the types of one system only. Openness has no attribute that separates
Comfort, Advanced and Professional, so the three are reported together as `classic`. A type that
arrives as the generic library type counts as `unified` when it has a minimum device version and
as `universal` when it has none.

## WinCC Unified

The `unified_*` tools work on WinCC Unified, on a Unified panel as well as on a Unified PC station.
`softwarePath` is the device name followed by the runtime item, e.g. `HMI_1/HMI_RT_1`.

### Screen items

`unified_manage_items` creates, updates, upserts and deletes items on screens, several at once. Each
property gets either a static value or a dynamization, and `events` attaches scripts:

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

- A plain value is a static value of the property's own type: a number, a boolean, a string, an
  enum member by name, a color as `#RRGGBB` or by name.
- `{ "resourceList": "...", "tag": "..." }` shows the entry of a text or graphic list that matches
  the tag (see below).
- `{ "tag": "..." }` binds the property to an HMI tag, `{ "script": "..." }` gives it a script
  dynamization, `{ "dynamization": "none" }` removes the dynamization.
- A tag binding takes options beside `tag`: `"readOnly"`, `"indirect"` (the tag is a String tag that holds the name of
  the tag to read), and either `"formula"` (`"'Tag_1'*2+1"`) or `"mapping"`: `{"type": "range", "entries": [{"from": 0,
  "to": 30, "value": "#00FF00"}, {"from": 31, "to": 70, "value": "Yellow", "flashing": true, "rate": "Fast",
  "alternate": "#808080"}]}`, `{"type": "singlebit", "entries": [{"bit": 0, "value": "Red"}, {"bit": 1, "value": "Green"}]}`
  or `{"type": "none"}`. Every range row needs both `from` and `to`: the rows "up to" and "from" a value cannot be
  created (Openness gives the range type of a row as read-only) - use a very large `to` for an open end.
- A script takes `"async"`, `"globalDefinitions"` (one area for all script dynamizations of the screen) and
  `"trigger"`: `"T1s"` (`T100ms` to `T10s`), `"AutomaticTags"`, `"Disabled"`, `{"type": "Tags", "tags": ["Tag_1"]}` or
  `{"type": "CustomCycle", "cycle": "Cycle name"}`. The type may be left out: `{"tags": [...]}` is the trigger `Tags`, `{"cycle": "..."}` is `CustomCycle`.
- `{ "expression": "formula" }` gives the property an expression, `{ "flashing": { "condition": "Always", "rate": "Fast",
  "color": "#FF0000", "alternateColor": "#0000FF" } }` makes a color property flash.
- After the writes TIA Portal validates the object (`Validate()`): a screen, graphic, tag, connection, list or cycle
  that does not exist, an initial value that does not fit the data type, and a script dynamization that no tag
  triggers fail the action, although Openness stored them without an error. Findings that were there before the
  write come back as `notes`. In a formula the tags are checked (`'Tag_1'` in single quotes); the syntax of a formula
  or a script is not - `unified_compile` compiles the HMI and reports such errors with the screen and the item.
- `events` also takes `{ "script": "...", "async": true, "globalDefinitions": "..." }` per event; `propertyEvents` sets the
  script that runs when a property changes (`{ "ProcessValue": "..." }`, `"ProcessValue.QualityCodeChange"` for the
  quality code, which needs a tag binding).
- A text is given as plain text and stored in the format WinCC Unified uses; a string sets every
  project language, `{ "texts": { "en-US": "..." } }` sets single ones.
- `events` maps an event name to its script; an empty script removes the handler. An unknown event
  name is answered with the events the item has. An empty `itemName` addresses the screen itself.
- Which item types exist depends on the device: a PC station has no `HmiText`, for instance, and
  TIA Portal says so.
- A call applies **all of its actions or none**. If one fails, the error names the action and the
  property, and the project is left as it was.

`unified_configure_trend_control` adds a trend to an `HmiTrendControl` and binds it to its data
source. An `HmiTrendCompanion` is an ordinary item: its `SourceTrendControl` property names the
trend control.

### Tags, tag tables and connections

`unified_manage_tags` creates, updates, upserts and deletes HMI tags, several at once:

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

- A new tag is an internal `Int` tag in the default tag table unless `tagTable` and
  `properties` say otherwise. An empty `Connection` turns a tag back into an internal one.
- A symbolic PLC tag needs `Connection` and `PlcTag`; the data type follows the PLC tag. An
  absolute one needs `Connection`, `AccessMode`, `DataType` and `Address`. The order in which
  you write them does not matter.
- `Name` renames the tag. `Comment` takes a string or `{ "en-US": "..." }`.
- `DisplayName` is refused: writing it through Openness closes TIA Portal.
- A tag cannot be moved to another tag table; delete it and create it there.

`unified_manage_tag_tables` creates, renames and deletes tag tables. Deleting a table deletes its
tags; the default tag table cannot be deleted.

`unified_manage_screen_groups` creates, renames and deletes screen groups (a group in a group is
written `Parent/Child`); deleting a group deletes its screens. `unified_create_screen` takes a
`group`, `unified_get_screens` returns the group of every screen and can filter by it. Screen names
are unique in the whole HMI, so every other tool finds a screen by its name alone.

`unified_get_scripts` reads the global script modules (global definitions, functions, the list of exported
functions); `unified_manage_scripts` creates or replaces a module **as a whole** and reads it back. TIA Portal
stores some code it cannot run mangled (a default parameter `f(a, b = 5)` becomes `b___5`), so such a write is
refused and rolled back. A plain syntax error (`return a *;`) is stored as written and the server cannot see it:
`unified_compile` with `pathFilter` `Scripts/<module>` reports it with line and column. Modules cannot be deleted
or renamed through Openness.

`unified_get_logs` / `unified_manage_logs` read and change data logs and alarm logs (size, duration, storage device,
segment, backup; settings by name such as `Settings.LogMaxSize`); audit trails are read-only.
`unified_get_logging_tags` / `unified_manage_logging_tags` archive HMI tags into data logs. A trend shows an
archived tag with the data source `<process tag>:<logging tag>` in `unified_configure_trend_control`. A structured
tag keeps its logging tags on its members, so the process tag is a path such as `AI_DB_CP10-U1.field_input_EUF`.
Deleting a log leaves its logging tags pointing at it (Openness does not touch them); renaming a log renames it in
them. `Cycle` is named `T500ms`, `T1s`, `T2s`, `T5s`, `T10s`; a cyclic logging tag may not be faster than 500 ms.

`unified_manage_connections` creates, updates and deletes connections. With `partner` - the
path of a PLC of the project - a new connection is an **integrated** one, on which HMI tags can
name PLC tags symbolically:

```json
{
  "softwarePath": "HMI_1/HMI_RT_1",
  "actions": [
    { "action": "create", "connectionName": "HMI_Connection_1", "partner": "PLC_1" }
  ]
}
```

- The HMI and the PLC need interfaces on a common subnet (`net_connect_subnet`). The tool picks
  the first such pair; `localInterface` and `partnerInterface` choose others.
- Without `partner` the connection is not integrated: set `CommunicationDriver` in `properties`
  and the address in `driverProperties`, e.g. `{ "Protocol.RemStAddress": "192.168.0.10" }` -
  `unified_get_connections` lists the parameters. Tags on it use absolute addresses.
- `properties` also takes `Comment`, `DisabledAtStartup` and `Name`.
- The partner of an existing connection cannot be changed; delete it and create it again.

All three apply all of their actions or none. A tag or a connection that is still in use is
deleted without a warning from TIA Portal.

### Alarms

`unified_manage_alarms` creates, updates, upserts and deletes discrete and analog alarms:

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

- A discrete alarm is raised by a bit of an HMI tag (`RaisedStateTag`, `RaisedStateTagBitNumber`,
  `TriggerMode`), an analog alarm by a limit (`RaisedStateTag`, `Condition`, `ConditionValue`).
- `EventText`, `EventText1` .. `EventText9` and `InfoText` take a string for every project
  language or `{ "en-US": "..." }` for single ones. `unified_get_alarms` returns them as plain text.
- The tag and the alarm class have to exist: Openness itself would accept any name.
- `unified_manage_alarm_classes` sets `Priority`, `StateMachine`, `Log` and the look of each
  state as `"RaisedState.BackColor": "#FFA500"` (states: `RaisedState`, `ClearedState`,
  `AcknowledgedState`, `AcknowledgedClearedState`; properties: `BackColor`, `TextColor`,
  `Flashing`). System classes cannot be deleted.

### Text lists and graphic lists

Openness has no objects for the entries of a list: it only exports and imports lists as YAML
files. `unified_get_text_lists`, `unified_get_graphic_lists` and `unified_manage_lists` do that
behind the scenes and show the entries plainly:

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

- An entry stands for one value (`value`), a range (`from` and `to`), a value and above
  (`from` alone), a value and below (`to` alone), or is the default entry (`"default": true`),
  shown for every value no other entry covers.
- A text list entry carries `text`, a graphic list entry `graphic` - the name of a graphic in
  the project graphics. TIA Portal does not check that name.
- A list is written **as a whole**: `entries` replaces everything the list had.
- TIA Portal drops what it does not understand in an import without an error. The tool therefore
  reads the list back after writing it and fails the call - undoing it - if anything differs.
- A list that is a library type is not among the lists of the HMI and cannot be read or written.

A list is bound to a screen item with `unified_manage_items`: the property shows the entry that
matches the value of the tag - a text list on a text property, a graphic list on a graphic one.

```json
{ "action": "update", "screenName": "Screen_1", "itemName": "Mode_Text",
  "properties": { "Text": { "resourceList": "Modes", "tag": "Pump1_Mode" } } }
```
### Runtime settings, system tags and screen windows

`unified_get_runtime_settings` returns the runtime settings of an HMI as one object: start screen, screen
resolution, auto log-off and the groups for the OPC UA server, login lock, exclusive operation, reporting,
telemetry, process diagnostics and the project languages with their fonts. A setting the device version does
not have is named under `NotAvailable`. `unified_set_runtime_settings` writes by name, a group with a dot and
a language by its name; all settings apply or none:

```json
{ "softwarePath": "HMI_1/HMI_RT_1",
  "settings": { "StartScreen": "Start", "MaxLoginRuntimeSettings.EnableLockAfterNumberOfAttempts": true,
                "MaxLoginRuntimeSettings.MaxLoginErrors": 5, "LanguageAndFonts.Russian (Russia).Enable": true } }
```

`unified_get_system_tags` lists the read-only system tags (`nameFilter`, `limit`, `offset`).

A screen window (a screen shown inside a screen) is an ordinary item: `unified_manage_items` with
`"itemType": "HmiScreenWindow"` and the property `"Screen": "<screen name>"`.

### Faceplates

A faceplate instance has its own tool, because what can be set on it is not fixed: it is the
interface of the faceplate type. `get_library_types` lists the types with the
`ContainedType` value of each version; `unified_manage_faceplate` creates or updates one instance
and returns its interface:

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

- A **tag interface** takes the name of an HMI tag as a plain value, or `{ "tagParameter": "..." }`.
- A **property interface** takes a static value, `{ "tag": "..." }` or `{ "script": "..." }`.
- `{ "dynamization": "none" }` removes a dynamization.
- Openness does not say which of the two an interface property is. A dynamization of the wrong
  kind is refused with a hint, and nothing is changed.
- Call it with `action: "update"` and no values to read the interface of an existing instance.

## Downloading to a PLC

1. Start the PLC or the PLCSIM instance yourself and make sure its address matches the project.
2. `get_download_targets` lists the targets as `mode / PC interface / target interface`.
3. `download_to_plc` takes those three values plus `hardware` and `software`.

The CPU is neither stopped nor started unless you pass `stopPlc` / `startPlc`; a download that needs
a stop is refused with that explanation. The response lists every configuration step TIA Portal
raised, the answer given to it, and the messages of the result. A step the server has no answer for
keeps TIA Portal's own preset and is listed as such; pass `selections` (`StepType=Option`) to decide
differently. There is no preview: a call loads.

## TIA Portal versions

- **V21** is the default. Earlier versions need `--tia-major-version`.
- Source documents (`.s7dcl` / `.s7res`) for blocks need TIA Portal V20 or newer.
- Source documents for PLC data types need **V21** or newer: Openness only added
  `PlcType.ExportAsDocuments` and `PlcTypeComposition.ImportFromDocuments` in V21.
- The verification of this server was done on V21. `plc_create_fb` for LAD, FBD and STL uses a
  SimaticML template taken from a V21 export and has not been tried on earlier versions.

## SIMATIC source documents

A source document is the readable, git-diffable form of an object: `<Name>.s7dcl` holds the
declaration and body as SCL/LAD/STL text, the optional `<Name>.s7res` holds comments and language
resources. The `xml` format of `export_objects` writes SimaticML XML instead, which diffs poorly.

The file names come from TIA Portal, not from this server: an export response lists the files that
were actually written. Tag tables and watch tables have no document API in Openness V21 and remain
XML-only.

A PLC data type name is unique across the whole PLC, not just within its group. Importing a name
that already exists into a *different* group therefore fails even when overwriting; target the
group the type already lives in.

## Projects and instances

- `connect` attaches to the only TIA Portal that has a project open; with several, pass `processId` or `projectPath`
  (`get_tia_instances` lists them). `get_state` says what to do when the server is not connected.
- `save_as_project` takes the **folder** of the new project, without an extension (`C:\Projects\NewPlant`); TIA Portal makes
  `NewPlant.apXX` in it and the answer gives that path. TIA Portal then works on the copy. The parent folder must exist and the folder
  itself must not exist or must be empty.
- The server handles one tool call at a time, so a `close_project` never disposes what a running read still uses.

## Documentation

- [`docs/tools/`](docs/tools/README.md) - every tool with its parameters, one page per area (generated from the descriptions in the code by `tools/make-tool-docs.ps1`).
- [`docs/recipes/`](docs/recipes/README.md) - scenarios of several tools, each run on a real project.
- [`docs/tools-list.txt`](docs/tools-list.txt) - the names of all tools; [`Implemented_Tools.md`](Implemented_Tools.md) - the same with a line each (Russian).
- [`docs/error-model.md`](docs/error-model.md) - how errors are raised and reported.
- [`CHANGELOG.md`](CHANGELOG.md) - what changed, with the list of renamed tools.

## Known limitations

- Importing ladder (LAD) blocks from source documents requires the companion `.s7res` file to
  contain en-US entries for all items; otherwise the import may fail. This is a limitation of TIA
  Portal Openness (observed 2025-09-02).
- **Watch table entries** cannot be created or deleted through this server yet.
- **A subnet cannot be deleted** through this server; `net_connect_subnet` creates one when needed.
- **HMI tools are WinCC Unified only.** For WinCC Comfort, Advanced and Professional the Openness
  API has no object model for screens: a screen cannot be created and its items cannot be read or
  changed, only exported and imported as XML. The `unified_*` tools refuse such an HMI with that
  explanation. What is known about the classic systems is kept in `docs/hmi-classic-notes.md`
  for a later set of tools.

Limits imposed by the Openness API itself - no input makes these work:

- **No move or copy for blocks and types.** `plc_copy_block`, `plc_move_block`, `plc_copy_type` and
  `plc_move_type` are composed from export and import. What follows from that:
  - The object must be consistent, because TIA Portal refuses to export an inconsistent one.
    Compile first.
  - A block name, a block number and a type name are unique within a PLC. A copy inside the same
    PLC therefore needs `newName`, and a copied block gets the first free number of its kind. To
    keep the name, copy into another PLC with `targetSoftwarePath`.
  - A move exports the object, deletes the original and imports it into the target group; name and
    number are kept. If the import fails, the object is imported back into its original group.
    Blocks that use a moved block (its instance DBs, its callers) are inconsistent afterwards until
    the PLC is compiled again.
- **No generic "create block".** `PlcBlockComposition.CreateFB` only creates ProDiag blocks.
  `plc_create_fb` therefore creates an SCL block from a one-block source text and a LAD, FBD or STL
  block by importing a minimal SimaticML document; other languages (GRAPH, ...) are refused. A
  ProDiag block brings its own instance DB and the `ProDiagOB` with it. Every other kind of block
  arrives through `plc_create_scl_block` or `import_objects`.
- **Block numbers.** Openness takes a block number literally even when auto numbering is
  requested - an instance DB created with number 0 really becomes `DB0`, which does not compile.
  The server picks the first free number itself and reports it in the response.
- **Read-only objects.** System constants cannot be created or changed, the force table cannot be
  created or deleted, the default tag table cannot be deleted, and the system groups
  (`Program blocks`, `PLC data types`, `PLC tags`, ...) cannot be renamed or deleted. These fail
  with a `NotSupported` message rather than an opaque Openness error.
- **No cross references** for watch tables, force tables or external sources.
- **Safety programs.** F-blocks and safety tags reject most edits, sometimes requiring the safety
  password. The underlying error is passed through with its original message.
- **Know-how protected** blocks and types are rejected before any edit, with a message asking you
  to remove the protection in TIA Portal first.
- **No download preview.** The configuration steps of a download can only be seen by answering
  them, and answering them is what starts the load.

## Error handling

- A failed tool returns a result with `isError: true` and a message the model can act on, not a
  JSON-RPC error. The message carries the reason reported by TIA Portal, an error code and the
  paths the call was about, for example
  `CreateFB failed: <Openness text> [code: CreateFailed; softwarePath: 'PLC_1'; groupPath: 'Tests']`.
- Error codes: `NotFound`, `InvalidParams`, `InvalidState`, `ExportFailed`, `ImportFailed`,
  `CreateFailed`, `DeleteFailed`, `RenameFailed`, `NotSupported`.
- TIA Portal never exports inconsistent blocks or types. A single export fails with a message to
  compile first; a bulk export skips inconsistent items and lists them.
- All Openness calls are serialized behind one lock: Openness objects are not thread-safe and the
  MCP SDK may dispatch tool calls concurrently.

The design is described in [`docs/error-model.md`](docs/error-model.md).

## MCP protocol and transports

- Built on the [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) .NET
  SDK **2.2.0**. Protocol revisions negotiated during `initialize`: `2024-11-05`, `2025-03-26`,
  `2025-06-18`, `2025-11-25`.
- Every tool advertises a human-readable `title` and behaviour annotations (`readOnlyHint`,
  `destructiveHint`, `idempotentHint`, `openWorldHint`). Most tools publish an `outputSchema` and
  return `structuredContent`.
- No prompts are registered; the tool descriptions carry the same guidance.
- Transport: **stdio** only. For stdio, logs go to stderr so they do not corrupt JSON-RPC.
- Streamable HTTP is not available from this process: the SDK ships it for .NET 8+, while this
  server targets `net48`, which TIA Openness requires. A separate proxy process would be needed.

## Building and testing

```powershell
dotnet build TiaMcpServer.sln -c Release
```

The result is in `src\TiaMcpServer\bin\Release\net48`.

Tests that need no TIA Portal (tool registration, error texts, paths, block transfer, download
arguments, command line):

```powershell
dotnet test tests\TiaMcpServer.Test\TiaMcpServer.Test.csproj -c Release --filter "FullyQualifiedName~Test7|FullyQualifiedName~Test8|FullyQualifiedName~Test9|FullyQualifiedName~Test10|FullyQualifiedName~Test11|FullyQualifiedName~Test12"
```

The remaining tests need a running TIA Portal and the project assets described in
[`tests/TiaMcpServer.Test/README.md`](tests/TiaMcpServer.Test/README.md).

If you work on this repository with an AI assistant, read [`AGENTS.md`](AGENTS.md) first: tests and
anything that touches TIA Portal are run only after explicit confirmation.

## Project documents

| Document | Content |
| -------- | ------- |
| [`CHANGELOG.md`](CHANGELOG.md) | What changed in each version |
| [`docs/tools-list.txt`](docs/tools-list.txt) | The registered tool names |
| [`Implemented_Tools.md`](Implemented_Tools.md) | One-line description of each tool (Russian) |
| [`docs/error-model.md`](docs/error-model.md) | How errors are raised and reported |
| [`docs/PLAN.md`](docs/PLAN.md) | Open work and proposals (Russian) |
| [`TODO.md`](TODO.md) | Longer-term task list (Russian) |
| [`docs/handoff/README.md`](docs/handoff/README.md) | For developers: project context, open tasks, Openness API references (Russian) |
| [`tools/README.md`](tools/README.md) | Development scripts: probing Openness, driving the server, finishing a change |
| [`Install/INSTALL.md`](Install/INSTALL.md) | Installation and client integration |

## Resources

- [TIA Portal Openness API documentation](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows)
- [Openness export/import documentation](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/export/import)

## Origin and license

This project started as a fork of
[heilingbrunner/tiaportal-mcp](https://github.com/heilingbrunner/tiaportal-mcp) by J. Heilingbrunner
and is now developed independently. Licensed under the MIT License, see
[`LICENSE.txt`](LICENSE.txt).
