using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        [McpServerTool(Name = "get_libraries", Title = "Get opened libraries", Destructive = false, OpenWorld = false, UseStructuredContent = true), Description("Lists the project library and any globally opened libraries")]
        public static ResponseLibraries GetLibraries()
        {
            try
            {
                var libs = Portal.GetLibraries();
                return new ResponseLibraries
                {
                    Items = libs,
                    Message = $"Found {libs.Count} open libraries",
                    Meta = new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error listing libraries: {Why(ex)}", ex);
            }
        }

        [McpServerTool(Name = "open_global_library", Title = "Open global library", Destructive = false, OpenWorld = false, UseStructuredContent = true), Description("Opens a global library file (.al1x) into the current TIA Portal session")]
        public static ResponseMessage OpenGlobalLibrary(
            [Description("filePath: absolute path to the global library file")] string filePath)
        {
            try
            {
                Portal.OpenGlobalLibrary(filePath);
                return new ResponseMessage
                {
                    Message = $"Global library '{filePath}' opened successfully",
                    Meta = new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error opening global library: {Why(ex)}", ex);
            }
        }

        [McpServerTool(Name = "get_master_copies", Title = "Get Master Copies in library", Destructive = false, OpenWorld = false, UseStructuredContent = true), Description("Lists all Master Copies inside a specified library recursively")]
        public static ResponseMasterCopies GetMasterCopies(
            [Description("libraryName: name of the library (use 'ProjectLibrary' for the project's library)")] string libraryName)
        {
            try
            {
                var copies = Portal.GetMasterCopies(libraryName);
                return new ResponseMasterCopies
                {
                    Items = copies,
                    Message = $"Found {copies.Count} Master Copies in library '{libraryName}'",
                    Meta = new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error getting Master Copies: {Why(ex)}", ex);
            }
        }

        [WriteTool]
        [McpServerTool(Name = "instantiate_master_copy", Title = "Instantiate Master Copy", Destructive = true, OpenWorld = false, UseStructuredContent = true), Description("Instantiates a Master Copy from a library into the project (e.g. into a PLC's block or type group)")]
        public static ResponseMessage InstantiateMasterCopy(
            [Description("libraryName: name of the library (e.g., 'ProjectLibrary')")] string libraryName,
            [Description("masterCopyPath: relative path of the master copy (e.g. 'ProjectLibrary/MasterCopy_1')")] string masterCopyPath,
            [Description("targetDeviceName: name of the PLC device to instantiate into")] string targetDeviceName,
            [Description("targetGroupName: target folder group inside the PLC (e.g. 'Program blocks' or 'PLC data types')")] string targetGroupName,
            [Description("targetType: either 'block' or 'type'")] string targetType)
        {
            try
            {
                Portal.InstantiateMasterCopy(libraryName, masterCopyPath, targetDeviceName, targetGroupName, targetType);
                return new ResponseMessage
                {
                    Message = $"Master Copy '{masterCopyPath}' instantiated successfully into '{targetGroupName}'",
                    Meta = new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error instantiating Master Copy: {Why(ex)}", ex);
            }
        }
    }
}
