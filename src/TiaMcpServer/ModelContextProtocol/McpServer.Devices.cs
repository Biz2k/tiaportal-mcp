using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
            [Description("devicePath: path of the device as 'get_devices' returns it, e.g. 'Group1/PC-System_1'. The device name alone, or the name of its CPU as the project tree shows it, is accepted when it is unique. A '/' inside a name is written '%2F'")] string devicePath)
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
                    throw new McpException($"Device not found at '{devicePath}'. Use 'get_devices' to list the device paths.");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving device info from '{devicePath}': {Why(ex)}", ex);
            }
        }

        [McpServerTool(Name = "get_device_item_info", Title = "Get device item info", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get info from a device item from the current project/session")]
        public static ResponseDeviceItemInfo GetDeviceItemInfo(
            [Description("deviceItemPath: device path followed by the item names, e.g. 'PC-System_1/Software PLC_1' or 'PLC_1/PROFINET interface_1'. The device name may be left out ('PLC_1'). A '/' inside a name is written '%2F'")] string deviceItemPath)
        {
            try
            {
                var deviceItem = Portal.GetDeviceItem(deviceItemPath);

                if (deviceItem != null)
                {
                    var attributes = Helper.GetAttributeList(deviceItem);

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
                throw new McpException($"Unexpected error retrieving device item info from '{deviceItemPath}': {Why(ex)}", ex);
            }
        }

        [McpServerTool(Name = "get_devices", Title = "Get devices", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get a list of all devices in the project/session")]
        public static ResponseDevices GetDevices()
        {
            try
            {
                var list = Portal.GetDevices();
                var responseList = new List<ResponseDeviceInfo>();

                if (list != null)
                {
                    foreach (var device in list)
                    {
                        if (device != null)
                        {
                            var attributes = Helper.GetAttributeList(device);
                            responseList.Add(new ResponseDeviceInfo
                            {
                                Path = Portal.GetDevicePath(device),
                                Name = device.Name,
                                Attributes = attributes,
                                Description = device.ToString()
                            });
                        }
                    }

                    return new ResponseDevices
                    {
                        Message = "Devices retrieved",
                        Items = responseList,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed retrieving devices");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving devices: {Why(ex)}", ex);
            }
        }

        #endregion
    }
}

