using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        // The project tree as a flat list of nodes, each with the path string the other tools accept.
        //
        // Callers: 'get_project_tree' with structured = true. Reads the device tree only; changes nothing.
        // The text tree stays what it was; this is the form a client can use without parsing it.

        /// <summary>
        /// Groups, devices and device items of the project, in tree order. <paramref name="depth"/> counts the levels
        /// from the top (1 = top-level groups and devices; 0 = no limit); <paramref name="filter"/> is a regular
        /// expression on the name or path, and keeps the matching nodes with the nodes above them.
        /// </summary>
        public List<ProjectNode> GetProjectNodes(int depth = 0, string filter = "")
        {
            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, NoProjectMessage);
            }

            Regex? pattern = null;

            if (!string.IsNullOrWhiteSpace(filter))
            {
                try
                {
                    pattern = new Regex(filter, RegexOptions.IgnoreCase);
                }
                catch (ArgumentException ex)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, $"'{filter}' is not a regular expression: {ex.Message}");
                }
            }

            var nodes = new List<ProjectNode>();
            var limit = depth <= 0 ? int.MaxValue : depth;

            void AddItems(IEnumerable<DeviceItem> items, string devicePath, string groupPath, string itemPath, int level)
            {
                foreach (var item in items)
                {
                    var path = itemPath.Length == 0 ? $"{devicePath}/{EscapeSegment(item.Name)}" : $"{itemPath}/{EscapeSegment(item.Name)}";
                    var node = new ProjectNode { Level = level, Kind = "deviceItem", Name = item.Name, Path = path, Type = SafeTypeIdentifier(item) };

                    if (item.GetService<SoftwareContainer>() != null)
                    {
                        node.SoftwarePath = new[]
                            {
                                path,
                                string.IsNullOrEmpty(groupPath) ? EscapeSegment(item.Name) : $"{groupPath}/{EscapeSegment(item.Name)}",
                                EscapeSegment(item.Name)
                            }
                            .FirstOrDefault(c => GetSoftwareContainer(c) != null);
                    }

                    nodes.Add(node);

                    if (level < limit && item.DeviceItems != null && item.DeviceItems.Count > 0)
                    {
                        AddItems(item.DeviceItems, devicePath, groupPath, path, level + 1);
                    }
                }
            }

            void AddDevices(IEnumerable<Device> devices, string groupPath, int level)
            {
                foreach (var device in devices)
                {
                    var path = GetDevicePath(device);

                    nodes.Add(new ProjectNode { Level = level, Kind = "device", Name = device.Name, Path = path, Type = SafeTypeIdentifier(device) });

                    if (level < limit)
                    {
                        AddItems(device.DeviceItems, path, groupPath, string.Empty, level + 1);
                    }
                }
            }

            void AddGroups(IEnumerable<DeviceUserGroup> groups, string parentPath, int level)
            {
                foreach (var group in groups)
                {
                    var path = JoinLeaf(parentPath, group.Name);

                    nodes.Add(new ProjectNode { Level = level, Kind = "group", Name = group.Name, Path = path });

                    if (level < limit)
                    {
                        AddDevices(group.Devices, path, level + 1);
                        AddGroups(group.Groups, path, level + 1);
                    }
                }
            }

            AddDevices(_project!.Devices, string.Empty, 1);
            AddGroups(_project.DeviceGroups, string.Empty, 1);

            try
            {
                if (_project.UngroupedDevicesGroup?.Devices != null)
                {
                    AddDevices(_project.UngroupedDevicesGroup.Devices, string.Empty, 1);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "The ungrouped devices group is not available in this project");
            }

            return pattern == null ? nodes : KeepMatching(nodes, pattern);
        }

        private static string? SafeTypeIdentifier(Device device)
        {
            try { return string.IsNullOrEmpty(device.TypeIdentifier) ? null : device.TypeIdentifier; }
            catch (Exception) { return null; }
        }

        private static string? SafeTypeIdentifier(DeviceItem item)
        {
            try { return string.IsNullOrEmpty(item.TypeIdentifier) ? null : item.TypeIdentifier; }
            catch (Exception) { return null; }
        }

        /// <summary>The nodes whose name or path matches, with the nodes above them (the nearest lower-level node before them).</summary>
        internal static List<ProjectNode> KeepMatching(List<ProjectNode> nodes, Regex pattern)
        {
            var keep = new bool[nodes.Count];

            for (var i = 0; i < nodes.Count; i++)
            {
                if (!pattern.IsMatch(nodes[i].Name) && !pattern.IsMatch(nodes[i].Path))
                {
                    continue;
                }

                keep[i] = true;

                var level = nodes[i].Level;

                for (var j = i - 1; j >= 0 && level > 1; j--)
                {
                    if (nodes[j].Level < level)
                    {
                        keep[j] = true;
                        level = nodes[j].Level;
                    }
                }
            }

            return nodes.Where((_, i) => keep[i]).ToList();
        }
    }

    /// <summary>One node of the project tree: a device group, a device or a device item.</summary>
    public class ProjectNode
    {
        /// <summary>Levels from the top of the tree; 1 is a top-level group or device.</summary>
        public int Level { get; set; }

        /// <summary>group, device or deviceItem.</summary>
        public string Kind { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>The path 'hw_get_devices', 'hw_*' and 'net_*' accept for a device, and 'hw_get_device_item_info' for an item.</summary>
        public string Path { get; set; } = string.Empty;

        public string? Type { get; set; }

        /// <summary>For an item that carries software: the path the plc_* and unified_* tools take as softwarePath.</summary>
        public string? SoftwarePath { get; set; }
    }
}
