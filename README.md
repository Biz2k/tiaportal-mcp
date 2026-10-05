# TIA-Portal MCP-Server

A MCP server which connects to Siemens TIA Portal.

## Features

- Connect to a running TIA Portal instance and open a project or a multiuser local session
- Read the PLC software: program blocks, PLC data types, tags and constants, watch and force
  tables, external source files, cross references, and the source text of blocks and types
- Create, rename, delete, copy, move, import and compile PLC objects
- Read the hardware topology, create devices, plug modules, build subnets and PROFINET IO systems
- Read and edit WinCC Unified / WinCC HMI screens, screen items, tags and faceplates
- Download hardware and software to a PLC or a simulated PLC

Starting and controlling PLCSIM is deliberately not part of this server; it lives in a separate
MCP server. `download_to_plc` expects the target to be running already.

## Command Line Arguments

| Argument                  | Description                                                                  |
| ------------------------- | ---------------------------------------------------------------------------- |
| `--tia-major-version <n>` | TIA Portal major version to bind against. Default `21`.                      |
| `--logging <1\|2\|3>`     | `1` stderr, `2` debug output, `3` Windows event log. Omit for no logging.    |
| `--doctor`                | Print the environment report and exit without starting the MCP server.       |
| `--read-only`             | Do not register the tools that change the project. See below.                |
| `--allow-write`           | Accepted for older configurations; writing is on by default, so it is a no-op. |
| `--debug-tools`           | Register the server-development tools (`hmi_debug_*`, `hmi_test_faceplate`). |

## Write mode

The tools that change the project are available by default. Start the server with `--read-only`
to leave them out.

- With `--read-only` the 58 project-changing tools are **not registered at all**, so they never
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
when the registered tools and that file disagree. 115 tools are registered by default.

Always available (57):

| Area                    | Tools |
| ----------------------- | ----- |
| Portal and state        | `connect`, `disconnect`, `get_state`, `doctor` |
| Project and session     | `open_tia_project`, `open_project`, `get_project`, `save_project`, `save_as_project`, `close_project` |
| Project structure       | `get_project_tree`, `get_devices`, `hw_get_device_info`, `get_device_item_info`, `get_hardware_topology`, `hw_search_catalog` |
| PLC software            | `get_plc_summary`, `plc_get_software_info`, `plc_get_software_tree`, `plc_compile_software` |
| Blocks                  | `plc_get_blocks`, `plc_get_blocks_hierarchy`, `plc_get_block_info`, `plc_get_block_data`, `get_block_interface`, `plc_get_block_source` |
| Types                   | `plc_get_types`, `plc_get_type_info`, `plc_get_type_source` |
| Tags and constants      | `plc_get_tag_tables`, `plc_get_tag_table_info`, `plc_get_tags`, `plc_get_tag_info`, `plc_get_constants` |
| Watch and force tables  | `plc_get_watch_tables`, `plc_get_watch_table_info`, `plc_get_force_tables` |
| External sources        | `plc_get_external_sources`, `plc_get_external_source_info`, `plc_generate_sources` |
| Search and references   | `plc_resolve_object_path`, `plc_find_in_code`, `plc_where_used`, `plc_get_cross_references` |
| Export and preview      | `export_objects`, `preview_import` |
| Libraries               | `get_libraries`, `open_global_library`, `get_master_copies` |
| HMI                     | `hmi_get_screens`, `hmi_get_screen_items`, `hmi_get_screen_item_properties`, `hmi_get_tags`, `hmi_get_connections`, `hmi_get_library_types`, `hmi_get_library_faceplates` |
| Download                | `get_download_targets` |

Left out with `--read-only` (58):

| Area                    | Tools |
| ----------------------- | ----- |
| Import                  | `import_objects`, `instantiate_master_copy` |
| Block and type groups   | `plc_create_block_group`, `plc_delete_block_group`, `plc_create_type_group`, `plc_delete_type_group` |
| Blocks                  | `plc_create_fb`, `plc_create_instance_db`, `plc_create_scl_block`, `plc_rename_block`, `plc_delete_block`, `plc_copy_block`, `plc_move_block`, `plc_compile_block` |
| Types                   | `plc_rename_type`, `plc_delete_type`, `plc_copy_type`, `plc_move_type` |
| Tag tables              | `plc_create_tag_table`, `plc_rename_tag_table`, `plc_delete_tag_table`, `plc_create_tag_table_group`, `plc_delete_tag_table_group` |
| Tags and constants      | `plc_create_tag`, `plc_update_tag`, `plc_delete_tag`, `plc_create_user_constant`, `plc_update_user_constant`, `plc_delete_user_constant`, `plc_manage_tag_table_entries` |
| Watch tables            | `plc_create_watch_table`, `plc_rename_watch_table`, `plc_delete_watch_table`, `plc_create_watch_table_group`, `plc_delete_watch_table_group` |
| External sources        | `plc_create_external_source`, `plc_delete_external_source`, `plc_create_external_source_group`, `plc_delete_external_source_group` |
| Hardware                | `hw_create_device`, `hw_plug_module`, `hw_delete_device` |
| Network                 | `net_connect_subnet`, `net_disconnect_subnet`, `net_create_io_system`, `net_connect_to_io_system` |
| HMI                     | `hmi_create_screen`, `hmi_delete_screen`, `hmi_create_screen_item`, `hmi_delete_screen_item`, `hmi_configure_screen_item`, `hmi_set_screen_item_property`, `hmi_set_unified_screen_item_event`, `hmi_configure_unified_trend_control`, `hmi_configure_unified_trend_companion`, `hmi_create_faceplate_instance`, `hmi_manage_unified_faceplate` |
| Download                | `download_to_plc` |

`plc_get_software_tree` accepts a `sections` argument - any comma separated subset of
`blocks,types,tags,watch,sources`, default `all` - to keep the output small on a large PLC.
`plc_get_cross_references` accepts `maxDepth` (1-3, default 1) for the same reason.

Paths used by these tools are **root-relative**: `1_Tests/FC_Block_1`, not
`Program blocks/1_Tests/FC_Block_1`. Use `get_project_tree` and `plc_get_software_tree` to
discover them; `plc_resolve_object_path` turns a bare name into a path.
TIA Portal allows `/` inside a name (a block group `Inputs/Outputs`, a station
`S7-1500/ET200MP station_1`). In a path such a slash is written `%2F`:
`Inputs%2FOutputs/AI_Handler`. Listings return paths in this form. The unescaped form
`Inputs/Outputs/AI_Handler` is accepted as well; if both a group `Inputs/Outputs` and a group
`Inputs` with a subgroup `Outputs` exist, the unescaped form means the nested one.

## Resources

- [TIA Portal Openness API Documentation](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows)
- [TIA Portal Openness API Overview](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/tia-portal-openness-api)
- [TIA Portal Openness API for automation of engineering workflows](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows)
- [TIA Portal Openness API for automation of engineering workflows - Export/Import Documentation](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/export/import)

## Requirements

- __.net Framework 4.8__ installed
- __Siemens TIA Portal V21__ installed and running on your machine
- Check if under `Environment Variables/User variable for user <name>` the variable `TiaPortalLocation` is set to `C:\Program Files\Siemens\Automation\Portal V21`
- User must be in Windows User Group `Siemens TIA Openness`

### Diagnose the environment

Run the server with `--doctor` to check all of the above without starting the MCP server:

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
└─ Write mode (--allow-write): disabled, read-only tools only
```

The same report is available to MCP clients through the `doctor` tool, which additionally returns
the findings as structured content. Both are read-only: they never connect to TIA Portal, open a
project, or change user group membership.

## TIA-Portal Versions

- __V21__ is the default version.
- Previous versions are also supported, but must use the `--tia-major-version` argument to specify the version.
- Export as documents (.s7dcl/.s7res) via `export_objects` requires TIA Portal V20 or newer.
- Import from documents (.s7dcl/.s7res) via `import_objects` also requires TIA Portal V20 or newer.
- The same for PLC data types - `export_objects`,
  `import_objects` - requires TIA Portal **V21** or newer:
  Openness only added `PlcType.ExportAsDocuments` and `PlcTypeComposition.ImportFromDocuments`
  in V21.

## SIMATIC Source Documents

A source document is the readable, git-diffable form of an object: `<Name>.s7dcl` holds the
declaration and body as SCL/LAD/STL text, the optional `<Name>.s7res` holds comments and
language resources. Every other export in this server writes SimaticML XML instead, which
diffs poorly.

The file names come from TIA Portal, not from this server: an export response lists the files
that were actually written, and a batch import discovers a document set by base name rather
than assuming one extension. Tag tables and watch tables have no document API in Openness V21
and remain XML-only.

With `preservePath` the export mirrors the project tree below the system folder - `Program
blocks` for blocks, `PLC data types` for types - using the folder name as TIA Portal reports it
in the current interface language. `import_objects`
accept that folder name back as a leading segment of `groupPath`, so an export can be fed
straight back in.

A PLC data type name is unique across the whole PLC, not just within its group. Importing a
name that already exists into a *different* group therefore fails with "an object with the name
... already exists in the plc", even with `importOption: Override`; point `groupPath` at the
group the type already lives in to replace it.

## Known Limitations

- As of 2025-09-02: Importing Ladder (LAD) blocks from SIMATIC SD documents requires the companion `.s7res` file to contain en-US tags for all items; otherwise import may fail. This is a known limitation/bug in TIA Portal Openness.
 - `export_objects` requires a fully qualified `blockPath` like `Group/Subgroup/Name`. If only a name is provided, the tool fails with an error result that may include suggestions for likely full paths.

### Limits imposed by the Openness API itself

These are not gaps in this server - the underlying API offers no operation for them.

- __No move or copy for blocks and types.__ `plc_copy_block`, `plc_move_block`, `plc_copy_type`
  and `plc_move_type` are composed from export and import. What follows from that:
  - The object must be consistent, because TIA Portal refuses to export an inconsistent one.
    Compile first.
  - A block name, a block number and a type name are unique within a PLC. A copy inside the same
    PLC therefore needs `newName`, and a copied block gets the first free number of its kind. To
    keep the name, copy into another PLC with `targetSoftwarePath`.
  - A move exports the object, deletes the original and imports it into the target group; name
    and number are kept. If the import fails, the object is imported back into its original
    group. Blocks that use a moved block (its instance DBs, its callers) are inconsistent
    afterwards until the PLC is compiled again.
- __No generic "create block".__ `PlcBlockComposition.CreateFB` only creates ProDiag blocks.
  `plc_create_fb` therefore creates an SCL block from a one-block source text and a LAD, FBD or
  STL block by importing a minimal SimaticML document; other languages (GRAPH, ...) are refused.
  A ProDiag block brings its own instance DB and the `ProDiagOB` with it. Every other kind of
  block arrives through `plc_create_scl_block` or `import_objects`.
- __Block numbers.__ Openness takes a block number literally even when auto numbering is requested
  - an instance DB created with number 0 really becomes `DB0`, which does not compile. The server
  picks the first free number itself and reports it in the response.
- __Read-only objects.__ System constants cannot be created or changed, the force table cannot
  be created or deleted (the system owns one per PLC), the default tag table cannot be deleted,
  and the system groups (`Program blocks`, `PLC data types`, `PLC tags`, ...) cannot be renamed
  or deleted. These all fail with a `NotSupported` message rather than an opaque Openness error.
- __No cross references__ for watch tables, force tables or external sources; `plc_get_cross_references`
  reports `NotSupported` for them.
- __Watch table entries__ cannot be created or deleted through this server yet. The Openness
  composition holding them exposes only a comment-row creator, so adding a real entry needs an
  untyped creation call whose required attribute names must first be read from the live API.
- __Safety programs.__ F-blocks and safety tags reject most edits, sometimes requiring the safety
  password. The server does not pre-guess Siemens' rules: the underlying error is passed through
  with its original message.
- __Know-how protected__ blocks and types are rejected before any edit, with a message asking you
  to remove the protection in TIA Portal first.

## Testing

- See `tests/TiaMcpServer.Test/README.md` for environment prerequisites and test asset setup.
- Standard command: `dotnet test` (run from the repo root).
- Test execution policy: offer to run tests, but only execute after explicit user confirmation. Details in `AGENTS.md`.

## Contributing

- See `agents.md` for guidance on working with agentic assistants and the test execution policy (offer to run tests only with explicit user confirmation).

## Error Handling

- The Portal layer throws `PortalException` with a short message and `PortalErrorCode`
  (`NotFound`, `InvalidParams`, `InvalidState`, `ExportFailed`, `ImportFailed`, `CreateFailed`,
  `DeleteFailed`, `RenameFailed`, `NotSupported`, `WriteDisabled`), and attaches `softwarePath`, `blockPath`, `exportPath` in `Exception.Data` while preserving `InnerException` on export failures.
- The MCP layer rethrows these as `McpException`. Since SDK 2.x, an `McpException` thrown from a tool is returned to the client as a `CallToolResult` with `isError: true` and the message as text content, rather than as a JSON-RPC error, so the model can read the reason and self-correct. For `ExportFailed` the message includes a concise reason from the underlying error; for `NotFound` it may suggest likely full block paths if a bare name was provided.
- Consistency required: TIA Portal never exports inconsistent blocks/types. Single export returns `InvalidParams` with a message to compile first. Bulk export skips inconsistent items and returns them in an `Inconsistent` list alongside `Items`.
- Standardization: Exception context metadata is attached in a single catch per portal method right before rethrow, not at inline throw sites. See `docs/error-model.md`.
- Since 0.2.0 this is implemented once, in the `Operation.Run` helper, which every portal method
  added for tags, watch tables, external sources, cross references and the write operations
  routes through. The older export/import methods still carry their hand-written equivalent of
  the same block; migrating them is tracked in `TODO.md`.
- `Operation.Run` also serializes all Openness calls behind a lock. Openness objects are not
  thread-safe and the MCP SDK may dispatch tool calls concurrently; with write tools enabled an
  unsynchronized race could corrupt project state rather than merely return stale data.

## MCP Protocol

- Built on the [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) .NET SDK **2.2.0**.
- Protocol revisions negotiated during `initialize`: `2024-11-05`, `2025-03-26`, `2025-06-18`, `2025-11-25` (the SDK picks the highest the client also supports).
- Every tool advertises a human-readable `title` and behaviour annotations (`readOnlyHint`, `destructiveHint`, `idempotentHint`, `openWorldHint`).
- 27 read tools and all 37 write tools publish an `outputSchema` and return `structuredContent`.
- Long-running export/import tools report progress via `notifications/progress` when the client supplies a `progressToken`.
- Tool failures are returned as tool results with `isError: true` (not JSON-RPC errors), so the model can read the message and retry.

## Transports

- Supported today: `stdio`
  - Program wires `AddMcpServer().WithStdioServerTransport()`.
  - For stdio, logs must go to stderr to avoid corrupting JSON-RPC.
- Available via SDK: `stream` (custom streams)
  - The SDK exposes `WithStreamServerTransport(Stream input, Stream output)` which can be used to host over TCP sockets or other streams.
  - Not wired in this repo yet.
- HTTP/Streamable HTTP: not implemented yet
  - `ModelContextProtocol` 2.2.0 ships its Streamable HTTP server transport in `ModelContextProtocol.AspNetCore`, which targets .NET 8+.
  - This server targets `net48` (required by TIA Openness), so Streamable HTTP cannot be hosted from this process.
  - A separate .NET 8+ proxy process would be required to expose this server over HTTP.

## Copilot Chat

- Example mcp.json, when using VS Code extension [TIA-Portal MCP-Server](https://marketplace.visualstudio.com/items?itemName=JHeilingbrunner.vscode-tiaportal-mcp) and TIA-Portal V18
  ```json
  {
      "servers": {
          "vscode-tiaportal-mcp": {
          "command": "c:\\Users\\<user>\\.vscode\\extensions\\jheilingbrunner.vscode-tiaportal-mcp-<version>\\srv\\net48\\TiaMcpServer.exe",
          "args": [
              "--tia-major-version",
              "18"
          ],
          "env": {}
          }
      }
  }
  ```

## Claude Desktop

- Create/Edit to add/remove server to `C:\Users\<user>\AppData\Roaming\Claude\claude_desktop_config.json`:

  ```json
  {
    "mcpServers": {
      "vscode-tiaportal-mcp": {
        "command": "<path-to>\\TiaMcpServer.exe",
        "args": [],
        "env": {}
      }
    }
  }
  ```
