# Change Log

## [Unreleased]

### Added (development)

- `tools/`: the scripts this series of work was done with - `mcp-call.ps1` drives a built server
  over stdio, `openness-probe.ps1` tries Openness calls one at a time with writes rolled back,
  `openness-reflect.ps1` lists what Openness offers in a namespace, `finish.ps1` fixes line
  endings, builds, runs the unit tests and updates `Install`, `start-tia.ps1` starts TIA Portal
  with the test project.
- `docs/handoff/`: project context, recipes for adding a tool, the open work split into tasks,
  and references of the Openness classes for WinCC Unified taken by reflection from V21.

### Changed (WinCC Unified faceplate interface)

- Number and color interface properties of a faceplate are set and read back (checked on a type with Int64, UInt64, Double and Color
  properties); values are reported culture-independent (`3.5`, not `3,5`).

### Added (WinCC Unified system text lists)

- `unified_get_text_lists` with `system: true` reads the system text lists of the HMI (alarm texts, error reasons; 22 on the
  test panel). They can only be read. The debug tool `unified_debug_screen_item` has an output schema now.

### Changed (WinCC Unified system alarm classes)

- `unified_manage_alarm_classes`: a system class refuses `Priority` and `Name` with an explanation, and `Log` has to name an
  existing alarm log (the names are listed otherwise). Colors and `Log` of a system class can be changed; it cannot be deleted.

### Added (WinCC Unified alarms: acknowledgement and parameter tags)

- `unified_get_alarms` shows the acknowledgement tags with their bits and the parameter tags; `unified_manage_alarms` sets
  `AlarmParameterTags` as an array of up to 10 HMI tag names (checked) and refuses bit numbers outside 0..63, which Openness
  would accept. Checked on the PC station.

### Added (WinCC Unified tag table groups)

- __`unified_get_tag_table_groups`__ and __`unified_manage_tag_table_groups`__: list, create, rename and delete groups of tag
  tables (nested ones as `Parent/Child`); `unified_manage_tag_tables` takes a `group` for a new table. Names of tables and of
  groups are unique in the whole HMI; deleting a group deletes its tables and their tags (reported); a table cannot be moved.
- 129 tools are registered by default.

### Added (WinCC Unified tags: scaling, substitute value, limits)

- `unified_manage_tags` sets linear scaling (`LinearScaling`, `HmiStartValue`, `HmiEndValue`, `PlcStartValue`, `PlcEndValue`),
  the substitute value (`"SubstituteValue.SubstituteValueUsage"`, `"SubstituteValue.Value"`) and the limits
  (`"InitialMaxValue.ValueType"` None/Constant/Tag, `"InitialMaxValue.Value"`, and the same for the minimum); a limit that
  names a tag is checked. `unified_get_tags` shows them. They are "disabled fields" on an internal tag: the error now says
  an external tag is needed first. Thresholds cannot be created through Openness ("New thresholds are not supported").

### Fixed (TIA Portal closed by a call)

- After a call that closes TIA Portal (`NonRecoverableException`) the server believed it was still connected and answered
  every later call with "Access to a disposed object of type 'Siemens.Engineering.Project' is not possible". It now
  recognises the loss, drops the connection and says what happened: TIA Portal was closed by the call, unsaved changes are
  lost, start it and call `connect`. `get_state` reports `isConnected: false`, also when TIA Portal was closed by the user.
  Disposing a transaction on a dead TIA Portal no longer replaces the real error. "No project is open" says "Not connected to
  TIA Portal" when that is the case.
- Audit of `GetAttributeInfos` (task 05): safe on every screen item type, dynamization, event handler, tag, tag member,
  connection, device, block, type and subnet that could be made or found in the test project; only the WinCC Unified alarm
  closes TIA Portal. A guard test keeps generic attribute access out of the alarm, log, script and screen group code.

### Added (WinCC Unified logs and logging tags)

- __`unified_get_logs`__ and __`unified_manage_logs`__: data logs, alarm logs (create, update, upsert, delete) and
  audit trails (read only). Settings by name - `Settings.LogMaxSize`, `Settings.LogTimePeriod` (`{"days": 7}`),
  `Settings.StorageDevice`, `Segment.SegmentStartTime` (ISO), `Backup.BackupMode`... Renaming a log renames it in
  the logging tags; deleting one leaves them pointing at it, which is reported.
- __`unified_get_logging_tags`__ and __`unified_manage_logging_tags`__: the archiving of an HMI tag into a data log.
  Data log and trigger tag are checked, the smoothing times are set in the order Openness needs.
- `unified_configure_trend_control` checks a data source of the form `<process tag>:<logging tag>` - how a trend shows an
  archived tag (as on the PC station of the test project); the process tag may be a member path `Tag.Member`.
- `ConvertHmiValue` reads `DateTime` (ISO) and `TimeSpan` (`hh:mm:ss` or seconds).
- Found on V21: a new log on a panel starts on a storage device the panel then refuses (only the medium of the
  existing logs is accepted).
- Logging tags of a structured tag sit on its __members__ (`HmiTag.Members`, recursive): the first version of the tools
  looked at top-level tags only and found 0 of the panel's 279 logging tags. `tagName` is now a path `Tag.Member`;
  `TriggerTag` takes paths too, quoted as TIA Portal writes them (`"Tag-Name".Member`).
- `Cycle` is a cycle name: `T100ms`, `T250ms`, `T500ms`, `T1s`, `T2s`, `T5s`, `T10s` are accepted (case-insensitive),
  `T30s`, `T1min` and plain times are not. With the mode `Cyclic` anything below 500 ms fails TIA Portal's consistency check
  (panel and PC station); the server says so before calling Openness. A cyclic logging tag without a cycle is accepted and
  reported in `Notes`.
- 127 tools are registered by default.

### Added (WinCC Unified script modules)

- __`unified_get_scripts`__ and __`unified_manage_scripts`__: read global script modules (global definitions,
  functions, exported functions with parameters) and create or replace them. Openness has no object model for
  the content, only export and import of `<name>.hmi.yml` + `<name>.hmi.js`; the tools do that in a temporary
  folder. `Import` replaces a module of the same name as a whole.
- TIA Portal accepts code it cannot parse and stores it mangled: a default parameter `f(a, b = 5)` comes back
  as `f(a, b___5)`, a syntax error returns success with the function body gone, and a comment between
  functions may move above the global-definitions marker. Every write is therefore read back and compared
  without comments and whitespace outside strings; a difference fails the call and rolls it back.
- Script modules cannot be deleted or renamed: `HmiScriptModule` has no `Delete`. `delete` answers with
  that explanation; a module can be emptied with `update` and empty `functions`.
- 123 tools are registered by default.

### Fixed (WinCC Unified screens in groups)

- Screens inside screen groups were invisible to every `unified_*` tool: they looked in
  `HmiSoftware.Screens`, which holds only the top-level screens (14 of the 74 screens of the test
  panel). Screens are now found in groups and nested groups; names are unique in the whole HMI
  (Openness answers "ValueIsNotUnique" otherwise), so a screen is still addressed by its name.
- `unified_get_screens` returns the `group` of each screen and takes a `group` filter;
  `unified_create_screen` takes a `group` (it has to exist; an unknown one is an error that lists
  the groups, not a silent new group).
- __`unified_get_screen_groups`__ and __`unified_manage_screen_groups`__: list, create, rename and
  delete screen groups, nested ones as `Parent/Child`. Deleting a group deletes its screens.
- Screen properties through `unified_manage_items` (empty `itemName`) checked: `Width`, `Height`,
  `ScreenNumber`, `BackColor`, `BackGraphic` and `DisplayName` (`{"texts": {...}}`) are written.
  Unlike `HmiTag.DisplayName`, the display name of a screen is harmless.
- 121 tools are registered by default.

### Changed (WinCC Unified lists)

- __Breaking: `unified_manage_text_lists` is now `unified_manage_lists`__ and writes graphic lists as
  well as text lists (`kind`). An entry can be a single value, a range (`from` and `to`), a value
  and above (`from`), a value and below (`to`) or the default entry (`default`). The file forms were
  taken from lists made by hand in TIA Portal.
- `unified_get_text_lists` and `unified_get_graphic_lists` report the type of each entry;
  `unified_get_graphic_lists` returns entries instead of the raw export.
- Corrected: an entry without value keys in the export is the entry for the value 0, not a default
  entry; the default entry is marked `IsDefaultEntry`.
- __`unified_manage_items` binds lists__: `{"resourceList": "List", "tag": "Tag"}` on a text or
  graphic property shows the list entry that matches the tag.
### Added (WinCC Unified text lists)

- __`unified_get_text_lists`__ and __`unified_manage_text_lists`__. Openness offers no objects for the
  entries of a list, only export and import of YAML files; the tools do that in a temporary folder
  and present entries as values with their texts per language. A list is written as a whole.
  Because the import drops entries it does not understand without an error, every write is read
  back and compared, and a difference fails the call. Value ranges, default entries and bit-number
  lists cannot be written.
- __`unified_get_graphic_lists`__: names and the exported YAML as it is. A graphic list can be
  deleted through `unified_manage_text_lists`; creating one is not supported.
- 119 tools are registered by default.
### Added (WinCC Unified alarms, integrated connections)

- __`unified_manage_alarms`__ and __`unified_get_alarms`__: discrete and analog alarms - the tag and
  bit or limit that raise them, alarm class, texts per language, priority, area, origin. Texts are
  given and returned as plain text; the document format Unified stores is handled by the tool. The
  tag and the alarm class are checked, because Openness accepts names that do not exist.
- __`unified_manage_alarm_classes`__ and __`unified_get_alarm_classes`__: priority, state machine and
  the colors of the four alarm states.
- __`unified_manage_connections` creates integrated connections.__ With `partner` set to the path of
  a PLC, the connection is made on the hardware side (`CommunicationManagement` of the HMI runtime
  item) between two interfaces on a common subnet, and HMI tags on it can name PLC tags
  symbolically. The earlier statement that Openness cannot do this was wrong: it cannot do it
  through `HmiSoftware.Connections`.
- 116 tools are registered by default.

### Notes

- Calling `GetAttributeInfos()` on a WinCC Unified alarm closes TIA Portal (`NonRecoverableException`,
  V21): the class declares an `AuditClass` attribute the object does not have. The alarm tools use
  the typed properties only and never touch `AuditClass`.
### Added (WinCC Unified tags and connections)

- __`unified_manage_tags`__: create, update, upsert and delete HMI tags, several per call - internal
  tags, symbolic PLC tags (`Connection` + `PlcTag`) and absolute ones (`Connection`, `AccessMode`,
  `DataType`, `Address`). The properties are applied in the order Openness needs, whatever order
  they are given in. `DisplayName` is refused: writing it through Openness ends in a
  `NonRecoverableException` that closes TIA Portal (found on V21, 2026-10-06).
- __`unified_manage_tag_tables`__: create, rename and delete tag tables.
- __`unified_manage_connections`__: create, update and delete connections and set their driver
  parameters. A connection created through Openness is not integrated - `Partner`, `Node` and
  `Station` are read-only - and the tool says so.
- __`unified_get_tag_tables`__: the tag tables with their group and tag count.
- `unified_get_tags` reports the tag table of each tag and takes `tagTable`; an internal tag has no
  connection instead of `<Internal tag>`. `unified_get_connections` lists the driver parameters.
- 112 tools are registered by default.
### Changed (WinCC Unified tool set)

- __Breaking: the HMI tools are now `unified_*` and serve WinCC Unified only.__ The `hmi_*` tools
  had grown one by one, each first meant for one WinCC system, and ended up as overlapping tools
  that half-served the classic systems. They are now one set for WinCC Unified, verified on a
  Unified panel and a Unified PC station:
  `unified_get_screens`, `unified_get_screen_items`, `unified_get_screen_item_properties`,
  `unified_get_tags`, `unified_get_connections`,
  `unified_create_screen`, `unified_delete_screen`, `unified_manage_items`,
  `unified_manage_faceplate`, `unified_configure_trend_control`. 108 tools are registered by default.
- __`unified_manage_items` covers events.__ An action takes `events`: event name to script; an empty
  script removes the handler, an unknown event name is answered with the events the item has. An
  empty `itemName` addresses the screen itself. This replaces `hmi_set_unified_screen_item_event`,
  `hmi_create_screen_item` and `hmi_delete_screen_item`.
- __`unified_manage_faceplate`__ replaces `hmi_create_faceplate_instance` and
  `hmi_manage_unified_faceplate`. Faceplates keep a tool of their own because what can be set is the
  interface of the faceplate type, not a fixed list. It creates or updates one instance, sets
  container properties and interface values, and returns the interface. A tag interface takes a tag
  name or `{"tagParameter": ...}`; a property interface a static value, `{"tag": ...}` or
  `{"script": ...}`. The old tool created the tag dynamization under a wrong name and could not bind
  a property interface at all.
- `unified_get_tags` takes `nameFilter`; a Unified PC station easily has hundreds of tags.
- `unified_get_screen_item_properties` lists the interface of a faceplate instance.
- __`get_library_types`__ (was `hmi_get_library_types`) is a library tool, not an HMI one. It lists
  the types of the project library or of an opened global library and says which system each type
  belongs to: `unified`, `classic` (WinCC Comfort / Advanced / Professional - Openness does not
  separate the three), `plc` or `universal` (not tied to a system, e.g. icons and graphics).
  `system` filters by it. A WinCC Unified type gets a `ContainedType` value per version.
- The Unified layer follows the rest of the server: `PortalException` with an error code, the common
  lock, a transaction around every write, typed responses with an output schema.

### Removed

- `hmi_configure_unified_trend_companion`: a trend companion is an ordinary item, its
  `SourceTrendControl` property is set through `unified_manage_items`.
- `hmi_get_library_faceplates` (covered by `get_library_types`) and the debug tool
  `hmi_test_faceplate`.
- The code paths for WinCC Comfort / Advanced / Professional. Openness has no object model for
  their screens, so those paths could list screens and tags and nothing more. What works is parked
  in `Siemens/Classic/Portal.HmiClassic.cs` without tools; the findings are in
  `docs/hmi-classic-notes.md`.
### Fixed

- __Failures now say why.__ A failed Openness call used to reach the client as `CreateFB failed`,
  `DownloadToPlc failed` or `CopyBlock failed`, with the reason left in `InnerException`, which the
  MCP SDK never sends. `Operation.Run` now puts the Openness text into the message, and the new
  `McpServer.ToolError` appends the error code and the paths the call was about. See
  `docs/error-model.md`.
- __`get_project`, `get_devices`, `hw_get_device_info` and `get_device_item_info`__ returned a bare
  `An error occurred invoking '...'`: attribute values such as `FileInfo` and engineering objects
  could not be serialized. Attribute values are now reduced to JSON-safe values
  (`Helper.ToJsonSafe`), and one unreadable attribute no longer fails the whole call.
- __HMI tools, `export_objects`, `import_objects` and `get_download_targets`__ report a failure as
  an error result (`isError: true`) with its reason. Before, the HMI tools returned it as an
  ordinary result with the text `Unexpected error: ...`, or threw a plain `Exception` that the SDK
  replaced with a generic message. Clients that parsed `Unexpected error` out of a successful
  result must check `isError` instead.

- __`export_objects` with no paths and an unknown format__ ended in a `NullReferenceException`; it now
  says which formats exist.
- __`plc_manage_tag_table_entries`__ treats a JSON `null` in `action`, `name`, `dataType`,
  `logicalAddress` or `comment` like a missing value instead of failing on it.
- The solution builds without warnings (103 nullable-reference warnings before). Four optional HMI
  string parameters (`processValue`, `text`, `trendMode`, `lineColor`) are now declared nullable in
  their schema, matching their `null` default.
- __`import_objects` respects the write gate.__ It changes the project but was registered in read-only
  mode.
- __`download_to_plc` reports what happened.__ It answered TIA Portal's configuration steps through
  `dynamic` inside an empty `catch` and returned three numbers, so any download that needed a decision
  ended as `DownloadToPlc failed`. Every step is now recorded with its options and the answer given,
  the message tree of the result is returned (informational lines capped by `maxMessages`, errors and
  warnings always in full), and a failure names the stage and the step that blocked it. New
  parameters: `stopPlc` and `startPlc` (both default false - the CPU is not touched unless asked),
  `selections` to answer a step differently, `maxMessages`. A download that needs the CPU stopped is
  refused with that explanation when `stopPlc` is false.
- __Hardware, network and library edits respect the write gate.__ `hw_create_device`, `hw_plug_module`,
  `hw_delete_device`, the four `net_*` tools, `instantiate_master_copy`, `hmi_create_faceplate_instance`
  and `hmi_manage_unified_faceplate` change the project but were registered even without
  `--allow-write`. They now carry `[WriteTool]`, and the hardware and network tools run under the
  shared lock and inside a transaction like every other write.
- __`hw_create_device` accepts station types.__ `System:Device.ET200SP` was rejected because every
  identifier went to `CreateWithItem`. A `System:` identifier now creates an empty station; the rack is
  added with `hw_plug_module` and an empty `parentItemName`, then the head module goes into the rack.
  New parameter `stationName`; the response lists the path and the items of the new device.
- __`net_create_io_system` and `net_connect_to_io_system` say which step is missing__ - no subnet yet,
  no such IO system (with the ones that exist) - instead of failing inside Openness.
- __`hw_plug_module` explains a refusal__ with the occupied positions of the rack.
- __A wrong HMI path is an error.__ `hmi_get_screens` and `hmi_get_tags` returned an empty list for a
  path that does not exist; a PLC path produced a binder error.
- __`hmi_manage_unified_faceplate` reports failure as an error result__, and rejects an item that is
  not a faceplate container before touching its interface.
- __`plc_create_fb` creates function blocks.__ It always failed with `CreateFB failed`: Openness
  creates only ProDiag blocks through `CreateFB`. SCL blocks are now generated from a source text,
  LAD, FBD and STL blocks are imported from a minimal SimaticML document, ProDiag keeps using
  `CreateFB`; other languages are refused with an explanation.
- __`plc_create_instance_db` no longer creates `DB0`.__ The server picks the first free number, checks
  an explicit one before creating anything, and checks that the function block exists.
  `plc_create_fb` and `plc_create_instance_db` return the block number.
- __`plc_copy_block` and `plc_copy_type` work.__ A copy into the same PLC could never succeed, because
  names and block numbers are unique within a PLC. New parameters: `newName` (required inside the same
  PLC; the copy also gets a free block number) and `targetSoftwarePath` (copy into another PLC,
  keeping the name).
- __`plc_move_block` and `plc_move_type` work.__ The original is now deleted before the import instead
  of after it, and imported back if the import fails. The `overwrite` parameter is gone: it could
  not do anything useful and led to "Access to a disposed object".
- __`plc_compile_block` compiles.__ It answered "Block is not compilable" for every block, because the
  compiler is a service of the block, not an interface it implements.
- __Names containing `/` are addressable.__ TIA Portal allows the slash in group, table and station
  names (`Inputs/Outputs`, `S7-1500/ET200MP station_1`), but paths were split on every `/`, so
  `plc_resolve_object_path` returned `Inputs/Outputs/AI_Handler` and no tool could open it. Paths
  now write such a slash as `%2F` (`Inputs%2FOutputs/AI_Handler`), and the unescaped form is still
  accepted: a segment that does not resolve as written is joined with the following ones.
- __Device lookup is one resolver.__ `hw_get_device_info`, `get_device_item_info`, `get_devices` and
  every `hw_*` / `net_*` tool accept the same forms: the path, the Openness device name, the bare
  name inside a group, or the CPU name the project tree shows. Devices in the ungrouped devices
  group (distributed IO) are found and listed. `get_devices`, `hw_get_device_info` and
  `get_hardware_topology` return a `path`. A name matching several devices is rejected with the
  candidate paths instead of silently picking the first.
- __Exact names win over patterns.__ A block or type path whose last segment contains a regex
  character (`A5.01`) is matched literally first and as a regular expression only if no object has
  that name.

### Changed

- A `preservePath` export of a group whose name contains `/` now writes one folder
  (`Inputs%2FOutputs`) instead of nested ones (`Inputs\Outputs`); the result imports back.
### Removed

- __Breaking: `hmi_configure_screen_item` and `hmi_set_screen_item_property`__ are replaced by
  `hmi_manage_items`. They were two partial answers to one question: the first set six fixed
  properties and silently skipped the ones that failed; its `processValue` was a string that always
  became a tag binding, so a constant could not be set through it. The second set one property per
  call and could not bind a tag. 114 tools are registered by default.
### Changed

- __Writing is on by default, `--read-only` turns it off.__ The code already registered the write
  tools unconditionally while the documentation described an opt-in `--allow-write`; the flag did
  nothing and there was no way to switch writing off. `--read-only` now leaves the 58
  project-changing tools out of `tools/list`. `--allow-write` is still accepted and is a no-op.
- __Breaking: `connect` no longer starts TIA Portal.__ With no TIA Portal running it used to launch a
  new instance with its window. It now fails with an explanation; pass `startIfNotRunning=true` to
  get the old behaviour. `open_tia_project` likewise needs a running TIA Portal.
- __Prompts follow the tools.__ The prompt texts named tools that were renamed or removed. They now
  name the current tools; the 34 prompts for the per-kind export and import tools that
  `export_objects` and `import_objects` replaced are gone (78 prompts remain). Prompts for tools added
  since then (hardware, network, HMI, download) have not been written.
- Assembly version is 0.4.0 (it was still 0.1.0).
- `README.md` and `Implemented_Tools.md` describe the current tool names, flags and write mode.
- `README.md` is restructured around installation, a quick start and the tool areas, and no longer
  points at the VS Code extension of the original project. `README_ru.md` is its Russian
  counterpart; each links to the other at the top.
### Added

- __`hmi_manage_items`__: create, update, upsert and delete items on WinCC Unified screens, several per
  call, each with any number of properties. A property takes a static value of its own type (number,
  boolean, string, enum member by name, color) or a dynamization: `{"tag": ...}`, `{"script": ...}`,
  or `{"dynamization": "none"}` to remove one. Plain text is stored in the document format Unified
  uses for item texts. A call applies all of its actions or none.
- __`hw_search_catalog`__: search the installed hardware catalog by article number or name and get the
  type identifiers `hw_create_device` and `hw_plug_module` need.
- __`--debug-tools`__: `hmi_debug_reflect`, `hmi_debug_screen_item` and `hmi_test_faceplate` are
  development aids and are now registered only with this flag.
- `docs/tools-list.txt`: the tool names the server registers, as a baseline for spotting tools that
  disappear or get renamed.
- `Test8ErrorReporting`: tests for error texts and attribute serialization; they need no TIA Portal.

- __Batch CRUD operations (Stage 3 & 5)__: 
  - `plc_manage_tag_table_entries` for efficient bulk create/update/delete of tags and constants via JSON arrays.
  - `hmi_manage_items` for upserting HMI screen items in bulk was announced here before the tool
    existed; it is implemented now, see above.
  - `hmi_create_faceplate_instance` (formerly `create_hmi_faceplate_instance`) updated to robustly handle complex faceplate parameterization.
- __Consolidated Read operations (Stage 4)__: `plc_get_block_data` combines block information, interface, and source into a single call with flags.

### Changed

- __Architecture segmentation (Stage 2)__: All tool names have been segmented using strict prefixes (`plc_`, `hmi_`, `hw_`, `net_`) and normalized to `snake_case` for better LLM context predictability and modularity.
- __Universal Import/Export (Stage 1)__: Replaced 24+ individual XML import/export tools with two unified tools: `export_objects` and `import_objects`.
- __WinCC Unified Integration__: Improved HMI parameterization for faceplate properties mapping directly to `_Interface` instead of generic dynamizations.

- __`Portal` partial classes consolidated__: the 18 `Portal.*.cs` files are now `Portal.cs`,
  `Portal.Blocks.cs`, `Portal.Types.cs`, `Portal.Tags.cs` and `Portal.Devices.cs`, plus two folders:
  `Siemens/Documents/` (`Portal.Documents.*.cs`, six files) and `Siemens/Software/`
  (`Portal.Software.*.cs`, seven files). Members were moved verbatim; no signature, visibility or
  behaviour changes. The class-level notes of the retired files are kept as comments above their
  former sections.

- __`McpServer` partial classes follow the same layout__: the ten `McpServer.*.cs` partials are now
  `McpServer.cs`, `McpServer.Devices.cs`, `McpServer.Blocks.cs`, `McpServer.Types.cs` and
  `McpServer.Tags.cs`, plus `ModelContextProtocol/Documents/` (`McpServer.Documents.*.cs`) and
  `ModelContextProtocol/Software/` (`McpServer.Software.*.cs`). Tool names, attributes and behaviour are
  unchanged.
- __Document and source methods regrouped__: `Portal` and `McpServer` now have `Portal.Documents.cs` /
  `McpServer.Documents.cs` (the `...AsDocuments` exports and the `...FromDocuments` imports) and
  `Portal.Sources.cs` / `McpServer.Sources.cs` (every source file method, including `GetBlockSource` and
  `GetTypeSource`, which moved out of the Blocks/Types partials). The `Documents/` subfolders are gone.
  Members were moved verbatim.
- __`McpServerWrite` merged into `McpServer`__: the project-mutating tools now live in the matching
  `McpServer` partials (`McpServer.Blocks.cs`, `.Types.cs`, `.Tags.cs`, `Documents/McpServer.Documents.*.cs`),
  and the shared write plumbing (`Guarded`, `SaveHint`, response builders) in `McpServer.cs`. Tool
  names, attributes and behaviour are unchanged. The `--allow-write` gate is preserved: the 40 write tools
  carry the new `[WriteTool]` attribute and `Program.BuildTools` registers them only under
  `--allow-write`, so they stay out of `tools/list` as before. `WritePolicy.EnsureEnabled` still runs
  first in every write tool. The write-side `Join` helper is now `JoinPath` (it does not trim a
  trailing slash, unlike the preview `Join`). New tests: `Test7ToolRegistration`.
- __Breaking: XML tools renamed__. The tools and the `Portal` methods that read and write TIA Portal
  XML files now say so in their name: `ExportBlock` to `ExportXmlBlock`, `ExportBlocks` to
  `ExportXmlBlocks`, `ImportBlock` to `ImportXmlBlock`, `ExportType` to `ExportXmlType`, `ExportTypes` to
  `ExportXmlTypes` and `ImportType` to `ImportXmlType`. Parameters and results are unchanged. MCP clients,
  prompts and permission allow-lists that name the old tools must be updated. The prompts
  `ExportBlocks` and `ExportTypes` are renamed to `ExportXmlBlocks` and `ExportXmlTypes` as well.
  Likewise the source file exports follow the Export naming: `GenerateBlockSource` to
  `ExportSourceBlock` and `GenerateTypeSource` to `ExportSourceType` (tools and `Portal` methods;
  `GenerateSources` and `ImportSources` keep their names). `GenerateBlocksFromSource` is now
  `ImportSourceBlocks` (tool and `Portal` method; the Siemens API method of the same name is unaffected).
  The tag and watch table exports follow suit: `ExportTagTable` is now `ExportXmlTagTable` and
  `ExportWatchTable` is now `ExportXmlWatchTable` (tool, response type, `Portal` method and
  prompt).

### Added

- __`ExportXmlTagTable` prompt__: a prompt template for exporting a PLC tag table to XML through the
  `ExportXmlTagTable` tool, next to the existing block and type export prompts. Likewise prompts for the source
  file tools: `ExportSourceBlock`, `ExportSourceType`, `ImportSources` and `ImportSourceBlocks`, plus the
  convenience prompts `ImportAllSources` and `ImportAllSourcesKeepOnError`. Every other tool now has a prompt
  of the same name as well (80 prompts, generated from the tool descriptions and parameter descriptions), so
  all 99 tools are reachable as prompts.

## [0.3.0] - 2026-09-11

Generate TIA Portal external source files - the format the compiler reads back - from blocks and
PLC data types. The read tool surface grows from 56 to 59.

### Added

- __`GenerateBlockSource`__: writes one program block as an external source file. The extension
  is dictated by the object, not chosen by the caller: `.db` for data blocks, `.awl` for STL
  blocks, `.scl` for SCL blocks. Openness throws on a mismatch, so the mapping is derived rather
  than passed in. Data blocks are recognised by type instead of by `ProgrammingLanguage`, because
  the DB family spans several language values (`DB`, `CPU_DB`, `F_DB`, `Motion_DB`) that all
  write `.db`.
- __`GenerateTypeSource`__: writes one PLC data type as a `*.udt` external source file. Unlike
  `GetTypeSource` and `ExportTypeAsDocuments` this needs no TIA Portal V21, and the result can be
  imported again.
- __`GenerateSources`__: the comfort function - writes every block and PLC data type of one PLC
  software into a folder tree that mirrors the project groups, `<exportPath>/Program blocks/...`
  and `<exportPath>/PLC data types/...`, one file per object. Accepts `regexName` to narrow the
  set. This is the compilable counterpart to `ExportPlcAsSourceTree`, which snapshots the same
  tree as source documents and XML.
- All three take `withDependencies`, which maps to `GenerateOptions.WithDependencies` and pulls
  every object the subject uses - called blocks, instance DBs, UDTs - into the same file, so it
  compiles on its own. Off by default, which keeps one object per file and therefore a tree that
  diffs cleanly.

### Notes

- Openness generates sources only from data blocks and STL or SCL blocks. LAD, FBD, GRAPH and the
  rest have no textual form: the single-object tools reject them with the reason and a pointer to
  `ExportBlock` or `ExportAsDocuments`, and `GenerateSources` reports them in `Skipped` rather
  than failing the whole run. Inconsistent and know-how protected objects are handled the same
  way.
- Openness treats an existing file at the target path as an error, not an overwrite. Every other
  exporter in this server overwrites, so the target file is deleted first and a second run
  succeeds instead of failing.
- Filesystem only: generating a source does not modify the project, so these tools are not gated
  behind `--allow-write`. They are marked destructive, like the `Export*` tools, because they
  overwrite files.
- New files: `Siemens/Portal.GenerateSource.cs` and
  `ModelContextProtocol/McpServer.GenerateSource.cs`, kept apart from the `*.Source.cs` pair,
  which deals in source documents and SimaticML rather than in compilable sources.

### Added (write side)

- __`ImportSources`__: the bulk-import counterpart to `GenerateSources`. Walks a folder tree of
  `.db`/`.awl`/`.scl`/`.udt` files and compiles each back into a block or PLC data type, placed
  into the group its folder path implies - the same layout `GenerateSources` writes. Requires
  `--allow-write`, since unlike the `Generate*` tools this mutates the project.
- Each file goes through the two Openness steps there is no shortcut around: register it as a
  scratch `PlcExternalSource` (reusing the existing `CreateExternalSourceFromFile`), call
  `PlcExternalSource.GenerateBlocksFromSource` - which returns a mix of `PlcBlock` and `PlcType`
  objects from a single file, since the source's own content decides what comes out - then
  delete the scratch source again (reusing `DeleteExternalSource`), regardless of outcome.
- `keepOnError` maps to `GenerateBlockOption.KeepOnError`: successfully generated objects from a
  file are kept even when others in the same file fail. Openness gives up per-object
  success/failure reporting in that mode, which is a limitation of the underlying API, not of
  this wrapper.
- A folder whose implied group does not exist in the project fails that one file (reported in
  `Failures`) rather than being created automatically; the group structure is expected to
  already exist, since these files were themselves generated from objects that lived in it.
- New files: `Siemens/Portal.ImportSources.cs` and
  `ModelContextProtocol/McpServerWrite.ImportSources.cs`. Reuses
  `GetPlcBlockGroupByPath`/`GetPlcTypeGroupByPath` and the private `StripSystemRootSegment`
  helper (`Portal.Documents.cs`) rather than re-deriving the same "does this folder name match
  the localized system root" logic a third time.

## [0.2.0] - 2026-09-10

Complete the PLC software area of the Openness API: tags, constants, watch and force tables,
external sources, cross references, and create/rename/delete/move for blocks and types. The tool
surface grows from 31 tools to 44 read tools plus 37 project-mutating tools.

### Added

- __Write mode__, opt-in through the new `--allow-write` command line argument. The 37
  project-mutating tools live in a separate `McpServerWrite` tool type that is only registered
  when the flag is present, so without it they are absent from `tools/list` rather than merely
  refused when called. `WritePolicy.EnsureEnabled` additionally guards every write tool at
  runtime, because these are `public static` methods that the test suite invokes directly and
  that path bypasses tool registration. Filesystem-only exports are deliberately not gated: they
  never modify the project. Both `GetState` and the `--doctor` report now show `AllowWrite`.
- __PLC tags and constants__: `GetTagTables`, `GetTagTableInfo`, `GetTags`, `GetTagInfo`,
  `GetConstants` (user and/or system) and `ExportTagTable`. Write side: `CreateTagTable`,
  `DeleteTagTable`, `RenameTagTable`, `CreateTagTableGroup`, `DeleteTagTableGroup`,
  `ImportTagTable`, `CreateTag`, `UpdateTag`, `DeleteTag`, `CreateUserConstant`,
  `UpdateUserConstant`, `DeleteUserConstant`.
- __Watch and force tables__: `GetWatchTables`, `GetWatchTableInfo` (including entries),
  `GetForceTables` and `ExportWatchTable`. Write side: `CreateWatchTable`, `RenameWatchTable`,
  `DeleteWatchTable`, `CreateWatchTableGroup`, `DeleteWatchTableGroup`, `ImportWatchTable`.
- __External source files__: `GetExternalSources`, `GetExternalSourceInfo`,
  `CreateExternalSourceFromFile`, `DeleteExternalSource`, `CreateExternalSourceGroup`,
  `DeleteExternalSourceGroup` and `GenerateBlocksFromSource`.
- __Cross references__: `GetCrossReferences` for a whole PLC software or for one block, type,
  tag table, tag or block group. Because `Sources -> References -> Locations` nests three deep
  and source children recurse, the tool takes `maxDepth` (default 1, maximum 3) and reports a
  `Truncated` flag with counts instead of returning megabytes.
- __Blocks and types__: `CreateBlockGroup`, `DeleteBlockGroup`, `CreateTypeGroup`,
  `DeleteTypeGroup`, `DeleteBlock`, `RenameBlock`, `DeleteType`, `RenameType`, `CreateFB`,
  `CreateInstanceDB`, plus `CopyBlock`, `MoveBlock`, `CopyType` and `MoveType`.
- `Portal.GetTypePath(PlcType)`, the counterpart to `GetBlockPath(PlcBlock)`.
- `Operation.Run`, the single exception-decoration point that `docs/error-model.md` prescribes.
  It wraps a non-`PortalException` into one, stamps context into `Exception.Data`, logs once
  (nested calls do not re-log) and rethrows. It also serializes all Openness traffic behind a
  `Monitor`; a `SemaphoreSlim` would self-deadlock, because portal methods call one another.
- `PortalErrorCode` gains `ImportFailed`, `CreateFailed`, `DeleteFailed`, `RenameFailed`,
  `NotSupported` and `WriteDisabled`. Existing members keep their order and values.

- __PLC data types as SIMATIC Source Documents__ (TIA Portal V21+): `ExportTypeAsDocuments` and
  `ExportTypesAsDocuments`, plus the write-gated `ImportTypeFromDocuments` and
  `ImportTypesFromDocuments`. Until now only program blocks could be written as documents - the
  readable, git-diffable form where `<Name>.s7dcl` holds the SCL/LAD source text and the
  optional `<Name>.s7res` the comments; every other export produced SimaticML XML. Openness only
  added `PlcType.ExportAsDocuments` and `PlcTypeComposition.ImportFromDocuments` in V21, hence
  the higher version gate than the V20 block tools. Tag tables and watch tables have no document
  API in V21 and remain XML-only.
  File names come from TIA Portal rather than from a hardcoded extension: an export reports the
  files it actually wrote (`DocumentExportResult.ExportedDocuments`, unioned with what is on
  disk) and a batch import discovers a document set by base name across a known extension set.
  Deleting a previous export stays restricted to `.s7dcl`/`.s7res`, so a hand-written `.scl` or
  `.udt` in the same directory is never removed.
  Unlike `ExportBlocksAsDocuments`, which only logs its failures, the bulk type export returns
  its inconsistent and failed types so the response can name them. The two type import tools sit
  in the `--allow-write` surface, where project-mutating tools belong; the older
  `ImportFromDocuments` and `ImportBlocksFromDocuments` remain ungated, which is a known
  inconsistency in those block tools rather than a pattern the new tools follow.

- __Ten comfort tools__ that shorten the path between a question and an answer. All read-only
  except where noted, and none change an existing tool's contract.
  - `GetBlockSource` / `GetTypeSource` return an object's source text inline instead of making
    the caller export a file and open it. Openness has no in-memory block body, so the readers
    export into a temp scratch directory and remove it again. STL and mixed-language blocks
    have no SIMATIC Source Document at all, so those fall back to XML and say so in `Format`.
  - `GetBlockInterface` lists a data block's members from `DataBlock.Interface` without any
    export, and works on inconsistent blocks. Data blocks only - V21 Openness offers no
    interface accessor for FB, FC or OB.
  - `FindInCode` searches the program text with a regular expression. Every other filter in the
    server matches object names only.
  - `CompileSoftware` now returns the whole `CompilerResult.Messages` tree flattened to
    `{path, state, description}` with error and warning counts, instead of one stringified
    sentence. Warnings are a successful compile with detail; only errors fail the call.
  - `GetPlcSummary` replaces six discovery calls: counts per area, a programming-language
    histogram, and the inconsistent and know-how-protected objects.
  - `WhereUsed` answers "what uses this?" from a bare name, flattening the cross-reference tree.
  - `ResolveObjectPath` turns a bare or partial name into the root-relative path the other tools
    need, across all six object areas. The `Path` property is also no longer commented out on
    `ResponseBlockInfo` and `ResponseTypeInfo`, so `GetBlocks` and `GetTypes` finally return
    round-trippable paths - exporting one type is now one call instead of three.
  - `OpenTiaProject` connects, opens and returns the device and PLC software paths in one call.
  - `ExportPlcAsSourceTree` snapshots a whole PLC to a git-ready folder tree in one call.
  - `PreviewImport` reports what an import would create, overwrite or collide with, without
    touching the project - including the PLC-global data type name rule.
- __Atomic, undoable writes.__ Every one of the write tools now runs inside
  `ExclusiveAccess.Transaction`, so a tool call commits as a unit and appears in the TIA Portal
  undo stack as one named entry. A body that throws rolls back instead of leaving the project
  half-edited (verified against a live V21). If TIA Portal refuses exclusive access the write
  still runs unwrapped, so this can never turn a working write into a failure.

### Changed

- `GetSoftwareTree` renders three further sections - PLC tags, watch and force tables, and
  external source files - and takes a `sections` argument accepting any comma separated subset
  of `blocks,types,tags,watch,sources` (default `all`) so the output stays manageable on a large
  PLC. Existing single-argument callers are unaffected.
- `Portal` and `McpServer` are now `partial` and split by area (`Portal.Tags.cs`,
  `Portal.WatchTables.cs`, `Portal.MoveCopy.cs`, `McpServer.Tags.cs`, ...). The generic path
  helpers in `Portal.Resolve.cs` (`WalkGroups`, `BuildGroupPath`, `WalkRecursive`) now back the
  existing block and type resolvers as well, so all five group hierarchies share one traversal.
- `Diagnostics.Run` takes an optional `bool allowWrite`. The flag is passed in rather than read
  from `WritePolicy`, so the Siemens layer keeps no dependency on the MCP layer.
- 27 read tools and all 37 write tools publish an `outputSchema` and return `structuredContent`
  (previously 13).

### Fixed

- The software tree omitted external source files even though the `GetSoftwareTree` description
  had always promised them.
- `GetBlockPath` returned paths prefixed with the `Program blocks` system group, which
  `GetBlock` then rejected - so the "Did you mean ...?" suggestions on a failed `ExportBlock`
  named paths that could not be used. Path building now takes an `includeSystemRoot` flag:
  suggestions are root-relative and round-trip, while `preservePath` exports keep the existing
  on-disk layout unchanged.
- `ExportTagTable` with `preservePath` wrote the group structure directly below `exportPath`,
  while block and type exports place theirs below the system folder (`Program blocks`,
  `PLC data types`). Tag tables now land in `<exportPath>/PLC tags/...`, using the system group
  name as TIA Portal reports it in the current interface language. `ImportTagTable` accepts a
  leading `PLC tags` segment in `groupPath` so the exported layout can be fed straight back.
- `--doctor --allow-write` reported write mode as disabled. `WritePolicy.AllowWrite` was only
  assigned inside `RunStdioHost`, which `--doctor` returns before reaching; it is now set in
  `Main`.

### Known gaps

- `CreateWatchTableEntry` and `DeleteWatchTableEntry` are not implemented. `PlcWatchTable.Entries`
  is a `PlcTableCommentEntryComposition` whose only typed creator produces a comment row; a real
  entry requires the untyped `IEngineeringComposition.Create(typeof(PlcWatchTableEntry), ...)`
  path, whose required attribute names must first be read from `GetCreationInfos()` against a
  live project rather than guessed.
- Openness offers no move or copy operation for blocks and types, so `CopyBlock`, `MoveBlock`,
  `CopyType` and `MoveType` are composed from export, import and - for a move - deleting the
  source after the import succeeds. Two consequences are visible to callers: the object must be
  consistent, and its block number travels with it, so importing into the same PLC can collide.

## [0.1.0] - 2026-09-07

Upgrade to the current MCP .NET SDK and adopt the newer protocol surface.

### Breaking

- Tool failures are no longer JSON-RPC errors. `ModelContextProtocol` 2.x turns an
  `McpException` thrown from a tool into a `CallToolResult` with `isError: true`, carrying
  the message as text content. Clients that inspected JSON-RPC `error.code` (`InvalidParams`,
  `InternalError`, ...) must read the tool result's `isError` flag and message instead.
  `McpErrorCode` was removed from all 75 throw sites; error codes now live only on the
  derived `McpProtocolException`, which is reserved for protocol-level faults.

### Added

- Environment diagnostics ('doctor'), available two ways: the `--doctor` command line argument
  prints a report and exits without starting the MCP server, and the new `Doctor` tool returns the
  same report plus structured content to MCP clients. Both report the connection state, the open
  project, the active TIA major version, every installed TIA Portal version >= V21 (with a check
  that its Openness assemblies and the Portal executable are present), and membership in the
  `Siemens TIA Openness` user group. Read-only throughout: unlike `Openness.IsUserInGroup`, the
  new `Openness.CheckUserInGroup` never adds the user to the group.
- `Engineering.GetTiaPortalInstallPath(int)` resolves the install path of any TIA major version and
  no longer depends on the `TIAP{version}\TIA_Opns` registry sub key alone - installations that do
  not write that key are now found via their other product sub keys, which carry the same path.

### Changed

- Siemens TIA Portal Openness updated to V21. `Siemens.Collaboration.Net.TiaPortal.Packages.Openness` 20.0.1744190253 -> 21.0.1765349347 and
  `Siemens.Collaboration.Net.TiaPortal.Openness.Resolver` 1.1.1725480302 -> 2.0.1765367256. The default
  TIA major version is now 21 (`Openness.Initialize`, `Program.Main`); older versions remain reachable via
  `--tia-major-version`. `Engineering.Resolver` now also excludes `V21` directories when targeting another
  version, and the test `App.config` probing path points at `Portal V21`. A machine with TIA Portal V21
  Openness installed is now required to build.
- `ModelContextProtocol` 0.3.0-preview.4 -> 2.2.0; `Microsoft.Extensions.Hosting`
  10.0.0-preview.4 -> 10.0.10. Negotiated protocol revisions are now `2024-11-05`,
  `2025-03-26`, `2025-06-18` and `2025-11-25`.
- `initialize` now reports `serverInfo` (name, title, version) and `instructions` telling the
  client to call `Connect` and `OpenProject` first and how to discover path arguments.
- Progress reporting for `ExportBlocks`, `ExportTypes`, `ExportBlocksAsDocuments` and
  `ImportBlocksFromDocuments` moved from hand-rolled `notifications/progress` calls to the
  SDK-injected `IProgress<ProgressNotificationValue>`. The removed `IMcpServer` and
  `RequestContext<CallToolRequestParams>` parameters were SDK-injected and never part of the
  tool input schemas, so the wire-visible schemas are unchanged. Progress notifications are
  emitted only when the client supplies a `progressToken`, as before; the non-standard
  `Error` field previously sent on failure notifications is gone.
- Tools and prompts are registered explicitly (`WithTools`/`WithPrompts`) instead of by
  assembly scanning, giving a stable `tools/list` order.

### Added

- All 30 tools carry a `title` and behaviour annotations: 13 read-only `Get*` tools are
  `readOnlyHint: true`; project mutations (`SaveProject`, `SaveAsProject`, `CloseProject`),
  all export tools (they overwrite files already present at the target path) and all import tools
  are `destructiveHint: true`. Everything is `openWorldHint: false`.
- The 13 read-only tools publish an `outputSchema` and return `structuredContent`.

### Removed

- `src/TiaMcpServer/packages.config` — a stale packages.config-era leftover pinning
  `ModelContextProtocol` 0.2.0-preview.1. The project has used SDK-style `PackageReference`
  for some time and the build ignored this file.

## [0.0.16] - 2025-09-02

- New: ImportFromDocuments and ImportBlocksFromDocuments (V20+)
- Guard: Version checks for export/import as documents (V20+)
- UX: Pre-check .s7res for missing en-US tags; warnings surfaced in responses
- Docs: README updates, prompts note V20+ and known LAD en-US limitation
- Refactor: Updated all McpException throws to SDK signature with McpErrorCode
- Chore: Added TODOs for tests/docs

## [0.0.15] - 2025-08-30

- prompts improved
- long running tasks as async tasks

## [0.0.14] - 2025-08-18

- better structure/tree format
- new GetSoftwareTree()
- bugfixes

## [0.0.13] - 2025-08-14

- logging integrated
- prompts added

## [0.0.12] - 2025-08-07

- export path fixed

## [0.0.11] - 2025-08-07

- project structure formatted as markdown code

## [0.0.10] - 2025-08-07

- tool responses improved

## [0.0.9] - 2025-08-04

- export of blocks and types with 'preservePath' option
- new tools
- some infos with attributes

## [0.0.8] - 2025-08-01

- improved jsonrpc responses
- updated dependencies

## [0.0.7] - 2025-07-18

- new GetState()
- return values fixed

## [0.0.6] - 2025-07-16

- refactored code to use new TIA Portal API
- only blocks (OB/FB/FC/DB) and types (UDT) are now retrieved from the PLC software
- use regex to filter blocks and types
- import of blocks and types to PLC software

## [0.0.5] - 2025-07-11

- locating of plc software by softwarePath. This makes it possible to access plc software in groups/subgroups
- new tool: retrieving of project structure as text
- new tool: compile plc software

## [0.0.4] - 2025-06-30

- opens local session or projects, depending on project file extension

## [0.0.3] - 2025-06-23

- Release on Visual Studio Code Narketplace

