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
                Path = GetDevicePath(device),
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

        // Hardware and network edits.
        //
        // Callers: the hw_* and net_* tools in McpServer.Hardware.cs, registered only under
        // '--allow-write'. Affected API: the methods keep their names; CreateHardwareDevice gained
        // an optional station name and now returns the device for System: identifiers too.
        // Reads/writes no data files; changes stay in the open project until it is saved.
        //
        // Every method runs inside Operation.Run, like the rest of the portal layer: that is what
        // serializes it against concurrent tool calls and turns an Openness failure into a
        // PortalException that names the device it was about.

        #region hardware (write)

        /// <summary>
        /// Creates a device. How depends on what the identifier names:
        /// - "OrderNumber:..." or "GSD:..." names a head module (a CPU, an interface module);
        ///   the station is created around it.
        /// - "System:Device...." names a bare station type (an empty rack); the head module is
        ///   plugged afterwards with PlugHardwareModule. CreateWithItem rejects these.
        /// </summary>
        /// <param name="name">Name of the head module, or of the station for a System: identifier.</param>
        /// <param name="stationName">Name of the station; empty uses <paramref name="name"/>.</param>
        public Device CreateHardwareDevice(string typeIdentifier, string name, string stationName = "")
        {
            return Operation.Run(_logger, nameof(CreateHardwareDevice), PortalErrorCode.CreateFailed,
                () =>
                {
                    if (IsProjectNull())
                    {
                        throw new PortalException(PortalErrorCode.InvalidState, NoProjectMessage);
                    }

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, "The name must not be empty.");
                    }

                    var identifier = (typeIdentifier ?? string.Empty).Trim();
                    var station = string.IsNullOrWhiteSpace(stationName) ? name : stationName.Trim();

                    if (EnumerateDevices().Any(d => d.Device.Name.Equals(station, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"A device named '{station}' already exists. Choose another name.");
                    }

                    if (identifier.StartsWith("OrderNumber:", StringComparison.OrdinalIgnoreCase)
                        || identifier.StartsWith("GSD:", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogInformation("Creating device {Station} with head module {Name} ({Identifier})", station, name, identifier);

                        return _project!.Devices.CreateWithItem(identifier, name, station);
                    }

                    if (identifier.StartsWith("System:", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogInformation("Creating empty station {Station} ({Identifier})", station, identifier);

                        return _project!.Devices.Create(identifier, station);
                    }

                    throw new PortalException(PortalErrorCode.InvalidParams,
                        $"'{typeIdentifier}' is not a type identifier. Use 'OrderNumber:<article number>/<firmware version>' for a head module " +
                        "(e.g. 'OrderNumber:6ES7 516-3AN02-0AB0/V2.9'), 'GSD:<file>/<type>' for a GSD device, or 'System:Device.<type>' " +
                        "for an empty station (e.g. 'System:Device.ET200SP'). 'hw_search_catalog' finds identifiers by article number or name.");
                },
                ("typeIdentifier", typeIdentifier), ("name", name), ("stationName", stationName));
        }

        public DeviceItem PlugHardwareModule(string deviceName, string parentItemName, int positionNumber, string typeIdentifier, string moduleName)
        {
            return Operation.Run(_logger, nameof(PlugHardwareModule), PortalErrorCode.CreateFailed,
                () =>
                {
                    var device = RequireDevice(deviceName);

                    // An empty parent means the station itself. That is where a rack goes: a
                    // station created from a 'System:Device.' identifier has no items at all.
                    if (string.IsNullOrWhiteSpace(parentItemName))
                    {
                        if (!device.CanPlugNew(typeIdentifier, moduleName, positionNumber))
                        {
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                $"TIA Portal will not plug '{typeIdentifier}' at position {positionNumber} directly into station '{device.Name}'. " +
                                "Only a rack goes there (e.g. 'System:Rack.ET200SP' at position 0); modules are plugged into the rack. " +
                                (device.DeviceItems.Any()
                                    ? $"The station already has: {string.Join(", ", device.DeviceItems.Select(i => $"'{i.Name}'"))}."
                                    : "The station is still empty."));
                        }

                        return device.PlugNew(typeIdentifier, moduleName, positionNumber);
                    }

                    var parentItem = RequireDeviceItem(device, parentItemName);

                    if (!parentItem.CanPlugNew(typeIdentifier, moduleName, positionNumber))
                    {
                        // Items lists what is plugged into the rack; DeviceItems would be its
                        // own sub-items, which a rack does not have.
                        var occupied = parentItem.Items
                            .OrderBy(i => i.PositionNumber)
                            .Select(i => $"{i.PositionNumber} ({i.Name})")
                            .ToList();

                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"TIA Portal will not plug '{typeIdentifier}' at position {positionNumber} of '{parentItem.Name}'. " +
                            (occupied.Count == 0
                                ? "No position of this item is occupied, so the type identifier does not fit this rack or is not installed. "
                                : $"Occupied positions: {string.Join(", ", occupied)}. ") +
                            "Check the position, and the identifier with 'hw_search_catalog'.");
                    }

                    _logger?.LogInformation("Plugging {Module} ({Identifier}) at position {Position} of {Parent}", moduleName, typeIdentifier, positionNumber, parentItem.Name);

                    return parentItem.PlugNew(typeIdentifier, moduleName, positionNumber);
                },
                ("deviceName", deviceName), ("parentItemName", parentItemName), ("positionNumber", positionNumber),
                ("typeIdentifier", typeIdentifier), ("moduleName", moduleName));
        }

        public void DeleteHardwareDevice(string deviceName)
        {
            Operation.Run(_logger, nameof(DeleteHardwareDevice), PortalErrorCode.DeleteFailed,
                () =>
                {
                    var device = RequireDevice(deviceName);

                    _logger?.LogInformation("Deleting device {Device}", device.Name);
                    device.Delete();
                },
                ("deviceName", deviceName));
        }

        #endregion

        #region network (write)

        public void ConnectToSubnet(string deviceName, string interfaceName, string subnetName)
        {
            Operation.Run(_logger, nameof(ConnectToSubnet), PortalErrorCode.CreateFailed,
                () =>
                {
                    if (string.IsNullOrWhiteSpace(subnetName))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, "The subnet name must not be empty.");
                    }

                    var node = RequireNode(deviceName, interfaceName);
                    var subnet = _project!.Subnets.Find(subnetName);

                    if (subnet == null)
                    {
                        _logger?.LogInformation("Creating PN/IE subnet {Subnet}", subnetName);
                        subnet = _project.Subnets.Create("System:Subnet.Ethernet", subnetName);
                    }

                    node.ConnectToSubnet(subnet);
                },
                ("deviceName", deviceName), ("interfaceName", interfaceName), ("subnetName", subnetName));
        }

        public void DisconnectSubnet(string deviceName, string interfaceName)
        {
            Operation.Run(_logger, nameof(DisconnectSubnet), PortalErrorCode.DeleteFailed,
                () => RequireNode(deviceName, interfaceName).DisconnectFromSubnet(),
                ("deviceName", deviceName), ("interfaceName", interfaceName));
        }

        public void CreateIoSystem(string deviceName, string interfaceName, string ioSystemName)
        {
            Operation.Run(_logger, nameof(CreateIoSystem), PortalErrorCode.CreateFailed,
                () =>
                {
                    var networkInterface = RequireNetworkInterface(deviceName, interfaceName);

                    var ioController = networkInterface.IoControllers.FirstOrDefault()
                        ?? throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Interface '{interfaceName}' of '{deviceName}' is not an IO controller, so it cannot own an IO system. " +
                            "Call this on the PROFINET interface of a PLC.");

                    if (networkInterface.Nodes.FirstOrDefault()?.ConnectedSubnet == null)
                    {
                        throw new PortalException(PortalErrorCode.InvalidState,
                            $"Interface '{interfaceName}' of '{deviceName}' is not connected to a subnet. " +
                            "Call 'net_connect_subnet' first: an IO system lives on a subnet.");
                    }

                    ioController.CreateIoSystem(ioSystemName);
                },
                ("deviceName", deviceName), ("interfaceName", interfaceName), ("ioSystemName", ioSystemName));
        }

        public void ConnectToIoSystem(string deviceName, string interfaceName, string ioSystemName)
        {
            Operation.Run(_logger, nameof(ConnectToIoSystem), PortalErrorCode.CreateFailed,
                () =>
                {
                    var networkInterface = RequireNetworkInterface(deviceName, interfaceName);

                    var ioConnector = networkInterface.IoConnectors.FirstOrDefault()
                        ?? throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Interface '{interfaceName}' of '{deviceName}' is not an IO device interface, so it cannot join an IO system.");

                    var available = _project!.Subnets.SelectMany(s => s.IoSystems).ToList();

                    var ioSystem = available.FirstOrDefault(s => s.Name.Equals(ioSystemName, StringComparison.OrdinalIgnoreCase))
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"IO system '{ioSystemName}' not found. " +
                            (available.Count == 0
                                ? "The project has no IO system yet; create one with 'net_create_io_system' on the PLC interface."
                                : $"Available: {string.Join(", ", available.Select(s => $"'{s.Name}'"))}."));

                    if (networkInterface.Nodes.FirstOrDefault()?.ConnectedSubnet == null)
                    {
                        throw new PortalException(PortalErrorCode.InvalidState,
                            $"Interface '{interfaceName}' of '{deviceName}' is not connected to a subnet. " +
                            "Call 'net_connect_subnet' with the subnet of the IO system first.");
                    }

                    ioConnector.ConnectToIoSystem(ioSystem);
                },
                ("deviceName", deviceName), ("interfaceName", interfaceName), ("ioSystemName", ioSystemName));
        }

        #endregion

        #region hardware lookup

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

        private DeviceItem RequireDeviceItem(Device device, string itemName)
        {
            return FindDeviceItem(device.DeviceItems, itemName)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"Item '{itemName}' not found in device '{device.Name}'. Its top-level items are: " +
                    string.Join(", ", device.DeviceItems.Select(i => $"'{i.Name}'")) +
                    ". 'get_hardware_topology' shows the full item tree.");
        }

        private NetworkInterface RequireNetworkInterface(string deviceName, string interfaceName)
        {
            var device = RequireDevice(deviceName);
            var item = RequireDeviceItem(device, interfaceName);

            return item.GetService<NetworkInterface>()
                ?? throw new PortalException(PortalErrorCode.InvalidParams,
                    $"'{interfaceName}' of '{device.Name}' is not a network interface. Pass an interface item such as 'PROFINET interface_1'.");
        }

        private Node RequireNode(string deviceName, string interfaceName)
        {
            return RequireNetworkInterface(deviceName, interfaceName).Nodes.FirstOrDefault()
                ?? throw new PortalException(PortalErrorCode.InvalidState,
                    $"Interface '{interfaceName}' of '{deviceName}' has no network node to connect.");
        }

        /// <summary>
        /// Searches the installed hardware catalog. Read through 'dynamic': the catalog API
        /// arrived in a later Openness version than this server's oldest supported one, and a
        /// typed reference would stop the assembly from loading there.
        /// </summary>
        public List<ResponseCatalogEntry> SearchHardwareCatalog(string query, int maxResults = 20)
        {
            return Operation.Run(_logger, nameof(SearchHardwareCatalog), PortalErrorCode.NotSupported,
                () =>
                {
                    if (_portal == null)
                    {
                        throw new PortalException(PortalErrorCode.InvalidState, "Not connected to TIA Portal. Call 'connect' first.");
                    }

                    if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 3)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            "Pass at least three characters of an article number or product name, e.g. '6ES7 155-6AU01' or 'IM 155-6 PN'.");
                    }

                    dynamic portal = _portal;
                    dynamic entries;

                    try
                    {
                        entries = portal.HardwareCatalog.Find(query.Trim());
                    }
                    catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
                    {
                        throw new PortalException(PortalErrorCode.NotSupported,
                            "This TIA Portal version does not offer a hardware catalog search through Openness.", null, ex);
                    }

                    var result = new List<ResponseCatalogEntry>();

                    foreach (var entry in entries)
                    {
                        if (result.Count >= Math.Max(1, maxResults))
                        {
                            break;
                        }

                        result.Add(new ResponseCatalogEntry
                        {
                            TypeIdentifier = ReadCatalogText(() => entry.TypeIdentifier),
                            ArticleNumber = ReadCatalogText(() => entry.ArticleNumber),
                            Version = ReadCatalogText(() => entry.Version),
                            TypeName = ReadCatalogText(() => entry.TypeName),
                            Description = ReadCatalogText(() => entry.Description),
                            CatalogPath = ReadCatalogText(() => entry.CatalogPath)
                        });
                    }

                    return result;
                },
                ("query", query));
        }

        private static string? ReadCatalogText(Func<object?> read)
        {
            try
            {
                return read()?.ToString();
            }
            catch (Exception)
            {
                // Not every catalog entry carries every attribute.
                return null;
            }
        }

        #endregion
    }
}
