using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    // What the installed TIA Portal can put into a project: the hardware catalog as a tree, the installed products and
    // the GSD files. The work is in Siemens/Portal.Catalog.cs. 'hw_search_catalog' (McpServer.Hardware.cs) stays the
    // tool for finding a known article.
    public static partial class McpServer
    {
        public class ResponseCatalogLevel : ResponseMessage
        {
            public CatalogLevel? Catalog { get; set; }
        }

        public class ResponseInstalledSoftware : ResponseMessage
        {
            public InstalledSoftwareInfo? Installed { get; set; }
        }

        [McpServerTool(Name = "hw_get_catalog", Title = "Browse the hardware catalog", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Browse the hardware catalog of the installed TIA Portal as a tree, the way its catalog pane shows it: everything that can be added to a project - controllers, IO, HMI, drives, network components, and the devices from installed GSD files under 'Other field devices'. Returns the folders below 'path' with the number of entries in each and, in a folder that holds entries itself (usually the firmware versions of one article), their type identifiers for 'hw_create_device' and 'hw_plug_module'. Start with an empty path and go down. To find a known article use 'hw_search_catalog'. Needs a connection to TIA Portal, not an open project")]
        public static ResponseCatalogLevel GetHardwareCatalog(
            [Description("path: catalog folder, names joined with '\\' (a '/' is part of a name, as in 'Distributed I/O'), e.g. 'Controllers\\SIMATIC S7-1500\\CPU'; empty (default) is the top")] string path = "",
            [Description("limit: the most entries of the folder itself to return (default 100); 0 returns all")] int limit = 100,
            [Description("offset: entries to skip (default 0)")] int offset = 0)
        {
            try
            {
                var level = Portal.GetHardwareCatalog(path, limit, offset);

                return new ResponseCatalogLevel
                {
                    Catalog = level,
                    Message = $"Catalog folder '{(level.Path.Length == 0 ? "(top)" : level.Path)}': {level.Folders.Count} folder(s), {level.TotalEntries} entr{(level.TotalEntries == 1 ? "y" : "ies")} in and below it" +
                              (level.Entries.Count > 0 ? $", {level.Entries.Count} returned from the folder itself" : string.Empty),
                    Meta = Ok(new JsonObject())
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolError(ex);
            }
        }

        [McpServerTool(Name = "get_installed_software", Title = "Get installed TIA Portal products and GSD files", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("What is installed in the TIA Portal this server is connected to: the products with their versions and options (STEP 7, Safety, WinCC, Startdrive with its drive families), the size of the hardware catalog, and the GSD files whose devices are in the catalog - file name, where its devices sit in the catalog, how many devices and modules it describes, and examples with their type identifiers. Tells which kinds of device a project can get at all. A GSD file or a support package that is missing has to be installed in TIA Portal by the user (Options menu): Openness has no call for it. Needs a connection to TIA Portal, not an open project")]
        public static ResponseInstalledSoftware GetInstalledSoftware(
            [Description("withGsdFiles: list the GSD files too (default true)")] bool withGsdFiles = true,
            [Description("gsdFilter: only the GSD files whose file name, catalog path or device name contains this text, e.g. 'SINAMICS', 'KUKA', 'PROFIBUS'; a list of up to 30 files carries example devices with their type identifiers. Empty (default) lists all files without examples")] string gsdFilter = "")
        {
            try
            {
                var info = Portal.GetInstalledSoftware(withGsdFiles, gsdFilter);

                return new ResponseInstalledSoftware
                {
                    Installed = info,
                    Message = $"{info.Products.Count} product(s): {string.Join(", ", info.Products.Select(p => $"{p.Name} {p.Version}".Trim()))}. Hardware catalog: {info.CatalogEntries} entries" +
                              (info.GsdFiles != null ? $", {info.GsdFileCount} GSD file(s)" + (info.GsdFiles.Count != info.GsdFileCount ? $", {info.GsdFiles.Count} of them match '{gsdFilter}'" : string.Empty) : string.Empty),
                    Meta = Ok(new JsonObject())
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolError(ex);
            }
        }
    }
}
