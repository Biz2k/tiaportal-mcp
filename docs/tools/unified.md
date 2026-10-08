# WinCC Unified

Screens, items, tags, scripts, logs, alarms, connections, lists and runtime settings.

| Tool | Kind |
|---|---|
| [`unified_compile`](#unified_compile) | session |
| [`unified_configure_trend_control`](#unified_configure_trend_control) | write |
| [`unified_create_screen`](#unified_create_screen) | write |
| [`unified_delete_screen`](#unified_delete_screen) | write |
| [`unified_get_alarm_classes`](#unified_get_alarm_classes) | read |
| [`unified_get_alarms`](#unified_get_alarms) | read |
| [`unified_get_connections`](#unified_get_connections) | read |
| [`unified_get_graphic_lists`](#unified_get_graphic_lists) | read |
| [`unified_get_logging_tags`](#unified_get_logging_tags) | read |
| [`unified_get_logs`](#unified_get_logs) | read |
| [`unified_get_runtime_settings`](#unified_get_runtime_settings) | read |
| [`unified_get_screen_groups`](#unified_get_screen_groups) | read |
| [`unified_get_screen_item_properties`](#unified_get_screen_item_properties) | read |
| [`unified_get_screen_items`](#unified_get_screen_items) | read |
| [`unified_get_screens`](#unified_get_screens) | read |
| [`unified_get_scripts`](#unified_get_scripts) | read |
| [`unified_get_system_tags`](#unified_get_system_tags) | read |
| [`unified_get_tag_table_groups`](#unified_get_tag_table_groups) | read |
| [`unified_get_tag_tables`](#unified_get_tag_tables) | read |
| [`unified_get_tags`](#unified_get_tags) | read |
| [`unified_get_text_lists`](#unified_get_text_lists) | read |
| [`unified_manage_alarm_classes`](#unified_manage_alarm_classes) | write |
| [`unified_manage_alarms`](#unified_manage_alarms) | write |
| [`unified_manage_connections`](#unified_manage_connections) | write |
| [`unified_manage_faceplate`](#unified_manage_faceplate) | write |
| [`unified_manage_items`](#unified_manage_items) | write |
| [`unified_manage_lists`](#unified_manage_lists) | write |
| [`unified_manage_logging_tags`](#unified_manage_logging_tags) | write |
| [`unified_manage_logs`](#unified_manage_logs) | write |
| [`unified_manage_screen_groups`](#unified_manage_screen_groups) | write |
| [`unified_manage_scripts`](#unified_manage_scripts) | write |
| [`unified_manage_tag_table_groups`](#unified_manage_tag_table_groups) | write |
| [`unified_manage_tag_tables`](#unified_manage_tag_tables) | write |
| [`unified_manage_tags`](#unified_manage_tags) | write |
| [`unified_set_runtime_settings`](#unified_set_runtime_settings) | write |

## unified_compile

Compile a WinCC Unified HMI and list the errors and warnings with the place of each (device/Screens/screen/item). This is the check for what the writing tools cannot see: a syntax error in the script of a dynamization or of an event and an invalid formula of a tag dynamization are reported here and nowhere else. Call it after a batch that wrote scripts or formulas, with pathFilter set to the screen. The compile is incremental, so after the first one it takes seconds. It compiles the device (Openness cannot compile one screen or one script), does not save the project and leaves it marked as modified. Not reported: a broken formula of an 'expression' dynamization and calls of functions that do not exist

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `pathFilter` | string | no (default ``) | pathFilter: keep only the messages whose path contains this text, e.g. the name of a screen; empty keeps all |
| `errorsOnly` | boolean | no (default `False`) | errorsOnly: leave the warnings out |

## unified_configure_trend_control

Add a trend (pen) to an HmiTrendControl on a WinCC Unified screen and bind it to a data source. Create the control itself with 'unified_manage_items' (itemType 'HmiTrendControl'); bind an HmiTrendCompanion to it by setting the companion's 'SourceTrendControl' property there

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `screenName` | string | yes | screenName: name of the screen |
| `trendControlName` | string | yes | trendControlName: name of the HmiTrendControl item |
| `trendName` | string | yes | trendName: display name of the trend |
| `dataSource` | string | yes | dataSource: what the trend shows: an HMI tag, or an archived tag as '<HMI tag>:<logging tag>' (the logging tag is checked; 'unified_get_logging_tags' lists them) |
| `trendMode` | string | no (default ``) | trendMode: how the trend is drawn, e.g. Points, Interpolated, Stepped, Bar, Value (optional) |
| `lineWidth` | integer | no (default ``) | lineWidth: line width, 0-255 (optional) |
| `lineColor` | string | no (default ``) | lineColor: line color by name (e.g. Red) or as '#RRGGBB' (optional) |

## unified_create_screen

Create an empty screen in a WinCC Unified HMI, at the top level or in a screen group. Set its size and other properties with 'unified_manage_items' (empty itemName)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `screenName` | string | yes | screenName: name of the new screen; unique in the whole HMI, groups included |
| `group` | string | no (default ``) | group: screen group to create the screen in, e.g. 'Pumps' or 'Pumps/Big'; empty (default) for the top level. The group has to exist: 'unified_manage_screen_groups' creates one |

## unified_delete_screen

Delete a screen of a WinCC Unified HMI with everything on it

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `screenName` | string | yes | screenName: name of the screen to delete |

## unified_get_alarm_classes

List the alarm classes of a WinCC Unified HMI with priority, state machine and the colors of each alarm state

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |

## unified_get_alarms

List the discrete and analog alarms of a WinCC Unified HMI: alarm class, the tag and bit or limit that raise the alarm, and the alarm text per language. An HMI can have hundreds of alarms: narrow the list with type or nameFilter

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `type` | string | no (default ``) | type: 'discrete' or 'analog'; empty (default) returns both |
| `nameFilter` | string | no (default ``) | nameFilter: regular expression on the alarm name, case-insensitive; empty (default) returns every alarm |
| `limit` | integer | no (default `500`) | limit: the most items to return in one page; 0 returns all. A longer list is cut and the answer says so |
| `offset` | integer | no (default `0`) | offset: items to skip, to read the next page of a long list: pass the 'nextOffset' of the previous answer (default 0) |

## unified_get_connections

List the connections of a WinCC Unified HMI with their attributes and driver parameters ('DriverProperties')

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |

## unified_get_graphic_lists

List the graphic lists of a WinCC Unified HMI with their entries. An entry is of type 'value', 'range', 'from', 'to' or 'default' and names a project graphic

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `listName` | string | no (default ``) | listName: return only this list; empty (default) returns all |
| `limit` | integer | no (default `500`) | limit: the most items to return in one page; 0 returns all. A longer list is cut and the answer says so |
| `offset` | integer | no (default `0`) | offset: items to skip, to read the next page of a long list: pass the 'nextOffset' of the previous answer (default 0) |

## unified_get_logging_tags

List the logging tags of a WinCC Unified HMI: which HMI tag is archived into which data log, and how (mode, cycle, aggregation, smoothing, limits, trigger). A large HMI has hundreds: narrow the list with tagName or logName

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `tagName` | string | no (default ``) | tagName: regular expression on the path of the process tag, case-insensitive (a member of a structured tag is 'Tag.Member'); empty (default) for all |
| `logName` | string | no (default ``) | logName: only the logging tags that archive into this data log |
| `limit` | integer | no (default `500`) | limit: the most items to return in one page; 0 returns all. A longer list is cut and the answer says so |
| `offset` | integer | no (default `0`) | offset: items to skip, to read the next page of a long list: pass the 'nextOffset' of the previous answer (default 0) |

## unified_get_logs

List the data logs, alarm logs and audit trails of a WinCC Unified HMI with their settings: maximum size, duration, storage device and folder, segment size, start time and duration, backup. For a data log also the number of logging tags that archive into it. Audit trails can only be read

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `type` | string | no (default ``) | type: 'data', 'alarm' or 'audit'; empty (default) returns all kinds |
| `logName` | string | no (default ``) | logName: return only this log; empty (default) returns all |

## unified_get_runtime_settings

Get the runtime settings of a WinCC Unified HMI: start screen, screen resolution, auto log-off, and the groups for the OPC UA server, login lock, exclusive operation, reporting, telemetry, process diagnostics, tag handling and the languages with their fonts. Nested groups come as nested objects; 'unified_set_runtime_settings' changes them

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |

## unified_get_screen_groups

List the screen groups of a WinCC Unified HMI, nested ones included, with the number of screens and groups directly in each. A nested group is written 'Parent/Child'

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |

## unified_get_screen_item_properties

Every property of a WinCC Unified screen item with its current value, plus '_Dynamizations', '_Events' and, for a faceplate instance, '_Interface'. With an empty itemName the properties of the screen itself. The property names are what 'unified_manage_items' takes

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `screenName` | string | yes | screenName: name of the screen |
| `itemName` | string | no (default ``) | itemName: name of the item; empty for the screen itself |

## unified_get_screen_items

List the items of a WinCC Unified screen: name, type, position, size, text, process value and the events that have a handler

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `screenName` | string | yes | screenName: name of the screen |
| `limit` | integer | no (default `500`) | limit: the most items to return in one page; 0 returns all. A longer list is cut and the answer says so |
| `offset` | integer | no (default `0`) | offset: items to skip, to read the next page of a long list: pass the 'nextOffset' of the previous answer (default 0) |

## unified_get_screens

List the screens of a WinCC Unified HMI with their group and size. Screens in screen groups are included; screen names are unique in the whole HMI, so every other tool finds a screen by its name alone

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `group` | string | no (default ``) | group: return only the screens of this screen group and the groups in it, e.g. 'Pumps' or 'Pumps/Big'; empty (default) returns every screen. 'unified_get_screen_groups' lists the groups |
| `limit` | integer | no (default `500`) | limit: the most items to return in one page; 0 returns all. A longer list is cut and the answer says so |
| `offset` | integer | no (default `0`) | offset: items to skip, to read the next page of a long list: pass the 'nextOffset' of the previous answer (default 0) |

## unified_get_scripts

List the global script modules of a WinCC Unified HMI with their source: global definitions (constants, variables, helpers), functions and the list of exported functions with their parameters. Scripts on events and dynamizations of screen items are not modules; see 'unified_get_screen_item_properties'

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `moduleName` | string | no (default ``) | moduleName: return only this module; empty (default) returns all |

## unified_get_system_tags

List the system tags of a WinCC Unified HMI (name and data type; read only). A long list: narrow it with nameFilter

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `nameFilter` | string | no (default ``) | nameFilter: regular expression on the tag name, case-insensitive; empty returns every tag |
| `limit` | integer | no (default `200`) | limit: the most items to return in one page; 0 returns all. A longer list is cut and the answer says so |
| `offset` | integer | no (default `0`) | offset: items to skip, to read the next page of a long list: pass the 'nextOffset' of the previous answer (default 0) |

## unified_get_tag_table_groups

List the tag table groups of a WinCC Unified HMI, nested ones included, with the number of tag tables and groups directly in each. A nested group is written 'Parent/Child'

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |

## unified_get_tag_tables

List the tag tables of a WinCC Unified HMI with the number of tags in each

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |

## unified_get_tags

List the HMI tags of a WinCC Unified HMI with tag table, data type, connection and PLC tag; a tag without a connection is an internal tag. With withMembers the members of structured tags are listed as well, by the path 'Tag.Member' that 'unified_manage_tags' takes. A large HMI has thousands of tags: narrow the list with nameFilter or tagTable

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `nameFilter` | string | no (default ``) | nameFilter: regular expression on the tag name, case-insensitive; empty (default) returns every tag |
| `tagTable` | string | no (default ``) | tagTable: return only the tags of this tag table; 'unified_get_tag_tables' lists the tables |
| `limit` | integer | no (default `200`) | limit: the most items to return in one page; 0 returns all. A longer list is cut and the answer says so |
| `offset` | integer | no (default `0`) | offset: items to skip, to read the next page of a long list: pass the 'nextOffset' of the previous answer (default 0) |
| `withMembers` | boolean | no (default `False`) | withMembers: true adds the members of structured tags, all levels, with data type, comment and acquisition mode (default false: a structured tag can have dozens) |

## unified_get_text_lists

List the text lists of a WinCC Unified HMI with their entries. An entry is of type 'value' (one value), 'range' (from..to), 'from' (a value and above), 'to' (a value and below) or 'default', and carries its text per language. A list that is a library type is not listed

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `listName` | string | no (default ``) | listName: return only this list; empty (default) returns all |
| `system` | boolean | no (default `False`) | system: true returns the system text lists of the HMI instead (alarm texts, error reasons); they can only be read |
| `limit` | integer | no (default `500`) | limit: the most items to return in one page; 0 returns all. A longer list is cut and the answer says so |
| `offset` | integer | no (default `0`) | offset: items to skip, to read the next page of a long list: pass the 'nextOffset' of the previous answer (default 0) |

## unified_manage_alarm_classes

Create, update, upsert or delete alarm classes of a WinCC Unified HMI, several at once: priority, state machine and the colors of the alarm states. System classes cannot be deleted. A call applies all of its actions or none

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `actions` | array of object | yes | actions: the changes to make, applied in order |

## unified_manage_alarms

Create, update, upsert or delete discrete and analog alarms of a WinCC Unified HMI, several at once. A discrete alarm is raised by a bit of an HMI tag (RaisedStateTag, RaisedStateTagBitNumber), an analog alarm by a limit (RaisedStateTag, Condition, ConditionValue). The tag and the alarm class have to exist. A call applies all of its actions or none

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `actions` | array of object | yes | actions: the changes to make, applied in order |

## unified_manage_connections

Create, update, upsert or delete connections of a WinCC Unified HMI, several at once. With 'partner' a new connection is an integrated connection to a PLC of the project, on which HMI tags can name PLC tags symbolically; without it the connection is not integrated and is addressed through driverProperties. A call applies all of its actions or none. A connection that tags still use is deleted without warning

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `actions` | array of object | yes | actions: the changes to make, applied in order |

## unified_manage_faceplate

Create or update a faceplate instance on a WinCC Unified screen and set the interface properties of its faceplate type. The interface differs from type to type, so the response always lists it - names, current values and tag bindings; call with only itemName and action 'update' to read it. All settings of a call are applied or none. Delete an instance with 'unified_manage_items'

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `screenName` | string | yes | screenName: name of the screen |
| `itemName` | string | yes | itemName: name of the faceplate instance |
| `action` | string | no (default `upsert`) | action: 'create' (fails if the item exists), 'update' (fails if it does not) or 'upsert' |
| `faceplateType` | string | no (default ``) | faceplateType: faceplate type and version, e.g. 'V0.0.2\\MyFaceplate' - the 'ContainedType' value from 'get_library_types'. Required when the instance is created |
| `properties` | object | no (default ``) | properties: properties of the instance itself, e.g. {"Left": 100, "Top": 50, "Width": 200}; same forms as in 'unified_manage_items' |
| `interfaceValues` | object | no (default ``) | interfaceValues: interface properties of the faceplate type, by name. A plain value sets a static value - for a tag interface that is the name of the HMI tag. A property interface can also be dynamized: {"tag": "HmiTagName"} or {"script": "return ...;"}. A tag interface can take {"tagParameter": "Name"}. {"dynamization": "none"} removes the dynamization |

## unified_manage_items

Create, update, upsert or delete items on WinCC Unified screens, several at once. Each action sets any number of properties - a static value or a dynamization (tag, script, or a text or graphic list driven by a tag) - and event handlers. Use 'unified_get_screen_items' and 'unified_get_screen_item_properties' to find item and property names. A call applies all of its actions or none: if one fails, the error names it and nothing is changed. Faceplate instances are parameterized with 'unified_manage_faceplate', trends with 'unified_configure_trend_control'

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `actions` | array of object | yes | actions: the changes to make, applied in order |
| `compile` | boolean | no (default `False`) | compile: true compiles the HMI after the changes are committed and returns the result in 'compile' - the only check of the syntax of a script, which neither Openness nor the validation sees. The state and the counts are those of the whole device; the messages are those of the screens or modules this call changed. A compile error does not fail the call and does not undo the write. It takes seconds (about a second after a change when the HMI was compiled before, much longer for the first compile of a large HMI) and marks the project as modified. Default false |

## unified_manage_lists

Create, replace or delete text lists and graphic lists of a WinCC Unified HMI, several at once. A list is written as a whole: 'entries' replaces everything the list had. An entry stands for one value ('value'), a range ('from' and 'to'), a value and above ('from'), a value and below ('to'), or is the default entry ('default': true); it carries 'text' in a text list and 'graphic', the name of a project graphic, in a graphic list. Every write is read back and compared. Bind a list to a screen item with 'unified_manage_items'. A call applies all of its actions or none

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `actions` | array of object | yes | actions: the changes to make, applied in order |

## unified_manage_logging_tags

Create, update, upsert or delete logging tags of a WinCC Unified HMI, several at once: the archiving of an HMI tag into a data log. A structured tag keeps its logging tags on its members: tagName is then 'Tag.Member'. A new logging tag starts in the first data log with the mode OnChange. Mode Cyclic needs a Cycle of at least T500ms; mode OnDemand needs TriggerMode and TriggerTag. The data log and the trigger tag are checked. A trend shows an archived tag when its data source is '<process tag>:<logging tag>', e.g. 'AI_DB_CP10-U1.field_input_EUF:AI_DB_CP10-U1' (see 'unified_configure_trend_control'). A call applies all of its actions or none

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `actions` | array of object | yes | actions: the changes to make, applied in order |

## unified_manage_logs

Create, update, upsert or delete data logs and alarm logs of a WinCC Unified HMI, several at once; settings are given by name, e.g. "Settings.LogMaxSize" or "Segment.SegmentMaxSize". Audit trails cannot be changed. Renaming a log renames it in the logging tags that use it; deleting one leaves them pointing at it. A call applies all of its actions or none

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `actions` | array of object | yes | actions: the changes to make, applied in order |

## unified_manage_screen_groups

Create, rename or delete screen groups of a WinCC Unified HMI, several at once. A group in a group is written 'Parent/Child'; the parent has to exist. Deleting a group deletes the screens in it. A call applies all of its actions or none

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `actions` | array of object | yes | actions: the changes to make, applied in order |

## unified_manage_scripts

Create or replace global script modules of a WinCC Unified HMI, several at once. A module is written as a whole (globalDefinitions and functions): what it held before is replaced, so read it first with 'unified_get_scripts' to change part of it. Every write is read back and compared; code that TIA Portal cannot parse (syntax errors, default parameter values) is reported and rolled back instead of being stored mangled. The syntax of the code is NOT checked here: code with a syntax error is stored as written (every action says so in its notes). Check it with 'unified_compile' and pathFilter 'Scripts/<module>': the compile reports the error with its line and column, e.g. "SyntaxError: Unexpected token ';' in line 2, in column 12" for the function in 'Scripts/<module>/<function>' (checked live). Script modules cannot be deleted or renamed through Openness. A call applies all of its actions or none

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `actions` | array of object | yes | actions: the changes to make, applied in order |
| `compile` | boolean | no (default `False`) | compile: true compiles the HMI after the changes are committed and returns the result in 'compile' - the only check of the syntax of a script, which neither Openness nor the validation sees. The state and the counts are those of the whole device; the messages are those of the screens or modules this call changed. A compile error does not fail the call and does not undo the write. It takes seconds (about a second after a change when the HMI was compiled before, much longer for the first compile of a large HMI) and marks the project as modified. Default false |

## unified_manage_tag_table_groups

Create, rename or delete tag table groups of a WinCC Unified HMI, several at once. A group in a group is written 'Parent/Child'; the parent has to exist. Deleting a group deletes the tag tables in it and their tags. A call applies all of its actions or none

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `actions` | array of object | yes | actions: the changes to make, applied in order |

## unified_manage_tag_tables

Create, rename or delete tag tables of a WinCC Unified HMI, several at once. Deleting a table deletes the tags in it; the default tag table cannot be deleted. A call applies all of its actions or none

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `actions` | array of object | yes | actions: the changes to make, applied in order |

## unified_manage_tags

Create, update, upsert or delete HMI tags of a WinCC Unified HMI, several at once. A new tag is an internal Int tag in the default tag table unless tagTable and properties say otherwise. For a PLC tag set Connection and PlcTag (symbolic; the data type follows the PLC tag) or Connection, AccessMode 'AbsoluteAccess', DataType and Address. A member of a structured tag is addressed as 'Tag.Member' ('unified_get_tags' with withMembers lists them) and can only be updated: it takes Comment and AcquisitionMode. A call applies all of its actions or none. A tag that screens still use is deleted without warning

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `actions` | array of object | yes | actions: the changes to make, applied in order |

## unified_set_runtime_settings

Set runtime settings of a WinCC Unified HMI by name. A setting of a group is written with a dotted name: {"StartScreen": "Start", "OpcUaServerRuntimeSettings.MaxSessionCount": 20, "MaxLoginRuntimeSettings.MaxLoginErrors": 5, "LanguageAndFonts.English (United States).Enable": true, "LanguageAndFonts.ru-RU.Enable": false}. A setting can depend on another one (MaxLoginErrors needs EnableLockAfterNumberOfAttempts true): give both in the call. All settings are applied or none; 'unified_get_runtime_settings' shows the names and current values

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it |
| `settings` | object | yes | settings: names and new values; groups with a dot, a language by the name TIA Portal gives it or by its culture code: 'LanguageAndFonts.English (United States).Enable' or 'LanguageAndFonts.en-US.Enable' |

