using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    // Technology objects of a PLC (axes, PID controllers, counters) and the motor of a drive. The work is in
    // Siemens/Portal.TechnologyObjects.cs and Siemens/Portal.Drives.cs.
    public static partial class McpServer
    {
        public class ResponseTechnologyObjects : ResponseMessage
        {
            public List<TechnologyObjectInfo>? Items { get; set; }
        }

        public class ResponseTechnologyObject : ResponseMessage
        {
            public TechnologyObjectInfo? Object { get; set; }
        }

        public class ResponseTechnologyParameters : ResponseMessage
        {
            public string? Name { get; set; }

            public List<TechnologyParameterChange>? Changes { get; set; }
        }

        public class ResponseTechnologyConnection : ResponseMessage
        {
            public string? Name { get; set; }

            public List<string>? Connections { get; set; }
        }

        public class ResponseDriveMotor : ResponseMessage
        {
            public DriveMotorResult? Result { get; set; }
        }

        /// <summary>The versions tried when none is named, newest first: what TIA Portal V17 to V21 offer for the common types.</summary>
        private static readonly string[] TechnologyVersions = { "9.0", "8.0", "7.0", "6.0", "5.0", "4.0", "3.0", "2.5", "2.4", "2.3", "2.2", "2.1", "2.0", "1.2", "1.1", "1.0" };

        private static JsonObject Pending() => new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true, ["pendingSave"] = true };

        [McpServerTool(Name = "plc_get_technology_objects", Title = "Get technology objects", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the technology objects of a PLC - axes, encoders, PID controllers, counters - with type, version, number of parameters and, for an axis, what its drive and encoder interfaces are connected to. With 'name' the parameters of that object are returned as well ('filter' narrows them by a text in the name, e.g. 'DynamicLimits', 'Sensor[1]'; paged)")]
        public static ResponseTechnologyObjects GetTechnologyObjects(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("name: one technology object, to read its parameters; empty (default) lists all without parameters")] string name = "",
            [Description("filter: text looked for in the parameter names of that object; empty returns all")] string filter = "",
            [Description("limit: the most parameters to return (default 80); 0 returns all")] int limit = 80,
            [Description("offset: parameters to skip (default 0)")] int offset = 0)
        {
            try
            {
                var items = Portal.GetTechnologyObjects(softwarePath, name, filter, limit, offset);
                var one = !string.IsNullOrWhiteSpace(name) && items.Count == 1 ? items[0] : null;

                return new ResponseTechnologyObjects
                {
                    Items = items,
                    Message = one != null
                        ? $"'{one.Name}' ({one.Type} {one.Version}): {one.Values?.Count ?? 0} of {one.Matching} matching parameter(s) returned, {one.Parameters} in all" +
                          (one.Matching > offset + (one.Values?.Count ?? 0) ? $"; pass offset={offset + (one.Values?.Count ?? 0)} for more" : string.Empty)
                        : $"{items.Count} technology object(s) in '{softwarePath}'" + (items.Count > 0 ? ": " + string.Join(", ", items.Select(i => $"{i.Name} ({i.Type})")) : string.Empty),
                    Meta = Ok(new JsonObject())
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolError(ex);
            }
        }

        [WriteTool]
        [McpServerTool(Name = "plc_create_technology_object", Title = "Create a technology object", Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true),
         Description("Create a technology object in a PLC: an axis (TO_SpeedAxis, TO_PositioningAxis, TO_SynchronousAxis), an encoder (TO_ExternalEncoder), a controller (PID_Compact, PID_3Step, PID_Temp), a counter (High_Speed_Counter) and whatever else the CPU offers. Without a version the newest one the CPU takes is used. The object comes with default values: set its parameters with 'plc_set_technology_parameters' and connect an axis to its drive with 'plc_connect_technology_object'")]
        public static ResponseTechnologyObject CreateTechnologyObject(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("name: name of the new object, e.g. 'Axis_1'")] string name,
            [Description("type: type of the object, e.g. 'TO_PositioningAxis' or 'PID_Compact'")] string type,
            [Description("version: version of the type, e.g. '6.0'; empty (default) takes the newest the CPU offers")] string version = "")
        {
            // No transaction around the whole call: without a version the versions are tried one by one, and a refused
            // Create forbids the commit of the transaction it happened in - so every try gets its own.
            return GuardedNoTransaction(nameof(CreateTechnologyObject), () =>
            {
                TechnologyObjectInfo? created = null;
                var candidates = string.IsNullOrWhiteSpace(version) ? TechnologyVersions : new[] { version };
                string? refusal = null;

                foreach (var candidate in candidates)
                {
                    try
                    {
                        created = Portal.InTransaction($"Create technology object {name}", () => Portal.CreateTechnologyObject(softwarePath, name, type, candidate));
                        break;
                    }
                    catch (PortalException ex) when (ex.Code == PortalErrorCode.NotSupported)
                    {
                        refusal = ex.Message;
                    }
                }

                if (created == null)
                {
                    throw new PortalException(PortalErrorCode.NotSupported,
                        candidates.Length == 1
                            ? refusal ?? $"Technology object '{type}' {version} could not be created."
                            : $"This CPU offers no technology object '{type}' in any of the versions {string.Join(", ", candidates)}. Check the type name (TO_SpeedAxis, TO_PositioningAxis, TO_SynchronousAxis, " +
                              "TO_ExternalEncoder, PID_Compact, PID_3Step, PID_Temp, High_Speed_Counter ...) or name the version. Nothing was created.");
                }

                return new ResponseTechnologyObject
                {
                    Object = created,
                    Message = $"Technology object '{created.Name}' created: {created.Type} {created.Version}, {created.Parameters} parameters. {SaveHint}",
                    Meta = Pending()
                };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "plc_set_technology_parameters", Title = "Set parameters of a technology object", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Set parameters of a technology object, several at once, all or nothing; each value is read back and returned as before/after. The names are paths as 'plc_get_technology_objects' lists them, e.g. 'DynamicLimits.MaxVelocity', 'Mechanics.LeadScrew', 'Sensor[1].Type'. Limits of an axis decide how a machine moves: set what the user asked for")]
        public static ResponseTechnologyParameters SetTechnologyParameters(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("name: name of the technology object")] string name,
            [Description("parameters: name -> value, e.g. {\"DynamicLimits.MaxVelocity\": 250, \"Modulo.Enable\": true}")] Dictionary<string, JsonElement> parameters)
        {
            return Guarded(nameof(SetTechnologyParameters), () =>
            {
                var changes = Portal.SetTechnologyParameters(softwarePath, name, parameters);

                return new ResponseTechnologyParameters
                {
                    Name = name,
                    Changes = changes,
                    Message = $"{changes.Count} parameter(s) of '{name}' set: {string.Join("; ", changes.Select(c => $"{c.Name} {c.Before} -> {c.After}"))}. {SaveHint}",
                    Meta = Pending()
                };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "plc_connect_technology_object", Title = "Connect an axis to a drive", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Connect an axis (or take it off) to the telegram of a SINAMICS drive: 'drive' is the setpoint side of the axis, 'encoder' its first encoder, 'torque' the torque data. The drive has to be in the IO system of the PLC first ('net_connect_subnet', 'net_connect_to_io_system'). Connecting the drive side to a telegram that carries encoder values (3, 5, 105 ...) connects the encoder with it. Returns the connections afterwards. Needs SINAMICS Startdrive")]
        public static ResponseTechnologyConnection ConnectTechnologyObject(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("name: name of the axis")] string name,
            [Description("drivePath: path of the drive from 'drive_get_objects'; not needed for disconnect")] string drivePath = "",
            [Description("action: connect (default) or disconnect")] string action = "connect",
            [Description("interface: drive (default), encoder or torque")] string @interface = "drive",
            [Description("telegramType: which telegram of the drive: main (default), supplementary, additional, torque")] string telegramType = "")
        {
            return Guarded(nameof(ConnectTechnologyObject), () =>
            {
                var connections = Portal.ConnectTechnologyObject(softwarePath, name, action, @interface, drivePath, telegramType);

                return new ResponseTechnologyConnection
                {
                    Name = name,
                    Connections = connections,
                    Message = $"'{name}': {string.Join("; ", connections)}. {SaveHint}",
                    Meta = Pending()
                };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "plc_delete_technology_object", Title = "Delete a technology object", Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a technology object of a PLC. Blocks that call it (MC_Power, MC_MoveAbsolute, PID calls) stop compiling: look for them first with 'plc_where_used'")]
        public static ResponseMessage DeleteTechnologyObject(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("name: name of the technology object")] string name)
        {
            return Guarded(nameof(DeleteTechnologyObject), () =>
            {
                var deleted = Portal.DeleteTechnologyObject(softwarePath, name);

                return new ResponseMessage { Message = $"Technology object {deleted} deleted. {SaveHint}", Meta = Pending() };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "drive_set_motor", Title = "Set the motor of a drive", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Set the motor type of a drive and its rating plate data, for drives whose motor is entered by data (G120 and the like): motorType e.g. 'InductionMotor', then the data TIA Portal asks for with that type - p304 rated voltage, p305 rated current, p307 rated power, p310 rated frequency, p311 rated speed, p335 cooling - in 'values'. Called with neither, it returns the data now in the project and the motor types. A G120 needs its power module first ('hw_plug_module' with the control unit as parentItemName, position 3). Use the data of the motor's rating plate as the user gave them: a wrong motor is overloaded or does not turn")]
        public static ResponseDriveMotor SetDriveMotor(
            [Description("drivePath: path of the drive from 'drive_get_objects', or the device path when the device has one drive object")] string drivePath,
            [Description("motorType: e.g. InductionMotor, SynchronousMotor, InductionMotor1LE1; empty leaves the type as it is")] string motorType = "",
            [Description("values: rating plate data by parameter name, e.g. {\"p305\": 1.5, \"p307\": 0.55, \"p311\": 1425}; empty changes none")] Dictionary<string, JsonElement>? values = null,
            [Description("dataSet: number of the drive data set (default 0)")] int dataSet = 0)
        {
            return Guarded(nameof(SetDriveMotor), () =>
            {
                var result = Portal.SetDriveMotor(drivePath, motorType, dataSet, values);

                return new ResponseDriveMotor
                {
                    Result = result,
                    Message = (result.Done.Count > 0 ? $"Motor of '{result.Path}' set: {string.Join("; ", result.Done)}. " : $"Motor of '{result.Path}', nothing changed. ") +
                              $"Motor data now: {string.Join(", ", result.Required.Select(r => $"{r.Name}={Convert.ToString(r.Value, System.Globalization.CultureInfo.InvariantCulture)}{(r.Unit != null ? " " + r.Unit : string.Empty)}"))}." +
                              (result.Done.Count > 0 ? " " + SaveHint : string.Empty),
                    Meta = Pending()
                };
            });
        }
    }
}
