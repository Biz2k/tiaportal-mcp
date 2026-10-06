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

        [McpServerTool(Name = "hw_get_device_item_info", Title = "Get device item info", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get info from a device item from the current project/session")]
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
                throw Failure($"retrieving device item info from '{deviceItemPath}'", ex);
            }
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

