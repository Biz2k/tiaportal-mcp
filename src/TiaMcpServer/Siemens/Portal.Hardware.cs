using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using Siemens.Engineering;
using System;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering.HW.Features;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        public List<ResponseHardwareDevice> GetHardwareTopology()
        {
            _logger?.LogInformation("Getting hardware topology...");

            if (IsProjectNull())
            {
                return new List<ResponseHardwareDevice>();
            }

            var devices = new List<ResponseHardwareDevice>();

            if (_project?.Devices != null)
            {
                foreach (Device device in _project.Devices)
                {
                    devices.Add(ProcessDevice(device));
                }
            }

            if (_project?.DeviceGroups != null)
            {
                ProcessDeviceGroups(_project.DeviceGroups, devices);
            }

            if (_project?.UngroupedDevicesGroup?.Devices != null)
            {
                foreach (Device device in _project.UngroupedDevicesGroup.Devices)
                {
                    devices.Add(ProcessDevice(device));
                }
            }

            return devices;
        }

        private void ProcessDeviceGroups(DeviceUserGroupComposition groups, List<ResponseHardwareDevice> devices)
        {
            foreach (DeviceUserGroup group in groups)
            {
                if (group.Devices != null)
                {
                    foreach (Device device in group.Devices)
                    {
                        devices.Add(ProcessDevice(device));
                    }
                }
                
                if (group.Groups != null)
                {
                    ProcessDeviceGroups(group.Groups, devices);
                }
            }
        }

        private ResponseHardwareDevice ProcessDevice(Device device)
        {
            var res = new ResponseHardwareDevice
            {
                Name = device.Name,
                DeviceItems = new List<ResponseHardwareItem>()
            };

            if (device.DeviceItems != null)
            {
                var items = new List<ResponseHardwareItem>();
                foreach (DeviceItem item in device.DeviceItems)
                {
                    items.Add(ProcessDeviceItem(item));
                }
                res.DeviceItems = items;
            }

            return res;
        }

        private ResponseHardwareItem ProcessDeviceItem(DeviceItem item)
        {
            string typeIdent = "";
            try { typeIdent = item.TypeIdentifier ?? ""; } catch { }

            string classif = "";
            try { classif = item.Classification.ToString(); } catch { }

            string article = "";
            string firmware = "";

            // Parse ArticleNumber and Firmware from TypeIdentifier, typically "OrderNumber:6ES7 131-6BH01-0BA0/V0.0"
            if (!string.IsNullOrEmpty(typeIdent) && typeIdent.StartsWith("OrderNumber:", StringComparison.OrdinalIgnoreCase))
            {
                var val = typeIdent.Substring("OrderNumber:".Length);
                var idx = val.IndexOf('/');
                if (idx > 0)
                {
                    article = val.Substring(0, idx);
                    firmware = val.Substring(idx + 1);
                }
                else
                {
                    article = val;
                }
            }
            else if (!string.IsNullOrEmpty(typeIdent))
            {
                article = typeIdent;
            }

            var res = new ResponseHardwareItem
            {
                Name = item.Name,
                TypeIdentifier = typeIdent,
                Classification = classif,
                ArticleNumber = article,
                FirmwareVersion = firmware,
                DeviceItems = new List<ResponseHardwareItem>(),
                NetworkInterfaces = GetNetworkInterfaces(item)
            };

            if (item.DeviceItems != null && item.DeviceItems.Count > 0)
            {
                var children = new List<ResponseHardwareItem>();
                foreach (DeviceItem child in item.DeviceItems)
                {
                    children.Add(ProcessDeviceItem(child));
                }
                res.DeviceItems = children;
            }

            return res;
        }

        private List<ResponseNetworkInterface>? GetNetworkInterfaces(DeviceItem item)
        {
            var results = new List<ResponseNetworkInterface>();
            try
            {
                NetworkInterface netIf = item.GetService<NetworkInterface>();
                if (netIf != null && netIf.Nodes != null)
                {
                    foreach (var node in netIf.Nodes)
                    {
                        string ip = "";
                        try { ip = node.GetAttribute("Address").ToString() ?? ""; } catch { }
                        
                        string subnetName = "";
                        if (node.ConnectedSubnet != null)
                        {
                            subnetName = node.ConnectedSubnet.Name;
                        }
                        
                        if (!string.IsNullOrEmpty(ip) || !string.IsNullOrEmpty(subnetName))
                        {
                            results.Add(new ResponseNetworkInterface
                            {
                                Name = item.Name,
                                Address = ip,
                                Subnet = subnetName
                            });
                        }
                    }
                }
            }
            catch { }
            
            return results.Count > 0 ? results : null;
        }

        public Device CreateHardwareDevice(string typeIdentifier, string name)
        {
            if (IsProjectNull()) throw new InvalidOperationException("Project is not open");

            _logger?.LogInformation($"Creating device '{name}' with type '{typeIdentifier}'");
            return _project!.Devices.CreateWithItem(typeIdentifier, name, name);
        }

        public DeviceItem PlugHardwareModule(string deviceName, string parentItemName, int positionNumber, string typeIdentifier, string moduleName)
        {
            if (IsProjectNull()) throw new InvalidOperationException("Project is not open");

            var device = _project!.Devices.Find(deviceName);
            if (device == null)
            {
                // check ungrouped devices
                device = _project.UngroupedDevicesGroup?.Devices.Find(deviceName);
                if (device == null)
                    throw new InvalidOperationException($"Device '{deviceName}' not found");
            }

            var parentItem = FindDeviceItem(device.DeviceItems, parentItemName);
            if (parentItem == null) throw new InvalidOperationException($"Parent item '{parentItemName}' not found in device '{deviceName}'");

            _logger?.LogInformation($"Plugging module '{moduleName}' ({typeIdentifier}) at position {positionNumber} in '{parentItemName}'");
            
            // Check if we can plug
            if (parentItem.CanPlugNew(typeIdentifier, moduleName, positionNumber))
            {
                return parentItem.PlugNew(typeIdentifier, moduleName, positionNumber);
            }
            else
            {
                throw new InvalidOperationException($"Cannot plug module '{typeIdentifier}' at position {positionNumber} in '{parentItemName}'");
            }
        }

        private DeviceItem? FindDeviceItem(DeviceItemComposition items, string targetName)
        {
            foreach (DeviceItem item in items)
            {
                if (item.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                    return item;

                if (item.DeviceItems != null && item.DeviceItems.Count > 0)
                {
                    var found = FindDeviceItem(item.DeviceItems, targetName);
                    if (found != null) return found;
                }
            }
            return null;
        }

        public void DeleteHardwareDevice(string deviceName)
        {
            if (IsProjectNull()) throw new InvalidOperationException("Project is not open");

            var device = _project!.Devices.Find(deviceName);
            if (device == null)
            {
                device = _project.UngroupedDevicesGroup?.Devices.Find(deviceName);
                if (device == null)
                    throw new InvalidOperationException($"Device '{deviceName}' not found");
            }

            _logger?.LogInformation($"Deleting device '{deviceName}'");
            device.Delete();
        }

        public void ConnectToSubnet(string deviceName, string interfaceName, string subnetName)
        {
            if (IsProjectNull()) throw new InvalidOperationException("Project is not open");

            var device = _project!.Devices.Find(deviceName) ?? _project.UngroupedDevicesGroup?.Devices.Find(deviceName);
            if (device == null) throw new InvalidOperationException($"Device '{deviceName}' not found");

            var interfaceItem = FindDeviceItem(device.DeviceItems, interfaceName);
            if (interfaceItem == null) throw new InvalidOperationException($"Interface '{interfaceName}' not found in device '{deviceName}'");

            var netIf = interfaceItem.GetService<NetworkInterface>();
            if (netIf == null || netIf.Nodes == null || netIf.Nodes.Count == 0)
                throw new InvalidOperationException($"Interface '{interfaceName}' has no nodes to connect");

            var subnet = _project.Subnets.Find(subnetName);
            if (subnet == null)
            {
                _logger?.LogInformation($"Creating PN/IE subnet '{subnetName}'");
                subnet = _project.Subnets.Create("System:Subnet.Ethernet", subnetName);
            }

            _logger?.LogInformation($"Connecting interface '{interfaceName}' to subnet '{subnetName}'");
            netIf.Nodes[0].ConnectToSubnet(subnet);
        }

        public void DisconnectSubnet(string deviceName, string interfaceName)
        {
            if (IsProjectNull()) throw new InvalidOperationException("Project is not open");

            var device = _project!.Devices.Find(deviceName) ?? _project.UngroupedDevicesGroup?.Devices.Find(deviceName);
            if (device == null) throw new InvalidOperationException($"Device '{deviceName}' not found");

            var interfaceItem = FindDeviceItem(device.DeviceItems, interfaceName);
            if (interfaceItem == null) throw new InvalidOperationException($"Interface '{interfaceName}' not found in device '{deviceName}'");

            var netIf = interfaceItem.GetService<NetworkInterface>();
            if (netIf == null || netIf.Nodes == null || netIf.Nodes.Count == 0)
                throw new InvalidOperationException($"Interface '{interfaceName}' has no nodes to disconnect");

            _logger?.LogInformation($"Disconnecting interface '{interfaceName}' from subnet");
            netIf.Nodes[0].DisconnectFromSubnet();
        }
    }
}
