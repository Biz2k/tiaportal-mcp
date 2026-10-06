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
                throw Failure($"retrieving hardware topology", ex);
            }
        }

        #endregion

        #region hardware catalog

        [McpServerTool(Name = "hw_search_catalog", Title = "Search hardware catalog", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Search the installed hardware catalog by article number or product name and get the type identifiers that 'hw_create_device' and 'hw_plug_module' need. Needs a connection to TIA Portal, not an open project")]
        public static ResponseCatalogSearch SearchHardwareCatalog(
            [Description("query: at least three characters of an article number or product name, e.g. '6ES7 155-6AU01' or 'IM 155-6 PN'")] string query,
            [Description("maxResults: stop after this many entries (default 20)")] int maxResults = 20)
        {
            try
            {
                var entries = Portal.SearchHardwareCatalog(query, maxResults);

                return new ResponseCatalogSearch
                {
                    Items = entries,
                    Message = entries.Count == 0
                        ? $"Nothing in the hardware catalog matches '{query}'. Try a shorter part of the article number."
                        : $"{entries.Count} catalog entr{(entries.Count == 1 ? "y" : "ies")} match '{query}'. Pass 'typeIdentifier' to 'hw_create_device' or 'hw_plug_module'.",
                    Meta = new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true }
                };
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
            }
        }

        #endregion

        #region create hardware

        [WriteTool]
        [McpServerTool(Name = "hw_create_device", Title = "Create hardware device", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a hardware device (PLC, HMI, IO station) at the project level. With an 'OrderNumber:' or 'GSD:' identifier the station is created around that head module. With a 'System:Device.' identifier an empty station is created and the head module is added with 'hw_plug_module'. Use 'hw_search_catalog' to find identifiers")]
        public static ResponseDeviceCreated CreateHardwareDevice(
            [Description("typeIdentifier: 'OrderNumber:<article>/<firmware>' (e.g. 'OrderNumber:6ES7 516-3AN02-0AB0/V2.9'), 'GSD:<file>/<type>', or 'System:Device.<type>' for an empty station (e.g. 'System:Device.ET200SP')")] string typeIdentifier,
            [Description("name: name of the head module (the CPU or interface module); for a 'System:' identifier, the name of the station")] string name,
            [Description("stationName: name of the station that holds the head module; empty (default) uses 'name'")] string stationName = "")
        {
            return Guarded(nameof(CreateHardwareDevice), () =>
            {
                var device = Portal.CreateHardwareDevice(typeIdentifier, name, stationName);
                var items = new List<string>();

                foreach (var item in device.DeviceItems)
                {
                    items.Add(item.Name);
                }

                return new ResponseDeviceCreated
                {
                    Path = Portal.GetDevicePath(device),
                    Name = device.Name,
                    Items = items,
                    Message = items.Count == 0
                        ? $"Empty station '{device.Name}' created. Add a rack with 'hw_plug_module' (empty parentItemName, e.g. typeIdentifier 'System:Rack.ET200SP', position 0), then plug the head module into the rack. {SaveHint}"
                        : $"Hardware device '{device.Name}' created with items: {string.Join(", ", items)}. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "hw_plug_module", Title = "Plug hardware module", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Plug a new module into a rack or module of an existing device at a position. 'get_hardware_topology' shows the racks and what is already plugged")]
        public static ResponseMessage PlugHardwareModule(
            [Description("deviceName: path or name of the device, as 'get_devices' returns it")] string deviceName,
            [Description("parentItemName: name of the item to plug into, usually the rack (e.g. 'Rack_0' or 'Rail_0'). Empty plugs into the station itself, which is how a rack is added to an empty station (typeIdentifier e.g. 'System:Rack.ET200SP', position 0)")] string parentItemName,
            [Description("positionNumber: the slot to plug into (e.g. 1)")] int positionNumber,
            [Description("typeIdentifier: Openness type identifier of the module (e.g. 'OrderNumber:6ES7 131-6BH01-0BA0/V0.0'); 'hw_search_catalog' finds it")] string typeIdentifier,
            [Description("moduleName: the name for the new module")] string moduleName)
        {
            return Guarded(nameof(PlugHardwareModule), () =>
            {
                var module = Portal.PlugHardwareModule(deviceName, parentItemName, positionNumber, typeIdentifier, moduleName);

                return new ResponseMessage
                {
                    Message = $"Module '{module.Name}' plugged into '{(string.IsNullOrWhiteSpace(parentItemName) ? deviceName : parentItemName)}' at position {positionNumber}. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "hw_delete_device", Title = "Delete hardware device", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a hardware device (PLC, HMI, IO station) and everything in it from the project")]
        public static ResponseMessage DeleteHardwareDevice(
            [Description("deviceName: path or name of the device, as 'get_devices' returns it")] string deviceName)
        {
            return Guarded(nameof(DeleteHardwareDevice), () =>
            {
                Portal.DeleteHardwareDevice(deviceName);

                return new ResponseMessage
                {
                    Message = $"Hardware device '{deviceName}' deleted. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }

        #endregion

        // Building a PROFINET IO system is a fixed sequence, and each step refuses to run before
        // the previous one:
        //   1. net_connect_subnet        - PLC interface onto a subnet (created if missing)
        //   2. net_create_io_system      - IO system on that PLC interface
        //   3. net_connect_subnet        - IO device interface onto the same subnet
        //   4. net_connect_to_io_system  - IO device interface into the IO system
        // The tool descriptions repeat the step number so a model does not have to guess the order.

        #region network and subnets

        [WriteTool]
        [McpServerTool(Name = "net_connect_subnet", Title = "Connect interface to subnet", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Connect a network interface to a subnet; a PN/IE subnet of that name is created if it does not exist. Steps 1 and 3 of building a PROFINET IO system: connect the PLC interface, create the IO system ('net_create_io_system'), connect the IO device interface to the same subnet, then 'net_connect_to_io_system'")]
        public static ResponseMessage ConnectSubnet(
            [Description("deviceName: path or name of the device, as 'get_devices' returns it")] string deviceName,
            [Description("interfaceName: name of the PROFINET/Ethernet interface item (e.g. 'PROFINET interface_1')")] string interfaceName,
            [Description("subnetName: name of the subnet to connect to (e.g. 'PN/IE_1')")] string subnetName)
        {
            return Guarded(nameof(ConnectSubnet), () =>
            {
                Portal.ConnectToSubnet(deviceName, interfaceName, subnetName);

                return new ResponseMessage
                {
                    Message = $"Interface '{interfaceName}' of '{deviceName}' connected to subnet '{subnetName}'. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "net_disconnect_subnet", Title = "Disconnect interface from subnet", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Disconnect a network interface from its subnet. An IO device leaves its IO system with it")]
        public static ResponseMessage DisconnectSubnet(
            [Description("deviceName: path or name of the device, as 'get_devices' returns it")] string deviceName,
            [Description("interfaceName: name of the PROFINET/Ethernet interface item")] string interfaceName)
        {
            return Guarded(nameof(DisconnectSubnet), () =>
            {
                Portal.DisconnectSubnet(deviceName, interfaceName);

                return new ResponseMessage
                {
                    Message = $"Interface '{interfaceName}' of '{deviceName}' disconnected from its subnet. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }

        #endregion

        #region IO systems

        [WriteTool]
        [McpServerTool(Name = "net_create_io_system", Title = "Create IO system on controller", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a PROFINET IO system on a PLC network interface. Step 2 of building an IO system: the interface must already be connected to a subnet with 'net_connect_subnet'")]
        public static ResponseMessage CreateIoSystem(
            [Description("deviceName: path or name of the PLC device, as 'get_devices' returns it")] string deviceName,
            [Description("interfaceName: name of the PROFINET interface item of the PLC (e.g. 'PROFINET interface_1')")] string interfaceName,
            [Description("ioSystemName: name of the IO system to create (e.g. 'PROFINET IO-System (100)')")] string ioSystemName)
        {
            return Guarded(nameof(CreateIoSystem), () =>
            {
                Portal.CreateIoSystem(deviceName, interfaceName, ioSystemName);

                return new ResponseMessage
                {
                    Message = $"IO system '{ioSystemName}' created on interface '{interfaceName}' of '{deviceName}'. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "net_connect_to_io_system", Title = "Connect IO device to IO system", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Connect the network interface of an IO device to an existing IO system. Step 4 of building an IO system: the IO device interface must already be on the IO system's subnet ('net_connect_subnet')")]
        public static ResponseMessage ConnectToIoSystem(
            [Description("deviceName: path or name of the IO device, as 'get_devices' returns it")] string deviceName,
            [Description("interfaceName: name of the PROFINET interface item of the IO device")] string interfaceName,
            [Description("ioSystemName: name of the IO system to join")] string ioSystemName)
        {
            return Guarded(nameof(ConnectToIoSystem), () =>
            {
                Portal.ConnectToIoSystem(deviceName, interfaceName, ioSystemName);

                return new ResponseMessage
                {
                    Message = $"Interface '{interfaceName}' of '{deviceName}' connected to IO system '{ioSystemName}'. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }

        #endregion
    }
}
