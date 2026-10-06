using ModelContextProtocol.Server;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    [McpServerPromptType]
    public static class McpPrompts
    {
        #region Basic Connection Templates

        [McpServerPrompt(Name = "doctor"), Description("Diagnose the TIA Portal environment")]
        public static string Doctor()
        {
            return @"Diagnose the TIA Portal environment.

The report shows:
- Connected: whether this server currently holds a TIA Portal connection
- Project: name and path of the open project, or 'No project open'
- Active Version: the TIA major version this server was started with (see the --tia-major-version argument)
- Installed TIA Portal versions: every installed version >= V21 with its installation path, plus a check
  that the Openness assemblies ('Engineering') and the Portal executable ('Portal') are present
- User in 'Siemens TIA Openness' user group: required for any Openness access

Start here when a connection fails: a missing user group membership, a missing installation or an
active version that is not installed are the usual causes.

Use the 'doctor' tool to run the diagnosis. It is read-only: it does not connect to TIA Portal,
does not open a project and does not change user group membership.";
        }

        [McpServerPrompt(Name = "connect"), Description("Connect to TIA Portal")]
        public static string Connect()
        {
            return @"Connect to TIA Portal.

This will establish a connection to either a running TIA Portal instance or start a new one.

Use the 'connect' tool to initiate the connection.";
        }

        [McpServerPrompt(Name = "open_project"), Description("Open project")]
        public static string OpenProject(string projectPath)
        {
            return $@"Open the TIA Portal project.

Common parameter values:
- projectPath: the full path to the project file (.ap19, .ap20, .ap21, etc.) or local session file (.als19, .als20, .als21, etc.).

Use the 'open_project' tool with this parameter:
- projectPath: {projectPath}";
        }

        [McpServerPrompt(Name = "close_project"), Description("Close project")]
        public static string CloseProject()
        {
            return @"Close the currently open TIA Portal project.

This will close the active project and return TIA Portal to the main screen.

Use the 'close_project' tool to close the current project.";
        }

        [McpServerPrompt(Name = "disconnect"), Description("Disconnect")]
        public static string Disconnect()
        {
            return @"Disconnect from TIA Portal.

Use the 'disconnect' tool to remove the connection.";
        }

        #endregion

        #region Project Information Templates

        [McpServerPrompt(Name = "get_project_tree"), Description("Get project tree")]
        public static string GetProjectTree()
        {
            return @"Retrieve the complete structure of the current TIA Portal project.

The hierarchical tree will display:
- All devices
- Device items
- Groups
- PLC/HMI software

Use the 'get_project_tree' tool to display the project organization and locate software paths for other operations.";
        }

        [McpServerPrompt(Name = "get_software_tree"), Description("Get software tree")]
        public static string GetSoftwareTree(string softwarePath)
        {
            return $@"Retrieve the complete structure of PLC software.

The hierarchical tree will display:
- Function (OB, FB, FC) and data (ArrayDB, GlobalDB, InstanceDB) blocks (organized by groups and subgroups)
- User-defined data types (organized by groups and subgroups)
- Hierarchical organization with proper tree formatting

Common parameter values:
- softwarePath: normally something like 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC

Use the 'plc_get_software_tree' tool with these parameters:
- softwarePath: {softwarePath}";
        }

        #endregion

        #region Export Templates

        #endregion

        #region Convenience Export Templates

        #endregion

        #region Import From Documents Templates

        #endregion

        #region Import From Sources Templates

        #endregion

        #region Convenience Import Templates

        #endregion

        #region Project and Session Templates

        [McpServerPrompt(Name = "get_state"), Description("Get state")]
        public static string GetState()
        {
            return $@"Get the state of the TIA-Portal MCP server.

Use the 'get_state' tool (it takes no parameters).";
        }

        [McpServerPrompt(Name = "get_project"), Description("Get project")]
        public static string GetProject()
        {
            return $@"Get open local project/session.

Use the 'get_project' tool (it takes no parameters).";
        }

        [McpServerPrompt(Name = "save_project"), Description("Save project")]
        public static string SaveProject()
        {
            return $@"Save the current TIA-Portal local project/session.

Use the 'save_project' tool (it takes no parameters).";
        }

        [McpServerPrompt(Name = "save_as_project"), Description("Save project as")]
        public static string SaveAsProject(string newProjectPath)
        {
            return $@"Save current TIA-Portal project/session with a new name.

Common parameter values:
- newProjectPath: defines the new path where to save the project

Use the 'save_as_project' tool with these parameters:
- newProjectPath: {newProjectPath}";
        }

        [McpServerPrompt(Name = "open_tia_project"), Description("Connect and open a project")]
        public static string OpenTiaProject(string path)
        {
            return $@"Connect to TIA Portal if not already connected, open the given project or session, and return the device and PLC software paths the other tools need. Replaces the connect, open_project, get_project_tree sequence. TIA Portal must already be running.

Common parameter values:
- path: full path of the .apXX project or .alsXX session file on the machine running this server

Use the 'open_tia_project' tool with these parameters:
- path: {path}";
        }

        [McpServerPrompt(Name = "preview_import"), Description("Preview import")]
        public static string PreviewImport(string softwarePath, string importPath, string kind, string groupPath = "")
        {
            return $@"Report what importing a directory would create, overwrite or collide with, without touching the project. Checks each file against the objects already in the PLC, including the rule that a PLC data type name must be unique across the whole PLC - importing an existing type name into a different group fails even with importOption 'Override'.

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- importPath: directory holding the files to import (.s7dcl source documents or .xml)
- kind: what the files contain - 'type' for PLC data types, 'block' for program blocks
- groupPath: the group the import would target; empty means the root of that area. A leading system folder segment is accepted

Use the 'preview_import' tool with these parameters:
- softwarePath: {softwarePath}
- importPath: {importPath}
- kind: {kind}
- groupPath: {groupPath}";
        }

        #endregion

        #region Device Templates

        [McpServerPrompt(Name = "get_device_info"), Description("Get device info")]
        public static string GetDeviceInfo(string devicePath)
        {
            return $@"Get info from a device from the current project/session.

Common parameter values:
- devicePath: defines the path in the project structure to the device

Use the 'hw_get_device_info' tool with these parameters:
- devicePath: {devicePath}";
        }

        [McpServerPrompt(Name = "get_device_item_info"), Description("Get device item info")]
        public static string GetDeviceItemInfo(string deviceItemPath)
        {
            return $@"Get info from a device item from the current project/session.

Common parameter values:
- deviceItemPath: defines the path in the project structure to the device item

Use the 'get_device_item_info' tool with these parameters:
- deviceItemPath: {deviceItemPath}";
        }

        [McpServerPrompt(Name = "get_devices"), Description("Get devices")]
        public static string GetDevices()
        {
            return $@"Get a list of all devices in the project/session.

Use the 'get_devices' tool (it takes no parameters).";
        }

        #endregion

        #region Software Templates

        [McpServerPrompt(Name = "get_software_info"), Description("Get software info")]
        public static string GetSoftwareInfo(string softwarePath)
        {
            return $@"Get plc software info.

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software

Use the 'plc_get_software_info' tool with these parameters:
- softwarePath: {softwarePath}";
        }

        [McpServerPrompt(Name = "compile_software"), Description("Compile software")]
        public static string CompileSoftware(string softwarePath, string password = "")
        {
            return $@"Compile the software and report every compiler message with the object it belongs to, so errors can be fixed without re-reading the whole PLC. Warnings are reported as a successful compile with detail; only errors fail the call.

Common parameter values:
|- softwarePath: defines the path in the project structure to the software
- password: the password to access administration, default: no password

Use the 'plc_compile_software' tool with these parameters:
- softwarePath: {softwarePath}
- password: {password}";
        }

        [McpServerPrompt(Name = "resolve_object_path"), Description("Resolve object path")]
        public static string ResolveObjectPath(string softwarePath, string name, string kind = "any")
        {
            return $@"Turn a bare or partial object name into the root-relative path the other tools need, searching program blocks, data types, tags, tag tables, watch tables and external sources. Exact matches win; substring matches are only reported when nothing matches exactly.

Common parameter values:
- softwarePath: defines the path in the project structure to the software
- name: the object name to look for, e.g. 'FC_Block_1'. A full path may be passed; only its last segment is matched
- kind: restrict the search to 'block', 'type', 'tag', 'tagTable', 'watchTable' or 'source'. Default 'any' searches all of them

Use the 'plc_resolve_object_path' tool with these parameters:
- softwarePath: {softwarePath}
- name: {name}
- kind: {kind}";
        }

        [McpServerPrompt(Name = "find_in_code"), Description("Search the program text")]
        public static string FindInCode(string softwarePath, string pattern, string nameFilter = "", string maxResults = "200")
        {
            return $@"Search the actual source text of program blocks and PLC data types with a regular expression - every other filter in this server matches object names only. Returns the object path, line number and the matching line. Each call exports the candidate objects behind the scenes, so narrow a large PLC with 'nameFilter'.

Common parameter values:
- softwarePath: defines the path in the project structure to the software
- pattern: regular expression matched against each line, case-insensitive. Plain text works too
- nameFilter: optional regular expression on object names, to limit which objects are searched. Empty (default) searches all of them
- maxResults: stop after this many matching lines (default 200)

Use the 'plc_find_in_code' tool with these parameters:
- softwarePath: {softwarePath}
- pattern: {pattern}
- nameFilter: {nameFilter}
- maxResults: {maxResults}";
        }

        [McpServerPrompt(Name = "get_plc_summary"), Description("Get PLC summary")]
        public static string GetPlcSummary(string softwarePath)
        {
            return $@"Counts, programming languages and health of one PLC software in a single call - replaces listing blocks, types, tags, tag tables and watch tables separately just to see what is there. Also names the inconsistent objects (which refuse to export until compiled) and the know-how protected ones (whose content cannot be read).

Common parameter values:
- softwarePath: defines the path in the project structure to the software

Use the 'get_plc_summary' tool with these parameters:
- softwarePath: {softwarePath}";
        }

        [McpServerPrompt(Name = "get_cross_references"), Description("Get cross references")]
        public static string GetCrossReferences(string softwarePath, string objectPath = "", string objectKind = "auto", string filter = "AllObjects", string maxDepth = "1")
        {
            return $@"Get cross references for a PLC software or for one block, type, tag table, tag or block group inside it. Watch tables, force tables and external sources have no cross references.

Common parameter values:
- softwarePath: defines the path in the project structure to the software
- objectPath: optional root-relative path of a block, type, tag table, tag or block group; empty targets the whole plc software
- objectKind: 'auto' (default), 'block', 'type', 'tagTable', 'tag' or 'blockGroup'
- filter: 'AllObjects' (default), 'ObjectsWithReferences', 'ObjectsWithoutReferences' or 'UnusedObjects'
- maxDepth: 1 = sources and their references (default), 2 = also source children, 3 = also reference locations. Keeps large results manageable

Use the 'plc_get_cross_references' tool with these parameters:
- softwarePath: {softwarePath}
- objectPath: {objectPath}
- objectKind: {objectKind}
- filter: {filter}
- maxDepth: {maxDepth}";
        }

        [McpServerPrompt(Name = "where_used"), Description("Where used")]
        public static string WhereUsed(string softwarePath, string name, string kind = "any")
        {
            return $@"Answer 'what uses this?' for a tag, block, PLC data type or tag table by name. Resolves the name, picks the right object kind and flattens the cross-reference tree to a plain list of users. Use 'plc_get_cross_references' instead when the full nested result or a specific filter is needed.

Common parameter values:
- softwarePath: defines the path in the project structure to the software
- name: the object to look up, by bare name or by full root-relative path
- kind: restrict resolution to 'block', 'type', 'tag' or 'tagTable'. Default 'any' picks the single match, and reports the candidates when the name is ambiguous

Use the 'plc_where_used' tool with these parameters:
- softwarePath: {softwarePath}
- name: {name}
- kind: {kind}";
        }

        #endregion

        #region Block Templates

        [McpServerPrompt(Name = "get_block_info"), Description("Get block info")]
        public static string GetBlockInfo(string softwarePath, string blockPath)
        {
            return $@"Get a block info, which is located in the software.

Common parameter values:
- softwarePath: defines the path in the project structure to the software
- blockPath: defines the path in the project structure to the block

Use the 'plc_get_block_info' tool with these parameters:
- softwarePath: {softwarePath}
- blockPath: {blockPath}";
        }

        [McpServerPrompt(Name = "get_blocks"), Description("Get blocks")]
        public static string GetBlocks(string softwarePath, string regexName = "")
        {
            return $@"Get a list of blocks, which are located in the software.

Common parameter values:
- softwarePath: defines the path in the project structure to the software
- regexName: defines the name or regular expression to find the block. Use empty string (default) to find all

Use the 'plc_get_blocks' tool with these parameters:
- softwarePath: {softwarePath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "get_blocks_with_hierarchy"), Description("Get blocks with hierarchy")]
        public static string GetBlocksWithHierarchy(string softwarePath)
        {
            return $@"Get a list of all blocks with their group hierarchy from the software.

Common parameter values:
- softwarePath: defines the path in the project structure to the software

Use the 'plc_get_blocks_hierarchy' tool with these parameters:
- softwarePath: {softwarePath}";
        }

        [McpServerPrompt(Name = "get_block_interface"), Description("Read a data block's members")]
        public static string GetBlockInterface(string softwarePath, string blockPath)
        {
            return $@"List the members of a data block with their data type and every attribute TIA Portal reports. Needs no export and works on inconsistent blocks. Data blocks only: Openness offers no interface accessor for FB, FC or OB, whose declarations come from 'plc_get_block_source' instead.

Common parameter values:
- softwarePath: defines the path in the project structure to the software
- blockPath: root-relative path of the data block, e.g. '1_Tests/DB_Block_1'

Use the 'get_block_interface' tool with these parameters:
- softwarePath: {softwarePath}
- blockPath: {blockPath}";
        }

        [McpServerPrompt(Name = "create_block_group"), Description("Create block group")]
        public static string CreateBlockGroup(string softwarePath, string parentGroupPath, string name)
        {
            return $@"Create a group below the Program blocks root of the plc software (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the software
- parentGroupPath: root-relative path of the parent group; empty creates directly below Program blocks
- name: name of the new group, without a slash

Use the 'plc_create_block_group' tool with these parameters:
- softwarePath: {softwarePath}
- parentGroupPath: {parentGroupPath}
- name: {name}";
        }

        [McpServerPrompt(Name = "delete_block_group"), Description("Delete block group")]
        public static string DeleteBlockGroup(string softwarePath, string groupPath)
        {
            return $@"Delete a block group and everything inside it. The Program blocks system group itself cannot be deleted (not available when the server runs with '--read-only').
- softwarePath: defines the path in the project structure to the software

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- groupPath: root-relative path of the group to delete, e.g. Common/CarrierRegister

Use the 'plc_delete_block_group' tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}";
        }

        [McpServerPrompt(Name = "delete_block"), Description("Delete block")]
        public static string DeleteBlock(string softwarePath, string blockPath)
        {
            return $@"Delete a program block. Know-how protected blocks are rejected: remove the protection in TIA Portal first (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- blockPath: root-relative path of the block, e.g. 1_Tests/FC_Block_1

Use the 'plc_delete_block' tool with these parameters:
- softwarePath: {softwarePath}
- blockPath: {blockPath}";
        }

        [McpServerPrompt(Name = "rename_block"), Description("Rename block")]
        public static string RenameBlock(string softwarePath, string blockPath, string newName)
        {
            return $@"Rename a program block. Know-how protected blocks are rejected (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- blockPath: root-relative path of the block, e.g. 1_Tests/FC_Block_1
- newName: the new block name, without a slash

Use the 'plc_rename_block' tool with these parameters:
- softwarePath: {softwarePath}
- blockPath: {blockPath}
- newName: {newName}";
        }

        [McpServerPrompt(Name = "create_fb"), Description("Create function block")]
        public static string CreateFB(string softwarePath, string groupPath, string name, string language = "LAD", string autoNumber = "true", string number = "0")
        {
            return $@"Create an empty function block in LAD, FBD, STL, SCL or ProDiag; the response carries the block number. For a block with code use 'plc_create_scl_block' or 'import_objects' (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- groupPath: root-relative block group that receives the FB; empty uses the Program blocks root
- name: name of the new function block, without a slash
- language: programming language such as LAD (default), FBD, STL, SCL or GRAPH
- autoNumber: let TIA Portal assign the block number (default true)
- number: explicit block number, used only when autoNumber is false

Use the 'plc_create_fb' tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- name: {name}
- language: {language}
- autoNumber: {NormalizeBool(autoNumber)}
- number: {number}";
        }

        [McpServerPrompt(Name = "create_instance_db"), Description("Create instance data block")]
        public static string CreateInstanceDB(string softwarePath, string groupPath, string name, string instanceOfName, string autoNumber = "true", string number = "0")
        {
            return $@"Create an instance data block for an existing function block (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- groupPath: root-relative block group that receives the DB; empty uses the Program blocks root
- name: name of the new instance data block, without a slash
- instanceOfName: name of the function block this instance DB belongs to
- autoNumber: let TIA Portal assign the block number (default true)
- number: explicit block number, used only when autoNumber is false

Use the 'plc_create_instance_db' tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- name: {name}
- instanceOfName: {instanceOfName}
- autoNumber: {NormalizeBool(autoNumber)}
- number: {number}";
        }

        [McpServerPrompt(Name = "copy_block"), Description("Copy block")]
        public static string CopyBlock(string softwarePath, string blockPath, string targetGroupPath, string newName = "", string targetSoftwarePath = "", string overwrite = "false")
        {
            return $@"Copy a program block. Block names and numbers are unique within a PLC, so a copy inside the same PLC requires 'newName' and gets a free block number; to keep the name, copy into another PLC with 'targetSoftwarePath'. To change only the group use 'plc_move_block'. The block must be consistent (compile first) (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software holding the block
- blockPath: root-relative path of the block to copy, e.g. 1_Tests/FC_Block_1
- targetGroupPath: root-relative block group that receives the copy; empty means the Program blocks root
- newName: name of the copy; required when copying inside the same PLC
- targetSoftwarePath: plc software that receives the copy; empty means the same PLC
- overwrite: replace a block of the final name that already exists in the target PLC (default false)

Use the 'plc_copy_block' tool with these parameters:
- softwarePath: {softwarePath}
- blockPath: {blockPath}
- targetGroupPath: {targetGroupPath}
- newName: {newName}
- targetSoftwarePath: {targetSoftwarePath}
- overwrite: {NormalizeBool(overwrite)}";
        }

        [McpServerPrompt(Name = "move_block"), Description("Move block")]
        public static string MoveBlock(string softwarePath, string blockPath, string targetGroupPath)
        {
            return $@"Move a program block into another block group of the same plc software; name and number are kept. Implemented as export, deleting the original and import; if the import fails the block is restored in its original group. The block must be consistent (compile first), and blocks that use it are inconsistent afterwards until the PLC is compiled again (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- blockPath: root-relative path of the block to move, e.g. 1_Tests/FC_Block_1
- targetGroupPath: root-relative block group that receives the block; empty means the Program blocks root

Use the 'plc_move_block' tool with these parameters:
- softwarePath: {softwarePath}
- blockPath: {blockPath}
- targetGroupPath: {targetGroupPath}";
        }

        #endregion

        #region Type Templates

        [McpServerPrompt(Name = "get_type_info"), Description("Get type info")]
        public static string GetTypeInfo(string softwarePath, string typePath)
        {
            return $@"Get a type info from the plc software.

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- typePath: defines the path in the project structure to the type

Use the 'plc_get_type_info' tool with these parameters:
- softwarePath: {softwarePath}
- typePath: {typePath}";
        }

        [McpServerPrompt(Name = "get_types"), Description("Get types")]
        public static string GetTypes(string softwarePath, string regexName = "")
        {
            return $@"Get a list of types from the plc software.

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- regexName: defines the name or regular expression to find the block. Use empty string (default) to find all

Use the 'plc_get_types' tool with these parameters:
- softwarePath: {softwarePath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "create_type_group"), Description("Create type group")]
        public static string CreateTypeGroup(string softwarePath, string parentGroupPath, string name)
        {
            return $@"Create a group below the PLC data types root of the plc software (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- parentGroupPath: root-relative path of the parent group; empty creates directly below PLC data types
- name: name of the new group, without a slash

Use the 'plc_create_type_group' tool with these parameters:
- softwarePath: {softwarePath}
- parentGroupPath: {parentGroupPath}
- name: {name}";
        }

        [McpServerPrompt(Name = "delete_type_group"), Description("Delete type group")]
        public static string DeleteTypeGroup(string softwarePath, string groupPath)
        {
            return $@"Delete a PLC data type group and everything inside it. The PLC data types system group itself cannot be deleted (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- groupPath: root-relative path of the group to delete

Use the 'plc_delete_type_group' tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}";
        }

        [McpServerPrompt(Name = "delete_type"), Description("Delete type")]
        public static string DeleteType(string softwarePath, string typePath)
        {
            return $@"Delete a PLC data type (UDT). Know-how protected types are rejected (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- typePath: root-relative path of the type, e.g. Common/CarrierRegister/ML_SubstratState

Use the 'plc_delete_type' tool with these parameters:
- softwarePath: {softwarePath}
- typePath: {typePath}";
        }

        [McpServerPrompt(Name = "rename_type"), Description("Rename type")]
        public static string RenameType(string softwarePath, string typePath, string newName)
        {
            return $@"Rename a PLC data type (UDT). Know-how protected types are rejected (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- typePath: root-relative path of the type
- newName: the new type name, without a slash

Use the 'plc_rename_type' tool with these parameters:
- softwarePath: {softwarePath}
- typePath: {typePath}
- newName: {newName}";
        }

        [McpServerPrompt(Name = "copy_type"), Description("Copy type")]
        public static string CopyType(string softwarePath, string typePath, string targetGroupPath, string newName = "", string targetSoftwarePath = "", string overwrite = "false")
        {
            return $@"Copy a PLC data type (UDT). A type name is unique across the whole PLC, so a copy inside the same PLC requires 'newName'; to keep the name, copy into another PLC with 'targetSoftwarePath'. To change only the group use 'plc_move_type'. The type must be consistent (compile first) (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software holding the type
- typePath: root-relative path of the type to copy
- targetGroupPath: root-relative type group that receives the copy; empty means the PLC data types root
- newName: name of the copy; required when copying inside the same PLC
- targetSoftwarePath: plc software that receives the copy; empty means the same PLC
- overwrite: replace a type of the final name that already exists in the target PLC (default false)

Use the 'plc_copy_type' tool with these parameters:
- softwarePath: {softwarePath}
- typePath: {typePath}
- targetGroupPath: {targetGroupPath}
- newName: {newName}
- targetSoftwarePath: {targetSoftwarePath}
- overwrite: {NormalizeBool(overwrite)}";
        }

        [McpServerPrompt(Name = "move_type"), Description("Move type")]
        public static string MoveType(string softwarePath, string typePath, string targetGroupPath)
        {
            return $@"Move a PLC data type (UDT) into another type group of the same plc software; the name is kept. Implemented as export, deleting the original and import; if the import fails the type is restored in its original group. The type must be consistent (compile first) (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- typePath: root-relative path of the type to move
- targetGroupPath: root-relative type group that receives the type; empty means the PLC data types root

Use the 'plc_move_type' tool with these parameters:
- softwarePath: {softwarePath}
- typePath: {typePath}
- targetGroupPath: {targetGroupPath}";
        }

        #endregion

        #region Tag and Table Templates

        [McpServerPrompt(Name = "get_tag_tables"), Description("Get tag tables")]
        public static string GetTagTables(string softwarePath, string regexName = "")
        {
            return $@"List the PLC tag tables of a plc software, optionally filtered by a regular expression on the table name.

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- regexName: optional regular expression to filter the tag table names

Use the 'plc_get_tag_tables' tool with these parameters:
- softwarePath: {softwarePath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "get_tag_table_info"), Description("Get tag table info")]
        public static string GetTagTableInfo(string softwarePath, string tagTablePath)
        {
            return $@"Get the details of a single tag table, including its tag and constant counts.

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- tagTablePath: root-relative path of the tag table, e.g. 'TagGroup1/Table1'

Use the 'plc_get_tag_table_info' tool with these parameters:
- softwarePath: {softwarePath}
- tagTablePath: {tagTablePath}";
        }

        [McpServerPrompt(Name = "get_tags"), Description("Get tags")]
        public static string GetTags(string softwarePath, string tagTablePath = "", string regexName = "")
        {
            return $@"List PLC tags, either of one tag table or of every tag table of the plc software, optionally filtered by a regular expression on the tag name.

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- tagTablePath: optional root-relative tag table path; empty searches every tag table
- regexName: optional regular expression to filter the tag names

Use the 'plc_get_tags' tool with these parameters:
- softwarePath: {softwarePath}
- tagTablePath: {tagTablePath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "get_tag_info"), Description("Get tag info")]
        public static string GetTagInfo(string softwarePath, string tagPath)
        {
            return $@"Get the details of a single PLC tag, including data type, logical address and external access flags.

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- tagPath: path of the tag including its table, e.g. 'TagGroup1/Table1/Tag_1'

Use the 'plc_get_tag_info' tool with these parameters:
- softwarePath: {softwarePath}
- tagPath: {tagPath}";
        }

        [McpServerPrompt(Name = "get_constants"), Description("Get constants")]
        public static string GetConstants(string softwarePath, string tagTablePath = "", string kind = "all", string regexName = "")
        {
            return $@"List PLC user and/or system constants, either of one tag table or of every tag table of the plc software.

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- tagTablePath: optional root-relative tag table path; empty searches every tag table
- kind: which constants to return - 'all' (default), 'user' or 'system'
- regexName: optional regular expression to filter the constant names

Use the 'plc_get_constants' tool with these parameters:
- softwarePath: {softwarePath}
- tagTablePath: {tagTablePath}
- kind: {kind}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "get_watch_tables"), Description("Get watch tables")]
        public static string GetWatchTables(string softwarePath, string regexName = "")
        {
            return $@"List the PLC watch tables of a plc software, optionally filtered by a regular expression on the table name. Entries are omitted; use GetWatchTableInfo for one table's rows.

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- regexName: optional regular expression to filter the watch table names

Use the 'plc_get_watch_tables' tool with these parameters:
- softwarePath: {softwarePath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "get_watch_table_info"), Description("Get watch table info")]
        public static string GetWatchTableInfo(string softwarePath, string watchTablePath)
        {
            return $@"Get a single PLC watch table including all of its entries (address, display format, monitor and modify settings).

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- watchTablePath: root-relative path of the watch table, e.g. 'WatchGroup1/WatchTable_1'

Use the 'plc_get_watch_table_info' tool with these parameters:
- softwarePath: {softwarePath}
- watchTablePath: {watchTablePath}";
        }

        [McpServerPrompt(Name = "get_force_tables"), Description("Get force tables")]
        public static string GetForceTables(string softwarePath)
        {
            return $@"List the PLC force tables of a plc software including their entries. A force table is created and owned by the system: it cannot be created or deleted through Openness.

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software

Use the 'plc_get_force_tables' tool with these parameters:
- softwarePath: {softwarePath}";
        }

        [McpServerPrompt(Name = "create_tag_table"), Description("Create a PLC tag table")]
        public static string CreateTagTable(string softwarePath, string groupPath, string name)
        {
            return $@"Create a PLC tag table in a tag table group (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- groupPath: root-relative tag table group; empty creates directly below the PLC tags root
- name: name of the new tag table, without a slash

Use the 'plc_create_tag_table' tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- name: {name}";
        }

        [McpServerPrompt(Name = "delete_tag_table"), Description("Delete tag table")]
        public static string DeleteTagTable(string softwarePath, string tagTablePath)
        {
            return $@"Delete a PLC tag table with all of its tags and user constants. The default tag table cannot be deleted (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- tagTablePath: root-relative path of the tag table, e.g. TagGroup1/Table1

Use the 'plc_delete_tag_table' tool with these parameters:
- softwarePath: {softwarePath}
- tagTablePath: {tagTablePath}";
        }

        [McpServerPrompt(Name = "rename_tag_table"), Description("Rename tag table")]
        public static string RenameTagTable(string softwarePath, string tagTablePath, string newName)
        {
            return $@"Rename a PLC tag table (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- tagTablePath: root-relative path of the tag table
- newName: the new tag table name, without a slash

Use the 'plc_rename_tag_table' tool with these parameters:
- softwarePath: {softwarePath}
- tagTablePath: {tagTablePath}
- newName: {newName}";
        }

        [McpServerPrompt(Name = "create_tag_table_group"), Description("Create tag table group")]
        public static string CreateTagTableGroup(string softwarePath, string parentGroupPath, string name)
        {
            return $@"Create a group below the PLC tags root of the plc software (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- parentGroupPath: root-relative path of the parent group; empty creates directly below PLC tags
- name: name of the new group, without a slash

Use the 'plc_create_tag_table_group' tool with these parameters:
- softwarePath: {softwarePath}
- parentGroupPath: {parentGroupPath}
- name: {name}";
        }

        [McpServerPrompt(Name = "delete_tag_table_group"), Description("Delete tag table group")]
        public static string DeleteTagTableGroup(string softwarePath, string groupPath)
        {
            return $@"Delete a tag table group and everything inside it. The PLC tags system group itself cannot be deleted (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- groupPath: root-relative path of the group to delete

Use the 'plc_delete_tag_table_group' tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}";
        }

        [McpServerPrompt(Name = "create_tag"), Description("Create tag")]
        public static string CreateTag(string softwarePath, string tagTablePath, string name, string dataTypeName = "", string logicalAddress = "")
        {
            return $@"Create a PLC tag in a tag table (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- tagTablePath: root-relative path of the tag table, e.g. TagGroup1/Table1
- name: name of the new tag, without a slash
- dataTypeName: PLC data type such as Bool, Int or Word; empty creates the tag with its default type
- logicalAddress: absolute address such as %I0.0, %QW4 or %M10.1

Use the 'plc_create_tag' tool with these parameters:
- softwarePath: {softwarePath}
- tagTablePath: {tagTablePath}
- name: {name}
- dataTypeName: {dataTypeName}
- logicalAddress: {logicalAddress}";
        }

        [McpServerPrompt(Name = "update_tag"), Description("Update tag")]
        public static string UpdateTag(string softwarePath, string tagPath, string newName = "", string dataTypeName = "", string logicalAddress = "", string externalAccessible = "", string externalVisible = "", string externalWritable = "")
        {
            return $@"Change one or more properties of an existing PLC tag. Every argument left empty or null keeps the current value (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- tagPath: path of the tag including its table, e.g. TagGroup1/Table1/Tag_1
- newName: optional new tag name, without a slash
- dataTypeName: optional new PLC data type
- logicalAddress: optional new absolute address
- externalAccessible: optional new value for accessibility from HMI/OPC UA
- externalVisible: optional new value for visibility in HMI/OPC UA
- externalWritable: optional new value for writability from HMI/OPC UA

Use the 'plc_update_tag' tool with these parameters:
- softwarePath: {softwarePath}
- tagPath: {tagPath}
- newName: {newName}
- dataTypeName: {dataTypeName}
- logicalAddress: {logicalAddress}
- externalAccessible: {externalAccessible}
- externalVisible: {externalVisible}
- externalWritable: {externalWritable}";
        }

        [McpServerPrompt(Name = "delete_tag"), Description("Delete tag")]
        public static string DeleteTag(string softwarePath, string tagPath)
        {
            return $@"Delete a PLC tag from its tag table (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- tagPath: path of the tag including its table, e.g. TagGroup1/Table1/Tag_1

Use the 'plc_delete_tag' tool with these parameters:
- softwarePath: {softwarePath}
- tagPath: {tagPath}";
        }

        [McpServerPrompt(Name = "create_user_constant"), Description("Create user constant")]
        public static string CreateUserConstant(string softwarePath, string tagTablePath, string name, string dataTypeName = "", string value = "")
        {
            return $@"Create a user constant in a tag table. System constants are read-only and cannot be created (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- tagTablePath: root-relative path of the tag table
- name: name of the new constant, without a slash
- dataTypeName: PLC data type such as Int or Real; empty creates the constant with its default type
- value: the constant value as text, e.g. 42 or 3.14

Use the 'plc_create_user_constant' tool with these parameters:
- softwarePath: {softwarePath}
- tagTablePath: {tagTablePath}
- name: {name}
- dataTypeName: {dataTypeName}
- value: {value}";
        }

        [McpServerPrompt(Name = "update_user_constant"), Description("Update user constant")]
        public static string UpdateUserConstant(string softwarePath, string tagTablePath, string name, string newName = "", string dataTypeName = "", string value = "")
        {
            return $@"Change the name, data type or value of an existing user constant. Every argument left null keeps the current value (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- tagTablePath: root-relative path of the tag table
- name: current name of the constant
- newName: optional new constant name, without a slash
- dataTypeName: optional new PLC data type
- value: optional new value as text

Use the 'plc_update_user_constant' tool with these parameters:
- softwarePath: {softwarePath}
- tagTablePath: {tagTablePath}
- name: {name}
- newName: {newName}
- dataTypeName: {dataTypeName}
- value: {value}";
        }

        [McpServerPrompt(Name = "delete_user_constant"), Description("Delete user constant")]
        public static string DeleteUserConstant(string softwarePath, string tagTablePath, string name)
        {
            return $@"Delete a user constant from a tag table. System constants cannot be deleted (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- tagTablePath: root-relative path of the tag table
- name: name of the constant to delete

Use the 'plc_delete_user_constant' tool with these parameters:
- softwarePath: {softwarePath}
- tagTablePath: {tagTablePath}
- name: {name}";
        }

        [McpServerPrompt(Name = "create_watch_table"), Description("Create watch table")]
        public static string CreateWatchTable(string softwarePath, string groupPath, string name)
        {
            return $@"Create watch table. Force tables cannot be created: the system owns the single force table per PLC (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- groupPath: root-relative watch table group; empty creates directly below the Watch and force tables root
- name: name of the new watch table, without a slash

Use the 'plc_create_watch_table' tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- name: {name}";
        }

        [McpServerPrompt(Name = "rename_watch_table"), Description("Rename a watch table")]
        public static string RenameWatchTable(string softwarePath, string watchTablePath, string newName)
        {
            return $@"Rename watch table (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- watchTablePath: root-relative path of the watch table
- newName: the new watch table name, without a slash

Use the 'plc_rename_watch_table' tool with these parameters:
- softwarePath: {softwarePath}
- watchTablePath: {watchTablePath}
- newName: {newName}";
        }

        [McpServerPrompt(Name = "delete_watch_table"), Description("Delete watch table")]
        public static string DeleteWatchTable(string softwarePath, string watchTablePath)
        {
            return $@"Delete watch table with all of its entries. Force tables cannot be deleted (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- watchTablePath: root-relative path of the watch table

Use the 'plc_delete_watch_table' tool with these parameters:
- softwarePath: {softwarePath}
- watchTablePath: {watchTablePath}";
        }

        [McpServerPrompt(Name = "create_watch_table_group"), Description("Create watch table group")]
        public static string CreateWatchTableGroup(string softwarePath, string parentGroupPath, string name)
        {
            return $@"Create watch table group below the Watch and force tables root of the plc software (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- parentGroupPath: root-relative path of the parent group; empty creates directly below the root
- name: name of the new group, without a slash

Use the 'plc_create_watch_table_group' tool with these parameters:
- softwarePath: {softwarePath}
- parentGroupPath: {parentGroupPath}
- name: {name}";
        }

        [McpServerPrompt(Name = "delete_watch_table_group"), Description("Delete watch table group")]
        public static string DeleteWatchTableGroup(string softwarePath, string groupPath)
        {
            return $@"Delete watch table group and everything inside it. The Watch and force tables system group itself cannot be deleted (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- groupPath: root-relative path of the group to delete

Use the 'plc_delete_watch_table_group' tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}";
        }

        #endregion

        #region Document Templates

        #endregion

        #region Source File Templates

        [McpServerPrompt(Name = "get_block_source"), Description("Get block source")]
        public static string GetBlockSource(string softwarePath, string blockPath, string format = "document", string maxChars = "40000")
        {
            return $@"Return the source text of one program block directly, instead of exporting a file and reading it back. 'document' gives readable SCL/LAD/STL (SIMATIC Source Document, V20+); objects TIA Portal cannot represent that way - STL and mixed-language blocks - fall back to XML automatically, and the response says which format was produced.

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- blockPath: root-relative path of the block, e.g. '0_OBs/Main'. Use 'ResolveObjectPath' if you only know the name
- format: 'document' (default) for readable source text, or 'xml' for the SimaticML export
- maxChars: truncate the text at this many characters, on a line boundary (default 40000)

Use the 'plc_get_block_source' tool with these parameters:
- softwarePath: {softwarePath}
- blockPath: {blockPath}
- format: {format}
- maxChars: {maxChars}";
        }

        [McpServerPrompt(Name = "get_type_source"), Description("Get type source")]
        public static string GetTypeSource(string softwarePath, string typePath, string format = "document", string maxChars = "40000")
        {
            return $@"Return the source text of one PLC data type directly, instead of exporting a file and reading it back. 'document' gives the readable TYPE ... END_TYPE declaration (SIMATIC Source Document, requires TIA Portal V21); 'xml' gives the SimaticML export.

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- typePath: root-relative path of the PLC data type, e.g. 'Common/BtnTyp_X'. Use 'ResolveObjectPath' if you only know the name
- format: 'document' (default) for readable source text, or 'xml' for the SimaticML export
- maxChars: truncate the text at this many characters, on a line boundary (default 40000)

Use the 'plc_get_type_source' tool with these parameters:
- softwarePath: {softwarePath}
- typePath: {typePath}
- format: {format}
- maxChars: {maxChars}";
        }

        [McpServerPrompt(Name = "generate_sources"), Description("Generate sources")]
        public static string GenerateSources(string softwarePath, string exportPath, string regexName = "", string withDependencies = "false")
        {
            return $@"Write every block and PLC data type of one PLC software as external source files into a folder tree that mirrors the project groups: '<exportPath>/Program blocks/...' and '<exportPath>/PLC data types/...', one file per object. The compilable counterpart to 'ExportPlcAsDocuments'. Objects with no source form (LAD, FBD, GRAPH), inconsistent objects and know-how protected ones are reported in 'Skipped' instead of failing the run.

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- exportPath: directory on this machine that receives the tree; existing files of the same name are overwritten
- regexName: optional regular expression, generates only objects whose name matches. Empty means all
- withDependencies: also write every object each one uses into its file. Default false, which keeps one object per file

Use the 'plc_generate_sources' tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}
- withDependencies: {NormalizeBool(withDependencies)}";
        }

        [McpServerPrompt(Name = "get_external_sources"), Description("Get external sources")]
        public static string GetExternalSources(string softwarePath, string regexName = "")
        {
            return $@"List the external source files of a plc software, optionally filtered by a regular expression on the source name.

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- regexName: optional regular expression to filter the external source names

Use the 'plc_get_external_sources' tool with these parameters:
- softwarePath: {softwarePath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "get_external_source_info"), Description("Get external source info")]
        public static string GetExternalSourceInfo(string softwarePath, string sourcePath)
        {
            return $@"Get a single external source file. Beyond its name, all metadata is returned in the generic Attributes list.

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- sourcePath: root-relative path of the external source, e.g. 'SourceGroup1/Source_1'

Use the 'plc_get_external_source_info' tool with these parameters:
- softwarePath: {softwarePath}
- sourcePath: {sourcePath}";
        }

        [McpServerPrompt(Name = "create_external_source_from_file"), Description("Create external source from file")]
        public static string CreateExternalSourceFromFile(string softwarePath, string groupPath, string name, string filePath)
        {
            return $@"Add a source file (for example an SCL file) from the file system into the external source files of the plc software (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- groupPath: root-relative external source group; empty uses the External source files root
- name: name the source gets in the project, without a slash
- filePath: full path of the source file on the machine running this server

Use the 'plc_create_external_source' tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- name: {name}
- filePath: {filePath}";
        }

        [McpServerPrompt(Name = "delete_external_source"), Description("Delete external source")]
        public static string DeleteExternalSource(string softwarePath, string sourcePath)
        {
            return $@"Remove an external source file from the plc software (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- sourcePath: root-relative path of the external source, e.g. SourceGroup1/Source_1

Use the 'plc_delete_external_source' tool with these parameters:
- softwarePath: {softwarePath}
- sourcePath: {sourcePath}";
        }

        [McpServerPrompt(Name = "create_external_source_group"), Description("Create external source group")]
        public static string CreateExternalSourceGroup(string softwarePath, string parentGroupPath, string name)
        {
            return $@"Create a group below the External source files root of the plc software (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- parentGroupPath: root-relative path of the parent group; empty creates directly below the root
- name: name of the new group, without a slash

Use the 'plc_create_external_source_group' tool with these parameters:
- softwarePath: {softwarePath}
- parentGroupPath: {parentGroupPath}
- name: {name}";
        }

        [McpServerPrompt(Name = "delete_external_source_group"), Description("Delete external source group")]
        public static string DeleteExternalSourceGroup(string softwarePath, string groupPath)
        {
            return $@"Delete an external source group and everything inside it. The External source files system group itself cannot be deleted (not available when the server runs with '--read-only').

Common parameter values:
- softwarePath: defines the path in the project structure to the plc software
- groupPath: root-relative path of the group to delete

Use the 'plc_delete_external_source_group' tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}";
        }

        #endregion

        #region hardware creation prompts

        [McpServerPrompt(Name = "create_hardware_device"), Description("Create a new hardware device (PLC/HMI/ET200) in the project")]
        public static string CreateHardwareDevice(string typeIdentifier, string name)
        {
            return $@"You are helping to create hardware in a TIA Portal project.

Use the 'hw_create_device' tool with these parameters:
- typeIdentifier: {typeIdentifier}
- name: {name}

TypeIdentifier format: 'OrderNumber:6ES7 516-3AN01-0AB0/V2.8'
After creation, verify with GetHardwareTopology.";
        }

        [McpServerPrompt(Name = "plug_hardware_module"), Description("Plug a module into an existing hardware device slot")]
        public static string PlugHardwareModule(string deviceName, string parentItemName, string positionNumber, string typeIdentifier, string moduleName)
        {
            return $@"You are helping to add a hardware module to an existing device in TIA Portal.

Use the 'hw_plug_module' tool with these parameters:
- deviceName: {deviceName}
- parentItemName: {parentItemName}
- positionNumber: {positionNumber}
- typeIdentifier: {typeIdentifier}
- moduleName: {moduleName}

Use GetHardwareTopology first to confirm item names and available slots.";
        }

        #endregion

        // MCP prompt arguments are always strings, so boolean flags arrive as text.
        private static string NormalizeBool(string? value)
        {
            var text = value?.Trim();
            // 'text == null' spelled out: string.IsNullOrEmpty carries no nullability
            // annotation on .NET Framework, so the compiler cannot see the guard.
            if (text == null || text.Length == 0)
            {
                return "false";
            }

            return text.Equals("true", System.StringComparison.OrdinalIgnoreCase)
                || text.Equals("1", System.StringComparison.Ordinal)
                || text.Equals("yes", System.StringComparison.OrdinalIgnoreCase)
                    ? "true"
                    : "false";
        }
    }
}

