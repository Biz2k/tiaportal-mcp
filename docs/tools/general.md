# General

Connection, project and session tools.

| Tool | Kind |
|---|---|
| [`archive_project`](#archive_project) | session |
| [`close_project`](#close_project) | write |
| [`connect`](#connect) | session |
| [`create_project`](#create_project) | session |
| [`disconnect`](#disconnect) | session |
| [`doctor`](#doctor) | read |
| [`drive_get_objects`](#drive_get_objects) | read |
| [`drive_get_parameters`](#drive_get_parameters) | read |
| [`drive_manage_telegrams`](#drive_manage_telegrams) | write |
| [`drive_set_motor`](#drive_set_motor) | write |
| [`drive_set_parameters`](#drive_set_parameters) | write |
| [`get_accessible_devices`](#get_accessible_devices) | read |
| [`get_installed_software`](#get_installed_software) | read |
| [`get_project`](#get_project) | read |
| [`get_project_tree`](#get_project_tree) | read |
| [`get_state`](#get_state) | read |
| [`get_tia_instances`](#get_tia_instances) | read |
| [`open_project`](#open_project) | session |
| [`open_tia_project`](#open_tia_project) | write |
| [`retrieve_project`](#retrieve_project) | session |
| [`save_as_project`](#save_as_project) | write |
| [`save_project`](#save_project) | write |
| [`sec_get_plc_security`](#sec_get_plc_security) | read |
| [`sec_get_project_users`](#sec_get_project_users) | read |
| [`sec_manage_opcua_users`](#sec_manage_opcua_users) | write |
| [`sec_manage_project_roles`](#sec_manage_project_roles) | write |
| [`sec_manage_project_users`](#sec_manage_project_users) | write |
| [`sec_manage_webserver_users`](#sec_manage_webserver_users) | write |
| [`sec_protect_project`](#sec_protect_project) | write |
| [`sec_set_block_protection`](#sec_set_block_protection) | write |
| [`sec_set_display_password`](#sec_set_display_password) | write |
| [`sec_set_password_policy`](#sec_set_password_policy) | write |
| [`sec_set_plc_access_level`](#sec_set_plc_access_level) | write |
| [`sec_set_plc_configuration_protection`](#sec_set_plc_configuration_protection) | write |

## archive_project

Write a backup of the open project to one archive file (.zapXX), which TIA Portal unpacks by itself ('retrieve_project'). Offer it to the user before a large change. The project stays open and unchanged. It takes time: about 30 seconds for 100 MB of archive. A project with unsaved changes is refused - call 'save_project' first, an archive holds only what is saved. An existing archive of that name and a folder that does not exist are refused; nothing is overwritten

| Parameter | Type | Required | Description |
|---|---|---|---|
| `targetDirectory` | string | yes | targetDirectory: absolute path of the existing folder to put the archive in, e.g. 'C:\Backups' |
| `name` | string | yes | name: file name of the archive without a folder. TIA Portal adds no extension; the server adds .zapXX (the project's version) unless the name has it |
| `mode` | string | no (default ``) | mode: 'compressed' (default), 'discardRestorableDataAndCompressed' (smaller: without the data that restores the state of the project after a crash), 'none' or 'discardRestorableData' (these two write a folder, not a file) |

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

## drive_get_objects

List the SINAMICS drives of the project that Startdrive knows: for each drive object its path (what the other drive tools take), device and device type, drive object number, number of parameters and its telegrams with number and size. A drive is created with 'hw_create_device' and an identifier from the catalog ('hw_get_catalog', folder 'Drives & starters'). Needs SINAMICS Startdrive installed

| Parameter | Type | Required | Description |
|---|---|---|---|
| `devicePath` | string | no (default ``) | devicePath: only the drives of this device; empty (default) lists all of the project |

## drive_get_parameters

Read the offline parameters of a drive object: by name ('names', with the list of values of an enumerated parameter, the bits of a bit-coded one and the elements of an indexed one) or by a text found in the name or in the description ('filter', paged). A drive has thousands of parameters - ask for what you need. 'p' parameters are settings, 'r' parameters display values; an indexed parameter is 'p1120' with elements 'p1120[0]' ..., and only the elements carry values. These are the values of the project, not of a running drive

| Parameter | Type | Required | Description |
|---|---|---|---|
| `drivePath` | string | yes | drivePath: path of the drive from 'drive_get_objects', or the device path when the device has one drive object |
| `names` | array of string null | no (default ``) | names: the parameters to read in full, e.g. ["p1082", "p1120", "r46"]; empty uses filter |
| `filter` | string | no (default ``) | filter: text looked for in the name and in the description, e.g. 'ramp', 'speed', 'p13'; empty (default) lists all |
| `onlyWritable` | boolean | no (default `False`) | onlyWritable: leave the 'r' parameters out (default false) |
| `limit` | integer | no (default `60`) | limit: the most parameters to return in one page (default 60); 0 returns all |
| `offset` | integer | no (default `0`) | offset: parameters to skip, to read the next page (default 0) |

## drive_manage_telegrams

Change the PROFIdrive telegrams of a drive object, several actions at once, all or nothing: 'change' gives the telegram of a type another number (e.g. main telegram 105 -> 3), 'insert' adds a supplementary, additional, safety or torque telegram, 'erase' removes one, 'resize' sets the size of an additional telegram. A number the drive does not offer is refused before anything changes. Returns the telegrams afterwards with their sizes. The PLC side - the technology object or the blocks that use the telegram - has to match

| Parameter | Type | Required | Description |
|---|---|---|---|
| `drivePath` | string | yes | drivePath: path of the drive from 'drive_get_objects', or the device path when the device has one drive object |
| `actions` | array of object | yes | actions: applied in order; fields: action (change, insert, erase, resize), type (main, supplementary, additional, safety, torque), number, inputSize, outputSize |

## drive_set_motor

Set the motor type of a drive and its rating plate data, for drives whose motor is entered by data (G120 and the like): motorType e.g. 'InductionMotor', then the data TIA Portal asks for with that type - p304 rated voltage, p305 rated current, p307 rated power, p310 rated frequency, p311 rated speed, p335 cooling - in 'values'. Called with neither, it returns the data now in the project and the motor types. A G120 needs its power module first ('hw_plug_module' with the control unit as parentItemName, position 3). Use the data of the motor's rating plate as the user gave them: a wrong motor is overloaded or does not turn

| Parameter | Type | Required | Description |
|---|---|---|---|
| `drivePath` | string | yes | drivePath: path of the drive from 'drive_get_objects', or the device path when the device has one drive object |
| `motorType` | string | no (default ``) | motorType: e.g. InductionMotor, SynchronousMotor, InductionMotor1LE1; empty leaves the type as it is |
| `values` | object | no (default ``) | values: rating plate data by parameter name, e.g. {"p305": 1.5, "p307": 0.55, "p311": 1425}; empty changes none |
| `dataSet` | integer | no (default `0`) | dataSet: number of the drive data set (default 0) |

## drive_set_parameters

Set offline parameters of a drive object, several at once, all or nothing; every value is read back and returned as before/after, since TIA Portal may round or limit it. Write an element of an indexed parameter ('p1120[0]'), not the parameter itself. 'r' parameters cannot be written. A G120 control unit without a power module takes no parameters. The change is in the project; the drive gets it with a download. Wrong drive parameters can damage a machine: change what the user asked for, with the values they gave

| Parameter | Type | Required | Description |
|---|---|---|---|
| `drivePath` | string | yes | drivePath: path of the drive from 'drive_get_objects', or the device path when the device has one drive object |
| `parameters` | object | yes | parameters: name -> value, e.g. {"p1082[0]": 3000, "p1121[0]": 2.5} |

## get_accessible_devices

Search the network behind a PC interface for devices, as 'Online access > Update accessible devices' in TIA Portal does: name, address, MAC address of each device found - a PLC, a PLCSIM instance. Also returns 'pcAddresses', the addresses of the PC interface itself - a device outside their subnets is found but cannot be loaded - and 'downloadAddresses', the addresses the project gives the PLC. The list of TIA Portal can lag behind: a device started or readdressed a moment ago may be missing or shown at its old address. softwarePath names a PLC of the project, whose download settings give the PC interfaces

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `pcInterfaceName` | string | yes | pcInterfaceName: PC interface, second part of a target of 'get_download_targets' (e.g. 'Siemens PLCSIM Virtual Ethernet Adapter') |
| `modeName` | string | no (default `PN/IE`) | modeName: first part of a target (default 'PN/IE') |

## get_installed_software

What is installed in the TIA Portal this server is connected to: the products with their versions and options (STEP 7, Safety, WinCC, Startdrive with its drive families), the size of the hardware catalog, and the GSD files whose devices are in the catalog - file name, where its devices sit in the catalog, how many devices and modules it describes, and examples with their type identifiers. Tells which kinds of device a project can get at all. A GSD file or a support package that is missing has to be installed in TIA Portal by the user (Options menu): Openness has no call for it. Needs a connection to TIA Portal, not an open project

| Parameter | Type | Required | Description |
|---|---|---|---|
| `withGsdFiles` | boolean | no (default `True`) | withGsdFiles: list the GSD files too (default true) |
| `gsdFilter` | string | no (default ``) | gsdFilter: only the GSD files whose file name, catalog path or device name contains this text, e.g. 'SINAMICS', 'KUKA', 'PROFIBUS'; a list of up to 30 files carries example devices with their type identifiers. Empty (default) lists all files without examples |

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

Open a TIA-Portal local project/session. A protected project opens only with the name and password of one of its users: pass userName and password as the user of this conversation gave them - never make them up, never repeat the password in your answer

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | path: defines the path where to the project/session |
| `userName` | string | no (default ``) | userName: a user of a protected project; empty for a project without protection |
| `password` | string | no (default ``) | password: the password of that user |

## open_tia_project

Connect to the running TIA Portal if not already connected, open the given project or session, and return the device and PLC software paths the other tools need. Replaces the connect, open_project, get_project_tree sequence. TIA Portal must already be running. A protected project opens only with the name and password of one of its users (userName, password), as the user of this conversation gave them

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | yes | path: full path of the .apXX project or .alsXX session file on the machine running this server |
| `userName` | string | no (default ``) | userName: a user of a protected project; empty for a project without protection |
| `password` | string | no (default ``) | password: the password of that user |

## retrieve_project

Unpack a project archive (.zapXX) into a new folder and open the project in it; the answer has the path of the project file. TIA Portal holds one project at a time: while a project is open the call is refused - save and close it first ('save_project', 'close_project'); to return to the earlier project afterwards use 'close_project' and 'open_project'. A target folder that is not empty is refused. An archive of an older TIA Portal version is not upgraded: TIA Portal's reason is returned. It takes time, as long as an archive does

| Parameter | Type | Required | Description |
|---|---|---|---|
| `archivePath` | string | yes | archivePath: absolute path of the archive file, e.g. 'C:\Backups\Plant.zap21' |
| `targetDirectory` | string | yes | targetDirectory: absolute path of the folder to unpack into; its parent must exist; the folder must not exist or must be empty |

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

## sec_get_project_users

Read the users and roles of the project ('Security settings > Users and roles' in TIA Portal): the users with their roles and whether they are active (the user 'Anonymous' is access without login), the user groups, the roles - those of TIA Portal (system: true) and those of the project - with their function rights per device, the password policy, and which devices have function rights. These users decide the access to CPUs with firmware V4 and newer, to Unified panels and to network devices. rightsOfDevice lists the function rights a device offers. No password is ever returned

| Parameter | Type | Required | Description |
|---|---|---|---|
| `rightsOfDevice` | string | no (default ``) | rightsOfDevice: name of a device (or of its CPU / panel) whose available function rights are listed too; empty lists none |

## sec_manage_opcua_users

Create or delete users of the OPC UA server of a CPU, or set their password ('update'), several at once, all or nothing. The OPC UA server and its authentication by user name have to be on first: 'hw_set_device_item_attributes' on the item '<CPU path>/OPC UA_1' with {"OpcUaServer": true} and then, in a second call, {"OpcUaPasswordAuthentication": true}; otherwise TIA Portal refuses a new user. SENSITIVE: before calling, tell the user in plain words what will change on which PLC and get their consent. A password is the one the user gave in this conversation - never make one up, never repeat it in your answer. The server passes it to TIA Portal and keeps it nowhere. TIA Portal refuses some passwords of a CPU (a password with a space in it, found by trial) and then answers with a bare EngineeringPasswordPolicyViolationException or just the name of the call that failed: ask the user for another password, do not change it yourself.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deviceItemPath` | string | yes | deviceItemPath: path of the CPU, e.g. 'Station_1/PLC_1' |
| `actions` | array of object | yes | actions: the changes, applied in order; fields: action (create, update, delete), userName, password |

## sec_manage_project_roles

Create, update or delete roles of the project and give them function rights of a device (addRights / removeRights with device; one device per action), several at once, all or nothing. A function right is e.g. full access to a PLC, reading tags over its web server, operating a Unified panel; 'sec_get_project_users' with rightsOfDevice lists what a device offers. The roles TIA Portal brings along cannot be changed. Deleting a role takes it away from every user that has it. SENSITIVE: before calling, tell the user in plain words what will change on which PLC and get their consent. A password is the one the user gave in this conversation - never make one up, never repeat it in your answer. The server passes it to TIA Portal and keeps it nowhere. TIA Portal refuses some passwords of a CPU (a password with a space in it, found by trial) and then answers with a bare EngineeringPasswordPolicyViolationException or just the name of the call that failed: ask the user for another password, do not change it yourself.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `actions` | array of object | yes | actions: the changes, applied in order; fields: action (create, update, delete), name, newName, comment, sessionTimeout, device, addRights, removeRights |

## sec_manage_project_users

Create, update or delete users and user groups of the project, several at once, all or nothing: name, password, roles (roles = exactly these, addRoles / removeRoles = change the present ones), active, comment, session timeout, alias, authentication. A user gets rights only through roles; 'sec_get_project_users' lists the roles. The user 'Anonymous' with active: true lets everybody in without login, with the roles it has. The role 'Engineering administrator' decides who administers a protected project: give or take it only on an explicit request. The devices have to be loaded again for a change to reach them. SENSITIVE: before calling, tell the user in plain words what will change on which PLC and get their consent. A password is the one the user gave in this conversation - never make one up, never repeat it in your answer. The server passes it to TIA Portal and keeps it nowhere. TIA Portal refuses some passwords of a CPU (a password with a space in it, found by trial) and then answers with a bare EngineeringPasswordPolicyViolationException or just the name of the call that failed: ask the user for another password, do not change it yourself.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `actions` | array of object | yes | actions: the changes, applied in order; fields: action (create, update, delete), kind (user, group), name, password, newName, comment, active, sessionTimeout, runtimeSessionTimeout, alias, authentication, roles, addRoles, removeRoles |

## sec_manage_webserver_users

Create, update or delete users of the web server of a CPU that has its own web server users (S7-1200, S7-1500 before firmware V4), several at once, all or nothing. A user has a name, a password and permissions; the user 'Everybody' is what anyone may do without logging in. The web server itself is switched on with 'hw_set_device_item_attributes' (WebserverActivate). SENSITIVE: before calling, tell the user in plain words what will change on which PLC and get their consent. A password is the one the user gave in this conversation - never make one up, never repeat it in your answer. The server passes it to TIA Portal and keeps it nowhere. TIA Portal refuses some passwords of a CPU (a password with a space in it, found by trial) and then answers with a bare EngineeringPasswordPolicyViolationException or just the name of the call that failed: ask the user for another password, do not change it yourself.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deviceItemPath` | string | yes | deviceItemPath: path of the CPU, e.g. 'Station_1/PLC_1' |
| `actions` | array of object | yes | actions: the changes, applied in order; fields: action (create, update, delete), userName, password, permissions |

## sec_protect_project

Protect the open project: a user with the role 'Engineering administrator' is made, and from then on the project opens - in TIA Portal and through 'open_project' - only with the name and password of one of its users. THIS CANNOT BE UNDONE: TIA Portal has no way to remove the protection of a project, and with the password lost the project cannot be opened any more. Call it only when the user asked for exactly this, after telling them it is final and getting a clear yes; suggest a copy of the project first ('save_as_project'). SENSITIVE: before calling, tell the user in plain words what will change on which PLC and get their consent. A password is the one the user gave in this conversation - never make one up, never repeat it in your answer. The server passes it to TIA Portal and keeps it nowhere. TIA Portal refuses some passwords of a CPU (a password with a space in it, found by trial) and then answers with a bare EngineeringPasswordPolicyViolationException or just the name of the call that failed: ask the user for another password, do not change it yourself.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `administratorName` | string | yes | administratorName: name of the user that becomes the administrator of the project |
| `password` | string | yes | password: the password of that user; TIA Portal asks for at least 10 characters here |

## sec_set_block_protection

Change the protection of a block with a password: 'protect' / 'unprotect' is the know-how protection (the code cannot be read or changed without the password), 'write_protect' / 'write_unprotect' / 'write_change_password' is the write protection (readable, not changeable). A know-how password has to have 8 to 120 characters with a digit, a special character, upper and lower case. A know-how password that is lost cannot be recovered: the code of the block is then gone for good. While a block is know-how protected its code can be neither read nor changed by the plc_* tools. SENSITIVE: before calling, tell the user in plain words what will change on which PLC and get their consent. A password is the one the user gave in this conversation - never make one up, never repeat it in your answer. The server passes it to TIA Portal and keeps it nowhere. TIA Portal refuses some passwords of a CPU (a password with a space in it, found by trial) and then answers with a bare EngineeringPasswordPolicyViolationException or just the name of the call that failed: ask the user for another password, do not change it yourself.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path of the PLC software, e.g. 'Station_1/PLC_1' |
| `blockPath` | string | yes | blockPath: path of the block, e.g. 'Group/Block_1' |
| `action` | string | yes | action: protect, unprotect, write_protect, write_unprotect or write_change_password |
| `password` | string | no (default ``) | password: the password to set, or the present one for unprotect, write_unprotect and write_change_password |
| `newPassword` | string | no (default ``) | newPassword: the new password, for write_change_password |

## sec_set_display_password

Set the password that protects the display of an S7-1500 CPU: 3 to 8 letters and digits, no special characters. The protection has to be on first: 'hw_set_device_item_attributes' on the item '<CPU path>/CPU display_1' with {"DisplayProtection": true}. SENSITIVE: before calling, tell the user in plain words what will change on which PLC and get their consent. A password is the one the user gave in this conversation - never make one up, never repeat it in your answer. The server passes it to TIA Portal and keeps it nowhere. TIA Portal refuses some passwords of a CPU (a password with a space in it, found by trial) and then answers with a bare EngineeringPasswordPolicyViolationException or just the name of the call that failed: ask the user for another password, do not change it yourself.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deviceItemPath` | string | yes | deviceItemPath: path of the CPU, e.g. 'Station_1/PLC_1' |
| `password` | string | yes | password: the password of the display |

## sec_set_password_policy

Set the password policy for the users of the project; only the settings given are changed. TIA Portal checks the limits (minimum length 8 to 32). The policy applies to passwords set from now on. SENSITIVE: before calling, tell the user in plain words what will change on which PLC and get their consent. A password is the one the user gave in this conversation - never make one up, never repeat it in your answer. The server passes it to TIA Portal and keeps it nowhere. TIA Portal refuses some passwords of a CPU (a password with a space in it, found by trial) and then answers with a bare EngineeringPasswordPolicyViolationException or just the name of the call that failed: ask the user for another password, do not change it yourself.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `minimumLength` | integer | no (default ``) | minimumLength: least number of characters, 8 to 32 |
| `minimumNumericCharacters` | integer | no (default ``) | minimumNumericCharacters: least number of digits |
| `minimumSpecialCharacters` | integer | no (default ``) | minimumSpecialCharacters: least number of special characters |
| `upperAndLowerCase` | boolean | no (default ``) | upperAndLowerCase: whether upper and lower case letters are both required |
| `passwordAging` | boolean | no (default ``) | passwordAging: whether passwords expire |
| `passwordValidity` | integer | no (default ``) | passwordValidity: days a password is valid |
| `prewarningTime` | integer | no (default ``) | prewarningTime: days of warning before a password expires |
| `passwordsBlockedForReuse` | integer | no (default ``) | passwordsBlockedForReuse: how many former passwords cannot be used again |

## sec_set_plc_access_level

Set the access level of a CPU with access levels (S7-1200, S7-1500 before firmware V4) and the passwords of its levels: accessLevel is what is allowed WITHOUT a password - FullAccess, ReadAccess, HMIAccess or NoAccess (FullAccessIncludingFailsafe on F-CPUs); passwordFor with password sets the password that opens a level; resetPasswordFor removes one. A wrong level locks people out of the PLC until the project is loaded again. A CPU with firmware V4 or newer has no access levels and is refused. SENSITIVE: before calling, tell the user in plain words what will change on which PLC and get their consent. A password is the one the user gave in this conversation - never make one up, never repeat it in your answer. The server passes it to TIA Portal and keeps it nowhere. TIA Portal refuses some passwords of a CPU (a password with a space in it, found by trial) and then answers with a bare EngineeringPasswordPolicyViolationException or just the name of the call that failed: ask the user for another password, do not change it yourself.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deviceItemPath` | string | yes | deviceItemPath: path of the CPU, e.g. 'Station_1/PLC_1' |
| `accessLevel` | string | no (default ``) | accessLevel: the level granted without a password; empty leaves it as it is |
| `passwordFor` | string | no (default ``) | passwordFor: the level the password opens, e.g. FullAccess; empty sets no password |
| `password` | string | no (default ``) | password: the password for passwordFor |
| `resetPasswordFor` | string | no (default ``) | resetPasswordFor: the level whose password is removed; empty removes none |

## sec_set_plc_configuration_protection

Change the protection of confidential PLC configuration data of a CPU: 'protect' sets a password, 'protect_all' protects all configuration data (with a password when one is given), 'unprotect' removes the protection (the present password is needed when one is set), 'unprotect_all' (both need a CPU that supports additional protection of downloadable configuration data: none of the seven CPU types tried in V21 did - S7-1200 V4.7, S7-1500 V2.9 / V4.0, 1511T, 1512SP, 1517H, 1518 - and TIA Portal answers 'not supported'), 'change_password' (password = the present one, newPassword), 'reset'. A password that is lost cannot be recovered from the project. The CPU has to be loaded again afterwards. SENSITIVE: before calling, tell the user in plain words what will change on which PLC and get their consent. A password is the one the user gave in this conversation - never make one up, never repeat it in your answer. The server passes it to TIA Portal and keeps it nowhere. TIA Portal refuses some passwords of a CPU (a password with a space in it, found by trial) and then answers with a bare EngineeringPasswordPolicyViolationException or just the name of the call that failed: ask the user for another password, do not change it yourself.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deviceItemPath` | string | yes | deviceItemPath: path of the CPU, e.g. 'Station_1/PLC_1' |
| `action` | string | yes | action: protect, protect_all, unprotect, unprotect_all, change_password or reset |
| `password` | string | no (default ``) | password: the password to set, or the present one for unprotect and change_password |
| `newPassword` | string | no (default ``) | newPassword: the new password, for change_password |

