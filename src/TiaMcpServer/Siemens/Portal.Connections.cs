using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.CommunicationConnections;
using Siemens.Engineering.HW.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TiaMcpServer.Siemens
{
    // Communication connections between controllers (S7, TCP, ISO-on-TCP, ISO, UDP) and the HMI
    // connections that appear beside them.
    //
    // Callers: the tools net_get_connections, net_create_connection and net_delete_connection in
    // McpServer.Hardware.cs. Reads and writes no data files.
    //
    // Openness findings (V21):
    //   - Only the CPU device item (the parent of the PLC software) offers CommunicationManagement;
    //     other items return null. A connection is listed by both of its ends.
    //   - A connection is made between two network nodes, so both devices need an interface on a
    //     common subnet (see 'net_connect_subnet'); the HMI code in Portal.Unified.Tags.cs works the same way.
    public partial class Portal
    {
        private static readonly string[] ConnectionSkippedProperties =
            { "Parent", "LocalInterface", "PartnerInterface", "LocalTarget", "PartnerTarget" };

        public List<Dictionary<string, object?>> GetCommunicationConnections(string? devicePath)
        {
            return Operation.Run(_logger, nameof(GetCommunicationConnections), PortalErrorCode.InvalidState,
                () =>
                {
                    if (_project == null)
                    {
                        throw new PortalException(PortalErrorCode.InvalidState, "No project is open. Call 'open_tia_project' first.");
                    }

                    var devices = string.IsNullOrWhiteSpace(devicePath)
                        ? _project.Devices.ToList()
                        : new List<Device> { RequireDevice(devicePath!) };

                    var result = new List<Dictionary<string, object?>>();

                    foreach (var device in devices)
                    {
                        foreach (var item in AllDeviceItems(device.DeviceItems))
                        {
                            var management = item.GetService<CommunicationManagement>();

                            if (management == null)
                            {
                                continue;
                            }

                            foreach (var connection in management.Connections)
                            {
                                var row = new Dictionary<string, object?>
                                {
                                    ["device"] = GetDevicePath(device),
                                    ["item"] = item.Name,
                                    ["type"] = connection.ConnectionType.ToString(),
                                    ["name"] = ReadProperty(connection, "LocalConnectionName"),
                                    ["partner"] = DescribeTarget(connection.PartnerTarget),
                                    ["subnet"] = connection.LocalSubnetName
                                };

                                // reflection does not promise an order, and the order of two identical calls differed (found by the grouped smoke run, 2026-10-08)
                                foreach (var property in connection.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name, StringComparer.Ordinal))
                                {
                                    if (ConnectionSkippedProperties.Contains(property.Name) || row.ContainsKey(ToCamel(property.Name)))
                                    {
                                        continue;
                                    }

                                    var type = property.PropertyType;

                                    if (property.GetIndexParameters().Length == 0 && (type == typeof(string) || type == typeof(bool) || type == typeof(int) || type == typeof(long) || type.IsEnum))
                                    {
                                        row[ToCamel(property.Name)] = ReadProperty(connection, property.Name);
                                    }
                                }

                                result.Add(row);
                            }
                        }
                    }

                    return result;
                });
        }

        /// <summary>Creates a connection between two PLCs. Returns a description of what was made.</summary>
        public string CreateCommunicationConnection(
            string localPlcPath, string partnerPlcPath, string connectionType, string? localInterface, string? partnerInterface, string? name)
        {
            return Operation.Run(_logger, nameof(CreateCommunicationConnection), PortalErrorCode.CreateFailed,
                () =>
                {
                    var local = RequirePlcItem(localPlcPath, "localPlc");
                    var partner = RequirePlcItem(partnerPlcPath, "partnerPlc");

                    var management = local.GetService<CommunicationManagement>()
                        ?? throw new PortalException(PortalErrorCode.NotSupported, $"'{localPlcPath}' does not support communication connections.");

                    var localNodes = NetworkNodes(new[] { local }).ToList();
                    var partnerNodes = NetworkNodes(new[] { partner }).ToList();

                    var localPick = PickNodes(localNodes, localInterface, "localInterface", localPlcPath);
                    var partnerPick = PickNodes(partnerNodes, partnerInterface, "partnerInterface", partnerPlcPath);

                    var pair = (from l in localPick
                                from r in partnerPick
                                where l.Node.ConnectedSubnet != null && r.Node.ConnectedSubnet != null
                                      && l.Node.ConnectedSubnet.Name == r.Node.ConnectedSubnet.Name
                                select new { Local = l, Remote = r }).FirstOrDefault()
                               ?? throw new PortalException(PortalErrorCode.InvalidState,
                                   $"'{localPlcPath}' and '{partnerPlcPath}' have no interfaces on a common subnet. " +
                                   $"Local: {DescribeNodes(localNodes)}. Partner: {DescribeNodes(partnerNodes)}. Connect them with 'net_connect_subnet' first.");

                    var connections = management.Connections;
                    var node = pair.Local.Node;
                    var remoteNode = pair.Remote.Node;

                    Connection created;

                    switch ((connectionType ?? string.Empty).Trim().ToLowerInvariant())
                    {
                        case "s7": created = connections.Create<S7Connection>(node, partner, remoteNode); break;
                        case "tcp": created = connections.Create<TcpConnection>(node, partner, remoteNode); break;
                        case "isoontcp": created = connections.Create<IsoOnTcpConnection>(node, partner, remoteNode); break;
                        case "iso": created = connections.Create<IsoConnection>(node, partner, remoteNode); break;
                        case "udp": created = connections.Create<UdpConnection>(node, partner, remoteNode); break;
                        default:
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                $"Unknown connectionType '{connectionType}'. Allowed: s7, tcp, isoOnTcp, iso, udp.");
                    }

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        created.GetType().GetProperty("LocalConnectionName")!.SetValue(created, name!.Trim());
                    }

                    return $"{created.ConnectionType} '{ReadProperty(created, "LocalConnectionName")}' from '{local.Name}' ({pair.Local.Interface}) to '{partner.Name}' ({pair.Remote.Interface}) over subnet '{node.ConnectedSubnet.Name}'";
                },
                ("localPlc", localPlcPath), ("partnerPlc", partnerPlcPath), ("connectionType", connectionType ?? string.Empty));
        }

        public void DeleteCommunicationConnection(string localPlcPath, string connectionName)
        {
            Operation.Run(_logger, nameof(DeleteCommunicationConnection), PortalErrorCode.DeleteFailed,
                () =>
                {
                    var local = RequirePlcItem(localPlcPath, "localPlc");

                    var management = local.GetService<CommunicationManagement>()
                        ?? throw new PortalException(PortalErrorCode.NotSupported, $"'{localPlcPath}' does not support communication connections.");

                    var connection = management.Connections
                        .FirstOrDefault(c => string.Equals(ReadProperty(c, "LocalConnectionName") as string, connectionName, StringComparison.OrdinalIgnoreCase))
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Connection '{connectionName}' not found on '{localPlcPath}'. " +
                            (management.Connections.Count == 0
                                ? "It has no connections."
                                : $"Available: {string.Join(", ", management.Connections.Select(c => $"'{ReadProperty(c, "LocalConnectionName")}'"))}."));

                    connection.Delete();
                },
                ("localPlc", localPlcPath), ("connectionName", connectionName));
        }

        private DeviceItem RequirePlcItem(string path, string parameter)
        {
            var container = GetSoftwareContainer(path);

            if (!(container?.Software is global::Siemens.Engineering.SW.PlcSoftware) || !(container.Parent is DeviceItem item))
            {
                throw new PortalException(PortalErrorCode.NotFound,
                    $"{parameter}: no PLC found at '{path}'. Pass the path of the PLC as the plc_* tools take it, e.g. 'PLC_1' or 'Station_1/PLC_1'.");
            }

            return item;
        }

        private static IEnumerable<DeviceItem> AllDeviceItems(DeviceItemComposition items)
        {
            foreach (var item in items)
            {
                yield return item;

                foreach (var nested in AllDeviceItems(item.DeviceItems))
                {
                    yield return nested;
                }
            }
        }

        private string? DescribeTarget(DeviceItem? target)
        {
            if (target == null)
            {
                return null;
            }

            IEngineeringObject? owner = target.Parent;

            while (owner != null && owner is not Device)
            {
                owner = owner.Parent;
            }

            return $"{(owner is Device d ? GetDevicePath(d) : "?")}/{target.Name}";
        }

        private static object? ReadProperty(object target, string name)
        {
            try
            {
                var value = target.GetType().GetProperty(name)?.GetValue(target);

                return value is Enum ? value.ToString() : value;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ToCamel(string name)
        {
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
