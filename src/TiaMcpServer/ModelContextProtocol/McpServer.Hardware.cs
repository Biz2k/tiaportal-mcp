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

        #region create hardware

        [McpServerTool(Name = "CreateHardwareDevice", Title = "Create hardware device", Destructive = true, OpenWorld = false, UseStructuredContent = true), Description("Creates a new hardware device (PLC, HMI, ET200 station, etc) at the project level")]
        public static ResponseMessage CreateHardwareDevice(
            [Description("typeIdentifier: the Openness type identifier (e.g., 'OrderNumber:6ES7 516-3AN01-0AB0/V2.8')")] string typeIdentifier,
            [Description("name: the name for the new station and device")] string name)
        {
            try
            {
                Portal.CreateHardwareDevice(typeIdentifier, name);

                return new ResponseMessage
                {
                    Message = $"Hardware device '{name}' created successfully",
                    Meta = new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error creating device '{name}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "PlugHardwareModule", Title = "Plug hardware module", Destructive = true, OpenWorld = false, UseStructuredContent = true), Description("Plugs a new module into an existing device item (e.g. into a Rack) at a specific position")]
        public static ResponseMessage PlugHardwareModule(
            [Description("deviceName: the name of the root device station")] string deviceName,
            [Description("parentItemName: the name of the parent device item to plug into (e.g. 'Rack_0')")] string parentItemName,
            [Description("positionNumber: the slot/position number to plug into (e.g. 1)")] int positionNumber,
            [Description("typeIdentifier: the Openness type identifier of the new module (e.g. 'OrderNumber:6ES7 131-6BH01-0BA0/V0.0')")] string typeIdentifier,
            [Description("moduleName: the name for the new module")] string moduleName)
        {
            try
            {
                Portal.PlugHardwareModule(deviceName, parentItemName, positionNumber, typeIdentifier, moduleName);

                return new ResponseMessage
                {
                    Message = $"Module '{moduleName}' plugged successfully into '{parentItemName}' at position {positionNumber}",
                    Meta = new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error plugging module '{moduleName}': {ex.Message}", ex);
            }
        }

        #endregion
    }
}
