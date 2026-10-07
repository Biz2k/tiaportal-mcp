# General

Connection, project and session tools.

| Tool | Kind |
|---|---|
| [`close_project`](#close_project) | write |
| [`connect`](#connect) | session |
| [`create_project`](#create_project) | session |
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
| [`sec_get_plc_security`](#sec_get_plc_security) | read |
| [`sec_manage_opcua_users`](#sec_manage_opcua_users) | write |
| [`sec_manage_webserver_users`](#sec_manage_webserver_users) | write |
| [`sec_set_display_password`](#sec_set_display_password) | write |
| [`sec_set_plc_access_level`](#sec_set_plc_access_level) | write |
| [`sec_set_plc_configuration_protection`](#sec_set_plc_configuration_protection) | write |

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

## create_project

Create a new, empty TIA Portal project and open it. The path is the FOLDER of the new project, without an extension: 'C:\Projects\Plant' gives 'C:\Projects\Plant\Plant.apXX', and the answer has the full path of that file. TIA Portal holds one project at a time: while a project is open the call is refused - save and close it first ('save_project', 'close_project'). A folder that exists and is not empty is refused. The project is empty: add a PLC or an HMI with 'hw_create_device'. Author and comment of a project cannot be set through Openness

| Parameter | Type | Required | Description |
|---|---|---|---|
| `projectPath` | string | yes | projectPath: absolute path of the folder of the new project, without an extension, e.g. 'C:\Projects\Plant'; its last part becomes the name of the project. The folder must not exist or must be empty |

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

Save the open local project under a new folder and switch TIA Portal to it. The path is the FOLDER of the new project, without an extension: TIA Portal makes <name>.apXX inside it, and the answer gives the full path of that file. A path with a project extension, a relative path, a missing parent folder and a folder that is not empty are refused before anything is written

| Parameter | Type | Required | Description |
|---|---|---|---|
| `newProjectPath` | string | yes | newProjectPath: absolute path of the new project's folder, without an extension, e.g. 'C:\Projects\NewPlant'. The parent folder must exist; the folder itself must not exist or must be empty |

## save_project

Save the current TIA-Portal local project/session

No parameters.

## sec_get_plc_security

Read how a PLC is protected: the protection of confidential PLC configuration data, the access level (CPUs with access levels) or the access control by users (CPUs with firmware V4 and newer), whether the web server is on, the users of the web server and of the OPC UA server, and whether the display takes a password. No password is ever returned: TIA Portal does not give them out

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deviceItemPath` | string | yes | deviceItemPath: path of the CPU, e.g. 'Station_1/PLC_1' |

## sec_manage_opcua_users

Create or delete users of the OPC UA server of a CPU, or set their password ('update'), several at once, all or nothing. The OPC UA server and its authentication by user name have to be on first ('hw_set_device_item_attributes' on the item 'OPC UA_1'); otherwise TIA Portal refuses a new user. SENSITIVE: before calling, tell the user in plain words what will change on which PLC and get their consent. A password is the one the user gave in this conversation - never make one up, never repeat it in your answer. The server passes it to TIA Portal and keeps it nowhere.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deviceItemPath` | string | yes | deviceItemPath: path of the CPU, e.g. 'Station_1/PLC_1' |
| `actions` | array of object | yes | actions: the changes, applied in order; fields: action (create, update, delete), userName, password |

## sec_manage_webserver_users

Create, update or delete users of the web server of a CPU that has its own web server users (S7-1200, S7-1500 before firmware V4), several at once, all or nothing. A user has a name, a password and permissions; the user 'Everybody' is what anyone may do without logging in. The web server itself is switched on with 'hw_set_device_item_attributes' (WebserverActivate). SENSITIVE: before calling, tell the user in plain words what will change on which PLC and get their consent. A password is the one the user gave in this conversation - never make one up, never repeat it in your answer. The server passes it to TIA Portal and keeps it nowhere.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deviceItemPath` | string | yes | deviceItemPath: path of the CPU, e.g. 'Station_1/PLC_1' |
| `actions` | array of object | yes | actions: the changes, applied in order; fields: action (create, update, delete), userName, password, permissions |

## sec_set_display_password

Set the password that protects the display of an S7-1500 CPU. SENSITIVE: before calling, tell the user in plain words what will change on which PLC and get their consent. A password is the one the user gave in this conversation - never make one up, never repeat it in your answer. The server passes it to TIA Portal and keeps it nowhere.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deviceItemPath` | string | yes | deviceItemPath: path of the CPU, e.g. 'Station_1/PLC_1' |
| `password` | string | yes | password: the password of the display |

## sec_set_plc_access_level

Set the access level of a CPU with access levels (S7-1200, S7-1500 before firmware V4) and the passwords of its levels: accessLevel is what is allowed WITHOUT a password - FullAccess, ReadAccess, HMIAccess or NoAccess (FullAccessIncludingFailsafe on F-CPUs); passwordFor with password sets the password that opens a level; resetPasswordFor removes one. A wrong level locks people out of the PLC until the project is loaded again. A CPU with firmware V4 or newer has no access levels and is refused. SENSITIVE: before calling, tell the user in plain words what will change on which PLC and get their consent. A password is the one the user gave in this conversation - never make one up, never repeat it in your answer. The server passes it to TIA Portal and keeps it nowhere.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deviceItemPath` | string | yes | deviceItemPath: path of the CPU, e.g. 'Station_1/PLC_1' |
| `accessLevel` | string | no (default ``) | accessLevel: the level granted without a password; empty leaves it as it is |
| `passwordFor` | string | no (default ``) | passwordFor: the level the password opens, e.g. FullAccess; empty sets no password |
| `password` | string | no (default ``) | password: the password for passwordFor |
| `resetPasswordFor` | string | no (default ``) | resetPasswordFor: the level whose password is removed; empty removes none |

## sec_set_plc_configuration_protection

Change the protection of confidential PLC configuration data of a CPU: 'protect' sets a password, 'protect_all' protects all configuration data (with a password when one is given), 'unprotect' removes the protection (the present password is needed when one is set), 'unprotect_all', 'change_password' (password = the present one, newPassword), 'reset'. A password that is lost cannot be recovered from the project. The CPU has to be loaded again afterwards. SENSITIVE: before calling, tell the user in plain words what will change on which PLC and get their consent. A password is the one the user gave in this conversation - never make one up, never repeat it in your answer. The server passes it to TIA Portal and keeps it nowhere.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deviceItemPath` | string | yes | deviceItemPath: path of the CPU, e.g. 'Station_1/PLC_1' |
| `action` | string | yes | action: protect, protect_all, unprotect, unprotect_all, change_password or reset |
| `password` | string | no (default ``) | password: the password to set, or the present one for unprotect and change_password |
| `newPassword` | string | no (default ``) | newPassword: the new password, for change_password |

