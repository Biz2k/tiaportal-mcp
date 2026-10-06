# Export and import

Blocks, types and tag tables as XML, SIMATIC documents or source files.

| Tool | Kind |
|---|---|
| [`export_objects`](#export_objects) | write |
| [`import_objects`](#import_objects) | write |
| [`preview_import`](#preview_import) | read |

## export_objects

Universal tool to export PLC objects (blocks, types, tag tables) to files. format must be 'xml', 'document', or 'source'. Set object_paths to empty to export everything (supported for block/type folders).

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to PLC software |
| `object_paths` | array of string | yes | object_paths: list of object paths or groups (e.g. 'Main', 'MyGroup') |
| `format` | string | yes | format: 'xml', 'document', or 'source' |
| `exportPath` | string | yes | exportPath: target directory |

## import_objects

Universal tool to import files into the PLC. format must be 'xml', 'document', or 'source'. conflict_resolution is 'overwrite' (a block or type of the same name in the target group is replaced; one in another group makes the import fail) or 'skip' (a name that is taken is left alone and reported as skipped); 'rename' is not offered. A 'source' file is compiled into the group its folder implies (target_group does not apply to it). Every file gets its own result; nothing is reported as imported unless TIA Portal imported it

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to PLC software |
| `file_paths` | array of string | yes | file_paths: list of file paths to import |
| `format` | string | yes | format: 'xml', 'document', or 'source' |
| `target_group` | string | no (default ``) | target_group: optional group path to import into (e.g. 'MyGroup'). Leave empty for root. |
| `conflict_resolution` | string | no (default `overwrite`) | conflict_resolution: 'overwrite' (default) or 'skip' |

## preview_import

Report what importing a directory would create, overwrite or collide with, without touching the project. Checks each file against the objects already in the PLC, including the rule that a PLC data type name must be unique across the whole PLC - importing an existing type name into a different group fails even with importOption 'Override'

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `importPath` | string | yes | importPath: directory holding the files to import (.s7dcl source documents or .xml) |
| `kind` | string | yes | kind: what the files contain - 'type' for PLC data types, 'block' for program blocks |
| `groupPath` | string | no (default ``) | groupPath: the group the import would target; empty means the root of that area. A leading system folder segment is accepted |

