using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace TiaMcpServer.Siemens
{
    /// <summary>One attribute a call changed.</summary>
    public class AttributeChange
    {
        public string Name { get; set; } = string.Empty;
        public string? Before { get; set; }
        public string? After { get; set; }
        public string? Note { get; set; }
    }

    // Setting the parameters of hardware: the CPU, its interfaces, ports, modules.
    //
    // Callers: the tool hw_set_device_item_attributes in McpServer.Devices.cs.
    //
    // Found on TIA Portal V21 (2026-10-07, CPU 1512SP-1 PN, read only): the parameters of a CPU are plain Openness
    // attributes of its device item - 58 of 74 are writable: cycle (CycleMaximumCycleTime, CycleMinimumCycleTime,
    // CycleCommunicationLoad), clock and system memory (ClockMemoryByte, ClockMemoryByteAddress, SystemMemoryByte ...),
    // startup (StartupActionAfterPowerOn ...), time of day, SNMP, WebserverActivate, protection
    // (ProtectionEnablePutGetCommunication, PlcAccessControlConfiguration). The interface (PROFINET interface_1) has
    // 18, a port 10, 'OPC UA_1' has OpcUaServer and OpcUaClient. The IP address is on the node of the interface, not on
    // the item: it is reached here as "Node.Address", "Node.SubnetMask", "Node.RouterAddress", "Node.UseRouter",
    // "Node.PnDeviceName". Many attributes hold a number that stands for a choice of the dialog (StartupActionAfterPowerOn
    // = 3): Openness gives no names for those, so the number is passed. Passwords and the protection of the PLC
    // configuration (PlcMasterSecretConfigurator) are not touched by the server.
    public partial class Portal
    {
        private const string NodePrefix = "Node.";

        public List<AttributeChange> SetDeviceItemAttributes(string deviceItemPath, Dictionary<string, JsonElement>? attributes)
        {
            return Operation.Run(_logger, nameof(SetDeviceItemAttributes), PortalErrorCode.InvalidState,
                () =>
                {
                    RequireProject();

                    var item = GetDeviceItemUnlocked(deviceItemPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"No device item at '{deviceItemPath}'. 'get_project_tree' with structured = true lists the paths, e.g. 'Station_1/PLC_1' or 'Station_1/PLC_1/PROFINET interface_1'.");

                    if (attributes == null || attributes.Count == 0)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            "No attributes given. Example: {\"CycleMaximumCycleTime\": 200, \"ClockMemoryByte\": true}. 'hw_get_device_item_info' lists the attributes with their values; those with accessMode ReadWrite can be set.");
                    }

                    var node = item.GetService<NetworkInterface>()?.Nodes.FirstOrDefault();
                    var plan = new List<(IEngineeringObject Target, string Name, string Shown, object? Value)>();
                    var problems = new List<string>();

                    foreach (var entry in attributes)
                    {
                        var onNode = entry.Key.StartsWith(NodePrefix, StringComparison.OrdinalIgnoreCase);
                        var name = onNode ? entry.Key.Substring(NodePrefix.Length) : entry.Key;
                        IEngineeringObject? target = onNode ? node : item;

                        if (target == null)
                        {
                            problems.Add($"'{entry.Key}': '{item.Name}' is not a network interface, so it has no node. The address of a PLC is on its interface item, e.g. '.../PROFINET interface_1'.");
                            continue;
                        }

                        var infos = target.GetAttributeInfos().ToList();
                        var info = infos.FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));

                        if (info == null)
                        {
                            var writable = infos.Where(i => i.AccessMode.HasFlag(EngineeringAttributeAccessMode.Write)).Select(i => (onNode ? NodePrefix : string.Empty) + i.Name).OrderBy(n => n).ToList();
                            var near = writable.Where(n => n.IndexOf(name.Length > 4 ? name.Substring(0, 4) : name, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                            problems.Add($"'{entry.Key}': {(onNode ? "the node of " : string.Empty)}'{item.Name}' has no such attribute. " +
                                         (near.Count > 0 ? $"Similar: {string.Join(", ", near.Take(12))}. " : string.Empty) +
                                         $"Writable ({writable.Count}): {string.Join(", ", writable.Take(70))}{(writable.Count > 70 ? " ..." : string.Empty)}.");
                            continue;
                        }

                        if (!info.AccessMode.HasFlag(EngineeringAttributeAccessMode.Write))
                        {
                            problems.Add($"'{entry.Key}' is read-only in Openness.");
                            continue;
                        }

                        object? current;

                        try
                        {
                            current = target.GetAttribute(info.Name);
                        }
                        catch (Exception ex)
                        {
                            problems.Add($"'{entry.Key}' cannot be read, so its type is not known: {ErrorText.Describe(ex)}");
                            continue;
                        }

                        if (!TryConvertAttribute(entry.Value, current, out var value, out var why))
                        {
                            problems.Add($"'{entry.Key}': {why}");
                            continue;
                        }

                        plan.Add((target, info.Name, entry.Key, value));
                    }

                    if (problems.Count > 0)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, $"Nothing was changed: {problems.Count} of {attributes.Count} attribute(s) cannot be set. {string.Join(" ", problems)}");
                    }

                    var changes = new List<AttributeChange>();
                    var step = 0;

                    foreach (var (target, name, shown, value) in plan)
                    {
                        Progress(++step, plan.Count, $"{deviceItemPath}: {shown}");

                        var change = new AttributeChange { Name = shown, Before = ShowAttribute(target.GetAttribute(name)) };

                        try
                        {
                            target.SetAttribute(name, value);
                        }
                        catch (Exception ex)
                        {
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                $"TIA Portal refused '{shown}' = {ShowAttribute(value)}: {ErrorText.Describe(ex)} Nothing was changed: the call was rolled back.", null, ex);
                        }

                        change.After = ShowAttribute(target.GetAttribute(name));

                        if (change.After != ShowAttribute(value))
                        {
                            change.Note = $"TIA Portal stored {change.After}, not {ShowAttribute(value)}.";
                        }

                        changes.Add(change);
                    }

                    return changes;
                },
                ("deviceItemPath", deviceItemPath));
        }

        /// <summary>The attributes of the node of a network interface, named as the setter takes them ("Node.Address").</summary>
        public List<ModelContextProtocol.Attribute> GetNodeAttributes(DeviceItem item)
        {
            return Operation.Run(_logger, nameof(GetNodeAttributes), PortalErrorCode.InvalidState,
                () =>
                {
                    var node = item.GetService<NetworkInterface>()?.Nodes.FirstOrDefault();
                    var list = node == null ? new List<ModelContextProtocol.Attribute>() : ModelContextProtocol.Helper.GetAttributeList(node);

                    foreach (var attribute in list)
                    {
                        attribute.Name = NodePrefix + attribute.Name;
                    }

                    return list;
                });
        }

        private static string ShowAttribute(object? value)
        {
            return value switch
            {
                null => "(none)",
                bool flag => flag ? "true" : "false",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty
            };
        }

        /// <summary>Turns a JSON value into the type the attribute holds now.</summary>
        private static bool TryConvertAttribute(JsonElement json, object? current, out object? value, out string why)
        {
            value = null;
            why = string.Empty;

            var type = current?.GetType();

            if (type == null)
            {
                switch (json.ValueKind)
                {
                    case JsonValueKind.String: value = json.GetString(); return true;
                    case JsonValueKind.True: case JsonValueKind.False: value = json.GetBoolean(); return true;
                    case JsonValueKind.Number: value = json.TryGetInt32(out var whole) ? whole : (object)json.GetDouble(); return true;
                    default: why = "the attribute is empty now, so its type is not known; pass a string, a number or true/false."; return false;
                }
            }

            try
            {
                if (type == typeof(bool))
                {
                    if (json.ValueKind != JsonValueKind.True && json.ValueKind != JsonValueKind.False)
                    {
                        why = $"takes true or false; got {json.GetRawText()}.";

                        return false;
                    }

                    value = json.GetBoolean();

                    return true;
                }

                if (type == typeof(string))
                {
                    value = json.ValueKind == JsonValueKind.String ? json.GetString() : json.GetRawText();

                    return true;
                }

                if (type.IsEnum)
                {
                    if (json.ValueKind == JsonValueKind.String && Enum.GetNames(type).Any(n => n.Equals(json.GetString(), StringComparison.OrdinalIgnoreCase)))
                    {
                        value = Enum.Parse(type, json.GetString()!, true);

                        return true;
                    }

                    if (json.ValueKind == JsonValueKind.Number && json.TryGetInt64(out var number) && Enum.IsDefined(type, Convert.ChangeType(number, Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture)))
                    {
                        value = Enum.ToObject(type, number);

                        return true;
                    }

                    why = $"takes one of {string.Join(", ", Enum.GetNames(type))}; got {json.GetRawText()}.";

                    return false;
                }

                if (type.IsPrimitive || type == typeof(decimal))
                {
                    if (json.ValueKind != JsonValueKind.Number)
                    {
                        why = $"takes a number ({type.Name}); got {json.GetRawText()}. Where the dialog of TIA Portal offers a choice, the number stands for the entry chosen.";

                        return false;
                    }

                    value = Convert.ChangeType(json.GetDecimal(), type, CultureInfo.InvariantCulture);

                    return true;
                }

                if (type == typeof(DateTime))
                {
                    if (json.ValueKind == JsonValueKind.String && DateTime.TryParse(json.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date))
                    {
                        value = date;

                        return true;
                    }

                    why = $"takes a date and time as text, e.g. \"2026-01-31T12:00:00\"; got {json.GetRawText()}.";

                    return false;
                }
            }
            catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is InvalidCastException)
            {
                why = $"{json.GetRawText()} does not fit the type {type.Name} of the attribute.";

                return false;
            }

            why = $"holds a {type.Name}, which cannot be set as a plain value here.";

            return false;
        }
    }
}
