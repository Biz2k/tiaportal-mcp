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

        [McpServerTool(Name = "get_hardware_topology", Title = "Get hardware topology", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get the hardware topology of the TIA-Portal project (devices, modules, MLFB/Article numbers, firmwares)")]
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

        [McpServerTool(Name = "hw_create_device", Title = "Create hardware device", Destructive = true, OpenWorld = false, UseStructuredContent = true), Description("Creates a new hardware device (PLC, HMI, ET200 station, etc) at the project level")]
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

        [McpServerTool(Name = "hw_plug_module", Title = "Plug hardware module", Destructive = true, OpenWorld = false, UseStructuredContent = true), Description("Plugs a new module into an existing device item (e.g. into a Rack) at a specific position")]
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

        [McpServerTool(Name = "hw_delete_device", Title = "Delete hardware device", Destructive = true, OpenWorld = false, UseStructuredContent = true), Description("Deletes a hardware device (PLC, HMI, etc) from the project")]
        public static ResponseMessage DeleteHardwareDevice([Description("deviceName: the name of the device to delete")] string deviceName)
        {
            try
            {
                Portal.DeleteHardwareDevice(deviceName);
                return new ResponseMessage
                {
                    Message = $"Hardware device '{deviceName}' deleted successfully",
                    Meta = new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error deleting device '{deviceName}': {ex.Message}", ex);
            }
        }

        #endregion

        #region network and subnets

        [McpServerTool(Name = "net_connect_subnet", Title = "Connect interface to subnet", Destructive = true, OpenWorld = false, UseStructuredContent = true), Description("Connects a network interface to a subnet (creates the PN/IE subnet if it doesn't exist)")]
        public static ResponseMessage ConnectSubnet(
            [Description("deviceName: the name of the device")] string deviceName,
            [Description("interfaceName: the name of the PROFINET/Ethernet interface (e.g. 'PROFINET interface_1')")] string interfaceName,
            [Description("subnetName: the name of the subnet to connect to (e.g. 'PN/IE_1')")] string subnetName)
        {
            try
            {
                Portal.ConnectToSubnet(deviceName, interfaceName, subnetName);
                return new ResponseMessage
                {
                    Message = $"Interface '{interfaceName}' connected to subnet '{subnetName}' successfully",
                    Meta = new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error connecting to subnet: {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "net_disconnect_subnet", Title = "Disconnect interface from subnet", Destructive = true, OpenWorld = false, UseStructuredContent = true), Description("Disconnects a network interface from its current subnet")]
        public static ResponseMessage DisconnectSubnet(
            [Description("deviceName: the name of the device")] string deviceName,
            [Description("interfaceName: the name of the PROFINET/Ethernet interface")] string interfaceName)
        {
            try
            {
                Portal.DisconnectSubnet(deviceName, interfaceName);
                return new ResponseMessage
                {
                    Message = $"Interface '{interfaceName}' disconnected from subnet successfully",
                    Meta = new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error disconnecting from subnet: {ex.Message}", ex);
            }
        }

        #endregion

        #region IO systems

        [McpServerTool(Name = "net_create_io_system", Title = "Create IO system on controller", Destructive = true, OpenWorld = false, UseStructuredContent = true), Description("Assigns (creates) a PROFINET IO system to a PLC network interface")]
        public static ResponseMessage CreateIoSystem(
            [Description("deviceName: the name of the PLC device")] string deviceName,
            [Description("interfaceName: the name of the PROFINET interface (e.g. 'PROFINET interface_1')")] string interfaceName,
            [Description("ioSystemName: the name of the IO system to create (e.g. 'PROFINET IO-System (100)')")] string ioSystemName)
        {
            try
            {
                Portal.CreateIoSystem(deviceName, interfaceName, ioSystemName);
                return new ResponseMessage
                {
                    Message = $"IO system '{ioSystemName}' created on interface '{interfaceName}' successfully",
                    Meta = new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error creating IO system: {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "net_connect_to_io_system", Title = "Connect IO device to IO system", Destructive = true, OpenWorld = false, UseStructuredContent = true), Description("Connects an IO device's network interface to an existing IO system")]
        public static ResponseMessage ConnectToIoSystem(
            [Description("deviceName: the name of the IO device")] string deviceName,
            [Description("interfaceName: the name of the PROFINET interface on the IO device")] string interfaceName,
            [Description("ioSystemName: the name of the target IO system to connect to")] string ioSystemName)
        {
            try
            {
                Portal.ConnectToIoSystem(deviceName, interfaceName, ioSystemName);
                return new ResponseMessage
                {
                    Message = $"Interface '{interfaceName}' connected to IO system '{ioSystemName}' successfully",
                    Meta = new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error connecting to IO system: {ex.Message}", ex);
            }
        }

        #endregion
    }
}

