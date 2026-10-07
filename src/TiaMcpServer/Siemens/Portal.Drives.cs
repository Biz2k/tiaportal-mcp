using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.MC.Drives;
using Siemens.Engineering.MC.Drives.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace TiaMcpServer.Siemens
{
    public class DriveTelegramInfo
    {
        /// <summary>MainTelegram, SupplementaryTelegram, AdditionalTelegram, SafetyTelegram, TorqueTelegram ...</summary>
        public string? Type { get; set; }

        public int Number { get; set; }

        public int InputBytes { get; set; }

        public int OutputBytes { get; set; }
    }

    public class DriveObjectInfo
    {
        /// <summary>What the other drive tools take as drivePath.</summary>
        public string? Path { get; set; }

        public string? Device { get; set; }

        public string? DeviceType { get; set; }

        public string? Item { get; set; }

        public int? DriveObjectNumber { get; set; }

        public int Parameters { get; set; }

        public List<DriveTelegramInfo> Telegrams { get; set; } = new List<DriveTelegramInfo>();

        public List<string>? Notes { get; set; }
    }

    public class DriveParameterInfo
    {
        /// <summary>'p1082' for a plain parameter, 'p1120[0]' for an element of an indexed one; 'r...' is read-only.</summary>
        public string? Name { get; set; }

        public string? Text { get; set; }

        public object? Value { get; set; }

        public string? Unit { get; set; }

        public object? Min { get; set; }

        public object? Max { get; set; }

        /// <summary>For an indexed parameter itself: how many elements it has; its values are in the elements.</summary>
        public int? Elements { get; set; }

        /// <summary>The values an enumerated parameter takes, value -> text.</summary>
        public Dictionary<string, string>? Enum { get; set; }

        /// <summary>The bits of a bit-coded parameter, 'r46.0 = 1 OFF1 enable missing'.</summary>
        public List<string>? Bits { get; set; }
    }

    public class DriveParametersResult
    {
        public string? Path { get; set; }

        public int Total { get; set; }

        public int Matching { get; set; }

        public List<DriveParameterInfo> Parameters { get; set; } = new List<DriveParameterInfo>();

        public List<string>? NotFound { get; set; }
    }

    public class DriveParameterChange
    {
        public string? Name { get; set; }

        public string? Text { get; set; }

        public string? Before { get; set; }

        public string? After { get; set; }

        public string? Unit { get; set; }
    }

    /// <summary>One change to the telegrams of a drive object.</summary>
    public class DriveTelegramAction
    {
        [System.ComponentModel.Description("'change' (give the telegram of this type another number), 'insert', 'erase' or 'resize' (additional telegram)")]
        public string? Action { get; set; }

        [System.ComponentModel.Description("Type of the telegram: main (default), supplementary, additional, safety, torque")]
        public string? Type { get; set; }

        [System.ComponentModel.Description("Telegram number, e.g. 1, 3, 5, 105, 111, 352, 750, 30; 999 is free telegram configuration")]
        public int? Number { get; set; }

        [System.ComponentModel.Description("Additional telegram: input size in words")]
        public int? InputSize { get; set; }

        [System.ComponentModel.Description("Additional telegram: output size in words")]
        public int? OutputSize { get; set; }

        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, JsonElement>? Unknown { get; set; }
    }

    // SINAMICS drives through Startdrive Openness (Siemens.Engineering.Startdrive.dll). Callers: the drive_* tools.
    //
    // Found on V21 with Startdrive Advanced (probes of 2026-10-07 on temporary devices, rolled back):
    //   - A drive is a device like any other: 'hw_create_device' with a catalog identifier
    //     ('OrderNumber:6SL3210-5HB10-1xFx/V5.2.3/S210') makes it. The device item that carries the drive object gives
    //     DriveObjectContainer: the control unit of a G120, the item 'Drive control' of an S210 (its 'Motor_1' gives the
    //     same drive object once more).
    //   - DriveObject.Parameters: 1361 on an S210, 4093 on a G120 CU240E-2. An indexed parameter is there twice: as
    //     'p1120' (ArrayLength 4, no value, not writable - "as header parameter cannot be written") and as its
    //     elements 'p1120[0]' ... which carry value, unit and limits. Find(name) and Find(number, index) both work.
    //     'r' parameters refuse a write ("as read-only parameter cannot be written").
    //   - A G120 control unit alone takes no parameter write: "There is no PowerModule added to the device".
    //   - Telegrams: Telegram.TelegramNumber is settable after CanChangeTelegram(n); the sizes follow (S210: 105 is
    //     20 bytes each way, 3 is 18 in). TelegramComposition has CanInsert... / Insert... per type and EraseTelegram.
    //     After EraseTelegram the telegram objects read before are disposed: ask the drive object for them again.
    //   - The types of this assembly load only where Startdrive is installed: they are kept inside DriveAccess, and a
    //     machine without Startdrive answers NotSupported instead of failing to load the server.
    public partial class Portal
    {
        private T WithStartdrive<T>(Func<T> body)
        {
            try
            {
                return body();
            }
            catch (Exception ex) when (StartdriveMissing(ex))
            {
                throw new PortalException(PortalErrorCode.NotSupported,
                    "The drive tools need SINAMICS Startdrive installed with TIA Portal; its Openness assembly (Siemens.Engineering.Startdrive.dll) was not found. 'get_installed_software' lists what is installed.", null, ex);
            }
        }

        /// <summary>Whether the failure is the Startdrive assembly not loading - by itself or wrapped by Operation.Run.</summary>
        private static bool StartdriveMissing(Exception? ex)
        {
            for (var depth = 0; ex != null && depth < 6; ex = ex.InnerException, depth++)
            {
                if ((ex is System.IO.FileNotFoundException || ex is TypeLoadException || ex is System.IO.FileLoadException) && ex.Message.IndexOf("Startdrive", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The device items of a device that carry a drive object, top ones first; one per drive object number.</summary>
        private List<(Device Device, DeviceItem Item, string Path)> DriveItems(Device device)
        {
            var result = new List<(Device, DeviceItem, string)>();
            var seen = new HashSet<string>();

            void Walk(IEnumerable<DeviceItem> items, string prefix, int depth)
            {
                foreach (var item in items)
                {
                    var path = $"{prefix}/{EscapeSegment(item.Name)}";
                    var number = DriveAccess.NumberOf(item);

                    if (number != null && seen.Add(number))
                    {
                        result.Add((device, item, path));
                    }

                    if (depth < 4)
                    {
                        Walk(item.DeviceItems, path, depth + 1);
                    }
                }
            }

            Walk(device.DeviceItems, GetDevicePath(device), 0);

            return result;
        }

        private (DeviceItem Item, string Path) RequireDriveItem(string drivePath)
        {
            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, NoProjectMessage);
            }

            var device = GetDevice(drivePath);

            if (device != null)
            {
                var items = DriveItems(device);

                return items.Count == 1
                    ? (items[0].Item, items[0].Path)
                    : throw new PortalException(items.Count == 0 ? PortalErrorCode.NotSupported : PortalErrorCode.InvalidParams,
                        items.Count == 0
                            ? $"Device '{device.Name}' has no drive object: it is no SINAMICS drive Startdrive knows. 'drive_get_objects' lists the drives of the project."
                            : $"Device '{device.Name}' has {items.Count} drive objects; name one: {string.Join(", ", items.Select(i => $"'{i.Path}'"))}.");
            }

            var item = GetDeviceItemUnlocked(drivePath);

            if (item != null && DriveAccess.NumberOf(item) != null)
            {
                return (item, drivePath);
            }

            throw new PortalException(PortalErrorCode.NotFound,
                item == null
                    ? $"No drive at '{drivePath}'. 'drive_get_objects' lists the drives of the project with their paths."
                    : $"'{drivePath}' carries no drive object. 'drive_get_objects' lists the items that do.");
        }

        public List<DriveObjectInfo> GetDriveObjects(string? devicePath)
        {
            return WithStartdrive(() => Operation.Run(_logger, nameof(GetDriveObjects), PortalErrorCode.InvalidState,
                () =>
                {
                    var devices = string.IsNullOrWhiteSpace(devicePath) ? GetDevicesUnlocked() : new List<Device> { RequireDevice(devicePath!) };
                    var result = new List<DriveObjectInfo>();

                    foreach (var device in devices)
                    {
                        foreach (var (owner, item, path) in DriveItems(device))
                        {
                            var info = DriveAccess.Describe(item);

                            info.Path = path;
                            info.Device = owner.Name;
                            info.DeviceType = owner.TypeIdentifier;
                            info.Item = item.Name;
                            result.Add(info);
                        }
                    }

                    return result;
                },
                ("devicePath", devicePath)));
        }

        public DriveParametersResult GetDriveParameters(string drivePath, List<string>? names, string? filter, bool onlyWritable, int limit, int offset)
        {
            return WithStartdrive(() => Operation.Run(_logger, nameof(GetDriveParameters), PortalErrorCode.InvalidState,
                () =>
                {
                    var (item, path) = RequireDriveItem(drivePath);
                    var result = DriveAccess.ReadParameters(item, names, filter, onlyWritable, limit, offset);

                    result.Path = path;

                    return result;
                },
                ("drivePath", drivePath)));
        }

        public List<DriveParameterChange> SetDriveParameters(string drivePath, Dictionary<string, JsonElement>? parameters)
        {
            return WithStartdrive(() => Operation.Run(_logger, nameof(SetDriveParameters), PortalErrorCode.InvalidState,
                () =>
                {
                    var (item, _) = RequireDriveItem(drivePath);

                    if (parameters == null || parameters.Count == 0)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, "No parameters given. Example: {\"p1082[0]\": 3000, \"p1121[0]\": 2.5}.");
                    }

                    return DriveAccess.WriteParameters(item, parameters, (i, name) => Progress(i, parameters.Count, $"{item.Name}: set {name}"));
                },
                ("drivePath", drivePath)));
        }

        public (List<string> Done, List<DriveTelegramInfo> Telegrams) ManageDriveTelegrams(string drivePath, List<DriveTelegramAction>? actions)
        {
            return WithStartdrive(() => Operation.Run(_logger, nameof(ManageDriveTelegrams), PortalErrorCode.InvalidState,
                () =>
                {
                    var (item, _) = RequireDriveItem(drivePath);

                    if (actions == null || actions.Count == 0)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, "No actions given. Example: [{\"action\": \"change\", \"type\": \"main\", \"number\": 105}].");
                    }

                    return DriveAccess.ManageTelegrams(item, actions);
                },
                ("drivePath", drivePath)));
        }

        /// <summary>
        /// Everything that names a type of Siemens.Engineering.Startdrive.dll. The methods are not inlined, so the
        /// assembly is asked for only when one of them runs.
        /// </summary>
        private static class DriveAccess
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            private static DriveObject? ObjectOf(DeviceItem item)
            {
                try
                {
                    return item.GetService<DriveObjectContainer>()?.DriveObjects.FirstOrDefault();
                }
                catch (EngineeringException)
                {
                    return null;
                }
            }

            /// <summary>A key for the drive object an item carries, null when it carries none.</summary>
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static string? NumberOf(DeviceItem item)
            {
                var drive = ObjectOf(item);

                if (drive == null)
                {
                    return null;
                }

                try
                {
                    // a G120 control unit answers without a number; its parent then tells the drives apart
                    var number = drive.DriveObjectNumber;

                    return number > 0 ? number.ToString(CultureInfo.InvariantCulture) : "item:" + (item.Parent as DeviceItem)?.Name + "/" + item.Name;
                }
                catch (Exception)
                {
                    return "item:" + item.Name;
                }
            }

            private static DriveObject Require(DeviceItem item) =>
                ObjectOf(item) ?? throw new PortalException(PortalErrorCode.NotSupported, $"'{item.Name}' carries no drive object.");

            private static List<DriveTelegramInfo> Telegrams(DriveObject drive)
            {
                var result = new List<DriveTelegramInfo>();

                foreach (var telegram in drive.Telegrams)
                {
                    var info = new DriveTelegramInfo { Type = telegram.Type.ToString(), Number = telegram.TelegramNumber };

                    try
                    {
                        info.InputBytes = telegram.GetSizeInBytes(AddressIoType.Input);
                        info.OutputBytes = telegram.GetSizeInBytes(AddressIoType.Output);
                    }
                    catch (EngineeringException)
                    {
                        // a telegram without a size
                    }

                    result.Add(info);
                }

                return result;
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static DriveObjectInfo Describe(DeviceItem item)
            {
                var drive = Require(item);
                var info = new DriveObjectInfo();

                try
                {
                    var number = drive.DriveObjectNumber;

                    info.DriveObjectNumber = number > 0 ? number : (int?)null;
                }
                catch (Exception)
                {
                    // no number
                }

                info.Parameters = drive.Parameters.Count;

                try
                {
                    info.Telegrams = Telegrams(drive);
                }
                catch (EngineeringException ex)
                {
                    info.Notes = new List<string> { "The telegrams could not be read: " + ErrorText.Describe(ex) };
                }

                return info;
            }

            private static object? Plain(object? value)
            {
                switch (value)
                {
                    case null:
                        return null;
                    case string text:
                        return text.Length == 0 ? null : text;
                    case float single:
                        // limits of TIA Portal's own "no limit"
                        return float.IsInfinity(single) || Math.Abs(single) >= 3.4e38f ? (object?)null : Math.Round((double)single, 6);
                    case double number:
                        return double.IsInfinity(number) || Math.Abs(number) >= 3.4e38 ? (object?)null : number;
                    case bool _:
                    case byte _:
                    case sbyte _:
                    case short _:
                    case ushort _:
                    case int _:
                    case uint _:
                    case long _:
                    case ulong _:
                        return value;
                    default:
                        return Convert.ToString(value, CultureInfo.InvariantCulture);
                }
            }

            private static DriveParameterInfo Info(DriveParameter parameter, bool detailed)
            {
                var info = new DriveParameterInfo { Name = parameter.Name, Text = parameter.ParameterText };

                if (parameter.ArrayLength > 0 && parameter.ArrayIndex < 0)
                {
                    info.Elements = parameter.ArrayLength;

                    return info;
                }

                try
                {
                    info.Value = Plain(parameter.Value);
                }
                catch (EngineeringException)
                {
                    // a parameter without a value in this configuration
                }

                info.Unit = string.IsNullOrEmpty(parameter.Unit) ? null : parameter.Unit;
                info.Min = Plain(parameter.MinValue);
                info.Max = Plain(parameter.MaxValue);

                if (!detailed)
                {
                    return info;
                }

                try
                {
                    var values = parameter.EnumValueList;

                    if (values != null && values.Count > 1)
                    {
                        info.Enum = new Dictionary<string, string>();

                        foreach (System.Collections.DictionaryEntry pair in (System.Collections.IDictionary)values)
                        {
                            info.Enum[Convert.ToString(pair.Key, CultureInfo.InvariantCulture) ?? string.Empty] = Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                        }
                    }
                }
                catch (Exception)
                {
                    // the list is a help, not the value
                }

                try
                {
                    var bits = parameter.Bits;

                    if (bits != null && bits.Count > 0)
                    {
                        info.Bits = bits.Select(b => $"{b.Name} = {Convert.ToString(b.Value, CultureInfo.InvariantCulture)} {b.ParameterText}".Trim()).ToList();
                    }
                }
                catch (Exception)
                {
                    // no bits
                }

                return info;
            }

            private static DriveParameter? Find(DriveObject drive, string name)
            {
                var wanted = (name ?? string.Empty).Trim();

                try
                {
                    return drive.Parameters.Find(wanted);
                }
                catch (EngineeringException)
                {
                    return null;
                }
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static DriveParametersResult ReadParameters(DeviceItem item, List<string>? names, string? filter, bool onlyWritable, int limit, int offset)
            {
                var drive = Require(item);
                var result = new DriveParametersResult { Total = drive.Parameters.Count };

                if (names != null && names.Count > 0)
                {
                    foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)))
                    {
                        var parameter = Find(drive, name);

                        if (parameter == null)
                        {
                            (result.NotFound ??= new List<string>()).Add(name);
                            continue;
                        }

                        result.Parameters.Add(Info(parameter, true));

                        // the elements of an indexed parameter come with it
                        if (parameter.ArrayLength > 0 && parameter.ArrayIndex < 0)
                        {
                            for (var i = 0; i < parameter.ArrayLength && i < 64; i++)
                            {
                                var element = Find(drive, $"{parameter.Name}[{i}]");

                                if (element != null)
                                {
                                    result.Parameters.Add(Info(element, false));
                                }
                            }
                        }
                    }

                    result.Matching = result.Parameters.Count;

                    return result;
                }

                var text = (filter ?? string.Empty).Trim();
                var skipped = 0;

                foreach (var parameter in drive.Parameters)
                {
                    var name = parameter.Name ?? string.Empty;

                    if (onlyWritable && name.StartsWith("r", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (text.Length > 0 && name.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0 && (parameter.ParameterText ?? string.Empty).IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    result.Matching++;

                    if (skipped < Math.Max(0, offset))
                    {
                        skipped++;
                        continue;
                    }

                    if (limit > 0 && result.Parameters.Count >= limit)
                    {
                        continue;
                    }

                    result.Parameters.Add(Info(parameter, false));
                }

                return result;
            }

            private static object Convert_(JsonElement given, object? present, string name)
            {
                object raw;

                switch (given.ValueKind)
                {
                    case JsonValueKind.Number:
                        raw = given.TryGetInt64(out var whole) ? whole : (object)given.GetDouble();
                        break;
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        raw = given.GetBoolean();
                        break;
                    case JsonValueKind.String:
                        raw = given.GetString() ?? string.Empty;
                        break;
                    default:
                        throw new PortalException(PortalErrorCode.InvalidParams, $"'{name}': a value has to be a number, true/false or a text.");
                }

                if (present == null || present is string)
                {
                    return raw;
                }

                try
                {
                    return Convert.ChangeType(raw, present.GetType(), CultureInfo.InvariantCulture);
                }
                catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, $"'{name}': '{raw}' is no {present.GetType().Name}, which is what the parameter holds (now {present}).", null, ex);
                }
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static List<DriveParameterChange> WriteParameters(DeviceItem item, Dictionary<string, JsonElement> parameters, Action<int, string> progress)
            {
                var drive = Require(item);
                var changes = new List<DriveParameterChange>();
                var problems = new List<string>();
                var found = new List<(string Name, DriveParameter Parameter)>();

                // everything that can be checked, before the first write
                foreach (var pair in parameters)
                {
                    var name = pair.Key.Trim();
                    var parameter = Find(drive, name);

                    if (parameter == null)
                    {
                        problems.Add($"'{name}': no such parameter on this drive object ('drive_get_parameters' with a filter finds the name).");
                    }
                    else if (name.StartsWith("r", StringComparison.OrdinalIgnoreCase))
                    {
                        problems.Add($"'{name}' ({parameter.ParameterText}): an 'r' parameter is a display value and cannot be written.");
                    }
                    else if (parameter.ArrayLength > 0 && parameter.ArrayIndex < 0)
                    {
                        problems.Add($"'{name}' ({parameter.ParameterText}) is an indexed parameter: write its element '{name}[0]'" + (parameter.ArrayLength > 1 ? $" (up to '{name}[{parameter.ArrayLength - 1}]')" : string.Empty) + ".");
                    }
                    else
                    {
                        found.Add((name, parameter));
                    }
                }

                if (problems.Count > 0)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, $"Nothing was changed: {problems.Count} of the {parameters.Count} parameter(s) cannot be written. {string.Join(" ", problems)}");
                }

                var index = 0;

                foreach (var (name, parameter) in found)
                {
                    progress(++index, name);

                    object? before = null;

                    try
                    {
                        before = parameter.Value;
                    }
                    catch (EngineeringException)
                    {
                        // written blind
                    }

                    var value = Convert_(parameters.First(p => p.Key.Trim() == name).Value, before, name);

                    try
                    {
                        parameter.Value = value;
                    }
                    catch (Exception ex) when (ex is not PortalException)
                    {
                        var reason = ErrorText.Describe(ex);
                        var hint = reason.IndexOf("no PowerModule", StringComparison.OrdinalIgnoreCase) >= 0
                            ? " The drive has no power module yet: plug one first ('hw_plug_module'), a control unit alone takes no parameters."
                            : $" Limits: {Convert.ToString(Plain(parameter.MinValue), CultureInfo.InvariantCulture)} .. {Convert.ToString(Plain(parameter.MaxValue), CultureInfo.InvariantCulture)} {parameter.Unit}.";

                        throw new PortalException(PortalErrorCode.InvalidParams, $"'{name}' ({parameter.ParameterText}) did not take the value {Convert.ToString(value, CultureInfo.InvariantCulture)}: {reason}.{hint} Nothing was changed.", null, ex);
                    }

                    // read back: TIA Portal may round or limit
                    var after = Find(drive, name)?.Value;

                    changes.Add(new DriveParameterChange
                    {
                        Name = name,
                        Text = parameter.ParameterText,
                        Before = Convert.ToString(before, CultureInfo.InvariantCulture),
                        After = Convert.ToString(after, CultureInfo.InvariantCulture),
                        Unit = string.IsNullOrEmpty(parameter.Unit) ? null : parameter.Unit
                    });
                }

                return changes;
            }

            private static TelegramType TypeOf(string? type, int index)
            {
                switch ((type ?? "main").Trim().ToLowerInvariant())
                {
                    case "main":
                    case "maintelegram":
                        return TelegramType.MainTelegram;
                    case "supplementary":
                    case "supplementarytelegram":
                        return TelegramType.SupplementaryTelegram;
                    case "additional":
                    case "additionaltelegram":
                        return TelegramType.AdditionalTelegram;
                    case "safety":
                    case "safetytelegram":
                        return TelegramType.SafetyTelegram;
                    case "torque":
                    case "torquetelegram":
                        return TelegramType.TorqueTelegram;
                    default:
                        throw new PortalException(PortalErrorCode.InvalidParams, $"Action {index}: type '{type}' is not known. Use main, supplementary, additional, safety or torque.");
                }
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static (List<string> Done, List<DriveTelegramInfo> Telegrams) ManageTelegrams(DeviceItem item, List<DriveTelegramAction> actions)
            {
                var done = new List<string>();

                for (var i = 0; i < actions.Count; i++)
                {
                    var action = actions[i] ?? throw new PortalException(PortalErrorCode.InvalidParams, $"Action {i + 1}: is empty.");

                    if (action.Unknown != null && action.Unknown.Count > 0)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, $"Action {i + 1}: unknown field(s) {string.Join(", ", action.Unknown.Keys)}. The fields are: action, type, number, inputSize, outputSize.");
                    }

                    var verb = (action.Action ?? string.Empty).Trim().ToLowerInvariant();
                    var type = TypeOf(action.Type, i + 1);

                    // asked for anew at every action: erasing or inserting a telegram disposes the objects read before it
                    var telegrams = Require(item).Telegrams;
                    var present = telegrams.Find(type);

                    int Number() => action.Number ?? throw new PortalException(PortalErrorCode.InvalidParams, $"Action {i + 1} ({verb}): 'number' is missing.");

                    switch (verb)
                    {
                        case "change":
                            if (present == null)
                            {
                                throw new PortalException(PortalErrorCode.NotFound, $"Action {i + 1}: the drive object has no {type}; use 'insert'.");
                            }

                            if (!present.CanChangeTelegram(Number()))
                            {
                                throw new PortalException(PortalErrorCode.InvalidParams, $"Action {i + 1}: this drive object does not take telegram {Number()} as its {type} (now {present.TelegramNumber}). Nothing was changed.");
                            }

                            var old = present.TelegramNumber;

                            present.TelegramNumber = Number();
                            done.Add($"{type}: {old} -> {Number()}");
                            break;

                        case "insert":
                            if (type == TelegramType.AdditionalTelegram)
                            {
                                var input = action.InputSize ?? 0;
                                var output = action.OutputSize ?? 0;

                                if (!telegrams.CanInsertAdditionalTelegram(input, output))
                                {
                                    throw new PortalException(PortalErrorCode.InvalidParams, $"Action {i + 1}: an additional telegram of {input} / {output} words cannot be inserted here. Nothing was changed.");
                                }

                                telegrams.InsertAdditionalTelegram(input, output);
                                done.Add($"inserted {type} ({input} in / {output} out)");
                                break;
                            }

                            if (!telegrams.CanInsertTelegram(Number(), type))
                            {
                                throw new PortalException(PortalErrorCode.InvalidParams,
                                    $"Action {i + 1}: telegram {Number()} cannot be inserted as {type} here" + (present != null ? $" (there is one already, number {present.TelegramNumber}: use 'change')" : string.Empty) + ". Nothing was changed.");
                            }

                            telegrams.InsertTelegram(Number(), type);
                            done.Add($"inserted {type} {Number()}");
                            break;

                        case "erase":
                            if (present == null)
                            {
                                throw new PortalException(PortalErrorCode.NotFound, $"Action {i + 1}: the drive object has no {type}.");
                            }

                            // read before the erase: the telegram object is disposed by it
                            var erased = present.TelegramNumber;

                            telegrams.EraseTelegram(type);
                            done.Add($"erased {type} {erased}");
                            break;

                        case "resize":
                            if (present == null)
                            {
                                throw new PortalException(PortalErrorCode.NotFound, $"Action {i + 1}: the drive object has no {type}.");
                            }

                            foreach (var (io, size) in new[] { (AddressIoType.Input, action.InputSize), (AddressIoType.Output, action.OutputSize) })
                            {
                                if (size == null)
                                {
                                    continue;
                                }

                                if (!present.CanChangeSize(io, size.Value, false))
                                {
                                    throw new PortalException(PortalErrorCode.InvalidParams, $"Action {i + 1}: the {io} size of the {type} cannot be set to {size}. Nothing was changed.");
                                }

                                present.ChangeSize(io, size.Value, false);
                                done.Add($"{type}: {io} size {size}");
                            }

                            break;

                        default:
                            throw new PortalException(PortalErrorCode.InvalidParams, $"Action {i + 1}: action '{action.Action}' is not known. Use change, insert, erase or resize.");
                    }
                }

                return (done, Telegrams(Require(item)));
            }
        }
    }
}
