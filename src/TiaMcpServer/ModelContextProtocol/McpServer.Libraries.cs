using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using TiaMcpServer.Siemens;

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
                throw Failure($"listing libraries", ex);
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
                throw Failure($"opening global library", ex);
            }
        }

        [McpServerTool(Name = "get_master_copies", Title = "Get Master Copies in library", Destructive = false, OpenWorld = false, UseStructuredContent = true), Description("Lists all Master Copies inside a specified library recursively")]
        public static ResponseMasterCopies GetMasterCopies(
            [Description("libraryName: name of the library (use 'ProjectLibrary' for the project's library)")] string libraryName,
            [Description(Paging.LimitText)] int limit = 500,
            [Description(Paging.OffsetText)] int offset = 0)
        {
            try
            {
                var page = Paging.Page(Portal.GetMasterCopies(libraryName), limit, offset);
                return new ResponseMasterCopies
                {
                    Items = page.Items,
                    Message = $"Found {page.Total} Master Copies in library '{libraryName}'" + page.Note("a smaller library"),
                    Meta = page.Meta(new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true })
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"getting Master Copies", ex);
            }
        }

        [McpServerTool(Name = "get_library_types", Title = "Get library types", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the types of a library with their versions, and say which system each type belongs to: 'unified' (WinCC Unified), 'classic' (WinCC Comfort / Advanced / Professional), 'plc', or 'universal' (not tied to a system, e.g. icons and graphics). For a WinCC Unified type each version carries the 'ContainedType' value that 'unified_manage_faceplate' takes as faceplateType")]
        public static ResponseLibraryTypes GetLibraryTypes(
            [Description("libraryName: 'ProjectLibrary' (default) or the name of an opened global library; 'get_libraries' lists them")] string libraryName = "ProjectLibrary",
            [Description("system: return only the types of one system - 'unified', 'classic', 'plc' or 'universal'; empty for all")] string system = "",
            [Description(Paging.LimitText)] int limit = 500,
            [Description(Paging.OffsetText)] int offset = 0)
        {
            try
            {
                var types = Portal.GetLibraryTypes(libraryName);

                var systems = types.GroupBy(t => t.System ?? string.Empty).ToDictionary(g => g.Key, g => g.Count());

                if (!string.IsNullOrWhiteSpace(system))
                {
                    if (!LibraryTypeInfo.Systems.Contains(system.Trim(), StringComparer.OrdinalIgnoreCase))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"system takes one of {string.Join(", ", LibraryTypeInfo.Systems)}; got '{system}'.");
                    }

                    types = types.Where(t => string.Equals(t.System, system.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                }

                var page = Paging.Page(types, limit, offset);

                return new ResponseLibraryTypes
                {
                    Message = $"{page.Total} type(s) in library '{libraryName}'" +
                        (string.IsNullOrWhiteSpace(system) ? string.Empty : $" for system '{system.Trim().ToLowerInvariant()}'") + page.Note("system"),
                    Items = page.Items,
                    Systems = systems,
                    Meta = page.Meta(new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true })
                };
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
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
                throw Failure($"instantiating Master Copy", ex);
            }
        }
    }
}
