using Siemens.Engineering;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.HmiUnified.HmiConnections;
using Siemens.Engineering.HmiUnified.HmiTags;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Siemens
{
    // WinCC Unified: HMI tags, tag tables and connections.
    //
    // Callers: the tools unified_get_tag_tables, unified_manage_tags, unified_manage_tag_tables
    // and unified_manage_connections in McpServer.Unified.cs. Reads and writes no data files.
    //
    // The three manage operations are batches and all-or-nothing, like ManageUnifiedItems and for
    // the same reason: inside a transaction TIA Portal refuses to commit once Openness has thrown.
    //
    // What Openness does here, as found on TIA Portal V21 (2026-10-06):
    //   - A new tag is internal (Connection "<Internal tag>") with data type Int.
    //   - PlcTag is resolved against the connection, so Connection has to be set first; setting
    //     PlcTag takes the data type over from the PLC tag.
    //   - An empty Connection turns the tag back into an internal one.
    //   - A tag cannot be moved between tables: TagTableName is read-only.
    //   - Deleting a tag table deletes its tags. The default tag table cannot be deleted.
    //   - A tag or a connection that is in use is deleted without complaint.
    //   - HmiSoftware.Connections.Create makes a non-integrated connection: Partner, Node and
    //     Station are read-only there, its address is set through driver properties.
    //   - An integrated connection to a PLC of the project is made on the hardware side: the
    //     device item that holds the HMI software offers CommunicationManagement, and
    //     Connections.Create<HmiConnection>(local node, PLC item, PLC node) there makes a
    //     connection that then shows up in HmiSoftware.Connections with its partner set.
    public partial class Portal
    {
        /// <summary>Tag properties in the order they have to be set; the rest follow as given.</summary>
        private static readonly string[] TagPropertyOrder = { "Connection", "AccessMode", "PlcTag", "DataType", "HmiDataType", "Address" };

        /// <summary>The driver decides which other properties and driver parameters exist.</summary>
        private static readonly string[] ConnectionPropertyOrder = { "CommunicationDriver" };

        #region tag tables

        public List<UnifiedTagTableInfo> GetUnifiedTagTables(string softwarePath)
        {
            return Operation.Run(_logger, nameof(GetUnifiedTagTables), PortalErrorCode.InvalidState,
                () => EnumerateTagTables(RequireUnifiedSoftware(softwarePath))
                    .Select(t => new UnifiedTagTableInfo { Name = t.Table.Name, Group = t.Group, TagCount = t.Table.Tags.Count })
                    .ToList(),
                ("softwarePath", softwarePath));
        }

        private static IEnumerable<(HmiTagTable Table, string Group)> EnumerateTagTables(HmiSoftware software)
        {
            foreach (var table in software.TagTables)
            {
                yield return (table, string.Empty);
            }

            foreach (var group in software.TagTableGroups)
            {
                foreach (var entry in EnumerateTagTables(group, group.Name))
                {
                    yield return entry;
                }
            }
        }

        private static IEnumerable<(HmiTagTable Table, string Group)> EnumerateTagTables(HmiTagTableGroup group, string path)
        {
            foreach (var table in group.TagTables)
            {
                yield return (table, path);
            }

            foreach (var subGroup in group.Groups)
            {
                foreach (var entry in EnumerateTagTables(subGroup, $"{path}/{subGroup.Name}"))
                {
                    yield return entry;
                }
            }
        }

        private static HmiTagTable? FindTagTable(HmiSoftware software, string name)
        {
            return EnumerateTagTables(software)
                .Select(t => t.Table)
                .FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static HmiTagTable RequireTagTable(HmiSoftware software, string name)
        {
            return FindTagTable(software, name)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"Tag table '{name}' not found. Existing: {string.Join(", ", EnumerateTagTables(software).Select(t => t.Table.Name))}.");
        }

        public List<UnifiedActionResult> ManageUnifiedTagTables(string softwarePath, IList<UnifiedTagTableAction>? actions)
        {
            return RunUnifiedBatch(nameof(ManageUnifiedTagTables), softwarePath, actions,
                "{ \"action\": \"create\", \"tableName\": \"Pumps\" }",
                a => a.TableName,
                (software, action, verb, result) =>
                {
                    var name = RequireName(action.TableName, "tableName");

                    switch (verb)
                    {
                        case "create":
                            if (FindTagTable(software, name) != null)
                            {
                                throw new PortalException(PortalErrorCode.InvalidParams, $"Tag table '{name}' already exists.");
                            }

                            software.TagTables.Create(name);

                            break;

                        case "rename":
                            var newName = RequireName(action.NewName, "newName");

                            if (FindTagTable(software, newName) != null)
                            {
                                throw new PortalException(PortalErrorCode.InvalidParams, $"Tag table '{newName}' already exists.");
                            }

                            RequireTagTable(software, name).Name = newName;
                            result.Applied.Add($"Name = {newName}");

                            break;

                        case "delete":
                            var table = RequireTagTable(software, name);
                            var count = table.Tags.Count;

                            table.Delete();

                            if (count > 0)
                            {
                                result.Notes.Add($"{count} tag(s) of the table were deleted with it.");
                            }

                            break;

                        default:
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                $"Unknown action '{action.Action}'. Use 'create', 'rename' or 'delete'.");
                    }
                });
        }

        #endregion

        #region tags

        public List<UnifiedActionResult> ManageUnifiedTags(string softwarePath, IList<UnifiedTagAction>? actions)
        {
            return RunUnifiedBatch(nameof(ManageUnifiedTags), softwarePath, actions,
                "{ \"action\": \"upsert\", \"tagName\": \"Speed\", \"properties\": { \"Connection\": \"HMI_Connection_1\", \"PlcTag\": \"Motor.Speed\" } }",
                a => a.TagName,
                (software, action, verb, result) =>
                {
                    var name = RequireName(action.TagName, "tagName");
                    var tag = software.Tags.Find(name);

                    switch (verb)
                    {
                        case "delete":
                            if (tag == null)
                            {
                                throw new PortalException(PortalErrorCode.NotFound, $"HMI tag '{name}' not found.");
                            }

                            tag.Delete();

                            return;

                        case "create" when tag != null:
                            throw new PortalException(PortalErrorCode.InvalidParams, $"HMI tag '{name}' already exists. Use 'update' or 'upsert'.");

                        case "update" when tag == null:
                            throw new PortalException(PortalErrorCode.NotFound,
                                $"HMI tag '{name}' not found. Use 'unified_get_tags' to list the tags, or 'upsert' to create it.");

                        case "create":
                        case "update":
                        case "upsert":
                            break;

                        default:
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                $"Unknown action '{action.Action}'. Use 'create', 'update', 'upsert' or 'delete'.");
                    }

                    var tableName = action.TagTable?.Trim();

                    if (tag == null)
                    {
                        if (string.IsNullOrEmpty(tableName))
                        {
                            tag = software.Tags.Create(name);
                        }
                        else
                        {
                            // Checked up front: a failed Create would cost the whole batch its commit.
                            tag = software.Tags.Create(name, RequireTagTable(software, tableName!).Name);
                        }

                        result.Notes.Add($"Created in tag table '{tag.TagTableName}'.");
                    }
                    else if (!string.IsNullOrEmpty(tableName) && !string.Equals(tableName, tag.TagTableName, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new PortalException(PortalErrorCode.NotSupported,
                            $"HMI tag '{name}' is in tag table '{tag.TagTableName}'. Openness cannot move a tag to another table; delete it and create it there.");
                    }

                    // Writing the display name of a tag ends in a NonRecoverableException, which
                    // closes TIA Portal and loses every unsaved change (V21, 2026-10-06; reading
                    // it is harmless). So it is refused here, before Openness is touched.
                    if (action.Properties != null && action.Properties.Keys.Any(k => k.Equals("DisplayName", StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new PortalException(PortalErrorCode.NotSupported,
                            "DisplayName of an HMI tag cannot be set through Openness: the attempt closes TIA Portal. Set it in the tag editor; Comment can be set here.");
                    }

                    SetUnifiedAttributes(tag, "An HMI tag", action.Properties, TagPropertyOrder, result);
                });
        }

        #endregion

        #region connections

        public List<UnifiedActionResult> ManageUnifiedConnections(string softwarePath, IList<UnifiedConnectionAction>? actions)
        {
            return RunUnifiedBatch(nameof(ManageUnifiedConnections), softwarePath, actions,
                "{ \"action\": \"upsert\", \"connectionName\": \"PLC_2\", \"properties\": { \"CommunicationDriver\": \"SIMATIC S7 1200/1500\" }, \"driverProperties\": { \"Protocol.RemStAddress\": \"192.168.0.10\" } }",
                a => a.ConnectionName,
                (software, action, verb, result) =>
                {
                    var name = RequireName(action.ConnectionName, "connectionName");
                    var connection = software.Connections.Find(name);

                    switch (verb)
                    {
                        case "delete":
                            if (connection == null)
                            {
                                throw new PortalException(PortalErrorCode.NotFound, $"Connection '{name}' not found.");
                            }

                            var bound = software.Tags.Count(t => string.Equals(t.Connection, connection.Name, StringComparison.OrdinalIgnoreCase));

                            connection.Delete();

                            if (bound > 0)
                            {
                                result.Notes.Add($"{bound} HMI tag(s) still name this connection and have to be given another one.");
                            }

                            return;

                        case "create" when connection != null:
                            throw new PortalException(PortalErrorCode.InvalidParams, $"Connection '{name}' already exists. Use 'update' or 'upsert'.");

                        case "update" when connection == null:
                            throw new PortalException(PortalErrorCode.NotFound,
                                $"Connection '{name}' not found. Use 'unified_get_connections' to list the connections, or 'upsert' to create it.");

                        case "create":
                        case "update":
                        case "upsert":
                            break;

                        default:
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                $"Unknown action '{action.Action}'. Use 'create', 'update', 'upsert' or 'delete'.");
                    }

                    var partner = action.Partner?.Trim();

                    if (connection == null && !string.IsNullOrEmpty(partner))
                    {
                        connection = CreateIntegratedConnection(software, softwarePath, name, partner!, action.LocalInterface, action.PartnerInterface, result);
                    }
                    else if (connection == null)
                    {
                        connection = software.Connections.Create(name);

                        result.Notes.Add("Created as a non-integrated connection: it has no partner in the project and its address is set through driverProperties. " +
                                         "Pass 'partner' with the path of a PLC to create an integrated connection instead.");
                    }
                    else if (!string.IsNullOrEmpty(partner) && !string.Equals(connection.Partner, PathSegments(partner!).LastOrDefault(), StringComparison.OrdinalIgnoreCase))
                    {
                        throw new PortalException(PortalErrorCode.NotSupported,
                            $"Connection '{name}' already exists with partner '{connection.Partner}'. The partner of an existing connection cannot be changed; delete the connection and create it again.");
                    }

                    SetUnifiedAttributes(connection, "A connection", action.Properties, ConnectionPropertyOrder, result);

                    if (action.DriverProperties == null)
                    {
                        return;
                    }

                    foreach (var entry in action.DriverProperties)
                    {
                        var property = connection.DriverProperties.Find(entry.Key)
                            ?? throw new PortalException(PortalErrorCode.NotFound,
                                $"Driver '{connection.CommunicationDriver}' has no parameter '{entry.Key}'. Available: " +
                                $"{string.Join(", ", connection.DriverProperties.Select(p => p.PropertyName).Distinct())}.");

                        property.Value = entry.Value ?? string.Empty;
                        result.Applied.Add($"driver {property.PropertyName}");
                    }
                });
        }

        /// <summary>The driver parameters of a connection by name; a parameter listed twice is given once.</summary>
        private static Dictionary<string, string?> DescribeDriverProperties(HmiConnection connection)
        {
            var result = new Dictionary<string, string?>();

            foreach (var property in connection.DriverProperties)
            {
                if (!result.ContainsKey(property.PropertyName))
                {
                    result[property.PropertyName] = property.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// Creates an HMI connection to a PLC of the project. It is made between two network
        /// nodes, so both devices need an interface on a common subnet.
        /// </summary>
        private HmiConnection CreateIntegratedConnection(
            HmiSoftware software, string softwarePath, string name, string partnerPath, string? localInterface, string? partnerInterface, UnifiedActionResult result)
        {
            var hmiItem = RequireHmiContainer(softwarePath).Parent as DeviceItem
                ?? throw new PortalException(PortalErrorCode.NotSupported, $"'{softwarePath}' is not hosted by a device item.");

            var management = hmiItem.GetService<CommunicationManagement>()
                ?? throw new PortalException(PortalErrorCode.NotSupported, $"'{softwarePath}' does not support connections to project devices.");

            var partnerContainer = GetSoftwareContainer(partnerPath);

            if (!(partnerContainer?.Software is global::Siemens.Engineering.SW.PlcSoftware) || !(partnerContainer.Parent is DeviceItem partnerItem))
            {
                throw new PortalException(PortalErrorCode.NotFound,
                    $"No PLC found at '{partnerPath}'. 'partner' is the path of the PLC as the plc_* tools take it, e.g. 'PLC_1' or 'Station_1/PLC_1'.");
            }

            var localNodes = NetworkNodes(TopDeviceItems(hmiItem)).ToList();
            var partnerNodes = NetworkNodes(new[] { partnerItem }).ToList();

            var local = PickNodes(localNodes, localInterface, "localInterface", softwarePath);
            var remote = PickNodes(partnerNodes, partnerInterface, "partnerInterface", partnerPath);

            // The pair has to share a subnet; without one the connection would be created unusable.
            var pair = (from l in local
                        from r in remote
                        where l.Node.ConnectedSubnet != null && r.Node.ConnectedSubnet != null
                              && l.Node.ConnectedSubnet.Name == r.Node.ConnectedSubnet.Name
                        select new { Local = l, Remote = r }).FirstOrDefault()
                       ?? throw new PortalException(PortalErrorCode.InvalidState,
                           $"'{softwarePath}' and '{partnerPath}' have no interfaces on a common subnet. " +
                           $"HMI: {DescribeNodes(localNodes)}. PLC: {DescribeNodes(partnerNodes)}. Connect them with 'net_connect_subnet' first.");

            var created = management.Connections.Create<global::Siemens.Engineering.HW.CommunicationConnections.HmiConnection>(pair.Local.Node, partnerItem, pair.Remote.Node);

            created.LocalConnectionName = name;

            result.Notes.Add($"Created as an integrated connection to '{partnerItem.Name}' over subnet '{pair.Local.Node.ConnectedSubnet.Name}' " +
                             $"({pair.Local.Interface} {created.LocalAddress} -> {pair.Remote.Interface} {created.PartnerAddress}).");

            return software.Connections.Find(name)
                ?? throw new PortalException(PortalErrorCode.CreateFailed,
                    $"The connection to '{partnerItem.Name}' was created but does not show up among the HMI connections as '{name}'.");
        }

        /// <summary>The top-level items of the device an item belongs to: an HMI keeps its interfaces beside its runtime item.</summary>
        private static IEnumerable<DeviceItem> TopDeviceItems(DeviceItem item)
        {
            IEngineeringObject current = item;

            while (current.Parent is DeviceItem parent)
            {
                current = parent;
            }

            return current.Parent is Device device ? device.DeviceItems : new[] { item };
        }

        private static IEnumerable<(Node Node, string Interface)> NetworkNodes(IEnumerable<DeviceItem> items)
        {
            foreach (var item in items)
            {
                var networkInterface = item.GetService<NetworkInterface>();

                if (networkInterface != null)
                {
                    foreach (var node in networkInterface.Nodes)
                    {
                        yield return (node, item.Name);
                    }
                }

                foreach (var nested in NetworkNodes(item.DeviceItems))
                {
                    yield return nested;
                }
            }
        }

        private static List<(Node Node, string Interface)> PickNodes(List<(Node Node, string Interface)> nodes, string? wanted, string parameter, string path)
        {
            if (string.IsNullOrWhiteSpace(wanted))
            {
                return nodes;
            }

            var picked = nodes
                .Where(n => n.Interface.Equals(wanted!.Trim(), StringComparison.OrdinalIgnoreCase) || n.Node.Name.Equals(wanted.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            return picked.Count > 0
                ? picked
                : throw new PortalException(PortalErrorCode.NotFound,
                    $"{parameter} '{wanted}' not found on '{path}'. Available: {DescribeNodes(nodes)}.");
        }

        private static string DescribeNodes(List<(Node Node, string Interface)> nodes)
        {
            return nodes.Count == 0
                ? "no network interfaces"
                : string.Join(", ", nodes.Select(n => $"{n.Interface} ({n.Node.Name}, {(n.Node.ConnectedSubnet == null ? "no subnet" : "subnet " + n.Node.ConnectedSubnet.Name)})"));
        }

        #endregion

        #region shared
        private static string RequireName(string? value, string parameter)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new PortalException(PortalErrorCode.InvalidParams, $"{parameter} is required.")
                : value!.Trim();
        }

        /// <summary>
        /// Runs a batch of actions on a Unified HMI. Each action reports its own failure; if any
        /// failed the whole batch is thrown, which is what rolls the transaction back.
        /// </summary>
        private List<UnifiedActionResult> RunUnifiedBatch<TAction>(
            string operation,
            string softwarePath,
            IList<TAction>? actions,
            string example,
            Func<TAction, string?> nameOf,
            Action<HmiSoftware, TAction, string, UnifiedActionResult> apply)
            where TAction : class
        {
            return Operation.Run(_logger, operation, PortalErrorCode.InvalidState,
                () =>
                {
                    if (actions == null || actions.Count == 0)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, $"No actions given. Pass at least one, e.g. {example}.");
                    }

                    var software = RequireUnifiedSoftware(softwarePath);
                    var results = new List<UnifiedActionResult>();

                    foreach (var action in actions)
                    {
                        var verb = (action.GetType().GetProperty("Action")?.GetValue(action) as string ?? string.Empty).Trim().ToLowerInvariant();
                        var result = new UnifiedActionResult { Action = verb, Name = nameOf(action) };

                        try
                        {
                            apply(software, action, verb, result);
                            result.Status = "success";
                        }
                        catch (Exception ex)
                        {
                            result.Status = "error";
                            result.Error = ErrorText.ForClient(ex);
                        }

                        results.Add(result);
                    }

                    var failed = results.Where(r => r.Status != "success").ToList();

                    if (failed.Count > 0)
                    {
                        var outcome = _inTransaction
                            ? "Nothing was changed: the whole batch was rolled back."
                            : "TIA Portal granted no transaction for this call, so the actions that succeeded remain applied: " +
                              (results.Count == failed.Count ? "none" : string.Join(", ", results.Where(r => r.Status == "success").Select(r => $"{r.Action} '{r.Name}'"))) + ".";

                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"{failed.Count} of {results.Count} action(s) failed. {outcome} " +
                            string.Join(" | ", failed.Select(r => $"{r.Action} '{r.Name}': {r.Error}")));
                    }

                    return results;
                },
                ("softwarePath", softwarePath));
        }

        /// <summary>
        /// Sets attributes of a tag or a connection. The type of each value is taken from the
        /// .NET property, not from the current value: reading an attribute that the object does
        /// not offer in its current state would throw, and inside a transaction even a caught
        /// Openness exception forbids the commit.
        /// </summary>
        private static void SetUnifiedAttributes(IEngineeringObject target, string what, Dictionary<string, JsonElement>? properties, string[] order, UnifiedActionResult result)
        {
            if (properties == null)
            {
                return;
            }

            var ordered = properties
                .OrderBy(p =>
                {
                    var index = Array.FindIndex(order, o => o.Equals(p.Key, StringComparison.OrdinalIgnoreCase));

                    // A rename goes last, so that messages about the other properties name the tag as the caller did.
                    return p.Key.Equals("Name", StringComparison.OrdinalIgnoreCase) ? int.MaxValue : index < 0 ? order.Length : index;
                })
                .ToList();

            var available = target.GetType().GetProperties()
                .Where(p => p.CanWrite || p.PropertyType == typeof(MultilingualText))
                .ToList();

            foreach (var entry in ordered)
            {
                var property = available.FirstOrDefault(p => p.Name.Equals(entry.Key, StringComparison.Ordinal))
                               ?? available.FirstOrDefault(p => p.Name.Equals(entry.Key, StringComparison.OrdinalIgnoreCase))
                               ?? throw new PortalException(PortalErrorCode.NotFound,
                                   $"{what} has no settable property '{entry.Key}'. Settable: {string.Join(", ", available.Select(p => p.Name).OrderBy(n => n))}.");

                if (property.PropertyType == typeof(MultilingualText))
                {
                    SetPlainMultilingualText((MultilingualText)property.GetValue(target)!, entry.Value, property.Name);
                }
                else
                {
                    var type = property.PropertyType == typeof(object) ? null : property.PropertyType;

                    var converted = ConvertHmiValue(entry.Value, type, property.Name) ?? (type == typeof(string) ? string.Empty : null);

                    try
                    {
                        target.SetAttribute(property.Name, converted);
                    }
                    catch (EngineeringException ex) when (property.Name == "CommunicationDriver")
                    {
                        // Openness answers an unknown driver with a bare "The property cannot be set".
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"'{converted}' is not a communication driver of this HMI. Use the name TIA Portal shows in the 'Communication driver' column, " +
                            "e.g. 'SIMATIC S7 1200/1500', 'SIMATIC S7 300/400' or 'OPC UA'.", null, ex);
                    }
                }

                result.Applied.Add(property.Name);
            }
        }

        /// <summary>Comments and display names are plain text, unlike the texts shown on screen items.</summary>
        private static void SetPlainMultilingualText(MultilingualText text, JsonElement value, string propertyName)
        {
            if (value.ValueKind == JsonValueKind.String || value.ValueKind == JsonValueKind.Null)
            {
                foreach (var item in text.Items)
                {
                    item.Text = value.GetString() ?? string.Empty;
                }

                return;
            }

            if (value.ValueKind != JsonValueKind.Object)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"{propertyName} takes a string, or {{ \"en-US\": \"...\" }} to set single languages.");
            }

            foreach (var entry in value.EnumerateObject())
            {
                var item = text.Items.FirstOrDefault(i => string.Equals(i.Language?.Culture?.Name, entry.Name, StringComparison.OrdinalIgnoreCase))
                    ?? throw new PortalException(PortalErrorCode.NotFound,
                        $"The project has no language '{entry.Name}'. Available: {string.Join(", ", text.Items.Select(i => i.Language?.Culture?.Name))}.");

                item.Text = entry.Value.GetString() ?? string.Empty;
            }
        }

        #endregion
    }
}
