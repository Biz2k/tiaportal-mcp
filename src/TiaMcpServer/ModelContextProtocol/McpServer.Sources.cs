using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.SW.ExternalSources;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.IO;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        // From the former McpServer.Source.cs:
        // Reading what an object contains, without going through the file system.
        //
        // Callers: the MCP host, through tool registration. Affected API: none existing - all three
        // tools are new. File I/O: none that the caller sees; the portal layer exports into a
        // scratch directory under the temp path and removes it again.
        //
        // Read-only: none of these modify the project, so none are gated behind '--allow-write'.
        // Unlike the Export* tools they are not marked destructive either - they leave nothing on
        // disk to overwrite.

        #region source

        [McpServerTool(Name = "plc_get_block_source", Title = "Get block source", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Return the source text of one program block directly, instead of exporting a file and reading it back. 'document' gives readable SCL/LAD/STL (SIMATIC Source Document, V20+); objects TIA Portal cannot represent that way - STL and mixed-language blocks - fall back to XML automatically, and the response says which format was produced")]
        public static ResponseSourceText GetBlockSource(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: root-relative path of the block, e.g. '0_OBs/Main'. Use 'ResolveObjectPath' if you only know the name")] string blockPath,
            [Description("format: 'document' (default) for readable source text, 'source' for the external source text (SCL blocks and data blocks) that 'plc_replace_source' takes back, or 'xml' for the SimaticML export")] string format = "document",
            [Description("maxChars: truncate the text at this many characters, on a line boundary (default 40000)")] int maxChars = 40000)
        {
            return Sourced(
                () => Portal.GetBlockSource(softwarePath, blockPath, format, maxChars),
                blockPath,
                "block");
        }

        /// <summary>Shared response shaping for the two source readers.</summary>
        private static ResponseSourceText Sourced(Func<SourceTextResult> read, string objectPath, string kind)
        {
            try
            {
                var result = read();

                var note = result.Truncated
                    ? $" (truncated to {result.Text.Length} of {result.TotalChars} characters)"
                    : string.Empty;

                return new ResponseSourceText
                {
                    Message = $"Source of {kind} '{objectPath}' as {result.Format}{note}",
                    Name = result.Name,
                    Path = result.Path,
                    Format = result.Format,
                    Text = result.Text,
                    TotalChars = result.TotalChars,
                    Truncated = result.Truncated,
                    FileNames = result.FileNames,
                    Meta = Ok(new JsonObject
                    {
                        ["format"] = result.Format,
                        ["totalChars"] = result.TotalChars,
                        ["truncated"] = result.Truncated
                    })
                };
            }
            catch (PortalException pex)
            {
                throw ToolError(pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"reading the source of '{objectPath}'", ex);
            }
        }

        [McpServerTool(Name = "plc_get_type_source", Title = "Get type source", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Return the source text of one PLC data type directly, instead of exporting a file and reading it back. 'document' gives the readable TYPE ... END_TYPE declaration (SIMATIC Source Document, requires TIA Portal V21); 'xml' gives the SimaticML export")]
        public static ResponseSourceText GetTypeSource(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("typePath: root-relative path of the PLC data type, e.g. 'Common/BtnTyp_X'. Use 'ResolveObjectPath' if you only know the name")] string typePath,
            [Description("format: 'document' (default) for the readable declaration, 'source' for the external source text 'plc_replace_source' takes back, or 'xml' for the SimaticML export")] string format = "document",
            [Description("maxChars: truncate the text at this many characters, on a line boundary (default 40000)")] int maxChars = 40000)
        {
            return Sourced(
                () => Portal.GetTypeSource(softwarePath, typePath, format, maxChars),
                typePath,
                "PLC data type");
        }

        // [McpServerTool(Name = "export_plc_as_documents", Title = "Export PLC as documents", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),`n         // Description("Write a whole PLC software to one folder tree that mirrors the project, ready to commit: program blocks and PLC data types as readable SIMATIC Source Documents where TIA Portal supports them, tag tables and watch tables as XML, each below its localised system folder. Replaces running the four bulk exports separately. Objects that cannot be exported are reported instead of failing the snapshot")]
        public static ResponseSourceTree ExportPlcAsDocuments(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the tree; existing files of the same name are overwritten")] string exportPath)
        {
            try
            {
                var result = Portal.ExportPlcAsDocuments(softwarePath, exportPath);

                return new ResponseSourceTree
                {
                    Message = $"Snapshot of '{softwarePath}' written to '{exportPath}': {result.TotalWritten} object(s), " +
                              $"{result.Skipped.Count} skipped, {result.Failures.Count} failed",
                    Directory = result.Directory,
                    Written = result.Written,
                    Formats = result.Formats,
                    Skipped = result.Skipped,
                    Failures = result.Failures,
                    Meta = Ok(new JsonObject
                    {
                        ["totalWritten"] = result.TotalWritten,
                        ["skipped"] = result.Skipped.Count,
                        ["failed"] = result.Failures.Count
                    })
                };
            }
            catch (PortalException pex)
            {
                throw ToolError(pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"snapshotting '{softwarePath}'", ex);
            }
        }

        #endregion

        // From the former McpServer.GenerateSource.cs:
        // Writing TIA Portal external source files (*.scl, *.db, *.awl, *.udt).
        //
        // Callers: the MCP host, through tool registration. Affected API: none existing - all three
        // tools are new.
        //
        // File I/O: these write to a caller-supplied directory and overwrite a file of the same
        // name, so they are marked destructive, exactly like the Export* tools. They do not modify
        // the project, so they are not gated behind '--allow-write'.
        //
        // Why this sits next to the existing exporters rather than replacing them: 'ExportXmlBlock'
        // writes SimaticML, 'ExportAsDocuments' writes SIMATIC Source Documents, and neither can be
        // compiled back. These files can - 'CreateExternalSourceFromFile' plus
        // 'ImportSourceBlocks' is the return path.

        #region generate source

        // [McpServerTool(Name = "export_source_block", Title = "Export block as source", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),`n         // Description("Write one program block as a TIA Portal external source file, in the format the compiler reads back. The extension follows the block: '.db' for data blocks, '.scl' for SCL blocks, '.awl' for STL blocks. Only those three can be generated - LAD, FBD and GRAPH blocks have no source form and are rejected with a reason, for which 'ExportXmlBlock' (SimaticML) or 'ExportAsDocuments' is the alternative")]
        public static ResponseGeneratedSource ExportSourceBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: root-relative path of the block, e.g. '0_OBs/Main'. Use 'ResolveObjectPath' if you only know the name")] string blockPath,
            [Description("exportPath: directory on this machine that receives the file; an existing file of the same name is overwritten")] string exportPath,
            [Description("withDependencies: also write every object the block uses (called blocks, instance DBs, UDTs) into the same file, so it compiles on its own. Default false")] bool withDependencies = false,
            [Description("preservePath: mirror the project groups below '<exportPath>/Program blocks', as the Export* tools do. Default false, which writes straight into exportPath")] bool preservePath = false)
        {
            return Generated(
                () => Portal.ExportSourceBlock(softwarePath, blockPath, exportPath, withDependencies, preservePath),
                blockPath,
                "block");
        }

        // [McpServerTool(Name = "export_source_type", Title = "Export type as source", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),`n         // Description("Write one PLC data type as a '*.udt' external source file, in the format the compiler reads back. Unlike 'plc_get_type_source' this needs no TIA Portal V21 and produces a file that can be imported again")]
        public static ResponseGeneratedSource ExportSourceType(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("typePath: root-relative path of the PLC data type, e.g. 'Common/BtnTyp_X'. Use 'ResolveObjectPath' if you only know the name")] string typePath,
            [Description("exportPath: directory on this machine that receives the file; an existing file of the same name is overwritten")] string exportPath,
            [Description("withDependencies: also write every data type this one uses into the same file. Default false")] bool withDependencies = false,
            [Description("preservePath: mirror the project groups below '<exportPath>/PLC data types', as the Export* tools do. Default false, which writes straight into exportPath")] bool preservePath = false)
        {
            return Generated(
                () => Portal.ExportSourceType(softwarePath, typePath, exportPath, withDependencies, preservePath),
                typePath,
                "PLC data type");
        }

        // [McpServerTool(Name = "export_source_blocks", Title = "Export blocks as source", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),`n         // Description("Write every program block below a block group, including all subgroups, as TIA Portal external source files. Blocks with no source form (LAD, FBD, GRAPH), inconsistent blocks and know-how protected ones are reported in 'Skipped' instead of failing the run. The recursive counterpart to 'ExportSourceBlock'")]
        public static ResponseGeneratedSources ExportSourceBlocks(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative path of the block group to export recursively, e.g. '0_OBs'. Empty means all blocks below 'Program blocks'")] string groupPath,
            [Description("exportPath: directory on this machine that receives the files; existing files of the same name are overwritten")] string exportPath,
            [Description("withDependencies: also write every object each block uses into its file. Default false, which keeps one object per file")] bool withDependencies = false,
            [Description("preservePath: mirror the project groups below '<exportPath>/Program blocks'. Default false, which writes straight into exportPath, so blocks of the same name in different groups overwrite each other")] bool preservePath = false)
        {
            return GeneratedMany(
                () => Portal.ExportSourceBlocks(softwarePath, groupPath, exportPath, withDependencies, preservePath),
                $"Block sources below '{groupPath}'",
                exportPath);
        }

        // [McpServerTool(Name = "export_source_types", Title = "Export types as source", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),`n         // Description("Write every PLC data type below a type group, including all subgroups, as '*.udt' external source files. Inconsistent and know-how protected types are reported in 'Skipped' instead of failing the run. The recursive counterpart to 'ExportSourceType'")]
        public static ResponseGeneratedSources ExportSourceTypes(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative path of the type group to export recursively, e.g. 'Common'. Empty means all types below 'PLC data types'")] string groupPath,
            [Description("exportPath: directory on this machine that receives the files; existing files of the same name are overwritten")] string exportPath,
            [Description("withDependencies: also write every data type each one uses into its file. Default false")] bool withDependencies = false,
            [Description("preservePath: mirror the project groups below '<exportPath>/PLC data types'. Default false, which writes straight into exportPath, so types of the same name in different groups overwrite each other")] bool preservePath = false)
        {
            return GeneratedMany(
                () => Portal.ExportSourceTypes(softwarePath, groupPath, exportPath, withDependencies, preservePath),
                $"Type sources below '{groupPath}'",
                exportPath);
        }

        [McpServerTool(Name = "plc_generate_sources", Title = "Generate sources",Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Write every block and PLC data type of one PLC software as external source files into a folder tree that mirrors the project groups: '<exportPath>/Program blocks/...' and '<exportPath>/PLC data types/...', one file per object. The compilable counterpart to 'ExportPlcAsDocuments'. Objects with no source form (LAD, FBD, GRAPH), inconsistent objects and know-how protected ones are reported in 'Skipped' instead of failing the run")]
        public static ResponseGeneratedSources GenerateSources(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the tree; existing files of the same name are overwritten")] string exportPath,
            [Description("regexName: optional regular expression, generates only objects whose name matches. Empty means all")] string regexName = "",
            [Description("withDependencies: also write every object each one uses into its file. Default false, which keeps one object per file")] bool withDependencies = false)
        {
            try
            {
                var result = Portal.GenerateSources(softwarePath, exportPath, regexName, withDependencies);

                return new ResponseGeneratedSources
                {
                    Message = $"Sources of '{softwarePath}' written to '{exportPath}': {result.Files.Count} file(s), " +
                              $"{result.Skipped.Count} skipped, {result.Failures.Count} failed",
                    Directory = result.Directory,
                    Written = result.Written,
                    Items = result.Files.Select(ToGeneratedSourceItem).ToList(),
                    Skipped = result.Skipped,
                    Failures = result.Failures,
                    Meta = Ok(new JsonObject
                    {
                        ["totalWritten"] = result.Files.Count,
                        ["skipped"] = result.Skipped.Count,
                        ["failed"] = result.Failures.Count
                    })
                };
            }
            catch (PortalException pex)
            {
                throw ToolError(pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"generating sources for '{softwarePath}'", ex);
            }
        }

        /// <summary>Shared response shaping for the two recursive group generators.</summary>
        private static ResponseGeneratedSources GeneratedMany(Func<GeneratedSourcesResult> generate, string what, string exportPath)
        {
            try
            {
                var result = generate();

                return new ResponseGeneratedSources
                {
                    Message = $"{what} written to '{exportPath}': {result.Files.Count} file(s), " +
                              $"{result.Skipped.Count} skipped, {result.Failures.Count} failed",
                    Directory = result.Directory,
                    Written = result.Written,
                    Items = result.Files.Select(ToGeneratedSourceItem).ToList(),
                    Skipped = result.Skipped,
                    Failures = result.Failures,
                    Meta = Ok(new JsonObject
                    {
                        ["totalWritten"] = result.Files.Count,
                        ["skipped"] = result.Skipped.Count,
                        ["failed"] = result.Failures.Count
                    })
                };
            }
            catch (PortalException pex)
            {
                throw ToolError(pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"exporting {what}", ex);
            }
        }

        /// <summary>Shared response shaping for the two single-object generators.</summary>
        private static ResponseGeneratedSource Generated(Func<GeneratedSource> generate, string objectPath, string kind)
        {
            try
            {
                var result = generate();

                return new ResponseGeneratedSource
                {
                    Message = $"Source of {kind} '{objectPath}' written to '{result.File}'",
                    Name = result.Name,
                    Path = result.Path,
                    File = result.File,
                    Format = result.Format,
                    Language = result.Language,
                    Size = result.Size,
                    Meta = Ok(new JsonObject
                    {
                        ["format"] = result.Format,
                        ["size"] = result.Size
                    })
                };
            }
            catch (PortalException pex)
            {
                throw ToolError(pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"generating the source of '{objectPath}'", ex);
            }
        }

        private static ResponseGeneratedSourceItem ToGeneratedSourceItem(GeneratedSource source) => new ResponseGeneratedSourceItem
        {
            Name = source.Name,
            Path = source.Path,
            File = source.File,
            Format = source.Format,
            Language = source.Language,
            Size = source.Size
        };

        #endregion

        // From the former McpServerWrite.ImportSources.cs:
        // Importing a whole tree of TIA Portal external source files back into the project.
        //
        // Callers: the MCP host, through tool registration under '--allow-write'. Affected API:
        // none existing - the tool is new. File I/O: reads the caller-supplied tree only; the
        // project change stays in memory until SaveProject/SaveSession, per the SaveHint convention
        // shared by every [WriteTool] tool.
        //
        // The bulk-import counterpart to 'GenerateSources' (McpServer.GenerateSource.cs): that tool
        // writes a folder tree of *.db/*.awl/*.scl/*.udt files, this one walks it back into blocks
        // and PLC data types via 'Portal.ImportSources'.

        #region import sources (write)

        // [McpServerTool(Name = "import_sources", Title = "Import sources", Destructive = true, OpenWorld = false, UseStructuredContent = true),`n         // Description("Compile every block and PLC data type source file (*.db, *.awl, *.scl, *.udt) under a folder tree back into the project - the counterpart to 'GenerateSources'. Each file is placed into the block or PLC data type group its folder path implies, matching the layout 'GenerateSources' writes; a folder whose group does not yet exist in the project fails that file rather than being created automatically. Existing blocks/types of the same name are overwritten")]
        public static ResponseImportedSources ImportSources(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("importPath: directory on this machine to walk recursively for *.db, *.awl, *.scl and *.udt files")] string importPath,
            [Description("regexName: optional regular expression, imports only files whose base name matches. Empty means all")] string regexName = "",
            [Description("keepOnError: keep successfully generated objects from a file even when others in it fail; TIA Portal then reports no per-object success/failure for that file. Default false, which rolls back a whole file on any error")] bool keepOnError = false)
        {
            return Guarded(nameof(ImportSources), () =>
            {
                var result = Portal.ImportSources(softwarePath, importPath, regexName, keepOnError);

                return new ResponseImportedSources
                {
                    Directory = result.Directory,
                    Written = result.Written,
                    Items = result.Items.Select(i => new ResponseImportedSourceItem
                    {
                        Name = i.Name,
                        Path = i.Path,
                        Kind = i.Kind
                    }).ToList(),
                    Failures = result.Failures,
                    Message = $"{result.Items.Count} object(s) imported from '{importPath}', {result.Failures.Count} failed. {SaveHint}",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["pendingSave"] = true,
                        ["importedCount"] = result.Items.Count,
                        ["failedCount"] = result.Failures.Count,
                        ["keepOnError"] = keepOnError
                    }
                };
            });
        }

        #endregion

        // From the former McpServer.ExternalSources.cs:
        // Read-only MCP tools for PLC external source files.
        //
        // Callers: registered through Program.BuildTools() and invoked directly by the external
        // source test class. Affected API: additive only - a new partial of the existing McpServer
        // type. Data: returns ResponseExternalSource* as MCP structuredContent. No file I/O.
        //
        // PlcExternalSource types only Name, so GetExternalSourceInfo leans on the generic
        // attribute bag for everything else rather than guessing at attribute names.

        #region external sources

        [McpServerTool(Name = "plc_get_external_sources", Title = "Get external sources", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the external source files of a plc software, optionally filtered by a regular expression on the source name")]
        public static ResponseExternalSources GetExternalSources(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("regexName: optional regular expression to filter the external source names")] string regexName = "")
        {
            try
            {
                var sources = Portal.GetExternalSources(softwarePath, regexName);

                return new ResponseExternalSources
                {
                    Message = $"{sources.Count} external source(s) retrieved from '{softwarePath}'",
                    Items = sources.Select(ToExternalSourceInfo).ToList(),
                    Meta = Ok(new JsonObject { ["totalExternalSources"] = sources.Count })
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"retrieving external sources from '{softwarePath}'", ex);
            }
        }

        [McpServerTool(Name = "plc_get_external_source_info", Title = "Get external source info", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Get a single external source file. Beyond its name, all metadata is returned in the generic Attributes list")]
        public static ResponseExternalSourceInfo GetExternalSourceInfo(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("sourcePath: root-relative path of the external source, e.g. 'SourceGroup1/Source_1'")] string sourcePath)
        {
            try
            {
                var source = Portal.GetExternalSource(softwarePath, sourcePath)
                    ?? throw ObjectNotFound("External source", sourcePath, softwarePath, "sourcePath", "plc_get_external_sources");

                var info = ToExternalSourceInfo(source);
                info.Message = $"External source info retrieved from '{sourcePath}' in '{softwarePath}'";
                info.Attributes = Helper.GetAttributeList(source);
                info.Meta = Ok();

                return info;
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"retrieving external source info from '{sourcePath}' in '{softwarePath}'", ex);
            }
        }

        private static ResponseExternalSourceInfo ToExternalSourceInfo(PlcExternalSource source)
        {
            return new ResponseExternalSourceInfo
            {
                Name = source.Name,
                Path = Portal.GetExternalSourcePath(source)
            };
        }

        #endregion

        // From the former McpServerWrite.Documents.ExternalSources.cs:

        #region external sources (write)

        [WriteTool]
        [McpServerTool(Name = "plc_create_external_source", Title = "Create external source from file", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Add a source file (for example an SCL file) from the file system into the external source files of the plc software")]
        public static ResponseCreated CreateExternalSourceFromFile(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative external source group; empty uses the External source files root")] string groupPath,
            [Description("name: name the source gets in the project, without a slash")] string name,
            [Description("filePath: full path of the source file on the machine running this server")] string filePath)
        {
            return Guarded(nameof(CreateExternalSourceFromFile), () =>
            {
                Portal.CreateExternalSourceFromFile(softwarePath, groupPath, name, filePath);
                return Created("External source", name, JoinPath(groupPath, name));
            });
        }

        [WriteTool]
        [McpServerTool(Name = "plc_delete_external_source", Title = "Delete external source", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Remove an external source file from the plc software")]
        public static ResponseDeleted DeleteExternalSource(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("sourcePath: root-relative path of the external source, e.g. SourceGroup1/Source_1")] string sourcePath)
        {
            return Guarded(nameof(DeleteExternalSource), () =>
            {
                Portal.DeleteExternalSource(softwarePath, sourcePath);
                return Deleted("External source", sourcePath);
            });
        }

        [WriteTool]
        [McpServerTool(Name = "plc_create_external_source_group", Title = "Create external source group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a group below the External source files root of the plc software")]
        public static ResponseCreated CreateExternalSourceGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("parentGroupPath: root-relative path of the parent group; empty creates directly below the root")] string parentGroupPath,
            [Description("name: name of the new group, without a slash")] string name)
        {
            return Guarded(nameof(CreateExternalSourceGroup), () =>
            {
                Portal.CreateExternalSourceGroup(softwarePath, parentGroupPath, name);
                return Created("External source group", name, JoinPath(parentGroupPath, name));
            });
        }

        [WriteTool]
        [McpServerTool(Name = "plc_delete_external_source_group", Title = "Delete external source group", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete an external source group and everything inside it. The External source files system group itself cannot be deleted")]
        public static ResponseDeleted DeleteExternalSourceGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative path of the group to delete")] string groupPath)
        {
            return Guarded(nameof(DeleteExternalSourceGroup), () =>
            {
                Portal.DeleteExternalSourceGroup(softwarePath, groupPath);
                return Deleted("External source group", groupPath);
            });
        }

        // [McpServerTool(Name = "import_source_blocks", Title = "Import source blocks", Destructive = true, OpenWorld = false, UseStructuredContent = true),`n         // Description("Compile an external source file into program blocks and PLC data types. A target must be a block user group: blocks cannot be generated into the Program blocks root")]
        public static ResponseGenerateBlocks ImportSourceBlocks(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("sourcePath: root-relative path of the external source, e.g. SourceGroup1/Source_1")] string sourcePath,
            [Description("targetGroupPath: optional root-relative block user group that receives the blocks; empty uses the source default location")] string targetGroupPath = "",
            [Description("keepOnError: keep successfully generated blocks even when others fail (default false)")] bool keepOnError = false)
        {
            return Guarded(nameof(ImportSourceBlocks), () =>
            {
                var names = Portal.ImportSourceBlocks(softwarePath, sourcePath, targetGroupPath, keepOnError);

                return new ResponseGenerateBlocks
                {
                    GeneratedNames = names,
                    Count = names.Count,
                    Message = $"{names.Count} object(s) generated from '{sourcePath}'. {SaveHint}",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["pendingSave"] = true,
                        ["generatedCount"] = names.Count,
                        ["keepOnError"] = keepOnError
                    }
                };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "plc_replace_source", Title = "Replace the code of a block or PLC data type", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Replace the code of an EXISTING SCL block (FB, FC, OB), LAD block, data block or PLC data type by a new source text, then compile it. The cycle is: read the present code with 'plc_get_block_source' / 'plc_get_type_source' and format 'source', change it, pass the WHOLE text here. A LAD block is read with format 'document' instead and takes that text back, both parts of it ('===== Name.s7dcl =====' and, when it has titles, '===== Name.s7res ====='); this is the way to change the interface of a LAD block, while single networks are changed with 'plc_manage_lad_networks'. The object keeps its place in the project, its block number and its instance DBs. The source must declare exactly this object (same kind and name). With compile='object' (default) the object is compiled right away; if the new code does not compile, the previous code is put back and the call fails with the compile errors (onCompileError='restore', default) or the new code stays and the errors are returned (onCompileError='keep'). Changing the interface of a block or the members of a type leaves its callers, instance DBs and users inconsistent: they are listed in nowInconsistent and need 'plc_compile_software', or pass compile='software'. LAD, FBD, STL and GRAPH blocks have no source text and are refused. To create a new block use 'plc_create_scl_block'")]
        public static ResponseSourceEdit ReplaceSource(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("objectPath: root-relative path of the block or PLC data type, e.g. 'Pumps/FB_Pump' or 'Types/UDT_Motor'")] string objectPath,
            [Description("source: the whole external source text of the object, e.g. FUNCTION_BLOCK \"FB_Pump\" ... END_FUNCTION_BLOCK, DATA_BLOCK ... END_DATA_BLOCK or TYPE ... END_TYPE")] string source,
            [Description("compile: 'object' (default) compiles the object itself, 'software' then compiles the whole PLC as well, 'none' compiles nothing")] string compile = "object",
            [Description("onCompileError: 'restore' (default) puts the previous code back when the new code does not compile, 'keep' leaves the new code in place")] string onCompileError = "restore")
        {
            return GuardedNoTransaction(nameof(ReplaceSource), () =>
            {
                var result = Portal.ReplaceSource(softwarePath, objectPath, source, compile, onCompileError);

                if (result.Restored)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams,
                        $"The new code of {result.Kind} '{result.Name}' does not compile ({result.ErrorCount} error(s)); the previous code was put back and compiled. " +
                        $"Nothing else changed. Errors: {Portal.DescribeMessages(result.Messages)}");
                }

                var errors = result.ErrorCount ?? 0;
                var message = $"The code of {result.Kind} '{result.Name}' was replaced" +
                              (result.State == null ? "; not compiled." : $"; compile ({result.Compile}): {result.State}, {errors} error(s), {result.WarningCount} warning(s).") +
                              (errors > 0 && !result.ObjectCompiles ? " The new code is in place with its errors (onCompileError='keep')." : string.Empty) +
                              (errors > 0 && result.ObjectCompiles ? " The object itself compiles; the errors are in objects that use it and have to follow the change." : string.Empty) +
                              (result.NowInconsistent.Count > 0 ? $" {result.NowInconsistent.Count} object(s) that use it now wait for a compile." : string.Empty) +
                              " " + SaveHint;

                return new ResponseSourceEdit
                {
                    Name = result.Name,
                    Path = result.Path,
                    Kind = result.Kind,
                    Number = result.Number,
                    State = result.State,
                    ErrorCount = result.ErrorCount,
                    WarningCount = result.WarningCount,
                    Messages = result.Messages.Where(m => m.Severity == "Error" || m.Severity == "Warning").ToList(),
                    NowInconsistent = result.NowInconsistent,
                    Notes = result.Notes,
                    Message = message,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = errors == 0,
                        ["pendingSave"] = true
                    }
                };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "plc_create_scl_block", Title = "Create SCL block", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a block directly from SCL source code text.")]
        public static ResponseGenerateBlocks CreateSclBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("sclCode: the raw SCL source code for the block")] string sclCode,
            [Description("targetGroupPath: optional root-relative block user group that receives the blocks; empty uses the source default location")] string targetGroupPath = "",
            [Description("keepOnError: keep successfully generated blocks even when others fail (default false)")] bool keepOnError = false)
        {
            return Guarded(nameof(CreateSclBlock), () =>
            {
                var names = Portal.GenerateFromSclText(softwarePath, sclCode, targetGroupPath, keepOnError);

                return new ResponseGenerateBlocks
                {
                    GeneratedNames = names,
                    Count = names.Count,
                    Message = $"{names.Count} object(s) generated from SCL code. {SaveHint}",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["pendingSave"] = true,
                        ["generatedCount"] = names.Count,
                        ["keepOnError"] = keepOnError
                    }
                };
            });
        }

        #endregion
    }
}

