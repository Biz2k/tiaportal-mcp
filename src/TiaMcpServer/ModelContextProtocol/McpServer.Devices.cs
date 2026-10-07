using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        #region devices

        [McpServerTool(Name = "hw_get_device_info", Title = "Get device info", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get info from a device from the current project/session")]
        public static ResponseDeviceInfo GetDeviceInfo(
            [Description("devicePath: path of the device as 'hw_get_devices' returns it, e.g. 'Group1/PC-System_1'. The device name alone, or the name of its CPU as the project tree shows it, is accepted when it is unique. A '/' inside a name is written '%2F'")] string devicePath)
        {
            try
            {
                var device = Portal.GetDevice(devicePath);

                if (device != null)
                {
                    var attributes = Helper.GetAttributeList(device);

                    return new ResponseDeviceInfo
                    {
                        Message = $"Device info retrieved from '{devicePath}'",
                        Path = Portal.GetDevicePath(device),
                        Name = device.Name,
                        Attributes = attributes,
                        Description = device.ToString(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Device not found at '{devicePath}'. Use 'hw_get_devices' to list the device paths.");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"retrieving device info from '{devicePath}'", ex);
            }
        }

        [McpServerTool(Name = "hw_get_device_item_info", Title = "Get device item info", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get a device item - a CPU, a module, an interface, a port - with all its attributes: name, value and accessMode. The parameters of a CPU are attributes of its item (cycle time, clock memory, startup, time of day, web server, PUT/GET ...); those with accessMode ReadWrite are set with 'hw_set_device_item_attributes'. For a network interface the attributes of its node are added as 'Node.Address', 'Node.SubnetMask', 'Node.RouterAddress' ...")]
        public static ResponseDeviceItemInfo GetDeviceItemInfo(
            [Description("deviceItemPath: device path followed by the item names, e.g. 'PC-System_1/Software PLC_1' or 'PLC_1/PROFINET interface_1'. The device name may be left out ('PLC_1'). A '/' inside a name is written '%2F'")] string deviceItemPath)
        {
            try
            {
                var deviceItem = Portal.GetDeviceItem(deviceItemPath);

                if (deviceItem != null)
                {
                    var attributes = Helper.GetAttributeList(deviceItem);

                    attributes.AddRange(Portal.GetNodeAttributes(deviceItem));

                    return new ResponseDeviceItemInfo
                    {
                        Message = $"Device item info retrieved from '{deviceItemPath}'",
                        Name = deviceItem.Name,
                        Attributes = attributes,
                        Description = deviceItem.ToString(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Device item not found at '{deviceItemPath}'. Use 'get_project_tree' to see the items of each device.");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"retrieving device item info from '{deviceItemPath}'", ex);
            }
        }

        [WriteTool]
        [McpServerTool(Name = "hw_set_device_item_attributes", Title = "Set attributes of a device item", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Set parameters of hardware: attributes of a CPU, a module, a network interface or a port, several at once and all or nothing. The parameters of a CPU are attributes of its device item - e.g. CycleMaximumCycleTime, CycleMinimumCycleTime, ClockMemoryByte, ClockMemoryByteAddress, SystemMemoryByte, StartupActionAfterPowerOn, WebserverActivate, ProtectionEnablePutGetCommunication, TimeOfDayLocalTimeZone; 'OPC UA_1' below the CPU has OpcUaServer; the IP address is on the interface item as 'Node.Address', 'Node.SubnetMask', 'Node.RouterAddress', 'Node.UseRouter'. Read the names, present values and which are writable with 'hw_get_device_item_info' first: names differ by CPU and firmware. A value is a boolean, a number or a string, as the attribute holds now; where the dialog of TIA Portal offers a choice, many attributes hold the number of the entry. Openness does not check the range of a value (a cycle time of 7 000 000 ms is stored): a value outside it shows only when the hardware is compiled in TIA Portal. Passwords and the protection of the PLC configuration are not set by the server. The change needs a hardware compile and download to take effect")]
        public static ResponseAttributesSet SetDeviceItemAttributes(
            [Description("deviceItemPath: device path followed by the item names, e.g. 'Station_1/PLC_1' for the CPU or 'Station_1/PLC_1/PROFINET interface_1' for its interface. A '/' inside a name is written '%2F'")] string deviceItemPath,
            [Description("attributes: the attributes to set, by name, e.g. {\"CycleMaximumCycleTime\": 200, \"ClockMemoryByte\": true} or {\"Node.Address\": \"192.168.0.10\"}")] Dictionary<string, System.Text.Json.JsonElement> attributes)
        {
            return Guarded(nameof(SetDeviceItemAttributes), () =>
            {
                var changes = Portal.SetDeviceItemAttributes(deviceItemPath, attributes);

                return new ResponseAttributesSet
                {
                    Path = deviceItemPath,
                    Changes = changes,
                    Message = $"{changes.Count} attribute(s) of '{deviceItemPath}' set. The change is in memory; call 'save_project' to persist it. It takes effect in the PLC after a hardware compile and download.",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["pendingSave"] = true
                    }
                };
            });
        }

        [McpServerTool(Name = "hw_get_devices", Title = "Get devices", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get a list of all devices in the project/session: path, name, type and the names of the top-level items. 'includeAttributes' adds every attribute of each device (long); 'hw_get_device_info' gives them for one device")]
        public static ResponseDevices GetDevices(
            [Description("includeAttributes: true adds the full attribute list of every device (default false)")] bool includeAttributes = false,
            [Description(Paging.LimitText)] int limit = 500,
            [Description(Paging.OffsetText)] int offset = 0)
        {
            try
            {
                Portal.EnsureProjectOpen();

                var list = Portal.GetDevices();
                var responseList = new List<ResponseDeviceInfo>();

                if (list != null)
                {
                    var page = Paging.Page(list.Where(d => d != null).ToList(), limit, offset);

                    foreach (var device in page.Items)
                    {
                        if (device != null)
                        {
                            responseList.Add(new ResponseDeviceInfo
                            {
                                Path = Portal.GetDevicePath(device),
                                Name = device.Name,
                                Type = device.TypeIdentifier,
                                Items = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(device.DeviceItems, i => i.Name)),
                                Attributes = includeAttributes ? Helper.GetAttributeList(device) : null,
                                Description = includeAttributes ? device.ToString() : null
                            });
                        }
                    }

                    return new ResponseDevices
                    {
                        Message = "Devices retrieved" + page.Note("includeAttributes=false"),
                        Items = responseList,
                        Meta = page.Meta(new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        })
                    };
                }
                else
                {
                    throw new McpException($"Failed retrieving devices");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"retrieving devices", ex);
            }
        }

        #endregion
    }
}

