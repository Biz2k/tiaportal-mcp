using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        #region hardware topology

        [McpServerTool(Name = "GetHardwareTopology", Title = "Get hardware topology", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get the hardware topology of the TIA-Portal project (devices, modules, MLFB/Article numbers, firmwares)")]
        public static ResponseHardwareTopology GetHardwareTopology()
        {
            try
            {
                var devices = Portal.GetHardwareTopology();

                return new ResponseHardwareTopology
                {
                    Message = "Hardware topology retrieved",
                    Devices = devices,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving hardware topology: {ex.Message}", ex);
            }
        }

        #endregion
    }
}
