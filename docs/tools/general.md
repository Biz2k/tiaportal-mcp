# General

Connection, project and session tools.

| Tool | Kind |
|---|---|
| [`close_project`](#close_project) | write |
| [`connect`](#connect) | session |
| [`disconnect`](#disconnect) | session |
| [`doctor`](#doctor) | read |
| [`get_project`](#get_project) | read |
| [`get_project_tree`](#get_project_tree) | read |
| [`get_state`](#get_state) | read |
| [`get_tia_instances`](#get_tia_instances) | read |
| [`open_project`](#open_project) | session |
| [`open_tia_project`](#open_tia_project) | write |
| [`save_as_project`](#save_as_project) | write |
| [`save_project`](#save_project) | write |

## close_project

Close the current TIA-Portal project/session

No parameters.

## connect

Connect to a running TIA Portal; the first one unless processId or projectPath picks another ('get_tia_instances' lists them). Fails when none is running, unless startIfNotRunning is set

| Parameter | Type | Required | Description |
|---|---|---|---|
| `startIfNotRunning` | boolean | no (default `False`) | startIfNotRunning: start a new TIA Portal window when none is running (default false) |
| `processId` | integer | no (default `0`) | processId: attach to the instance with this process id (see 'get_tia_instances'); 0 means not set |
| `projectPath` | string | no (default ``) | projectPath: attach to the instance that has this project open, by full path or file name; empty means not set |

## disconnect

Disconnect from TIA-Portal

No parameters.

## doctor

Diagnose the TIA-Portal environment: connection, open project, active and installed TIA-Portal versions, Openness user group membership

No parameters.

## get_project

Get open local project/session

No parameters.

## get_project_tree

Get the project structure: devices, device groups and device items. By default a text tree (about 25 000 characters on a large project: narrow it with 'depth' and 'filter'). With structured = true a flat list of nodes instead, each with the 'path' that 'hw_get_devices', 'hw_*', 'net_*' and 'hw_get_device_item_info' accept and, for an item that carries software, the 'softwarePath' the plc_* and unified_* tools take

| Parameter | Type | Required | Description |
|---|---|---|---|
| `depth` | integer | no (default `0`) | depth: levels shown below the project, 0 = all (default). 1 = devices and groups only, 2 = their top-level items |
| `filter` | string | no (default ``) | filter: regular expression on names; keeps the matching nodes and the nodes above them. Empty = no filter (default) |
| `structured` | boolean | no (default `False`) | structured: true returns 'nodes' with paths instead of the text tree (default false) |

## get_state

Get the state of the TIA-Portal MCP server

No parameters.

## get_tia_instances

List the running TIA Portal instances (process id, open project, mode) without connecting. Use it to pick the instance for 'connect' when several are open

No parameters.

## open_project

Open a TIA-Portal local project/session

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | path: defines the path where to the project/session |

## open_tia_project

Connect to the running TIA Portal if not already connected, open the given project or session, and return the device and PLC software paths the other tools need. Replaces the connect, open_project, get_project_tree sequence. TIA Portal must already be running

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | path: full path of the .apXX project or .alsXX session file on the machine running this server |

## save_as_project

Save current TIA-Portal project/session with a new name

| Parameter | Type | Required | Description |
|---|---|---|---|
| `newProjectPath` | string | yes | newProjectPath: defines the new path where to save the project |

## save_project

Save the current TIA-Portal local project/session

No parameters.

