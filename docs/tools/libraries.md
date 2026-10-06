# Libraries

The project library, global libraries and master copies.

| Tool | Kind |
|---|---|
| [`get_libraries`](#get_libraries) | session |
| [`get_library_types`](#get_library_types) | read |
| [`get_master_copies`](#get_master_copies) | session |
| [`instantiate_master_copy`](#instantiate_master_copy) | write |
| [`open_global_library`](#open_global_library) | session |

## get_libraries

Lists the project library and any globally opened libraries

No parameters.

## get_library_types

List the types of a library with their versions, and say which system each type belongs to: 'unified' (WinCC Unified), 'classic' (WinCC Comfort / Advanced / Professional), 'plc', or 'universal' (not tied to a system, e.g. icons and graphics). For a WinCC Unified type each version carries the 'ContainedType' value that 'unified_manage_faceplate' takes as faceplateType

| Parameter | Type | Required | Description |
|---|---|---|---|
| `libraryName` | string | no (default `ProjectLibrary`) | libraryName: 'ProjectLibrary' (default) or the name of an opened global library; 'get_libraries' lists them |
| `system` | string | no (default ``) | system: return only the types of one system - 'unified', 'classic', 'plc' or 'universal'; empty for all |

## get_master_copies

Lists all Master Copies inside a specified library recursively

| Parameter | Type | Required | Description |
|---|---|---|---|
| `libraryName` | string | yes | libraryName: name of the library (use 'ProjectLibrary' for the project's library) |

## instantiate_master_copy

Instantiates a Master Copy from a library into the project (e.g. into a PLC's block or type group)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `libraryName` | string | yes | libraryName: name of the library (e.g., 'ProjectLibrary') |
| `masterCopyPath` | string | yes | masterCopyPath: relative path of the master copy (e.g. 'ProjectLibrary/MasterCopy_1') |
| `targetDeviceName` | string | yes | targetDeviceName: name of the PLC device to instantiate into |
| `targetGroupName` | string | yes | targetGroupName: target folder group inside the PLC (e.g. 'Program blocks' or 'PLC data types') |
| `targetType` | string | yes | targetType: either 'block' or 'type' |

## open_global_library

Opens a global library file (.al1x) into the current TIA Portal session

| Parameter | Type | Required | Description |
|---|---|---|---|
| `filePath` | string | yes | filePath: absolute path to the global library file |

