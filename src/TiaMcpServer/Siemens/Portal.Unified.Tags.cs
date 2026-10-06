using Siemens.Engineering;
using Siemens.Engineering.HmiUnified;
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
    //   - A new connection is not integrated: Partner, Node and Station are read-only, so it
    //     cannot be pointed at a PLC of the project. Its address is set through driver properties.
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

                    if (connection == null)
                    {
                        connection = software.Connections.Create(name);

                        result.Notes.Add("Created as a non-integrated connection: Openness cannot assign a PLC of the project as its partner. " +
                                         "Set the address through driverProperties; a connection to a project PLC is made in the network view.");
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
