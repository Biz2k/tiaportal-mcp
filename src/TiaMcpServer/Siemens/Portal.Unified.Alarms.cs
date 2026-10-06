using Siemens.Engineering;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.HmiUnified.HmiAlarm;
using Siemens.Engineering.HmiUnified.HmiAlarm.HmiAlarmCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Siemens
{
    // WinCC Unified: discrete alarms, analog alarms and alarm classes.
    //
    // Callers: the tools unified_get_alarms, unified_get_alarm_classes, unified_manage_alarms
    // and unified_manage_alarm_classes in McpServer.Unified.cs. Reads and writes no data files.
    //
    // Acknowledgement tags and parameter tags, V21, 2026-10-06 (PC station): AcknowledgmentStateTag / ControlTag and their
    // bit numbers write normally, but Openness takes any text for a tag and any number (-1, 99) for a bit, so both are checked
    // here. AlarmParameterTags is a List<string> of ten slots, all empty on the alarms of the test project; a shorter list
    // fills the first slots and no name is checked by Openness.
    //
    // System alarm classes, V21, 2026-10-06 (PC station): the colors of the states and Log can be changed; Priority and Name
    // are refused by Openness, and a system class cannot be deleted ("Alarm object cannot be deleted"). Log names an alarm log
    // and Openness refuses a name that does not exist (an exception, so it is checked here first).
    //
    // DANGER, found on TIA Portal V21 (2026-10-06): calling GetAttributeInfos() on an alarm ends
    // in a NonRecoverableException ("PropertyDoesNotExists") that closes TIA Portal, because the
    // class declares an attribute, AuditClass, that the object does not have. So everything
    // here goes through the typed .NET properties and never through GetAttributeInfos,
    // GetAttributes or SetAttribute, and AuditClass is never touched. Reading AuditClass through
    // its typed getter fails with an ordinary exception, but inside a transaction that still
    // costs the commit.
    //
    // Other findings:
    //   - EventText and EventText1..9 hold the small HTML document Unified uses for texts
    //     ("<body><p>...</p></body>"); a bare string is rejected. InfoText is plain text.
    //   - Openness accepts the name of a tag or of an alarm class that does not exist, so both
    //     are checked here.
    //   - A tag that is not set reads as "<No tag>".
    public partial class Portal
    {
        private const string NoTag = "<No tag>";

        /// <summary>Declared by the alarm classes but absent on the objects; see the note at the top.</summary>
        private static readonly string[] AlarmForbiddenProperties = { "AuditClass" };

        private static readonly string[] AlarmTagProperties = { "RaisedStateTag", "AcknowledgmentStateTag", "AcknowledgmentControlTag" };

        private static readonly string[] AlarmClassStates = { "RaisedState", "ClearedState", "AcknowledgedState", "AcknowledgedClearedState" };

        #region read

        /// <param name="type">"discrete", "analog" or empty for both.</param>
        /// <param name="nameFilter">Regular expression on the alarm name; empty returns every alarm.</param>
        public List<UnifiedAlarmInfo> GetUnifiedAlarms(string softwarePath, string type = "", string nameFilter = "")
        {
            return Operation.Run(_logger, nameof(GetUnifiedAlarms), PortalErrorCode.InvalidState,
                () =>
                {
                    var kind = (type ?? string.Empty).Trim().ToLowerInvariant();

                    if (kind != string.Empty && kind != "discrete" && kind != "analog")
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, $"type takes 'discrete', 'analog' or nothing; got '{type}'.");
                    }

                    Regex? filter = null;

                    if (!string.IsNullOrWhiteSpace(nameFilter))
                    {
                        try
                        {
                            filter = new Regex(nameFilter, RegexOptions.IgnoreCase);
                        }
                        catch (ArgumentException ex)
                        {
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                $"'{nameFilter}' is not a valid regular expression: {ex.Message}");
                        }
                    }

                    var software = RequireUnifiedSoftware(softwarePath);
                    var alarms = new List<UnifiedAlarmInfo>();

                    if (kind != "analog")
                    {
                        foreach (var alarm in software.DiscreteAlarms)
                        {
                            if (filter != null && !filter.IsMatch(alarm.Name))
                            {
                                continue;
                            }

                            var info = DescribeAlarm(alarm, "discrete");

                            info.RaisedStateTagBitNumber = alarm.RaisedStateTagBitNumber;
                            info.TriggerMode = alarm.TriggerMode.ToString();

                            alarms.Add(info);
                        }
                    }

                    if (kind != "discrete")
                    {
                        foreach (var alarm in software.AnalogAlarms)
                        {
                            if (filter != null && !filter.IsMatch(alarm.Name))
                            {
                                continue;
                            }

                            var info = DescribeAlarm(alarm, "analog");

                            info.Condition = alarm.Condition.ToString();
                            info.ConditionValue = Convert.ToString(alarm.ConditionValue, System.Globalization.CultureInfo.InvariantCulture);

                            alarms.Add(info);
                        }
                    }

                    return alarms;
                },
                ("softwarePath", softwarePath), ("type", type), ("nameFilter", nameFilter));
        }

        private static UnifiedAlarmInfo DescribeAlarm(AlarmBase alarm, string kind)
        {
            return new UnifiedAlarmInfo
            {
                Name = alarm.Name,
                Type = kind,
                Id = alarm.Id,
                AlarmClass = alarm.AlarmClass,
                RaisedStateTag = string.IsNullOrEmpty(alarm.RaisedStateTag) || alarm.RaisedStateTag == NoTag ? null : alarm.RaisedStateTag,
                Priority = alarm.Priority,
                Area = alarm.Area,
                Origin = string.IsNullOrEmpty(alarm.Origin) ? null : alarm.Origin,
                EventText = DescribeTexts(alarm.EventText),
                InfoText = DescribeTexts(alarm.InfoText),
                AcknowledgmentStateTag = ReadAlarmTag(alarm, "AcknowledgmentStateTag"),
                AcknowledgmentStateTagBitNumber = ReadAlarmBit(alarm, "AcknowledgmentStateTag", "AcknowledgmentStateTagBitNumber"),
                AcknowledgmentControlTag = ReadAlarmTag(alarm, "AcknowledgmentControlTag"),
                AcknowledgmentControlTagBitNumber = ReadAlarmBit(alarm, "AcknowledgmentControlTag", "AcknowledgmentControlTagBitNumber"),
                AlarmParameterTags = ReadAlarmParameterTags(alarm)
            };
        }

        private static string? ReadAlarmTag(AlarmBase alarm, string property)
        {
            var value = alarm.GetType().GetProperty(property)?.GetValue(alarm) as string;

            return string.IsNullOrEmpty(value) || value == NoTag ? null : value;
        }

        /// <summary>The bit number of a tag property, only when that tag is set.</summary>
        private static int? ReadAlarmBit(AlarmBase alarm, string tagProperty, string bitProperty)
        {
            return ReadAlarmTag(alarm, tagProperty) == null ? null : alarm.GetType().GetProperty(bitProperty)?.GetValue(alarm) as int?;
        }

        private static List<string>? ReadAlarmParameterTags(AlarmBase alarm)
        {
            var tags = (alarm.GetType().GetProperty("AlarmParameterTags")?.GetValue(alarm) as System.Collections.IEnumerable)?
                .Cast<object?>().Select(o => o?.ToString() ?? string.Empty).ToList();

            // Openness holds ten slots, mostly empty: only the ones in use are worth reporting.
            var used = tags?.Where(s => s.Length > 0 && s != NoTag).ToList();

            return used == null || used.Count == 0 ? null : used;
        }

        private static Dictionary<string, string>? DescribeTexts(MultilingualText text)
        {
            var result = new Dictionary<string, string>();

            foreach (var item in text.Items)
            {
                var plain = PlainUnifiedText(item.Text);

                if (plain.Length > 0 && item.Language?.Culture != null)
                {
                    result[item.Language.Culture.Name] = plain;
                }
            }

            return result.Count == 0 ? null : result;
        }

        /// <summary>The text of the "&lt;body&gt;&lt;p&gt;...&lt;/p&gt;&lt;/body&gt;" document Unified stores; paragraphs become lines.</summary>
        internal static string PlainUnifiedText(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (!text!.TrimStart().StartsWith("<body", StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }

            var lines = Regex.Replace(text, @"</p>\s*<p[^>]*>", "\n", RegexOptions.IgnoreCase);
            var plain = Regex.Replace(lines, "<[^>]+>", string.Empty);

            return System.Net.WebUtility.HtmlDecode(plain).Trim();
        }

        public List<UnifiedAlarmClassInfo> GetUnifiedAlarmClasses(string softwarePath)
        {
            return Operation.Run(_logger, nameof(GetUnifiedAlarmClasses), PortalErrorCode.InvalidState,
                () => RequireUnifiedSoftware(softwarePath).AlarmClasses
                    .Select(c => new UnifiedAlarmClassInfo
                    {
                        Name = c.Name,
                        Id = c.Id,
                        IsSystem = c.IsSystem,
                        Priority = c.Priority,
                        StateMachine = c.StateMachine.ToString(),
                        Log = string.IsNullOrEmpty(c.Log) ? null : c.Log,
                        States = new Dictionary<string, UnifiedAlarmStateLook>
                        {
                            ["RaisedState"] = DescribeLook(c.RaisedState),
                            ["ClearedState"] = DescribeLook(c.ClearedState),
                            ["AcknowledgedState"] = DescribeLook(c.AcknowledgedState),
                            ["AcknowledgedClearedState"] = DescribeLook(c.AcknowledgedClearedState)
                        }
                    })
                    .ToList(),
                ("softwarePath", softwarePath));
        }

        private static UnifiedAlarmStateLook DescribeLook(AlarmStatusVisuals look)
        {
            return new UnifiedAlarmStateLook
            {
                BackColor = HexColor(look.BackColor),
                TextColor = HexColor(look.TextColor),
                Flashing = look.Flashing
            };
        }

        private static string HexColor(System.Drawing.Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        #endregion

        #region alarms (write)

        public List<UnifiedActionResult> ManageUnifiedAlarms(string softwarePath, IList<UnifiedAlarmAction>? actions)
        {
            return RunUnifiedBatch(nameof(ManageUnifiedAlarms), softwarePath, actions,
                "{ \"action\": \"upsert\", \"alarmName\": \"Pump1_Fault\", \"properties\": { \"RaisedStateTag\": \"Pump1_Status\", \"RaisedStateTagBitNumber\": 3, \"AlarmClass\": \"Alarm\", \"EventText\": \"Pump 1 fault\" } }",
                a => a.AlarmName,
                (software, action, verb, result) =>
                {
                    var name = RequireName(action.AlarmName, "alarmName");

                    AlarmBase? alarm = (AlarmBase?)software.DiscreteAlarms.Find(name) ?? software.AnalogAlarms.Find(name);

                    switch (verb)
                    {
                        case "delete":
                            if (alarm == null)
                            {
                                throw new PortalException(PortalErrorCode.NotFound, $"Alarm '{name}' not found.");
                            }

                            if (alarm is HmiDiscreteAlarm discrete)
                            {
                                discrete.Delete();
                            }
                            else
                            {
                                ((HmiAnalogAlarm)alarm).Delete();
                            }

                            return;

                        case "create" when alarm != null:
                            throw new PortalException(PortalErrorCode.InvalidParams, $"Alarm '{name}' already exists. Use 'update' or 'upsert'.");

                        case "update" when alarm == null:
                            throw new PortalException(PortalErrorCode.NotFound,
                                $"Alarm '{name}' not found. Use 'unified_get_alarms' to list the alarms, or 'upsert' to create it.");

                        case "create":
                        case "update":
                        case "upsert":
                            break;

                        default:
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                $"Unknown action '{action.Action}'. Use 'create', 'update', 'upsert' or 'delete'.");
                    }

                    var kind = (action.Type ?? string.Empty).Trim().ToLowerInvariant();

                    if (kind != string.Empty && kind != "discrete" && kind != "analog")
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, $"type takes 'discrete' or 'analog'; got '{action.Type}'.");
                    }

                    if (alarm == null)
                    {
                        alarm = kind == "analog" ? (AlarmBase)software.AnalogAlarms.Create(name) : software.DiscreteAlarms.Create(name);

                        result.Notes.Add($"Created as {(kind == "analog" ? "an analog" : "a discrete")} alarm with id {alarm.Id}.");
                    }
                    else if (kind != string.Empty && (kind == "analog") != (alarm is HmiAnalogAlarm))
                    {
                        throw new PortalException(PortalErrorCode.NotSupported,
                            $"Alarm '{name}' is {(alarm is HmiAnalogAlarm ? "an analog" : "a discrete")} alarm. The kind of an alarm cannot be changed; delete it and create it again.");
                    }

                    WithUnifiedValidation(alarm, result.Notes,
                        () => SetAlarmProperties(software, alarm, alarm is HmiAnalogAlarm ? "An analog alarm" : "A discrete alarm", action.Properties, result));
                });
        }

        private static void SetAlarmProperties(HmiSoftware software, object alarm, string what, Dictionary<string, JsonElement>? properties, UnifiedActionResult result)
        {
            if (properties == null)
            {
                return;
            }

            var available = alarm.GetType().GetProperties()
                .Where(p => (p.CanWrite || p.PropertyType == typeof(MultilingualText)) && !AlarmForbiddenProperties.Contains(p.Name))
                .ToList();

            // A rename goes last, so that messages about the other properties name the alarm as the caller did.
            foreach (var entry in properties.OrderBy(p => p.Key.Equals("Name", StringComparison.OrdinalIgnoreCase) ? 1 : 0))
            {
                var property = FindTypedProperty(available, entry.Key, what);

                if (property.PropertyType == typeof(MultilingualText))
                {
                    // EventText and EventText1..9 are documents, InfoText is plain text.
                    SetAlarmText((MultilingualText)property.GetValue(alarm)!, entry.Value, property.Name, property.Name.StartsWith("EventText", StringComparison.Ordinal));
                }
                else if (property.Name == "AlarmParameterTags")
                {
                    SetTypedProperty(alarm, property, ReadAlarmParameterTagList(software, entry.Value));
                }
                else
                {
                    var type = property.PropertyType == typeof(object) ? null : property.PropertyType;
                    var value = ConvertHmiValue(entry.Value, type, property.Name);

                    // Openness takes any number, even -1 or 99, for the bit of a tag (checked 2026-10-06).
                    if (property.Name.EndsWith("TagBitNumber", StringComparison.Ordinal) && value != null
                        && (Convert.ToInt64(value) < 0 || Convert.ToInt64(value) > MaxAlarmBitNumber))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"{property.Name} is a bit number from 0 to {MaxAlarmBitNumber} (0-15 for a Word tag, 0-31 for a DWord); got {value}.");
                    }

                    if (AlarmTagProperties.Contains(property.Name))
                    {
                        var tagName = value as string;

                        if (string.IsNullOrEmpty(tagName))
                        {
                            value = NoTag;
                        }
                        else if (tagName != NoTag && software.Tags.Find(tagName) == null)
                        {
                            throw new PortalException(PortalErrorCode.NotFound,
                                $"{property.Name}: HMI tag '{tagName}' does not exist. Use 'unified_get_tags' to list the tags.");
                        }
                    }
                    else if (property.Name == "AlarmClass" && software.AlarmClasses.Find(value as string ?? string.Empty) == null)
                    {
                        throw new PortalException(PortalErrorCode.NotFound,
                            $"Alarm class '{value}' does not exist. Existing: {string.Join(", ", software.AlarmClasses.Select(c => c.Name))}.");
                    }

                    SetTypedProperty(alarm, property, value);
                }

                result.Applied.Add(property.Name);
            }
        }

        /// <summary>The highest bit of a 64-bit tag.</summary>
        private const int MaxAlarmBitNumber = 63;

        /// <summary>Openness holds the parameter tags of an alarm in ten slots; a shorter list fills the first ones.</summary>
        private const int AlarmParameterSlots = 10;

        private static List<string> ReadAlarmParameterTagList(HmiSoftware software, JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Array)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"AlarmParameterTags takes an array of up to {AlarmParameterSlots} HMI tag names, e.g. [\"Pressure\", \"Temperature\"]; got {value.GetRawText()}.");
            }

            var tags = value.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() ?? string.Empty : e.GetRawText()).ToList();

            if (tags.Count > AlarmParameterSlots)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"An alarm has {AlarmParameterSlots} parameter tag slots; got {tags.Count}.");
            }

            // Checked first: Openness stores a name that does not exist.
            foreach (var name in tags.Where(n => n.Length > 0 && n != NoTag))
            {
                if (software.Tags.Find(name) == null)
                {
                    throw new PortalException(PortalErrorCode.NotFound,
                        $"AlarmParameterTags: HMI tag '{name}' does not exist. Use 'unified_get_tags' to list the tags.");
                }
            }

            while (tags.Count < AlarmParameterSlots)
            {
                tags.Add(string.Empty);
            }

            return tags;
        }

        private static void SetAlarmText(MultilingualText text, JsonElement value, string propertyName, bool document)
        {
            string Format(string? plain) => document ? FormatUnifiedText(plain ?? string.Empty, "<body/>") : plain ?? string.Empty;

            if (value.ValueKind == JsonValueKind.String || value.ValueKind == JsonValueKind.Null)
            {
                foreach (var item in text.Items)
                {
                    item.Text = Format(value.GetString());
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

                item.Text = Format(entry.Value.GetString());
            }
        }

        #endregion

        #region alarm classes (write)

        public List<UnifiedActionResult> ManageUnifiedAlarmClasses(string softwarePath, IList<UnifiedAlarmClassAction>? actions)
        {
            return RunUnifiedBatch(nameof(ManageUnifiedAlarmClasses), softwarePath, actions,
                "{ \"action\": \"upsert\", \"className\": \"Process\", \"properties\": { \"Priority\": 6, \"StateMachine\": \"RaiseClear\", \"RaisedState.BackColor\": \"#FFA500\" } }",
                a => a.ClassName,
                (software, action, verb, result) =>
                {
                    var name = RequireName(action.ClassName, "className");
                    var alarmClass = software.AlarmClasses.Find(name);

                    switch (verb)
                    {
                        case "delete":
                            if (alarmClass == null)
                            {
                                throw new PortalException(PortalErrorCode.NotFound, $"Alarm class '{name}' not found.");
                            }

                            if (alarmClass.IsSystem)
                            {
                                throw new PortalException(PortalErrorCode.NotSupported, $"'{name}' is a system alarm class and cannot be deleted.");
                            }

                            var users = software.DiscreteAlarms.Count(a => a.AlarmClass == alarmClass.Name) + software.AnalogAlarms.Count(a => a.AlarmClass == alarmClass.Name);

                            alarmClass.Delete();

                            if (users > 0)
                            {
                                result.Notes.Add($"{users} alarm(s) still name this class and have to be given another one.");
                            }

                            return;

                        case "create" when alarmClass != null:
                            throw new PortalException(PortalErrorCode.InvalidParams, $"Alarm class '{name}' already exists. Use 'update' or 'upsert'.");

                        case "update" when alarmClass == null:
                            throw new PortalException(PortalErrorCode.NotFound,
                                $"Alarm class '{name}' not found. Use 'unified_get_alarm_classes' to list the classes, or 'upsert' to create it.");

                        case "create":
                        case "update":
                        case "upsert":
                            break;

                        default:
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                $"Unknown action '{action.Action}'. Use 'create', 'update', 'upsert' or 'delete'.");
                    }

                    if (alarmClass == null)
                    {
                        alarmClass = software.AlarmClasses.Create(name);
                        result.Notes.Add($"Created with id {alarmClass.Id}.");
                    }

                    if (action.Properties == null)
                    {
                        return;
                    }

                    var available = alarmClass.GetType().GetProperties().Where(p => p.CanWrite).ToList();
                    var classFindings = ReadUnifiedFindings(alarmClass);

                    foreach (var entry in action.Properties.OrderBy(p => p.Key.Equals("Name", StringComparison.OrdinalIgnoreCase) ? 1 : 0))
                    {
                        var parts = entry.Key.Split('.');

                        if (parts.Length == 2)
                        {
                            var state = AlarmClassStates.FirstOrDefault(s => s.Equals(parts[0], StringComparison.OrdinalIgnoreCase))
                                ?? throw new PortalException(PortalErrorCode.NotFound,
                                    $"An alarm class has no state '{parts[0]}'. States: {string.Join(", ", AlarmClassStates)}.");

                            var look = alarmClass.GetType().GetProperty(state)!.GetValue(alarmClass)!;
                            var lookProperty = FindTypedProperty(look.GetType().GetProperties().Where(p => p.CanWrite).ToList(), parts[1], $"The look of {state}");

                            SetTypedProperty(look, lookProperty, ConvertHmiValue(entry.Value, lookProperty.PropertyType, entry.Key));
                            result.Applied.Add($"{state}.{lookProperty.Name}");

                            continue;
                        }

                        var property = FindTypedProperty(available, entry.Key, "An alarm class");

                        // Openness answers these with an exception; the reason is said here before it comes to that.
                        if (alarmClass.IsSystem && (property.Name == "Priority" || property.Name == "Name"))
                        {
                            throw new PortalException(PortalErrorCode.NotSupported,
                                $"'{alarmClass.Name}' is a system alarm class: its {property.Name} cannot be changed. Its colors, Log and the like can.");
                        }

                        var classValue = ConvertHmiValue(entry.Value, property.PropertyType, property.Name);

                        if (property.Name == "Log" && !string.IsNullOrEmpty(classValue as string) && software.AlarmLogs.Find((string)classValue!) == null)
                        {
                            throw new PortalException(PortalErrorCode.NotFound,
                                $"Log: alarm log '{classValue}' does not exist. Existing: {string.Join(", ", software.AlarmLogs.Select(l => l.Name))}. 'unified_get_logs' lists them.");
                        }

                        SetTypedProperty(alarmClass, property, classValue);
                        result.Applied.Add(property.Name);
                    }

                    WithUnifiedValidation(alarmClass, result.Notes, () => { }, classFindings);
                });
        }

        #endregion

        #region typed access

        private static PropertyInfo FindTypedProperty(List<PropertyInfo> available, string name, string what)
        {
            return available.FirstOrDefault(p => p.Name.Equals(name, StringComparison.Ordinal))
                   ?? available.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                   ?? throw new PortalException(PortalErrorCode.NotFound,
                       $"{what} has no settable property '{name}'. Settable: {string.Join(", ", available.Select(p => p.Name).OrderBy(n => n))}.");
        }

        /// <summary>Sets a property through its .NET setter and lets the Openness exception out as it is.</summary>
        private static void SetTypedProperty(object target, PropertyInfo property, object? value)
        {
            try
            {
                property.SetValue(target, value);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }

        #endregion
    }
}
