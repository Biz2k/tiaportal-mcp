using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.Multiuser;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Types;
using Siemens.Engineering.Safety;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        // Device and device item lookup.
        //
        // Callers: the device tools in McpServer.Devices.cs, the hardware and network methods in
        // Portal.Hardware.cs, and Test3Devices. Affected API: GetDevices, GetDevice and
        // GetDeviceItem keep their signatures; GetDevices now also returns the devices of the
        // ungrouped devices group, GetDevice throws on an ambiguous name instead of picking one.
        // Reads/writes no data files.
        //
        // A device is addressable in more ways than a block, because what TIA Portal shows is
        // not what Openness names: a hardware PLC station is called "S7-1500/ET200MP station_1"
        // in Openness (with a slash) while the project tree shows its CPU, "PLC_1". Every form a
        // caller can read off a listing is accepted here, in one place, so the tools agree.

        #region devices

        /// <summary>
        /// Every device of the project with its path: top-level devices, devices in user groups
        /// at any depth, and the ungrouped devices group that holds distributed IO stations.
        /// </summary>
        private List<(Device Device, string GroupPath, string Path)> EnumerateDevices()
        {
            var result = new List<(Device, string, string)>();

            if (_project == null)
            {
                return result;
            }

            void Add(IEnumerable<Device>? devices, string groupPath)
            {
                if (devices == null)
                {
                    return;
                }

                foreach (var device in devices)
                {
                    if (device != null)
                    {
                        result.Add((device, groupPath, JoinLeaf(groupPath, device.Name)));
                    }
                }
            }

            void AddGroups(IEnumerable<DeviceUserGroup>? groups, string parentPath)
            {
                if (groups == null)
                {
                    return;
                }

                foreach (var group in groups)
                {
                    var groupPath = JoinLeaf(parentPath, group.Name);

                    Add(group.Devices, groupPath);
                    AddGroups(group.Groups, groupPath);
                }
            }

            Add(_project.Devices, string.Empty);
            AddGroups(_project.DeviceGroups, string.Empty);

            try
            {
                // The ungrouped devices group has no path segment of its own: its devices are
                // addressed like top-level ones, which is how the hardware tools always did it.
                Add(_project.UngroupedDevicesGroup?.Devices, string.Empty);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "The ungrouped devices group is not available in this project");
            }

            return result;
        }

        public List<Device> GetDevices(string regexName = "")
        {
            _logger?.LogInformation("Getting devices...");

            if (IsProjectNull())
            {
                return [];
            }

            var list = new List<Device>();

            foreach (var entry in EnumerateDevices())
            {
                try
                {
                    if (!string.IsNullOrEmpty(regexName) && !Regex.IsMatch(entry.Device.Name, regexName, RegexOptions.IgnoreCase))
                    {
                        continue;
                    }
                }
                catch (ArgumentException)
                {
                    // Invalid regex pattern - skip, as the block and type listings do.
                    continue;
                }

                list.Add(entry.Device);
            }

            return list;
        }

        /// <summary>
        /// The path GetDevice accepts back for this device, e.g. "Group1/PC-System_1" or
        /// "S7-1500%2FET200MP station_1".
        /// </summary>
        public string GetDevicePath(Device device)
        {
            if (device == null)
            {
                return string.Empty;
            }

            var segments = new List<string> { EscapeSegment(device.Name) };

            try
            {
                var parent = device.Parent;

                while (parent is DeviceUserGroup group)
                {
                    segments.Insert(0, EscapeSegment(group.Name));
                    parent = group.Parent;
                }
            }
            catch (Exception)
            {
                // The parent chain is not walkable; the name alone still resolves when unique.
            }

            return string.Join("/", segments);
        }

        /// <summary>
        /// Finds a device by any form a caller can read off a listing: its path, its Openness
        /// name with or without the slash escaped, its bare name inside a group, or the name
        /// of its head module as the project tree shows it. Returns null when nothing matches.
        /// </summary>
        /// <exception cref="PortalException">InvalidParams when the name fits several devices.</exception>
        public Device? GetDevice(string devicePath)
        {
            _logger?.LogInformation($"Getting device by path: {devicePath}");

            if (IsProjectNull() || string.IsNullOrWhiteSpace(devicePath))
            {
                return null;
            }

            var devices = EnumerateDevices();
            var wanted = NormalizeGroupPath(devicePath);
            var plain = UnescapeSegment(wanted);

            bool Same(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);

            // Most specific reading first; a later one is only tried when the earlier found nothing.
            var readings = new Func<(Device Device, string GroupPath, string Path), bool>[]
            {
                d => Same(d.Path, wanted),
                d => Same(UnescapeSegment(d.Path), plain),
                d => Same(d.Device.Name, plain),
                d => d.Device.DeviceItems.Any(i => Same(i.Name, plain))
            };

            foreach (var reading in readings)
            {
                var matches = devices.Where(reading).ToList();

                if (matches.Count == 1)
                {
                    return matches[0].Device;
                }

                if (matches.Count > 1)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams,
                        $"'{devicePath}' matches {matches.Count} devices: " +
                        string.Join(", ", matches.Select(m => $"'{m.Path}'")) + ". Pass one of these paths.");
                }
            }

            return null;
        }

        /// <summary>
        /// GetDevice for callers inside Operation.Run: a missing device becomes a NotFound that
        /// names the tool listing the valid paths.
        /// </summary>
        private Device RequireDevice(string devicePath)
        {
            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, NoProjectMessage);
            }

            return GetDevice(devicePath)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"Device not found at '{devicePath}'. Use 'hw_get_devices' to list the device paths.");
        }

        /// <summary>
        /// Finds a device item. Accepted forms: "{device path}/{item}/{sub item}", the same
        /// without the device name ("Group1/PLC_1", "PLC_1" - a hardware PLC is known by its CPU,
        /// not by its station), and a device path alone when the device has an item of its name.
        /// </summary>
        public DeviceItem? GetDeviceItem(string deviceItemPath)
        {
            _logger?.LogInformation($"Getting device item by path: {deviceItemPath}");

            if (IsProjectNull() || string.IsNullOrWhiteSpace(deviceItemPath))
            {
                return null;
            }

            var segments = PathSegments(deviceItemPath);

            if (segments.Length == 0)
            {
                return null;
            }

            static IEnumerable<DeviceItem> Children(DeviceItem item) => item.DeviceItems;
            static string Name(DeviceItem item) => item.Name;

            var devices = EnumerateDevices();

            // 1. Qualified: the path starts with a device path.
            foreach (var entry in devices)
            {
                var devicePath = PathSegments(entry.Path);
                var consumed = MatchPrefix(segments, devicePath);

                if (consumed < 0)
                {
                    continue;
                }

                var item = consumed == segments.Length
                    ? entry.Device.DeviceItems.FirstOrDefault(i => i.Name.Equals(entry.Device.Name, StringComparison.OrdinalIgnoreCase))
                    : WalkNamed(entry.Device.DeviceItems, segments, consumed, Children, Name);

                if (item != null)
                {
                    return item;
                }
            }

            // 2. Unqualified: the group path, then the item, with the device name left out.
            foreach (var entry in devices)
            {
                var consumed = MatchPrefix(segments, PathSegments(entry.GroupPath));

                if (consumed < 0 || consumed == segments.Length)
                {
                    continue;
                }

                var item = WalkNamed(entry.Device.DeviceItems, segments, consumed, Children, Name);

                if (item != null)
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// How many leading segments of <paramref name="path"/> spell out
        /// <paramref name="prefix"/>, or -1 when they do not. A prefix segment holding an
        /// escaped slash also matches the same name written unescaped across several segments.
        /// </summary>
        internal static int MatchPrefix(string[] path, string[] prefix)
        {
            var index = 0;

            foreach (var segment in prefix)
            {
                var name = UnescapeSegment(segment);
                var matched = false;

                for (var take = 1; index + take <= path.Length; take++)
                {
                    var candidate = string.Join("/", path.Skip(index).Take(take).Select(UnescapeSegment));

                    if (candidate.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        index += take;
                        matched = true;

                        break;
                    }
                }

                if (!matched)
                {
                    return -1;
                }
            }

            return index;
        }

        #endregion
    }
}
