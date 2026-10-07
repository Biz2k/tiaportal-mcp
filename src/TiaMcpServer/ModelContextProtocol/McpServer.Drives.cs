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
    // The tools of the area 'drive': SINAMICS drives through Startdrive. The work is in Siemens/Portal.Drives.cs.
    // A drive is created like any device ('hw_create_device' with an identifier from the catalog); these tools read
    // and change what Startdrive adds to it: the drive object, its parameters and its telegrams.
    public static partial class McpServer
    {
        public class ResponseDriveObjects : ResponseMessage
        {
            public List<DriveObjectInfo>? Items { get; set; }
        }

        public class ResponseDriveParameters : ResponseMessage
        {
            public DriveParametersResult? Result { get; set; }
        }

        public class ResponseDriveParametersSet : ResponseMessage
        {
            public string? Path { get; set; }

            public List<DriveParameterChange>? Changes { get; set; }
        }

        public class ResponseDriveTelegrams : ResponseMessage
        {
            public string? Path { get; set; }

            public List<string>? Done { get; set; }

            public List<DriveTelegramInfo>? Telegrams { get; set; }
        }

        [McpServerTool(Name = "drive_get_objects", Title = "Get the drives of the project", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the SINAMICS drives of the project that Startdrive knows: for each drive object its path (what the other drive tools take), device and device type, drive object number, number of parameters and its telegrams with number and size. A drive is created with 'hw_create_device' and an identifier from the catalog ('hw_get_catalog', folder 'Drives & starters'). Needs SINAMICS Startdrive installed")]
        public static ResponseDriveObjects GetDriveObjects(
            [Description("devicePath: only the drives of this device; empty (default) lists all of the project")] string devicePath = "")
        {
            try
            {
                var items = Portal.GetDriveObjects(devicePath);

                return new ResponseDriveObjects
                {
                    Items = items,
                    Message = items.Count == 0 ? "No drive Startdrive knows in " + (string.IsNullOrWhiteSpace(devicePath) ? "the project" : $"'{devicePath}'") : $"{items.Count} drive object(s): {string.Join(", ", items.Select(i => i.Path))}",
                    Meta = Ok(new JsonObject())
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolError(ex);
            }
        }

        [McpServerTool(Name = "drive_get_parameters", Title = "Get parameters of a drive", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Read the offline parameters of a drive object: by name ('names', with the list of values of an enumerated parameter, the bits of a bit-coded one and the elements of an indexed one) or by a text found in the name or in the description ('filter', paged). A drive has thousands of parameters - ask for what you need. 'p' parameters are settings, 'r' parameters display values; an indexed parameter is 'p1120' with elements 'p1120[0]' ..., and only the elements carry values. These are the values of the project, not of a running drive")]
        public static ResponseDriveParameters GetDriveParameters(
            [Description("drivePath: path of the drive from 'drive_get_objects', or the device path when the device has one drive object")] string drivePath,
            [Description("names: the parameters to read in full, e.g. [\"p1082\", \"p1120\", \"r46\"]; empty uses filter")] List<string>? names = null,
            [Description("filter: text looked for in the name and in the description, e.g. 'ramp', 'speed', 'p13'; empty (default) lists all")] string filter = "",
            [Description("onlyWritable: leave the 'r' parameters out (default false)")] bool onlyWritable = false,
            [Description("limit: the most parameters to return in one page (default 60); 0 returns all")] int limit = 60,
            [Description("offset: parameters to skip, to read the next page (default 0)")] int offset = 0)
        {
            try
            {
                var result = Portal.GetDriveParameters(drivePath, names, filter, onlyWritable, limit, offset);
                var more = result.Matching - offset - result.Parameters.Count;

                return new ResponseDriveParameters
                {
                    Result = result,
                    Message = $"{result.Parameters.Count} parameter(s) of '{result.Path}' returned ({result.Matching} match, {result.Total} in all)" +
                              (names == null || names.Count == 0 ? (more > 0 ? $"; {more} more - pass offset={offset + result.Parameters.Count}" : string.Empty) : string.Empty) +
                              (result.NotFound != null ? $"; not found: {string.Join(", ", result.NotFound)}" : string.Empty),
                    Meta = Ok(new JsonObject())
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolError(ex);
            }
        }

        [WriteTool]
        [McpServerTool(Name = "drive_set_parameters", Title = "Set parameters of a drive", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Set offline parameters of a drive object, several at once, all or nothing; every value is read back and returned as before/after, since TIA Portal may round or limit it. Write an element of an indexed parameter ('p1120[0]'), not the parameter itself. 'r' parameters cannot be written. A G120 control unit without a power module takes no parameters. The change is in the project; the drive gets it with a download. Wrong drive parameters can damage a machine: change what the user asked for, with the values they gave")]
        public static ResponseDriveParametersSet SetDriveParameters(
            [Description("drivePath: path of the drive from 'drive_get_objects', or the device path when the device has one drive object")] string drivePath,
            [Description("parameters: name -> value, e.g. {\"p1082[0]\": 3000, \"p1121[0]\": 2.5}")] Dictionary<string, JsonElement> parameters)
        {
            return Guarded(nameof(SetDriveParameters), () =>
            {
                var changes = Portal.SetDriveParameters(drivePath, parameters);

                return new ResponseDriveParametersSet
                {
                    Path = drivePath,
                    Changes = changes,
                    Message = $"{changes.Count} parameter(s) of '{drivePath}' set: {string.Join("; ", changes.Select(c => $"{c.Name} {c.Before} -> {c.After}{(c.Unit != null ? " " + c.Unit : string.Empty)}"))}. {SaveHint}",
                    Meta = new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true, ["pendingSave"] = true }
                };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "drive_manage_telegrams", Title = "Manage the telegrams of a drive", Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true),
         Description("Change the PROFIdrive telegrams of a drive object, several actions at once, all or nothing: 'change' gives the telegram of a type another number (e.g. main telegram 105 -> 3), 'insert' adds a supplementary, additional, safety or torque telegram, 'erase' removes one, 'resize' sets the size of an additional telegram. A number the drive does not offer is refused before anything changes. Returns the telegrams afterwards with their sizes. The PLC side - the technology object or the blocks that use the telegram - has to match")]
        public static ResponseDriveTelegrams ManageDriveTelegrams(
            [Description("drivePath: path of the drive from 'drive_get_objects', or the device path when the device has one drive object")] string drivePath,
            [Description("actions: applied in order; fields: action (change, insert, erase, resize), type (main, supplementary, additional, safety, torque), number, inputSize, outputSize")] List<DriveTelegramAction> actions)
        {
            return Guarded(nameof(ManageDriveTelegrams), () =>
            {
                var (done, telegrams) = Portal.ManageDriveTelegrams(drivePath, actions);

                return new ResponseDriveTelegrams
                {
                    Path = drivePath,
                    Done = done,
                    Telegrams = telegrams,
                    Message = $"Telegrams of '{drivePath}': {string.Join("; ", done)}. Now: {string.Join(", ", telegrams.Select(t => $"{t.Type} {t.Number} ({t.InputBytes}/{t.OutputBytes} bytes)"))}. {SaveHint}",
                    Meta = new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true, ["pendingSave"] = true }
                };
            });
        }
    }
}
