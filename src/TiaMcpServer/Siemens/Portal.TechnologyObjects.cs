using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.TechnologicalObjects;
using Siemens.Engineering.SW.TechnologicalObjects.Motion;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace TiaMcpServer.Siemens
{
    public class TechnologyParameterInfo
    {
        public string? Name { get; set; }

        public object? Value { get; set; }
    }

    public class TechnologyObjectInfo
    {
        public string? Name { get; set; }

        /// <summary>Group path below 'Technology objects'; empty for the top.</summary>
        public string? Group { get; set; }

        /// <summary>TO_PositioningAxis, TO_SpeedAxis, PID_Compact, High_Speed_Counter ...</summary>
        public string? Type { get; set; }

        public string? Version { get; set; }

        public int Parameters { get; set; }

        /// <summary>For an axis: what its drive, encoder and torque interfaces are connected to.</summary>
        public List<string>? Connections { get; set; }

        /// <summary>The parameters asked for (only when one object is read).</summary>
        public List<TechnologyParameterInfo>? Values { get; set; }

        public int? Matching { get; set; }
    }

    public class TechnologyParameterChange
    {
        public string? Name { get; set; }

        public string? Before { get; set; }

        public string? After { get; set; }
    }

    // Technology objects of a PLC (Siemens.Engineering.SW.TechnologicalObjects). Callers: the plc_*technology* tools.
    //
    // Found on V21 (probes of 2026-10-07 on a temporary CPU 1511-1 PN V2.9, rolled back):
    //   - PlcSoftware.TechnologicalObjectGroup.TechnologicalObjects.Create(name, type, version) makes one; the version has
    //     to be one the CPU offers, a wrong one is refused. On that CPU: TO_SpeedAxis, TO_PositioningAxis,
    //     TO_SynchronousAxis and TO_ExternalEncoder 6.0, PID_Compact 2.4, High_Speed_Counter 5.0. There is no call that
    //     lists the offered versions, so the tool tries them from the newest down - each try in a transaction of its
    //     own, because a refused Create forbids the commit of the transaction it happened in.
    //   - TechnologicalInstanceDB.Parameters: 142 on a positioning axis, 83 on PID_Compact; names are paths
    //     ('DynamicLimits.MaxVelocity', 'Sensor[1].Type'), values are read and written.
    //   - An axis gives AxisHardwareConnectionProvider (ActorInterface, SensorInterface[], TorqueInterface) to read and
    //     to undo a connection, and with Startdrive AxisHardwareConnectionSDRProvider to connect it to the telegram of
    //     a SINAMICS drive. The drive has to be in the IO system of the PLC first, or the answer is "Target not
    //     available for input=-1 and output=-1". Connecting the actor to telegram 105 sets the encoder to the same
    //     telegram; a second Connect for the sensor is then refused.
    //   - A technology object is deleted with Delete().
    public partial class Portal
    {
        private PlcSoftware RequirePlcSoftware(string softwarePath)
        {
            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, NoProjectMessage);
            }

            return GetSoftwareContainer(softwarePath)?.Software as PlcSoftware
                   ?? throw new PortalException(PortalErrorCode.NotFound, DescribeMissingSoftware(softwarePath));
        }

        private static IEnumerable<(TechnologicalInstanceDB Object, string Group)> AllTechnologyObjects(TechnologicalInstanceDBGroup group, string path)
        {
            foreach (var item in group.TechnologicalObjects)
            {
                yield return (item, path);
            }

            foreach (var child in group.Groups)
            {
                foreach (var nested in AllTechnologyObjects(child, path.Length == 0 ? child.Name : $"{path}/{child.Name}"))
                {
                    yield return nested;
                }
            }
        }

        private static (TechnologicalInstanceDB Object, string Group) RequireTechnologyObject(PlcSoftware software, string name)
        {
            var all = AllTechnologyObjects(software.TechnologicalObjectGroup, string.Empty).ToList();
            var found = all.Where(t => string.Equals(t.Object.Name, (name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

            return found.Count == 1
                ? found[0]
                : throw new PortalException(PortalErrorCode.NotFound,
                    $"No technology object '{name}' in this PLC. Technology objects: {(all.Count == 0 ? "none" : string.Join(", ", all.Select(t => t.Object.Name)))}.");
        }

        private static object? PlainValue(object? value)
        {
            switch (value)
            {
                case null:
                    return null;
                case string _:
                case bool _:
                case byte _:
                case sbyte _:
                case short _:
                case ushort _:
                case int _:
                case uint _:
                case long _:
                case ulong _:
                case double _:
                    return value;
                case float single:
                    return Math.Round((double)single, 6);
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        private static List<string>? AxisConnections(TechnologicalInstanceDB item)
        {
            AxisHardwareConnectionProvider? provider;

            try
            {
                provider = item.GetService<AxisHardwareConnectionProvider>();
            }
            catch (EngineeringException)
            {
                return null;
            }

            if (provider == null)
            {
                return null;
            }

            string One(string what, AxisEncoderHardwareConnectionInterface? link)
            {
                if (link == null)
                {
                    return $"{what}: n/a";
                }

                try
                {
                    return link.IsConnected ? $"{what}: connected (input address {link.InputAddress / 8}, output address {link.OutputAddress / 8})" : $"{what}: not connected";
                }
                catch (EngineeringException)
                {
                    return $"{what}: n/a";
                }
            }

            var result = new List<string>();

            try
            {
                result.Add(One("drive", provider.ActorInterface));

                var index = 1;

                foreach (var sensor in provider.SensorInterface)
                {
                    result.Add(One($"encoder {index++}", sensor));
                }
            }
            catch (EngineeringException)
            {
                // an object whose interfaces cannot be read
            }

            return result.Count > 0 ? result : null;
        }

        public List<TechnologyObjectInfo> GetTechnologyObjects(string softwarePath, string? name, string? filter, int limit, int offset)
        {
            return Operation.Run(_logger, nameof(GetTechnologyObjects), PortalErrorCode.InvalidState,
                () =>
                {
                    var software = RequirePlcSoftware(softwarePath);
                    var wanted = string.IsNullOrWhiteSpace(name)
                        ? AllTechnologyObjects(software.TechnologicalObjectGroup, string.Empty).ToList()
                        : new List<(TechnologicalInstanceDB, string)> { RequireTechnologyObject(software, name!) };
                    var result = new List<TechnologyObjectInfo>();

                    foreach (var (item, group) in wanted)
                    {
                        var info = new TechnologyObjectInfo
                        {
                            Name = item.Name,
                            Group = group.Length == 0 ? null : group,
                            Type = item.OfSystemLibElement,
                            Version = item.OfSystemLibVersion?.ToString(),
                            Parameters = item.Parameters.Count,
                            Connections = AxisConnections(item)
                        };

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            var text = (filter ?? string.Empty).Trim();
                            var matching = item.Parameters.Where(p => text.Length == 0 || (p.Name ?? string.Empty).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                            info.Matching = matching.Count;
                            info.Values = matching.Skip(Math.Max(0, offset)).Take(limit <= 0 ? int.MaxValue : limit)
                                .Select(p => new TechnologyParameterInfo { Name = p.Name, Value = PlainValue(p.Value) })
                                .ToList();
                        }

                        result.Add(info);
                    }

                    return result;
                },
                ("softwarePath", softwarePath), ("name", name));
        }

        public TechnologyObjectInfo CreateTechnologyObject(string softwarePath, string name, string type, string? version)
        {
            return Operation.Run(_logger, nameof(CreateTechnologyObject), PortalErrorCode.InvalidState,
                () =>
                {
                    var software = RequirePlcSoftware(softwarePath);

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, "'name' and 'type' are needed, e.g. name 'Axis_1', type 'TO_PositioningAxis'.");
                    }

                    var objects = software.TechnologicalObjectGroup.TechnologicalObjects;


                    if (AllTechnologyObjects(software.TechnologicalObjectGroup, string.Empty).Any(t => string.Equals(t.Object.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, $"A technology object '{name}' exists already.");
                    }

                    if (!Version.TryParse((version ?? string.Empty).Trim().TrimStart('V', 'v'), out var asked))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, $"'{version}' is no version; write it like '6.0'.");
                    }

                    TechnologicalInstanceDB created;

                    try
                    {
                        created = objects.Create(name.Trim(), type.Trim(), asked);
                    }
                    catch (EngineeringException ex) when (ErrorText.Describe(ex).IndexOf("does not exist or is not a valid technology object", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        throw new PortalException(PortalErrorCode.NotSupported, $"This CPU offers no technology object '{type}' in version {asked}. TIA Portal: {ErrorText.Describe(ex)}", null, ex);
                    }

                    return new TechnologyObjectInfo
                    {
                        Name = created.Name,
                        Type = created.OfSystemLibElement,
                        Version = created.OfSystemLibVersion?.ToString(),
                        Parameters = created.Parameters.Count,
                        Connections = AxisConnections(created)
                    };
                },
                ("softwarePath", softwarePath), ("name", name), ("type", type));
        }

        public List<TechnologyParameterChange> SetTechnologyParameters(string softwarePath, string name, Dictionary<string, JsonElement>? parameters)
        {
            return Operation.Run(_logger, nameof(SetTechnologyParameters), PortalErrorCode.InvalidState,
                () =>
                {
                    var (item, _) = RequireTechnologyObject(RequirePlcSoftware(softwarePath), name);

                    if (parameters == null || parameters.Count == 0)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, "No parameters given. Example: {\"DynamicLimits.MaxVelocity\": 250, \"Modulo.Enable\": true}.");
                    }

                    var found = new List<(string Name, TechnologicalParameter Parameter, JsonElement Value)>();
                    var missing = new List<string>();

                    foreach (var pair in parameters)
                    {
                        var parameter = item.Parameters.Find(pair.Key.Trim());

                        if (parameter == null)
                        {
                            missing.Add(pair.Key);
                        }
                        else
                        {
                            found.Add((pair.Key.Trim(), parameter, pair.Value));
                        }
                    }

                    if (missing.Count > 0)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Nothing was changed: '{item.Name}' ({item.OfSystemLibElement}) has no parameter {string.Join(", ", missing.Select(m => $"'{m}'"))}. 'plc_get_technology_objects' with the name of the object and a filter lists its parameters.");
                    }

                    var changes = new List<TechnologyParameterChange>();
                    var index = 0;

                    foreach (var (parameterName, parameter, given) in found)
                    {
                        Progress(++index, found.Count, $"{item.Name}: set {parameterName}");

                        var before = parameter.Value;
                        object value;

                        switch (given.ValueKind)
                        {
                            case JsonValueKind.Number:
                                value = given.TryGetInt64(out var whole) ? whole : (object)given.GetDouble();
                                break;
                            case JsonValueKind.True:
                            case JsonValueKind.False:
                                value = given.GetBoolean();
                                break;
                            case JsonValueKind.String:
                                value = given.GetString() ?? string.Empty;
                                break;
                            default:
                                throw new PortalException(PortalErrorCode.InvalidParams, $"'{parameterName}': a value has to be a number, true/false or a text. Nothing was changed.");
                        }

                        if (before != null && !(before is string))
                        {
                            try
                            {
                                value = Convert.ChangeType(value, before.GetType(), CultureInfo.InvariantCulture);
                            }
                            catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
                            {
                                throw new PortalException(PortalErrorCode.InvalidParams, $"'{parameterName}': '{value}' is no {before.GetType().Name}, which is what the parameter holds (now {before}). Nothing was changed.", null, ex);
                            }
                        }

                        try
                        {
                            parameter.Value = value;
                        }
                        catch (Exception ex) when (ex is not PortalException)
                        {
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                $"'{parameterName}' of '{item.Name}' did not take the value {Convert.ToString(value, CultureInfo.InvariantCulture)}: {ErrorText.Describe(ex)}. Nothing was changed.", null, ex);
                        }

                        // read back: TIA Portal may round the value or correct a dependent one
                        changes.Add(new TechnologyParameterChange
                        {
                            Name = parameterName,
                            Before = Convert.ToString(before, CultureInfo.InvariantCulture),
                            After = Convert.ToString(item.Parameters.Find(parameterName)?.Value, CultureInfo.InvariantCulture)
                        });
                    }

                    return changes;
                },
                ("softwarePath", softwarePath), ("name", name));
        }

        public string DeleteTechnologyObject(string softwarePath, string name)
        {
            return Operation.Run(_logger, nameof(DeleteTechnologyObject), PortalErrorCode.InvalidState,
                () =>
                {
                    var (item, _) = RequireTechnologyObject(RequirePlcSoftware(softwarePath), name);
                    var described = $"{item.Name} ({item.OfSystemLibElement} {item.OfSystemLibVersion})";

                    item.Delete();

                    return described;
                },
                ("softwarePath", softwarePath), ("name", name));
        }

        /// <param name="action">connect or disconnect.</param>
        /// <param name="what">drive (the actor interface), encoder, torque.</param>
        public List<string> ConnectTechnologyObject(string softwarePath, string name, string? action, string? what, string? drivePath, string? telegramType)
        {
            return WithStartdrive(() => Operation.Run(_logger, nameof(ConnectTechnologyObject), PortalErrorCode.InvalidState,
                () =>
                {
                    var (item, _) = RequireTechnologyObject(RequirePlcSoftware(softwarePath), name);
                    var verb = (action ?? "connect").Trim().ToLowerInvariant();
                    var side = (what ?? "drive").Trim().ToLowerInvariant();
                    var provider = item.GetService<AxisHardwareConnectionProvider>()
                                   ?? throw new PortalException(PortalErrorCode.NotSupported, $"'{item.Name}' ({item.OfSystemLibElement}) is no axis: it has no drive or encoder interface to connect.");

                    if (side != "drive" && side != "encoder" && side != "torque")
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, $"interface '{what}' is not known. Use drive, encoder or torque.");
                    }

                    if (verb == "disconnect")
                    {
                        if (side == "drive")
                        {
                            provider.ActorInterface.Disconnect();
                        }
                        else if (side == "encoder")
                        {
                            (provider.SensorInterface.FirstOrDefault() ?? throw new PortalException(PortalErrorCode.NotSupported, $"'{item.Name}' has no encoder interface.")).Disconnect();
                        }
                        else
                        {
                            throw new PortalException(PortalErrorCode.NotSupported, "The torque interface is taken off by erasing the torque telegram of the drive ('drive_manage_telegrams').");
                        }
                    }
                    else if (verb == "connect")
                    {
                        if (string.IsNullOrWhiteSpace(drivePath))
                        {
                            throw new PortalException(PortalErrorCode.InvalidParams, "'drivePath' is missing: the drive whose telegram the axis is connected to ('drive_get_objects' lists the drives).");
                        }

                        var (driveItem, _) = RequireDriveItem(drivePath!);

                        DriveAccess.ConnectAxis(item, driveItem, side, telegramType);
                    }
                    else
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, $"action '{action}' is not known. Use connect or disconnect.");
                    }

                    return AxisConnections(item) ?? new List<string>();
                },
                ("softwarePath", softwarePath), ("name", name), ("drivePath", drivePath)));
        }
    }
}
