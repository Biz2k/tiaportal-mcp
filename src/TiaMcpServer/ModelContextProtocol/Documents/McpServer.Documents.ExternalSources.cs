using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.SW.ExternalSources;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
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

        [McpServerTool(Name = "GetExternalSources", Title = "Get PLC external sources", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
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
                throw new McpException($"Unexpected error retrieving external sources from '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "GetExternalSourceInfo", Title = "Get PLC external source info", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Get a single external source file. Beyond its name, all metadata is returned in the generic Attributes list")]
        public static ResponseExternalSourceInfo GetExternalSourceInfo(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("sourcePath: root-relative path of the external source, e.g. 'SourceGroup1/Source_1'")] string sourcePath)
        {
            try
            {
                var source = Portal.GetExternalSource(softwarePath, sourcePath)
                    ?? throw new McpException($"External source not found at '{sourcePath}' in '{softwarePath}'. Use 'GetExternalSources' to list the available sources.");

                var info = ToExternalSourceInfo(source);
                info.Message = $"External source info retrieved from '{sourcePath}' in '{softwarePath}'";
                info.Attributes = Helper.GetAttributeList(source);
                info.Meta = Ok();

                return info;
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving external source info from '{sourcePath}' in '{softwarePath}': {ex.Message}", ex);
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
        [McpServerTool(Name = "CreateExternalSourceFromFile", Title = "Add an external source file", Destructive = true, OpenWorld = false, UseStructuredContent = true),
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
        [McpServerTool(Name = "DeleteExternalSource", Title = "Delete an external source file", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
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
        [McpServerTool(Name = "CreateExternalSourceGroup", Title = "Create an external source group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
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
        [McpServerTool(Name = "DeleteExternalSourceGroup", Title = "Delete an external source group", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
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

        [WriteTool]
        [McpServerTool(Name = "GenerateBlocksFromSource", Title = "Generate blocks from an external source", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Compile an external source file into program blocks and PLC data types. A target must be a block user group: blocks cannot be generated into the Program blocks root")]
        public static ResponseGenerateBlocks GenerateBlocksFromSource(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("sourcePath: root-relative path of the external source, e.g. SourceGroup1/Source_1")] string sourcePath,
            [Description("targetGroupPath: optional root-relative block user group that receives the blocks; empty uses the source default location")] string targetGroupPath = "",
            [Description("keepOnError: keep successfully generated blocks even when others fail (default false)")] bool keepOnError = false)
        {
            return Guarded(nameof(GenerateBlocksFromSource), () =>
            {
                var names = Portal.GenerateBlocksFromSource(softwarePath, sourcePath, targetGroupPath, keepOnError);

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

        #endregion
    }
}
