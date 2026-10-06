# PLC software

Blocks, data types, tags, tables, external sources, cross references and compile.

| Tool | Kind |
|---|---|
| [`plc_compile_block`](#plc_compile_block) | write |
| [`plc_compile_software`](#plc_compile_software) | session |
| [`plc_copy_block`](#plc_copy_block) | write |
| [`plc_copy_type`](#plc_copy_type) | write |
| [`plc_create_block_group`](#plc_create_block_group) | write |
| [`plc_create_external_source`](#plc_create_external_source) | write |
| [`plc_create_external_source_group`](#plc_create_external_source_group) | write |
| [`plc_create_fb`](#plc_create_fb) | write |
| [`plc_create_instance_db`](#plc_create_instance_db) | write |
| [`plc_create_scl_block`](#plc_create_scl_block) | write |
| [`plc_create_tag`](#plc_create_tag) | write |
| [`plc_create_tag_table`](#plc_create_tag_table) | write |
| [`plc_create_tag_table_group`](#plc_create_tag_table_group) | write |
| [`plc_create_type_group`](#plc_create_type_group) | write |
| [`plc_create_user_constant`](#plc_create_user_constant) | write |
| [`plc_create_watch_table`](#plc_create_watch_table) | write |
| [`plc_create_watch_table_group`](#plc_create_watch_table_group) | write |
| [`plc_delete_block`](#plc_delete_block) | write |
| [`plc_delete_block_group`](#plc_delete_block_group) | write |
| [`plc_delete_external_source`](#plc_delete_external_source) | write |
| [`plc_delete_external_source_group`](#plc_delete_external_source_group) | write |
| [`plc_delete_tag`](#plc_delete_tag) | write |
| [`plc_delete_tag_table`](#plc_delete_tag_table) | write |
| [`plc_delete_tag_table_group`](#plc_delete_tag_table_group) | write |
| [`plc_delete_type`](#plc_delete_type) | write |
| [`plc_delete_type_group`](#plc_delete_type_group) | write |
| [`plc_delete_user_constant`](#plc_delete_user_constant) | write |
| [`plc_delete_watch_table`](#plc_delete_watch_table) | write |
| [`plc_delete_watch_table_group`](#plc_delete_watch_table_group) | write |
| [`plc_find_in_code`](#plc_find_in_code) | read |
| [`plc_generate_sources`](#plc_generate_sources) | write |
| [`plc_get_block_data`](#plc_get_block_data) | session |
| [`plc_get_block_info`](#plc_get_block_info) | read |
| [`plc_get_block_interface`](#plc_get_block_interface) | read |
| [`plc_get_block_source`](#plc_get_block_source) | read |
| [`plc_get_blocks`](#plc_get_blocks) | read |
| [`plc_get_blocks_hierarchy`](#plc_get_blocks_hierarchy) | read |
| [`plc_get_constants`](#plc_get_constants) | read |
| [`plc_get_cross_references`](#plc_get_cross_references) | read |
| [`plc_get_external_source_info`](#plc_get_external_source_info) | read |
| [`plc_get_external_sources`](#plc_get_external_sources) | read |
| [`plc_get_force_tables`](#plc_get_force_tables) | read |
| [`plc_get_software_info`](#plc_get_software_info) | read |
| [`plc_get_software_tree`](#plc_get_software_tree) | read |
| [`plc_get_summary`](#plc_get_summary) | read |
| [`plc_get_tag_info`](#plc_get_tag_info) | read |
| [`plc_get_tag_table_info`](#plc_get_tag_table_info) | read |
| [`plc_get_tag_tables`](#plc_get_tag_tables) | read |
| [`plc_get_tags`](#plc_get_tags) | read |
| [`plc_get_type_info`](#plc_get_type_info) | read |
| [`plc_get_type_source`](#plc_get_type_source) | read |
| [`plc_get_types`](#plc_get_types) | read |
| [`plc_get_watch_table_info`](#plc_get_watch_table_info) | read |
| [`plc_get_watch_tables`](#plc_get_watch_tables) | read |
| [`plc_manage_tag_table_entries`](#plc_manage_tag_table_entries) | write |
| [`plc_move_block`](#plc_move_block) | write |
| [`plc_move_type`](#plc_move_type) | write |
| [`plc_rename_block`](#plc_rename_block) | write |
| [`plc_rename_tag_table`](#plc_rename_tag_table) | write |
| [`plc_rename_type`](#plc_rename_type) | write |
| [`plc_rename_watch_table`](#plc_rename_watch_table) | write |
| [`plc_replace_source`](#plc_replace_source) | write |
| [`plc_resolve_object_path`](#plc_resolve_object_path) | read |
| [`plc_update_tag`](#plc_update_tag) | write |
| [`plc_update_user_constant`](#plc_update_user_constant) | write |
| [`plc_where_used`](#plc_where_used) | read |

## plc_compile_block

Compile a program block. Returns the compiler result with any errors or warnings

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `blockPath` | string | yes | blockPath: root-relative path of the block to compile, e.g. 1_Tests/FC_Block_1 |

## plc_compile_software

Compile the plc software and report every compiler message with the object it belongs to, so errors can be fixed without re-reading the whole PLC. Warnings are reported as a successful compile with detail; only errors fail the call

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `password` | string | no (default ``) | password: the password to access adminsitration, default: no password |

## plc_copy_block

Copy a program block. Block names and numbers are unique within a PLC, so a copy inside the same PLC requires 'newName' and gets a free block number; to keep the name, copy into another PLC with 'targetSoftwarePath'. To change only the group use 'plc_move_block'. Implemented as export plus import, so the block must be consistent (compile first)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software holding the block |
| `blockPath` | string | yes | blockPath: root-relative path of the block to copy, e.g. 1_Tests/FC_Block_1 |
| `targetGroupPath` | string | yes | targetGroupPath: root-relative block group that receives the copy; empty means the Program blocks root |
| `newName` | string | no (default ``) | newName: name of the copy. Required when copying inside the same PLC; empty keeps the name when copying into another PLC |
| `targetSoftwarePath` | string | no (default ``) | targetSoftwarePath: plc software that receives the copy; empty (default) means the same PLC |
| `overwrite` | boolean | no (default `False`) | overwrite: replace a block of the final name that already exists in the target PLC (default false) |

## plc_copy_type

Copy a PLC data type (UDT). A type name is unique across the whole PLC, so a copy inside the same PLC requires 'newName'; to keep the name, copy into another PLC with 'targetSoftwarePath'. To change only the group use 'plc_move_type'. Implemented as export plus import, so the type must be consistent (compile first)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software holding the type |
| `typePath` | string | yes | typePath: root-relative path of the type to copy, e.g. Common/CarrierRegister/ML_SubstratState |
| `targetGroupPath` | string | yes | targetGroupPath: root-relative type group that receives the copy; empty means the PLC data types root |
| `newName` | string | no (default ``) | newName: name of the copy. Required when copying inside the same PLC; empty keeps the name when copying into another PLC |
| `targetSoftwarePath` | string | no (default ``) | targetSoftwarePath: plc software that receives the copy; empty (default) means the same PLC |
| `overwrite` | boolean | no (default `False`) | overwrite: replace a type of the final name that already exists in the target PLC (default false) |

## plc_create_block_group

Create a group below the Program blocks root of the plc software

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `parentGroupPath` | string | yes | parentGroupPath: root-relative path of the parent group; empty creates directly below Program blocks |
| `name` | string | yes | name: name of the new group, without a slash |

## plc_create_external_source

Add a source file (for example an SCL file) from the file system into the external source files of the plc software

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `groupPath` | string | yes | groupPath: root-relative external source group; empty uses the External source files root |
| `name` | string | yes | name: name the source gets in the project, without a slash |
| `filePath` | string | yes | filePath: full path of the source file on the machine running this server |

## plc_create_external_source_group

Create a group below the External source files root of the plc software

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `parentGroupPath` | string | yes | parentGroupPath: root-relative path of the parent group; empty creates directly below the root |
| `name` | string | yes | name: name of the new group, without a slash |

## plc_create_fb

Create an empty function block in LAD, FBD, STL, SCL or ProDiag (LAD, FBD and STL are built from a template made for TIA Portal V21; on an older version they are untested). For a block with code use 'plc_create_scl_block' (SCL text) or 'import_objects' (exported XML). The response carries the block number TIA Portal assigned

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `groupPath` | string | yes | groupPath: root-relative block group that receives the FB; empty uses the Program blocks root |
| `name` | string | yes | name: name of the new function block, without a slash; must not exist in the PLC yet |
| `language` | string | no (default `LAD`) | language: LAD (default), FBD, STL, SCL or ProDiag |
| `autoNumber` | boolean | no (default `True`) | autoNumber: let the server pick the first free FB number (default true) |
| `number` | integer | no (default `0`) | number: explicit block number from 1, used only when autoNumber is false; must be free |

## plc_create_instance_db

Create an instance data block for an existing function block. The response carries the block number TIA Portal assigned

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `groupPath` | string | yes | groupPath: root-relative block group that receives the DB; empty uses the Program blocks root |
| `name` | string | yes | name: name of the new instance data block, without a slash; must not exist in the PLC yet |
| `instanceOfName` | string | yes | instanceOfName: name of the function block this instance DB belongs to |
| `autoNumber` | boolean | no (default `True`) | autoNumber: let the server pick the first free DB number (default true) |
| `number` | integer | no (default `0`) | number: explicit block number from 1, used only when autoNumber is false; must be free |

## plc_create_scl_block

Create a block directly from SCL source code text.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `sclCode` | string | yes | sclCode: the raw SCL source code for the block |
| `targetGroupPath` | string | no (default ``) | targetGroupPath: optional root-relative block user group that receives the blocks; empty uses the source default location |
| `keepOnError` | boolean | no (default `False`) | keepOnError: keep successfully generated blocks even when others fail (default false) |

## plc_create_tag

Create a PLC tag in a tag table

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `tagTablePath` | string | yes | tagTablePath: root-relative path of the tag table, e.g. TagGroup1/Table1 |
| `name` | string | yes | name: name of the new tag, without a slash |
| `dataTypeName` | string | no (default ``) | dataTypeName: PLC data type such as Bool, Int or Word; empty creates the tag with its default type |
| `logicalAddress` | string | no (default ``) | logicalAddress: absolute address such as %I0.0, %QW4 or %M10.1 |

## plc_create_tag_table

Create a PLC tag table in a tag table group

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `groupPath` | string | yes | groupPath: root-relative tag table group; empty creates directly below the PLC tags root |
| `name` | string | yes | name: name of the new tag table, without a slash |

## plc_create_tag_table_group

Create a group below the PLC tags root of the plc software

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `parentGroupPath` | string | yes | parentGroupPath: root-relative path of the parent group; empty creates directly below PLC tags |
| `name` | string | yes | name: name of the new group, without a slash |

## plc_create_type_group

Create a group below the PLC data types root of the plc software

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `parentGroupPath` | string | yes | parentGroupPath: root-relative path of the parent group; empty creates directly below PLC data types |
| `name` | string | yes | name: name of the new group, without a slash |

## plc_create_user_constant

Create a user constant in a tag table. System constants are read-only and cannot be created

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `tagTablePath` | string | yes | tagTablePath: root-relative path of the tag table |
| `name` | string | yes | name: name of the new constant, without a slash |
| `dataTypeName` | string | no (default ``) | dataTypeName: PLC data type such as Int or Real; empty creates the constant with its default type |
| `value` | string | no (default ``) | value: the constant value as text, e.g. 42 or 3.14 |

## plc_create_watch_table

Create a PLC watch table. Force tables cannot be created: the system owns the single force table per PLC

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `groupPath` | string | yes | groupPath: root-relative watch table group; empty creates directly below the Watch and force tables root |
| `name` | string | yes | name: name of the new watch table, without a slash |

## plc_create_watch_table_group

Create a group below the Watch and force tables root of the plc software

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `parentGroupPath` | string | yes | parentGroupPath: root-relative path of the parent group; empty creates directly below the root |
| `name` | string | yes | name: name of the new group, without a slash |

## plc_delete_block

Delete a program block. Know-how protected blocks are rejected: remove the protection in TIA Portal first

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `blockPath` | string | yes | blockPath: root-relative path of the block, e.g. 1_Tests/FC_Block_1 |

## plc_delete_block_group

Delete a block group and everything inside it. The Program blocks system group itself cannot be deleted

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `groupPath` | string | yes | groupPath: root-relative path of the group to delete, e.g. Common/CarrierRegister |

## plc_delete_external_source

Remove an external source file from the plc software

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `sourcePath` | string | yes | sourcePath: root-relative path of the external source, e.g. SourceGroup1/Source_1 |

## plc_delete_external_source_group

Delete an external source group and everything inside it. The External source files system group itself cannot be deleted

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `groupPath` | string | yes | groupPath: root-relative path of the group to delete |

## plc_delete_tag

Delete a PLC tag from its tag table

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `tagPath` | string | yes | tagPath: path of the tag including its table, e.g. TagGroup1/Table1/Tag_1 |

## plc_delete_tag_table

Delete a PLC tag table with all of its tags and user constants. The default tag table cannot be deleted

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `tagTablePath` | string | yes | tagTablePath: root-relative path of the tag table, e.g. TagGroup1/Table1 |

## plc_delete_tag_table_group

Delete a tag table group and everything inside it. The PLC tags system group itself cannot be deleted

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `groupPath` | string | yes | groupPath: root-relative path of the group to delete |

## plc_delete_type

Delete a PLC data type (UDT). Know-how protected types are rejected

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `typePath` | string | yes | typePath: root-relative path of the type, e.g. Common/CarrierRegister/ML_SubstratState |

## plc_delete_type_group

Delete a PLC data type group and everything inside it. The PLC data types system group itself cannot be deleted

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `groupPath` | string | yes | groupPath: root-relative path of the group to delete |

## plc_delete_user_constant

Delete a user constant from a tag table. System constants cannot be deleted

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `tagTablePath` | string | yes | tagTablePath: root-relative path of the tag table |
| `name` | string | yes | name: name of the constant to delete |

## plc_delete_watch_table

Delete a PLC watch table with all of its entries. Force tables cannot be deleted

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `watchTablePath` | string | yes | watchTablePath: root-relative path of the watch table |

## plc_delete_watch_table_group

Delete a watch table group and everything inside it. The Watch and force tables system group itself cannot be deleted

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `groupPath` | string | yes | groupPath: root-relative path of the group to delete |

## plc_find_in_code

Search the actual source text of program blocks and PLC data types with a regular expression - every other filter in this server matches object names only. Returns the object path, line number and the matching line. Each call exports the candidate objects behind the scenes, so narrow a large PLC with 'nameFilter'

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `pattern` | string | yes | pattern: regular expression matched against each line, case-insensitive. Plain text works too |
| `nameFilter` | string | no (default ``) | nameFilter: optional regular expression on object names, to limit which objects are searched. Empty (default) searches all of them |
| `maxResults` | integer | no (default `200`) | maxResults: stop after this many matching lines (default 200) |

## plc_generate_sources

Write every block and PLC data type of one PLC software as external source files into a folder tree that mirrors the project groups: '<exportPath>/Program blocks/...' and '<exportPath>/PLC data types/...', one file per object. The compilable counterpart to 'ExportPlcAsDocuments'. Objects with no source form (LAD, FBD, GRAPH), inconsistent objects and know-how protected ones are reported in 'Skipped' instead of failing the run

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `exportPath` | string | yes | exportPath: directory on this machine that receives the tree; existing files of the same name are overwritten |
| `regexName` | string | no (default ``) | regexName: optional regular expression, generates only objects whose name matches. Empty means all |
| `withDependencies` | boolean | no (default `False`) | withDependencies: also write every object each one uses into its file. Default false, which keeps one object per file |

## plc_get_block_data

Get comprehensive block information including metadata, interface, and source code depending on requested parts.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes |  |
| `blockName` | string | yes |  |
| `parts` | array | yes |  |

## plc_get_block_info

Get a block info, which is located in the plc software

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `blockPath` | string | yes | blockPath: defines the path in the project structure to the block |

## plc_get_block_interface

List the members of a data block with their data type and every attribute TIA Portal reports. Needs no export and works on inconsistent blocks. Data blocks only: Openness offers no interface accessor for FB, FC or OB, whose declarations come from 'plc_get_block_source' instead

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `blockPath` | string | yes | blockPath: root-relative path of the data block, e.g. '1_Tests/DB_Block_1' |

## plc_get_block_source

Return the source text of one program block directly, instead of exporting a file and reading it back. 'document' gives readable SCL/LAD/STL (SIMATIC Source Document, V20+); objects TIA Portal cannot represent that way - STL and mixed-language blocks - fall back to XML automatically, and the response says which format was produced

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `blockPath` | string | yes | blockPath: root-relative path of the block, e.g. '0_OBs/Main'. Use 'ResolveObjectPath' if you only know the name |
| `format` | string | no (default `document`) | format: 'document' (default) for readable source text, 'source' for the external source text (SCL blocks and data blocks) that 'plc_replace_source' takes back, or 'xml' for the SimaticML export |
| `maxChars` | integer | no (default `40000`) | maxChars: truncate the text at this many characters, on a line boundary (default 40000) |

## plc_get_blocks

Get a list of blocks, which are located in plc software. A large PLC has thousands: the first 500 are returned, narrow with regexName or page with offset

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `regexName` | string | no (default ``) | regexName: defines the name or regular expression to find the block. Use empty string (default) to find all |
| `limit` | integer | no (default `500`) | limit: the most items to return (default 500); 0 returns all |
| `offset` | integer | no (default `0`) | offset: items to skip, to read the next page of a long list (default 0) |

## plc_get_blocks_hierarchy

Get a list of all blocks with their group hierarchy from the plc software.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |

## plc_get_constants

List PLC user and/or system constants, either of one tag table or of every tag table of the plc software

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `tagTablePath` | string | no (default ``) | tagTablePath: optional root-relative tag table path; empty searches every tag table |
| `kind` | string | no (default `all`) | kind: which constants to return - 'all' (default), 'user' or 'system' |
| `regexName` | string | no (default ``) | regexName: optional regular expression to filter the constant names |

## plc_get_cross_references

Get cross references for a PLC software or for one block, type, tag table, tag or block group inside it. Watch tables, force tables and external sources have no cross references

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `objectPath` | string | no (default ``) | objectPath: optional root-relative path of a block, type, tag table, tag or block group; empty targets the whole plc software |
| `objectKind` | string | no (default `auto`) | objectKind: 'auto' (default), 'block', 'type', 'tagTable', 'tag' or 'blockGroup' |
| `filter` | string | no (default `AllObjects`) | filter: 'AllObjects' (default), 'ObjectsWithReferences', 'ObjectsWithoutReferences' or 'UnusedObjects' |
| `maxDepth` | integer | no (default `1`) | maxDepth: 1 = sources and their references (default), 2 = also source children, 3 = also reference locations. Keeps large results manageable |

## plc_get_external_source_info

Get a single external source file. Beyond its name, all metadata is returned in the generic Attributes list

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `sourcePath` | string | yes | sourcePath: root-relative path of the external source, e.g. 'SourceGroup1/Source_1' |

## plc_get_external_sources

List the external source files of a plc software, optionally filtered by a regular expression on the source name

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `regexName` | string | no (default ``) | regexName: optional regular expression to filter the external source names |

## plc_get_force_tables

List the PLC force tables of a plc software including their entries. A force table is created and owned by the system: it cannot be created or deleted through Openness

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |

## plc_get_software_info

Get plc software info

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |

## plc_get_software_tree

Get the structure/tree of a given PLC software showing program blocks, PLC data types, PLC tags, watch and force tables, and external source files

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `sections` | string | no (default `all`) | sections: optional comma separated subset of 'blocks,types,tags,watch,sources' to keep the output small; defaults to 'all' |

## plc_get_summary

Counts, programming languages and health of one PLC software in a single call - replaces listing blocks, types, tags, tag tables and watch tables separately just to see what is there. Also names the inconsistent objects (which refuse to export until compiled) and the know-how protected ones (whose content cannot be read)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |

## plc_get_tag_info

Get the details of a single PLC tag, including data type, logical address and external access flags

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `tagPath` | string | yes | tagPath: path of the tag including its table, e.g. 'TagGroup1/Table1/Tag_1' |

## plc_get_tag_table_info

Get the details of a single PLC tag table, including its tag and constant counts

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `tagTablePath` | string | yes | tagTablePath: root-relative path of the tag table, e.g. 'TagGroup1/Table1' |

## plc_get_tag_tables

List the PLC tag tables of a plc software, optionally filtered by a regular expression on the table name

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `regexName` | string | no (default ``) | regexName: optional regular expression to filter the tag table names |

## plc_get_tags

List PLC tags, either of one tag table or of every tag table of the plc software, optionally filtered by a regular expression on the tag name

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `tagTablePath` | string | no (default ``) | tagTablePath: optional root-relative tag table path; empty searches every tag table |
| `regexName` | string | no (default ``) | regexName: optional regular expression to filter the tag names |
| `limit` | integer | no (default `200`) | limit: the most items to return (default 200); 0 returns all |
| `offset` | integer | no (default `0`) | offset: items to skip, to read the next page of a long list (default 0) |

## plc_get_type_info

Get a type info from the plc software

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `typePath` | string | yes | typePath: defines the path in the project structure to the type |

## plc_get_type_source

Return the source text of one PLC data type directly, instead of exporting a file and reading it back. 'document' gives the readable TYPE ... END_TYPE declaration (SIMATIC Source Document, requires TIA Portal V21); 'xml' gives the SimaticML export

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `typePath` | string | yes | typePath: root-relative path of the PLC data type, e.g. 'Common/BtnTyp_X'. Use 'ResolveObjectPath' if you only know the name |
| `format` | string | no (default `document`) | format: 'document' (default) for the readable declaration, 'source' for the external source text 'plc_replace_source' takes back, or 'xml' for the SimaticML export |
| `maxChars` | integer | no (default `40000`) | maxChars: truncate the text at this many characters, on a line boundary (default 40000) |

## plc_get_types

Get a list of types from the plc software

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `regexName` | string | no (default ``) | regexName: defines the name or regular expression to find the type. Use empty string (default) to find all |
| `limit` | integer | no (default `500`) | limit: the most items to return (default 500); 0 returns all |
| `offset` | integer | no (default `0`) | offset: items to skip, to read the next page of a long list (default 0) |

## plc_get_watch_table_info

Get a single PLC watch table including all of its entries (address, display format, monitor and modify settings)

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `watchTablePath` | string | yes | watchTablePath: root-relative path of the watch table, e.g. 'WatchGroup1/WatchTable_1' |

## plc_get_watch_tables

List the PLC watch tables of a plc software, optionally filtered by a regular expression on the table name. Entries are omitted; use GetWatchTableInfo for one table's rows

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `regexName` | string | no (default ``) | regexName: optional regular expression to filter the watch table names |

## plc_manage_tag_table_entries

Batch CRUD for tags/constants. Provide an array of actions (create/update/delete) with name, dataType, logicalAddress, comment.

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes |  |
| `tagTablePath` | string | yes |  |
| `actions` | array | yes |  |

## plc_move_block

Move a program block into another block group of the same plc software; name and number are kept. Implemented as export, deleting the original and import, so the block must be consistent (compile first). If the import fails the block is restored in its original group

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `blockPath` | string | yes | blockPath: root-relative path of the block to move, e.g. 1_Tests/FC_Block_1 |
| `targetGroupPath` | string | yes | targetGroupPath: root-relative block group that receives the block; empty means the Program blocks root |

## plc_move_type

Move a PLC data type (UDT) into another type group of the same plc software; the name is kept. Implemented as export, deleting the original and import, so the type must be consistent (compile first). If the import fails the type is restored in its original group

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `typePath` | string | yes | typePath: root-relative path of the type to move |
| `targetGroupPath` | string | yes | targetGroupPath: root-relative type group that receives the type; empty means the PLC data types root |

## plc_rename_block

Rename a program block. Know-how protected blocks are rejected

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `blockPath` | string | yes | blockPath: root-relative path of the block, e.g. 1_Tests/FC_Block_1 |
| `newName` | string | yes | newName: the new block name, without a slash |

## plc_rename_tag_table

Rename a PLC tag table

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `tagTablePath` | string | yes | tagTablePath: root-relative path of the tag table |
| `newName` | string | yes | newName: the new tag table name, without a slash |

## plc_rename_type

Rename a PLC data type (UDT). Know-how protected types are rejected

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `typePath` | string | yes | typePath: root-relative path of the type |
| `newName` | string | yes | newName: the new type name, without a slash |

## plc_rename_watch_table

Rename a PLC watch table

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `watchTablePath` | string | yes | watchTablePath: root-relative path of the watch table |
| `newName` | string | yes | newName: the new watch table name, without a slash |

## plc_replace_source

Replace the code of an EXISTING SCL block (FB, FC, OB), data block or PLC data type by a new source text, then compile it. The cycle is: read the present code with 'plc_get_block_source' / 'plc_get_type_source' and format 'source', change it, pass the WHOLE text here. The object keeps its place in the project, its block number and its instance DBs. The source must declare exactly this object (same kind and name). With compile='object' (default) the object is compiled right away; if the new code does not compile, the previous code is put back and the call fails with the compile errors (onCompileError='restore', default) or the new code stays and the errors are returned (onCompileError='keep'). Changing the interface of a block or the members of a type leaves its callers, instance DBs and users inconsistent: they are listed in nowInconsistent and need 'plc_compile_software', or pass compile='software'. LAD, FBD, STL and GRAPH blocks have no source text and are refused. To create a new block use 'plc_create_scl_block'

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `objectPath` | string | yes | objectPath: root-relative path of the block or PLC data type, e.g. 'Pumps/FB_Pump' or 'Types/UDT_Motor' |
| `source` | string | yes | source: the whole external source text of the object, e.g. FUNCTION_BLOCK "FB_Pump" ... END_FUNCTION_BLOCK, DATA_BLOCK ... END_DATA_BLOCK or TYPE ... END_TYPE |
| `compile` | string | no (default `object`) | compile: 'object' (default) compiles the object itself, 'software' then compiles the whole PLC as well, 'none' compiles nothing |
| `onCompileError` | string | no (default `restore`) | onCompileError: 'restore' (default) puts the previous code back when the new code does not compile, 'keep' leaves the new code in place |

## plc_resolve_object_path

Turn a bare or partial object name into the root-relative path the other tools need, searching program blocks, PLC data types, tags, tag tables, watch tables and external sources. Exact matches win; substring matches are only reported when nothing matches exactly

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `name` | string | yes | name: the object name to look for, e.g. 'FC_Block_1'. A full path may be passed; only its last segment is matched |
| `kind` | string | no (default `any`) | kind: restrict the search to 'block', 'type', 'tag', 'tagTable', 'watchTable' or 'source'. Default 'any' searches all of them |

## plc_update_tag

Change one or more properties of an existing PLC tag. Every argument left empty or null keeps the current value

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `tagPath` | string | yes | tagPath: path of the tag including its table, e.g. TagGroup1/Table1/Tag_1 |
| `newName` | string | no (default ``) | newName: optional new tag name, without a slash |
| `dataTypeName` | string | no (default ``) | dataTypeName: optional new PLC data type |
| `logicalAddress` | string | no (default ``) | logicalAddress: optional new absolute address |
| `externalAccessible` | boolean | no (default ``) | externalAccessible: optional new value for accessibility from HMI/OPC UA |
| `externalVisible` | boolean | no (default ``) | externalVisible: optional new value for visibility in HMI/OPC UA |
| `externalWritable` | boolean | no (default ``) | externalWritable: optional new value for writability from HMI/OPC UA |

## plc_update_user_constant

Change the name, data type or value of an existing user constant. Every argument left null keeps the current value

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `tagTablePath` | string | yes | tagTablePath: root-relative path of the tag table |
| `name` | string | yes | name: current name of the constant |
| `newName` | string | no (default ``) | newName: optional new constant name, without a slash |
| `dataTypeName` | string | no (default ``) | dataTypeName: optional new PLC data type |
| `value` | string | no (default ``) | value: optional new value as text |

## plc_where_used

Answer 'what uses this?' for a tag, block, PLC data type or tag table by name. Resolves the name, picks the right object kind and flattens the cross-reference tree to a plain list of users. Use 'plc_get_cross_references' instead when the full nested result or a specific filter is needed

| Parameter | Type | Required | Description |
|---|---|---|---|
| `softwarePath` | string | yes | softwarePath: defines the path in the project structure to the plc software |
| `name` | string | yes | name: the object to look up, by bare name or by full root-relative path |
| `kind` | string | no (default `any`) | kind: restrict resolution to 'block', 'type', 'tag' or 'tagTable'. Default 'any' picks the single match, and reports the candidates when the name is ambiguous |

