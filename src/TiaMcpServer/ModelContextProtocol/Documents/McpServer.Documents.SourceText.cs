using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        #region source

        [McpServerTool(Name = "ExportPlcAsSourceTree", Title = "Snapshot a PLC to a source tree", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Write a whole PLC software to one folder tree that mirrors the project, ready to commit: program blocks and PLC data types as readable SIMATIC Source Documents where TIA Portal supports them, tag tables and watch tables as XML, each below its localised system folder. Replaces running the four bulk exports separately. Objects that cannot be exported are reported instead of failing the snapshot")]
        public static ResponseSourceTree ExportPlcAsSourceTree(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the tree; existing files of the same name are overwritten")] string exportPath)
        {
            try
            {
                var result = Portal.ExportPlcAsSourceTree(softwarePath, exportPath);

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
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error snapshotting '{softwarePath}': {ex.Message}", ex);
            }
        }

        #endregion
    }
}
