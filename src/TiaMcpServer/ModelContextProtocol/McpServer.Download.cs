using System.Collections.Generic;
using System.ComponentModel;
using TiaMcpServer.Siemens;
using Siemens.Engineering.Download;
using ModelContextProtocol.Server;

namespace TiaMcpServer.ModelContextProtocol
{
    public partial class McpServer
    {
        [McpServerTool(Name = "GetDownloadTargets", Title = "Get download targets", Destructive = false, OpenWorld = false, UseStructuredContent = true)]
        [Description("Returns available download targets (modes and interfaces) for a PLC.")]
        public static List<string> GetDownloadTargets(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath)
        {
            return Portal.GetDownloadTargets(softwarePath);
        }

        [WriteTool]
        [McpServerTool(Name = "DownloadToPlc", Title = "Download to PLC", Destructive = true, OpenWorld = false, UseStructuredContent = true)]
        [Description("Downloads hardware and/or software configuration to a PLC. Requires target interface from get_download_targets.")]
        public static ResponseDownloadResult DownloadToPlc(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("modeName: Download mode name (e.g. PN/IE)")] string modeName,
            [Description("pcInterfaceName: PC interface name (e.g. PLCSIM)")] string pcInterfaceName,
            [Description("targetInterfaceName: Target interface name (e.g. 1 X1)")] string targetInterfaceName,
            [Description("hardware: Whether to download hardware configuration")] bool hardware,
            [Description("software: Whether to download software configuration")] bool software)
        {
            return GuardedNoTransaction(nameof(DownloadToPlc), () =>
            {
                var result = Portal.DownloadToPlc(softwarePath, modeName, pcInterfaceName, targetInterfaceName, hardware, software);
                return MapDownloadResult(result);
            });
        }


        private static ResponseDownloadResult MapDownloadResult(DownloadResult? result)
        {
            if (result == null)
            {
                return new ResponseDownloadResult
                {
                    State = "Unknown",
                    ErrorCount = 0,
                    WarningCount = 0
                };
            }

            return new ResponseDownloadResult
            {
                State = result.State.ToString(),
                ErrorCount = result.ErrorCount,
                WarningCount = result.WarningCount
            };
        }
    }
    
    public class ResponseDownloadResult
    {
        public string State { get; set; } = string.Empty;
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
    }
}
