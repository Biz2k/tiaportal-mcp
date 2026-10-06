using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    // The tools for WinCC Unified. Every one of them is named unified_* and refuses other HMI
    // systems with an explanation, so the name says what the tool is for.
    //
    // Callers: the MCP host, through tool registration; the write tools only unless the server
    // runs with '--read-only'. Affected API: replaces the hmi_* tools. Reads and writes no data
    // files.
    //
    // Editing is done by three tools, on purpose:
    //  - unified_manage_items     - any screen item: its properties, dynamizations and events;
    //  - unified_manage_faceplate - faceplate instances, whose parameters are the interface of
    //                               a faceplate type and therefore differ from type to type;
    //  - unified_configure_trend_control - trends, which live in nested parts of the control.
    public static partial class McpServer
    {
        private const string UnifiedPath = "softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'; 'get_project_tree' shows it";

        #region read

        [McpServerTool(Name = "unified_get_screens", Title = "Get WinCC Unified screens", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the screens of a WinCC Unified HMI with their group and size. Screens in screen groups are included; screen names are unique in the whole HMI, so every other tool finds a screen by its name alone")]
        public static ResponseHmiScreens GetUnifiedScreens(
            [Description(UnifiedPath)] string softwarePath,
            [Description("group: return only the screens of this screen group and the groups in it, e.g. 'Pumps' or 'Pumps/Big'; empty (default) returns every screen. 'unified_get_screen_groups' lists the groups")] string group = "")
        {
            try
            {
                var screens = Portal.GetUnifiedScreens(softwarePath, group);

                return new ResponseHmiScreens
                {
                    Message = string.IsNullOrWhiteSpace(group)
                        ? $"{screens.Count} screen(s) in '{softwarePath}'"
                        : $"{screens.Count} screen(s) in group '{group}' of '{softwarePath}'",
                    Items = screens,
                    Meta = ReadMeta()
                };
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
            }
        }

        [McpServerTool(Name = "unified_get_screen_items", Title = "Get WinCC Unified screen items", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the items of a WinCC Unified screen: name, type, position, size, text, process value and the events that have a handler")]
        public static ResponseHmiScreenItems GetUnifiedScreenItems(
            [Description(UnifiedPath)] string softwarePath,
            [Description("screenName: name of the screen")] string screenName)
        {
            try
            {
                var items = Portal.GetUnifiedScreenItems(softwarePath, screenName);

                return new ResponseHmiScreenItems
                {
                    Message = $"{items.Count} item(s) on screen '{screenName}'",
                    Items = items,
                    Meta = ReadMeta()
                };
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
            }
        }

        [McpServerTool(Name = "unified_get_screen_item_properties", Title = "Get WinCC Unified item properties", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Every property of a WinCC Unified screen item with its current value, plus '_Dynamizations', '_Events' and, for a faceplate instance, '_Interface'. With an empty itemName the properties of the screen itself. The property names are what 'unified_manage_items' takes")]
        public static ResponseUnifiedProperties GetUnifiedScreenItemProperties(
            [Description(UnifiedPath)] string softwarePath,
            [Description("screenName: name of the screen")] string screenName,
            [Description("itemName: name of the item; empty for the screen itself")] string itemName = "")
        {
            try
            {
                var properties = Portal.GetUnifiedScreenItemProperties(softwarePath, screenName, itemName);

                return new ResponseUnifiedProperties
                {
                    Message = $"{properties.Count(p => !p.Key.StartsWith("_", StringComparison.Ordinal))} properties of '{(string.IsNullOrEmpty(itemName) ? screenName : itemName)}'",
                    Items = properties,
                    Meta = ReadMeta()
                };
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
            }
        }

        [McpServerTool(Name = "unified_get_tags", Title = "Get WinCC Unified tags", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the HMI tags of a WinCC Unified HMI with tag table, data type, connection and PLC tag; a tag without a connection is an internal tag. A large HMI has thousands of tags: narrow the list with nameFilter or tagTable")]
        public static ResponseHmiTags GetUnifiedTags(
            [Description(UnifiedPath)] string softwarePath,
            [Description("nameFilter: regular expression on the tag name, case-insensitive; empty (default) returns every tag")] string nameFilter = "",
            [Description("tagTable: return only the tags of this tag table; 'unified_get_tag_tables' lists the tables")] string tagTable = "")
        {
            try
            {
                var tags = Portal.GetUnifiedTags(softwarePath, nameFilter, tagTable);

                return new ResponseHmiTags
                {
                    Message = string.IsNullOrEmpty(nameFilter)
                        ? $"{tags.Count} tag(s) in '{softwarePath}'"
                        : $"{tags.Count} tag(s) in '{softwarePath}' match '{nameFilter}'",
                    Items = tags,
                    Meta = ReadMeta()
                };
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
            }
        }

        [McpServerTool(Name = "unified_get_tag_table_groups", Title = "Get WinCC Unified tag table groups", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the tag table groups of a WinCC Unified HMI, nested ones included, with the number of tag tables and groups directly in each. A nested group is written 'Parent/Child'")]
        public static ResponseUnifiedTagTableGroups GetUnifiedTagTableGroups(
            [Description(UnifiedPath)] string softwarePath)
        {
            try
            {
                var groups = Portal.GetUnifiedTagTableGroups(softwarePath);

                return new ResponseUnifiedTagTableGroups
                {
                    Message = $"{groups.Count} tag table group(s) in '{softwarePath}'",
                    Items = groups,
                    Meta = ReadMeta()
                };
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
            }
        }

        [McpServerTool(Name = "unified_get_tag_tables", Title = "Get WinCC Unified tag tables", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the tag tables of a WinCC Unified HMI with the number of tags in each")]
        public static ResponseUnifiedTagTables GetUnifiedTagTables(
            [Description(UnifiedPath)] string softwarePath)
        {
            try
            {
                var tables = Portal.GetUnifiedTagTables(softwarePath);

                return new ResponseUnifiedTagTables
                {
                    Message = $"{tables.Count} tag table(s) in '{softwarePath}'",
                    Items = tables,
                    Meta = ReadMeta()
                };
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
            }
        }

        [McpServerTool(Name = "unified_get_screen_groups", Title = "Get WinCC Unified screen groups", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the screen groups of a WinCC Unified HMI, nested ones included, with the number of screens and groups directly in each. A nested group is written 'Parent/Child'")]
        public static ResponseUnifiedScreenGroups GetUnifiedScreenGroups(
            [Description(UnifiedPath)] string softwarePath)
        {
            try
            {
                var groups = Portal.GetUnifiedScreenGroups(softwarePath);

                return new ResponseUnifiedScreenGroups
                {
                    Message = $"{groups.Count} screen group(s) in '{softwarePath}'",
                    Items = groups,
                    Meta = ReadMeta()
                };
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
            }
        }

        [McpServerTool(Name = "unified_get_scripts", Title = "Get WinCC Unified script modules", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the global script modules of a WinCC Unified HMI with their source: global definitions (constants, variables, helpers), functions and the list of exported functions with their parameters. Scripts on events and dynamizations of screen items are not modules; see 'unified_get_screen_item_properties'")]
        public static ResponseUnifiedScripts GetUnifiedScripts(
            [Description(UnifiedPath)] string softwarePath,
            [Description("moduleName: return only this module; empty (default) returns all")] string moduleName = "")
        {
            try
            {
                var modules = Portal.GetUnifiedScripts(softwarePath, moduleName);

                return new ResponseUnifiedScripts
                {
                    Message = $"{modules.Count} script module(s) in '{softwarePath}'",
                    Items = modules,
                    Meta = ReadMeta()
                };
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
            }
        }

        [McpServerTool(Name = "unified_get_logs", Title = "Get WinCC Unified logs", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the data logs, alarm logs and audit trails of a WinCC Unified HMI with their settings: maximum size, duration, storage device and folder, segment size, start time and duration, backup. For a data log also the number of logging tags that archive into it. Audit trails can only be read")]
        public static ResponseUnifiedLogs GetUnifiedLogs(
            [Description(UnifiedPath)] string softwarePath,
            [Description("type: 'data', 'alarm' or 'audit'; empty (default) returns all kinds")] string type = "",
            [Description("logName: return only this log; empty (default) returns all")] string logName = "")
        {
            try
            {
                var logs = Portal.GetUnifiedLogs(softwarePath, type, logName);

                return new ResponseUnifiedLogs
                {
                    Message = $"{logs.Count} log(s) in '{softwarePath}'",
                    Items = logs,
                    Meta = ReadMeta()
                };
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
            }
        }

        [McpServerTool(Name = "unified_get_logging_tags", Title = "Get WinCC Unified logging tags", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the logging tags of a WinCC Unified HMI: which HMI tag is archived into which data log, and how (mode, cycle, aggregation, smoothing, limits, trigger). A large HMI has hundreds: narrow the list with tagName or logName")]
        public static ResponseUnifiedLoggingTags GetUnifiedLoggingTags(
            [Description(UnifiedPath)] string softwarePath,
            [Description("tagName: regular expression on the path of the process tag, case-insensitive (a member of a structured tag is 'Tag.Member'); empty (default) for all")] string tagName = "",
            [Description("logName: only the logging tags that archive into this data log")] string logName = "",
            [Description("limit: the most logging tags to return (default 500)")] int limit = 500)
        {
            try
            {
                var items = Portal.GetUnifiedLoggingTags(softwarePath, tagName, logName, limit, out var truncated);

                return new ResponseUnifiedLoggingTags
                {
                    Message = truncated
                        ? $"{items.Count} logging tag(s) returned, there are more: narrow the list with tagName or logName"
                        : $"{items.Count} logging tag(s) in '{softwarePath}'",
                    Items = items,
                    Truncated = truncated,
                    Meta = ReadMeta()
                };
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
            }
        }

        [McpServerTool(Name = "unified_get_connections", Title = "Get WinCC Unified connections", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the connections of a WinCC Unified HMI with their attributes and driver parameters ('DriverProperties')")]
        public static ResponseUnifiedList GetUnifiedConnections(
            [Description(UnifiedPath)] string softwarePath)
        {
            try
            {
                var connections = Portal.GetUnifiedConnections(softwarePath);

                return new ResponseUnifiedList
                {
                    Message = $"{connections.Count} connection(s) in '{softwarePath}'",
                    Items = connections,
                    Meta = ReadMeta()
                };
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
            }
        }

        [McpServerTool(Name = "unified_get_alarms", Title = "Get WinCC Unified alarms", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the discrete and analog alarms of a WinCC Unified HMI: alarm class, the tag and bit or limit that raise the alarm, and the alarm text per language. An HMI can have hundreds of alarms: narrow the list with type or nameFilter")]
        public static ResponseUnifiedAlarms GetUnifiedAlarms(
            [Description(UnifiedPath)] string softwarePath,
            [Description("type: 'discrete' or 'analog'; empty (default) returns both")] string type = "",
            [Description("nameFilter: regular expression on the alarm name, case-insensitive; empty (default) returns every alarm")] string nameFilter = "")
        {
            try
            {
                var alarms = Portal.GetUnifiedAlarms(softwarePath, type, nameFilter);

                return new ResponseUnifiedAlarms
                {
                    Message = $"{alarms.Count} alarm(s) in '{softwarePath}'" + (string.IsNullOrEmpty(nameFilter) ? string.Empty : $" match '{nameFilter}'"),
                    Items = alarms,
                    Meta = ReadMeta()
                };
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
            }
        }

        [McpServerTool(Name = "unified_get_alarm_classes", Title = "Get WinCC Unified alarm classes", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the alarm classes of a WinCC Unified HMI with priority, state machine and the colors of each alarm state")]
        public static ResponseUnifiedAlarmClasses GetUnifiedAlarmClasses(
            [Description(UnifiedPath)] string softwarePath)
        {
            try
            {
                var classes = Portal.GetUnifiedAlarmClasses(softwarePath);

                return new ResponseUnifiedAlarmClasses
                {
                    Message = $"{classes.Count} alarm class(es) in '{softwarePath}'",
                    Items = classes,
                    Meta = ReadMeta()
                };
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
            }
        }

        [McpServerTool(Name = "unified_get_text_lists", Title = "Get WinCC Unified text lists", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the text lists of a WinCC Unified HMI with their entries. An entry is of type 'value' (one value), 'range' (from..to), 'from' (a value and above), 'to' (a value and below) or 'default', and carries its text per language. A list that is a library type is not listed")]
        public static ResponseUnifiedLists GetUnifiedTextLists(
            [Description(UnifiedPath)] string softwarePath,
            [Description("listName: return only this list; empty (default) returns all")] string listName = "",
            [Description("system: true returns the system text lists of the HMI instead (alarm texts, error reasons); they can only be read")] bool system = false)
        {
            return ReadUnifiedLists(softwarePath, system ? "system" : "text", listName);
        }

        [McpServerTool(Name = "unified_get_graphic_lists", Title = "Get WinCC Unified graphic lists", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the graphic lists of a WinCC Unified HMI with their entries. An entry is of type 'value', 'range', 'from', 'to' or 'default' and names a project graphic")]
        public static ResponseUnifiedLists GetUnifiedGraphicLists(
            [Description(UnifiedPath)] string softwarePath,
            [Description("listName: return only this list; empty (default) returns all")] string listName = "")
        {
            return ReadUnifiedLists(softwarePath, "graphic", listName);
        }

        private static ResponseUnifiedLists ReadUnifiedLists(string softwarePath, string kind, string listName)
        {
            try
            {
                var lists = Portal.GetUnifiedLists(softwarePath, kind, listName);

                return new ResponseUnifiedLists
                {
                    Message = $"{lists.Count} {kind} list(s) in '{softwarePath}'",
                    Items = lists,
                    Meta = ReadMeta()
                };
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
            }
        }
        private static System.Text.Json.Nodes.JsonObject ReadMeta() => new System.Text.Json.Nodes.JsonObject
        {
            ["timestamp"] = DateTime.Now,
            ["success"] = true
        };

        #endregion

        #region screens (write)

        [WriteTool]
        [McpServerTool(Name = "unified_create_screen", Title = "Create WinCC Unified screen", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create an empty screen in a WinCC Unified HMI, at the top level or in a screen group. Set its size and other properties with 'unified_manage_items' (empty itemName)")]
        public static ResponseCreated CreateUnifiedScreen(
            [Description(UnifiedPath)] string softwarePath,
            [Description("screenName: name of the new screen; unique in the whole HMI, groups included")] string screenName,
            [Description("group: screen group to create the screen in, e.g. 'Pumps' or 'Pumps/Big'; empty (default) for the top level. The group has to exist: 'unified_manage_screen_groups' creates one")] string group = "")
        {
            return Guarded(nameof(CreateUnifiedScreen), () =>
            {
                var screen = Portal.CreateUnifiedScreen(softwarePath, screenName, group);

                return Created("Screen", screen.Name, screen.Name);
            });
        }

        [WriteTool]
        [McpServerTool(Name = "unified_delete_screen", Title = "Delete WinCC Unified screen", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a screen of a WinCC Unified HMI with everything on it")]
        public static ResponseDeleted DeleteUnifiedScreen(
            [Description(UnifiedPath)] string softwarePath,
            [Description("screenName: name of the screen to delete")] string screenName)
        {
            return Guarded(nameof(DeleteUnifiedScreen), () =>
            {
                Portal.DeleteUnifiedScreen(softwarePath, screenName);

                return Deleted("Screen", screenName);
            });
        }

        [WriteTool]
        [McpServerTool(Name = "unified_manage_screen_groups", Title = "Manage WinCC Unified screen groups", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create, rename or delete screen groups of a WinCC Unified HMI, several at once. A group in a group is written 'Parent/Child'; the parent has to exist. Deleting a group deletes the screens in it. A call applies all of its actions or none")]
        public static ResponseUnifiedActions ManageUnifiedScreenGroups(
            [Description(UnifiedPath)] string softwarePath,
            [Description("actions: the changes to make, applied in order")] List<UnifiedScreenGroupAction> actions)
        {
            return Guarded(nameof(ManageUnifiedScreenGroups), () => UnifiedActions(Portal.ManageUnifiedScreenGroups(softwarePath, actions)));
        }

        #endregion

        #region tags, connections and alarms (write)

        [WriteTool]
        [McpServerTool(Name = "unified_manage_tags", Title = "Manage WinCC Unified tags", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create, update, upsert or delete HMI tags of a WinCC Unified HMI, several at once. A new tag is an internal Int tag in the default tag table unless tagTable and properties say otherwise. For a PLC tag set Connection and PlcTag (symbolic; the data type follows the PLC tag) or Connection, AccessMode 'AbsoluteAccess', DataType and Address. A call applies all of its actions or none. A tag that screens still use is deleted without warning")]
        public static ResponseUnifiedActions ManageUnifiedTags(
            [Description(UnifiedPath)] string softwarePath,
            [Description("actions: the changes to make, applied in order")] List<UnifiedTagAction> actions)
        {
            return Guarded(nameof(ManageUnifiedTags), () => UnifiedActions(Portal.ManageUnifiedTags(softwarePath, actions)));
        }

        [WriteTool]
        [McpServerTool(Name = "unified_manage_scripts", Title = "Manage WinCC Unified script modules", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create or replace global script modules of a WinCC Unified HMI, several at once. A module is written as a whole (globalDefinitions and functions): what it held before is replaced, so read it first with 'unified_get_scripts' to change part of it. Every write is read back and compared; code that TIA Portal cannot parse (syntax errors, default parameter values) is reported and rolled back instead of being stored mangled. Script modules cannot be deleted or renamed through Openness. A call applies all of its actions or none")]
        public static ResponseUnifiedActions ManageUnifiedScripts(
            [Description(UnifiedPath)] string softwarePath,
            [Description("actions: the changes to make, applied in order")] List<UnifiedScriptAction> actions)
        {
            return Guarded(nameof(ManageUnifiedScripts), () => UnifiedActions(Portal.ManageUnifiedScripts(softwarePath, actions)));
        }

        [WriteTool]
        [McpServerTool(Name = "unified_manage_logs", Title = "Manage WinCC Unified logs", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create, update, upsert or delete data logs and alarm logs of a WinCC Unified HMI, several at once; settings are given by name, e.g. \"Settings.LogMaxSize\" or \"Segment.SegmentMaxSize\". Audit trails cannot be changed. Renaming a log renames it in the logging tags that use it; deleting one leaves them pointing at it. A call applies all of its actions or none")]
        public static ResponseUnifiedActions ManageUnifiedLogs(
            [Description(UnifiedPath)] string softwarePath,
            [Description("actions: the changes to make, applied in order")] List<UnifiedLogAction> actions)
        {
            return Guarded(nameof(ManageUnifiedLogs), () => UnifiedActions(Portal.ManageUnifiedLogs(softwarePath, actions)));
        }

        [WriteTool]
        [McpServerTool(Name = "unified_manage_logging_tags", Title = "Manage WinCC Unified logging tags", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create, update, upsert or delete logging tags of a WinCC Unified HMI, several at once: the archiving of an HMI tag into a data log. A structured tag keeps its logging tags on its members: tagName is then 'Tag.Member'. A new logging tag starts in the first data log with the mode OnChange. Mode Cyclic needs a Cycle of at least T500ms; mode OnDemand needs TriggerMode and TriggerTag. The data log and the trigger tag are checked. A trend shows an archived tag when its data source is '<process tag>:<logging tag>', e.g. 'AI_DB_CP10-U1.field_input_EUF:AI_DB_CP10-U1' (see 'unified_configure_trend_control'). A call applies all of its actions or none")]
        public static ResponseUnifiedActions ManageUnifiedLoggingTags(
            [Description(UnifiedPath)] string softwarePath,
            [Description("actions: the changes to make, applied in order")] List<UnifiedLoggingTagAction> actions)
        {
            return Guarded(nameof(ManageUnifiedLoggingTags), () => UnifiedActions(Portal.ManageUnifiedLoggingTags(softwarePath, actions)));
        }

        [WriteTool]
        [McpServerTool(Name = "unified_manage_tag_table_groups", Title = "Manage WinCC Unified tag table groups", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create, rename or delete tag table groups of a WinCC Unified HMI, several at once. A group in a group is written 'Parent/Child'; the parent has to exist. Deleting a group deletes the tag tables in it and their tags. A call applies all of its actions or none")]
        public static ResponseUnifiedActions ManageUnifiedTagTableGroups(
            [Description(UnifiedPath)] string softwarePath,
            [Description("actions: the changes to make, applied in order")] List<UnifiedTagTableGroupAction> actions)
        {
            return Guarded(nameof(ManageUnifiedTagTableGroups), () => UnifiedActions(Portal.ManageUnifiedTagTableGroups(softwarePath, actions)));
        }

        [WriteTool]
        [McpServerTool(Name = "unified_manage_tag_tables", Title = "Manage WinCC Unified tag tables", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create, rename or delete tag tables of a WinCC Unified HMI, several at once. Deleting a table deletes the tags in it; the default tag table cannot be deleted. A call applies all of its actions or none")]
        public static ResponseUnifiedActions ManageUnifiedTagTables(
            [Description(UnifiedPath)] string softwarePath,
            [Description("actions: the changes to make, applied in order")] List<UnifiedTagTableAction> actions)
        {
            return Guarded(nameof(ManageUnifiedTagTables), () => UnifiedActions(Portal.ManageUnifiedTagTables(softwarePath, actions)));
        }

        [WriteTool]
        [McpServerTool(Name = "unified_manage_connections", Title = "Manage WinCC Unified connections", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create, update, upsert or delete connections of a WinCC Unified HMI, several at once. With 'partner' a new connection is an integrated connection to a PLC of the project, on which HMI tags can name PLC tags symbolically; without it the connection is not integrated and is addressed through driverProperties. A call applies all of its actions or none. A connection that tags still use is deleted without warning")]
        public static ResponseUnifiedActions ManageUnifiedConnections(
            [Description(UnifiedPath)] string softwarePath,
            [Description("actions: the changes to make, applied in order")] List<UnifiedConnectionAction> actions)
        {
            return Guarded(nameof(ManageUnifiedConnections), () => UnifiedActions(Portal.ManageUnifiedConnections(softwarePath, actions)));
        }

        [WriteTool]
        [McpServerTool(Name = "unified_manage_alarms", Title = "Manage WinCC Unified alarms", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create, update, upsert or delete discrete and analog alarms of a WinCC Unified HMI, several at once. A discrete alarm is raised by a bit of an HMI tag (RaisedStateTag, RaisedStateTagBitNumber), an analog alarm by a limit (RaisedStateTag, Condition, ConditionValue). The tag and the alarm class have to exist. A call applies all of its actions or none")]
        public static ResponseUnifiedActions ManageUnifiedAlarms(
            [Description(UnifiedPath)] string softwarePath,
            [Description("actions: the changes to make, applied in order")] List<UnifiedAlarmAction> actions)
        {
            return Guarded(nameof(ManageUnifiedAlarms), () => UnifiedActions(Portal.ManageUnifiedAlarms(softwarePath, actions)));
        }

        [WriteTool]
        [McpServerTool(Name = "unified_manage_alarm_classes", Title = "Manage WinCC Unified alarm classes", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create, update, upsert or delete alarm classes of a WinCC Unified HMI, several at once: priority, state machine and the colors of the alarm states. System classes cannot be deleted. A call applies all of its actions or none")]
        public static ResponseUnifiedActions ManageUnifiedAlarmClasses(
            [Description(UnifiedPath)] string softwarePath,
            [Description("actions: the changes to make, applied in order")] List<UnifiedAlarmClassAction> actions)
        {
            return Guarded(nameof(ManageUnifiedAlarmClasses), () => UnifiedActions(Portal.ManageUnifiedAlarmClasses(softwarePath, actions)));
        }

        [WriteTool]
        [McpServerTool(Name = "unified_manage_lists", Title = "Manage WinCC Unified text and graphic lists", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create, replace or delete text lists and graphic lists of a WinCC Unified HMI, several at once. A list is written as a whole: 'entries' replaces everything the list had. An entry stands for one value ('value'), a range ('from' and 'to'), a value and above ('from'), a value and below ('to'), or is the default entry ('default': true); it carries 'text' in a text list and 'graphic', the name of a project graphic, in a graphic list. Every write is read back and compared. Bind a list to a screen item with 'unified_manage_items'. A call applies all of its actions or none")]
        public static ResponseUnifiedActions ManageUnifiedLists(
            [Description(UnifiedPath)] string softwarePath,
            [Description("actions: the changes to make, applied in order")] List<UnifiedListAction> actions)
        {
            return Guarded(nameof(ManageUnifiedLists), () => UnifiedActions(Portal.ManageUnifiedLists(softwarePath, actions)));
        }
        // All or nothing: the Portal method throws when any action fails, which rolls the
        // transaction back, so a list that arrives here holds only applied actions.
        private static ResponseUnifiedActions UnifiedActions(List<UnifiedActionResult> results) => new ResponseUnifiedActions
        {
            Results = results,
            SuccessCount = results.Count,
            Message = $"{results.Count} action(s) applied. {SaveHint}",
            Meta = OkMeta()
        };

        #endregion

        #region items (write)

        [WriteTool]
        [McpServerTool(Name = "unified_manage_items", Title = "Manage WinCC Unified screen items", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create, update, upsert or delete items on WinCC Unified screens, several at once. Each action sets any number of properties - a static value or a dynamization (tag, script, or a text or graphic list driven by a tag) - and event handlers. Use 'unified_get_screen_items' and 'unified_get_screen_item_properties' to find item and property names. A call applies all of its actions or none: if one fails, the error names it and nothing is changed. Faceplate instances are parameterized with 'unified_manage_faceplate', trends with 'unified_configure_trend_control'")]
        public static ResponseHmiManageItems ManageUnifiedItems(
            [Description(UnifiedPath)] string softwarePath,
            [Description("actions: the changes to make, applied in order")] List<HmiItemAction> actions)
        {
            return Guarded(nameof(ManageUnifiedItems), () =>
            {
                // All or nothing: Portal.ManageUnifiedItems throws when any action fails, which
                // rolls the transaction back, so reaching the next line means every action took.
                var results = Portal.ManageUnifiedItems(softwarePath, actions);

                return new ResponseHmiManageItems
                {
                    Results = results,
                    SuccessCount = results.Count,
                    Message = $"{results.Count} action(s) applied. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "unified_manage_faceplate", Title = "Manage WinCC Unified faceplate instance", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create or update a faceplate instance on a WinCC Unified screen and set the interface properties of its faceplate type. The interface differs from type to type, so the response always lists it - names, current values and tag bindings; call with only itemName and action 'update' to read it. All settings of a call are applied or none. Delete an instance with 'unified_manage_items'")]
        public static ResponseUnifiedFaceplate ManageUnifiedFaceplate(
            [Description(UnifiedPath)] string softwarePath,
            [Description("screenName: name of the screen")] string screenName,
            [Description("itemName: name of the faceplate instance")] string itemName,
            [Description("action: 'create' (fails if the item exists), 'update' (fails if it does not) or 'upsert'")] string action = "upsert",
            [Description("faceplateType: faceplate type and version, e.g. 'V0.0.2\\\\MyFaceplate' - the 'ContainedType' value from 'get_library_types'. Required when the instance is created")] string faceplateType = "",
            [Description("properties: properties of the instance itself, e.g. {\"Left\": 100, \"Top\": 50, \"Width\": 200}; same forms as in 'unified_manage_items'")] Dictionary<string, JsonElement>? properties = null,
            [Description("interfaceValues: interface properties of the faceplate type, by name. A plain value sets a static value - for a tag interface that is the name of the HMI tag. A property interface can also be dynamized: {\"tag\": \"HmiTagName\"} or {\"script\": \"return ...;\"}. A tag interface can take {\"tagParameter\": \"Name\"}. {\"dynamization\": \"none\"} removes the dynamization")] Dictionary<string, JsonElement>? interfaceValues = null)
        {
            return Guarded(nameof(ManageUnifiedFaceplate), () =>
            {
                var result = Portal.ManageUnifiedFaceplate(softwarePath, screenName, itemName, action, faceplateType, properties, interfaceValues);

                return new ResponseUnifiedFaceplate
                {
                    Result = result,
                    Message = $"Faceplate instance '{result.ItemName}' {(result.Created ? "created" : "updated")}" +
                              $"{(result.Applied.Count == 0 ? string.Empty : ": " + string.Join(", ", result.Applied))}. " +
                              $"Its interface has {result.Interface.Count} propert{(result.Interface.Count == 1 ? "y" : "ies")}. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "unified_configure_trend_control", Title = "Configure WinCC Unified trend control", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Add a trend (pen) to an HmiTrendControl on a WinCC Unified screen and bind it to a data source. Create the control itself with 'unified_manage_items' (itemType 'HmiTrendControl'); bind an HmiTrendCompanion to it by setting the companion's 'SourceTrendControl' property there")]
        public static ResponseMessage ConfigureUnifiedTrendControl(
            [Description(UnifiedPath)] string softwarePath,
            [Description("screenName: name of the screen")] string screenName,
            [Description("trendControlName: name of the HmiTrendControl item")] string trendControlName,
            [Description("trendName: display name of the trend")] string trendName,
            [Description("dataSource: what the trend shows: an HMI tag, or an archived tag as '<HMI tag>:<logging tag>' (the logging tag is checked; 'unified_get_logging_tags' lists them)")] string dataSource,
            [Description("trendMode: how the trend is drawn, e.g. Points, Interpolated, Stepped, Bar, Value (optional)")] string? trendMode = null,
            [Description("lineWidth: line width, 0-255 (optional)")] int? lineWidth = null,
            [Description("lineColor: line color by name (e.g. Red) or as '#RRGGBB' (optional)")] string? lineColor = null)
        {
            return Guarded(nameof(ConfigureUnifiedTrendControl), () =>
            {
                var message = Portal.ConfigureUnifiedTrendControl(softwarePath, screenName, trendControlName, trendName, dataSource, trendMode, lineWidth, lineColor);

                return new ResponseMessage
                {
                    Message = $"{message} {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }

        #endregion

        #region debug

        [DebugTool]
        [McpServerTool(Name = "unified_debug_reflect", Title = "Debug: find Openness types", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Development aid: list the Openness types whose full name contains the given text")]
        public static ResponseUnifiedList DebugUnifiedReflect(
            [Description("typeName: part of a type name, e.g. 'Dynamization'")] string typeName)
        {
            try
            {
                var assemblies = new[]
                {
                    typeof(global::Siemens.Engineering.TiaPortal).Assembly,
                    typeof(global::Siemens.Engineering.HmiUnified.HmiSoftware).Assembly
                };

                var types = assemblies.Distinct()
                    .SelectMany(a => a.GetTypes())
                    .Where(t => t.FullName != null && t.FullName.IndexOf(typeName, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(t => new Dictionary<string, object?> { ["FullName"] = t.FullName })
                    .ToList();

                return new ResponseUnifiedList { Message = $"{types.Count} type(s)", Items = types, Meta = ReadMeta() };
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
            }
        }

        [DebugTool]
        [McpServerTool(Name = "unified_debug_screen_item", Title = "Debug: reflect a screen item", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Development aid: the .NET type of a WinCC Unified screen item (or screen) and its public properties")]
        public static ResponseUnifiedProperties DebugUnifiedScreenItem(
            [Description(UnifiedPath)] string softwarePath,
            [Description("screenName: name of the screen")] string screenName,
            [Description("itemName: name of the item; empty for the screen itself")] string itemName = "")
        {
            try
            {
                var reflected = Portal.DebugUnifiedScreenItem(softwarePath, screenName, itemName);

                return new ResponseUnifiedProperties { Message = $"{reflected["Type"]}", Items = reflected, Meta = ReadMeta() };
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
            }
        }

        #endregion
    }
}
