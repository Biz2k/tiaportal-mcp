# Download

Download targets and download to a PLC.

| Tool | Kind |
|---|---|
| [`download_to_plc`](#download_to_plc) | write |
| [`get_download_targets`](#get_download_targets) | read |

## download_to_plc

Download hardware configuration and/or software to a PLC or a simulated PLC. The target must already be running and reachable: this server does not start PLCSIM. Take the three interface values from 'get_download_targets'. There is no preview: a call loads. The CPU is neither stopped nor started unless stopPlc / startPlc say so; a hardware download normally needs stopPlc. The response lists every step, its answer and the messages of the result

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `modeName` | string | yes | modeName: download mode, first part of a target (e.g. 'PN/IE') |
| `pcInterfaceName` | string | yes | pcInterfaceName: PC interface, second part of a target (e.g. 'Siemens PLCSIM Virtual Ethernet Adapter') |
| `targetInterfaceName` | string | yes | targetInterfaceName: target interface, third part of a target (e.g. '1 X1') |
| `hardware` | boolean | yes | hardware: download the hardware configuration |
| `software` | boolean | yes | software: download the software |
| `stopPlc` | boolean | no (default `False`) | stopPlc: allow the CPU to be stopped when the download requires it (default false) |
| `startPlc` | boolean | no (default `False`) | startPlc: start the CPU after the download (default false) |
| `maxMessages` | integer | no (default `40`) | maxMessages: how many informational result messages to return (default 40); errors and warnings are always returned in full |
| `selections` | string | no (default ``) | selections: optional answers that override the defaults, as 'StepType=Option' pairs separated by commas, e.g. 'OverwriteSystemData=Overwrite,StopModules=StopAll'. Step types and their options are listed under 'steps' in every response |
| `downloadUserManagement` | string | no (default `keep`) | downloadUserManagement: what to do with the user management data (users, roles) when the CPU holds data that differs from the project: 'keep' (default) leaves the CPU's data as it is, 'update' takes the users of the project but keeps the CPU's passwords, 'overwrite' replaces all of it by the project's data and resets the passwords. The same answer can be given as 'UserManagementDownload=<option>' in selections, which wins |

## get_download_targets

Returns the download targets of a PLC as 'mode / PC interface / target interface' - the three values 'download_to_plc' takes.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |

