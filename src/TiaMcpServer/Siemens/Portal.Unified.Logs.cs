using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.HmiUnified.HmiLogging;
using Siemens.Engineering.HmiUnified.HmiLogging.HmiLoggingCommon;
using Siemens.Engineering.HmiUnified.HmiTags;
using Siemens.Engineering.HmiUnified.LoggingTags;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Siemens
{
    // WinCC Unified: data logs, alarm logs and the logging tags that archive HMI tags into them.
    //
    // Callers: unified_get_logs, unified_manage_logs, unified_get_logging_tags and
    // unified_manage_logging_tags in McpServer.Unified.cs. Reads and writes no data files.
    // Everything goes through the typed .NET properties, never GetAttributeInfos/SetAttribute.
    //
    // What Openness does here, as found on TIA Portal V21 (2026-10-06, panel and PC station):
    //   - HmiSoftware.DataLogs / AlarmLogs / AuditTrails; the audit trails cannot be created or
    //     deleted. Create(name) needs a name that is unique across data logs AND alarm logs
    //     ("The values must be unique"). Settings, Segment and Backup are objects of their own.
    //   - A durations (LogTimePeriod, SegmentTimePeriod) is written as a whole with
    //     SetLogDuration / SetSegmentDuration (days, hours, minutes, seconds, ticks).
    //   - StorageDevice: a new log on a panel starts on SDX51, which the panel then refuses for the
    //     log ("must be on the same medium as the main database") - only the medium of the existing
    //     logs (USBX61 on the test panel) is accepted; a PC station accepts Default and Local. The
    //     other values fail with a message that carries an internal "ResourceID" text.
    //     StorageFolder with a plain path is refused ("'\' is not supported for this device").
    //   - Renaming a log renames the DataLog of the logging tags that use it. Deleting a log does
    //     NOT touch them: they keep pointing at the deleted name.
    //   - HmiTag.LoggingTags: Create(name) puts the new logging tag into the first data log; the
    //     name is unique within the tag only. DataLog, TriggerTag and Source are checked by
    //     Openness against what exists (an exception, which would cost the batch its commit, so
    //     DataLog and TriggerTag are checked here first). SmoothingMinTime may not exceed
    //     SmoothingMaxTime, so the maximum is set first, and is refused with no smoothing mode.
    //     Cycle is refused for every text tried ("1 s", "1s", "00:00:01", "Cycle_1s", ...): it
    //     names something the project defines, and no hand-made cyclic logging tag exists in the
    //     test project to read the form from. All 176 logging tags of the PC station are OnChange.
    //   - A trend takes an archived tag as the source "<HMI tag>:<logging tag>"; that is how the
    //     trends of the PC station read.
    public partial class Portal
    {
        /// <summary>The dotted settings of a log: setting name -> the part of the log it belongs to.</summary>
        private static readonly Dictionary<string, string> LogSettingGroups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LogMaxSize"] = "Settings",
            ["LogTimePeriod"] = "Settings",
            ["StorageDevice"] = "Settings",
            ["StorageFolder"] = "Settings",
            ["SegmentMaxSize"] = "Segment",
            ["SegmentStartTime"] = "Segment",
            ["SegmentTimePeriod"] = "Segment",
            ["BackupMode"] = "Backup",
            ["PrimaryPath"] = "Backup"
        };

        /// <summary>Order a logging tag's properties are set in: what later ones depend on comes first.</summary>
        private static readonly string[] LoggingTagPropertyOrder =
        {
            "DataLog", "LoggingMode", "Cycle", "CycleFactor", "AggregationMode", "AggregationDelay", "SmoothingMode",
            "SmoothingDeltaValue", "SmoothingMaxTime", "SmoothingMinTime", "LimitScope", "HighLimit", "LowLimit",
            "TriggerMode", "TriggerTag", "TriggerTagBitNumber", "Source"
        };

        #region logs

        /// <param name="type">'data', 'alarm' or 'audit'; empty returns all kinds.</param>
        public List<UnifiedLogInfo> GetUnifiedLogs(string softwarePath, string type = "", string logName = "")
        {
            return Operation.Run(_logger, nameof(GetUnifiedLogs), PortalErrorCode.InvalidState,
                () =>
                {
                    var software = RequireUnifiedSoftware(softwarePath);
                    var wanted = type?.Trim().ToLowerInvariant() ?? string.Empty;

                    if (wanted.Length > 0 && wanted != "data" && wanted != "alarm" && wanted != "audit")
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, $"Unknown log type '{type}'. Use 'data', 'alarm' or 'audit'.");
                    }

                    var logs = new List<UnifiedLogInfo>();
                    Dictionary<string, int>? users = null;

                    foreach (var (kind, log) in EnumerateLogs(software))
                    {
                        if ((wanted.Length > 0 && kind != wanted)
                            || (!string.IsNullOrWhiteSpace(logName) && !string.Equals(log.Name, logName.Trim(), StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        var info = DescribeLog(kind, log);

                        if (kind == "data")
                        {
                            users ??= CountLoggingTagsPerLog(software);
                            info.LoggingTagCount = users.TryGetValue(log.Name, out var count) ? count : 0;
                        }

                        logs.Add(info);
                    }

                    if (!string.IsNullOrWhiteSpace(logName) && logs.Count == 0)
                    {
                        throw new PortalException(PortalErrorCode.NotFound,
                            $"Log '{logName}' not found. Existing: {DescribeLogNames(software)}.");
                    }

                    return logs;
                },
                ("softwarePath", softwarePath), ("type", type ?? string.Empty), ("logName", logName ?? string.Empty));
        }

        private static IEnumerable<(string Kind, LoggingBase Log)> EnumerateLogs(HmiSoftware software)
        {
            foreach (var log in software.DataLogs)
            {
                yield return ("data", log);
            }

            foreach (var log in software.AlarmLogs)
            {
                yield return ("alarm", log);
            }

            foreach (var log in software.AuditTrails)
            {
                yield return ("audit", log);
            }
        }

        private static string DescribeLogNames(HmiSoftware software)
        {
            var names = EnumerateLogs(software).Select(l => $"{l.Log.Name} ({l.Kind})").ToList();

            return names.Count == 0 ? "none" : string.Join(", ", names);
        }

        private static Dictionary<string, int> CountLoggingTagsPerLog(HmiSoftware software)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var tag in software.Tags)
            {
                foreach (var loggingTag in tag.LoggingTags)
                {
                    var log = loggingTag.DataLog ?? string.Empty;

                    counts[log] = counts.TryGetValue(log, out var n) ? n + 1 : 1;
                }
            }

            return counts;
        }

        private static UnifiedLogInfo DescribeLog(string kind, LoggingBase log)
        {
            return new UnifiedLogInfo
            {
                Type = kind,
                Name = log.Name,
                LogMaxSize = log.Settings.LogMaxSize,
                LogTimePeriod = new UnifiedLogDuration
                {
                    Days = log.Settings.LogTimePeriod.Days,
                    Hours = log.Settings.LogTimePeriod.Hours,
                    Minutes = log.Settings.LogTimePeriod.Minutes,
                    Seconds = log.Settings.LogTimePeriod.Seconds,
                    Ticks = log.Settings.LogTimePeriod.Ticks
                },
                StorageDevice = log.Settings.StorageDevice.ToString(),
                StorageFolder = log.Settings.StorageFolder,
                SegmentMaxSize = log.Segment.SegmentMaxSize,
                SegmentStartTime = log.Segment.SegmentStartTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                SegmentTimePeriod = new UnifiedLogDuration
                {
                    Days = log.Segment.SegmentTimePeriod.Days,
                    Hours = log.Segment.SegmentTimePeriod.Hours,
                    Minutes = log.Segment.SegmentTimePeriod.Minutes,
                    Seconds = log.Segment.SegmentTimePeriod.Seconds,
                    Ticks = log.Segment.SegmentTimePeriod.Ticks
                },
                BackupMode = log.Backup.BackupMode.ToString(),
                PrimaryPath = log.Backup.PrimaryPath
            };
        }

        public List<UnifiedActionResult> ManageUnifiedLogs(string softwarePath, IList<UnifiedLogAction>? actions)
        {
            return RunUnifiedBatch(nameof(ManageUnifiedLogs), softwarePath, actions,
                "{ \"action\": \"upsert\", \"type\": \"data\", \"logName\": \"Process\", \"properties\": { \"Settings.LogMaxSize\": 5000, \"Settings.LogTimePeriod\": { \"days\": 14 } } }",
                a => a.LogName,
                (software, action, verb, result) =>
                {
                    var name = RequireName(action.LogName, "logName");
                    var existing = EnumerateLogs(software).FirstOrDefault(l => string.Equals(l.Log.Name, name, StringComparison.OrdinalIgnoreCase));
                    var wanted = action.Type?.Trim().ToLowerInvariant() ?? string.Empty;

                    if (wanted.Length > 0 && wanted != "data" && wanted != "alarm")
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, $"Unknown log type '{action.Type}'. Use 'data' or 'alarm'.");
                    }

                    if (existing.Log != null && wanted.Length > 0 && wanted != existing.Kind)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, $"'{name}' is {(existing.Kind == "alarm" ? "an" : "a")} {existing.Kind} log, not {(wanted == "alarm" ? "an" : "a")} {wanted} log.");
                    }

                    if (existing.Log != null && existing.Kind == "audit")
                    {
                        throw new PortalException(PortalErrorCode.NotSupported, $"'{name}' is an audit trail. Openness offers no way to change audit trails; they can only be read.");
                    }

                    switch (verb)
                    {
                        case "delete":
                            if (existing.Log == null)
                            {
                                throw new PortalException(PortalErrorCode.NotFound, $"Log '{name}' not found. Existing: {DescribeLogNames(software)}.");
                            }

                            var users = existing.Kind == "data" ? CountLoggingTagsPerLog(software).TryGetValue(existing.Log.Name, out var n) ? n : 0 : 0;

                            if (existing.Kind == "data")
                            {
                                ((HmiDataLog)existing.Log).Delete();
                            }
                            else
                            {
                                ((HmiAlarmLog)existing.Log).Delete();
                            }

                            if (users > 0)
                            {
                                result.Notes.Add($"{users} logging tag(s) still name this log; Openness leaves them pointing at it. Give them another one with 'unified_manage_logging_tags'.");
                            }

                            return;

                        case "create" when existing.Log != null:
                            throw new PortalException(PortalErrorCode.InvalidParams, $"Log '{name}' already exists. Use 'update' or 'upsert'.");

                        case "update" when existing.Log == null:
                            throw new PortalException(PortalErrorCode.NotFound,
                                $"Log '{name}' not found. Existing: {DescribeLogNames(software)}. Use 'upsert' to create it.");

                        case "create":
                        case "update":
                        case "upsert":
                            break;

                        default:
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                $"Unknown action '{action.Action}'. Use 'create', 'update', 'upsert' or 'delete'.");
                    }

                    LoggingBase log;

                    if (existing.Log != null)
                    {
                        log = existing.Log;
                    }
                    else
                    {
                        if (wanted.Length == 0)
                        {
                            throw new PortalException(PortalErrorCode.InvalidParams, "type is required to create a log: 'data' or 'alarm'.");
                        }

                        var others = EnumerateLogs(software).Where(l => l.Kind == wanted).Select(l => l.Log.Settings.StorageDevice.ToString()).Distinct().ToList();

                        log = wanted == "data" ? (LoggingBase)software.DataLogs.Create(name) : software.AlarmLogs.Create(name);
                        var started = log.Settings.StorageDevice.ToString();

                        result.Notes.Add(others.Count > 0 && !others.Contains(started)
                            ? $"Created. The other {wanted} logs of this HMI use the storage device {string.Join(", ", others)} but a new log starts on {started}, which an HMI may refuse: set Settings.StorageDevice to match."
                            : "Created.");
                    }

                    SetLogProperties(log, action.Properties, result);
                });
        }

        private static void SetLogProperties(LoggingBase log, Dictionary<string, JsonElement>? properties, UnifiedActionResult result)
        {
            if (properties == null)
            {
                return;
            }

            // A rename goes last, so that messages about the other settings name the log as the caller did.
            foreach (var entry in properties.OrderBy(p => p.Key.Equals("Name", StringComparison.OrdinalIgnoreCase) ? 1 : 0))
            {
                if (entry.Key.Equals("Name", StringComparison.OrdinalIgnoreCase))
                {
                    var newName = entry.Value.ValueKind == JsonValueKind.String ? entry.Value.GetString() : null;

                    if (string.IsNullOrWhiteSpace(newName))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, "Name takes the new name of the log.");
                    }

                    log.Name = newName!;
                    result.Applied.Add($"Name = {newName}");

                    continue;
                }

                var parts = entry.Key.Split('.');
                var setting = parts[parts.Length - 1];

                if (parts.Length > 2 || !LogSettingGroups.TryGetValue(setting, out var group)
                    || (parts.Length == 2 && !parts[0].Equals(group, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new PortalException(PortalErrorCode.NotFound,
                        $"A log has no setting '{entry.Key}'. Settings: {string.Join(", ", LogSettingGroups.Select(g => $"{g.Value}.{g.Key}"))}, Name.");
                }

                setting = LogSettingGroups.Keys.First(k => k.Equals(setting, StringComparison.OrdinalIgnoreCase));

                object part = group == "Settings" ? log.Settings : group == "Segment" ? log.Segment : (object)log.Backup;

                if (setting == "LogTimePeriod" || setting == "SegmentTimePeriod")
                {
                    var duration = UnifiedLogDuration.Parse(entry.Value, entry.Key);

                    if (setting == "LogTimePeriod")
                    {
                        log.Settings.LogTimePeriod.SetLogDuration(duration.Days, duration.Hours, duration.Minutes, duration.Seconds, duration.Ticks);
                    }
                    else
                    {
                        log.Segment.SegmentTimePeriod.SetSegmentDuration(duration.Days, duration.Hours, duration.Minutes, duration.Seconds, duration.Ticks);
                    }

                    result.Applied.Add($"{group}.{setting}");

                    continue;
                }

                var property = part.GetType().GetProperty(setting)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"{group} has no property '{setting}'.");
                var value = ConvertHmiValue(entry.Value, property.PropertyType, entry.Key);

                if (setting == "StorageDevice")
                {
                    try
                    {
                        SetTypedProperty(part, property, value);
                    }
                    catch (Exception ex) when (ex is not PortalException)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"StorageDevice '{value}' is not accepted for this HMI ({LastLine(ex.Message)}). The storage device of the logs it already has is the safe choice: read it with 'unified_get_logs'. Values: {string.Join(", ", Enum.GetNames(typeof(DeviceNode)))}.");
                    }
                }
                else
                {
                    SetTypedProperty(part, property, value);
                }

                result.Applied.Add($"{group}.{setting}");
            }
        }

        private static string LastLine(string message)
        {
            var text = System.Text.RegularExpressions.Regex.Replace(message ?? string.Empty, @"ERROR: ResourceID '\d+' not found in resource of type [\w.]+", "unknown medium");

            return text.Trim().TrimStart('-', ' ');
        }

        #endregion

        #region logging tags

        /// <param name="tagName">Regular expression on the HMI tag name; empty for all tags.</param>
        /// <param name="logName">Only the logging tags that archive into this data log.</param>
        public List<UnifiedLoggingTagInfo> GetUnifiedLoggingTags(string softwarePath, string tagName, string logName, int limit, out bool truncated)
        {
            var wasTruncated = false;

            var result = Operation.Run(_logger, nameof(GetUnifiedLoggingTags), PortalErrorCode.InvalidState,
                () =>
                {
                    var software = RequireUnifiedSoftware(softwarePath);
                    System.Text.RegularExpressions.Regex? filter = null;

                    if (!string.IsNullOrWhiteSpace(tagName))
                    {
                        try
                        {
                            filter = new System.Text.RegularExpressions.Regex(tagName, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        }
                        catch (ArgumentException ex)
                        {
                            throw new PortalException(PortalErrorCode.InvalidParams, $"'{tagName}' is not a valid regular expression: {ex.Message}");
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(logName) && software.DataLogs.Find(logName.Trim()) == null)
                    {
                        throw new PortalException(PortalErrorCode.NotFound,
                            $"Data log '{logName}' not found. Existing: {string.Join(", ", software.DataLogs.Select(l => l.Name))}.");
                    }

                    var items = new List<UnifiedLoggingTagInfo>();

                    foreach (var tag in software.Tags)
                    {
                        if (filter != null && !filter.IsMatch(tag.Name))
                        {
                            continue;
                        }

                        foreach (var loggingTag in tag.LoggingTags)
                        {
                            if (!string.IsNullOrWhiteSpace(logName) && !string.Equals(loggingTag.DataLog, logName.Trim(), StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (items.Count >= limit)
                            {
                                wasTruncated = true;

                                return items;
                            }

                            items.Add(DescribeLoggingTag(tag.Name, loggingTag));
                        }
                    }

                    return items;
                },
                ("softwarePath", softwarePath), ("tagName", tagName ?? string.Empty), ("logName", logName ?? string.Empty));

            truncated = wasTruncated;

            return result;
        }

        private static UnifiedLoggingTagInfo DescribeLoggingTag(string tagName, HmiLoggingTag t)
        {
            return new UnifiedLoggingTagInfo
            {
                TagName = tagName,
                Name = t.Name,
                DataLog = t.DataLog,
                LoggingMode = t.LoggingMode.ToString(),
                Cycle = t.Cycle,
                CycleFactor = t.CycleFactor,
                AggregationMode = t.AggregationMode.ToString(),
                AggregationDelay = t.AggregationDelay.ToString(),
                SmoothingMode = t.SmoothingMode.ToString(),
                SmoothingDeltaValue = t.SmoothingDeltaValue,
                SmoothingMinTime = t.SmoothingMinTime.ToString(),
                SmoothingMaxTime = t.SmoothingMaxTime.ToString(),
                LimitScope = t.LimitScope.ToString(),
                HighLimit = t.HighLimit?.ToString(),
                LowLimit = t.LowLimit?.ToString(),
                TriggerMode = t.TriggerMode.ToString(),
                TriggerTag = t.TriggerTag,
                TriggerTagBitNumber = t.TriggerTagBitNumber,
                Source = t.Source
            };
        }

        public List<UnifiedActionResult> ManageUnifiedLoggingTags(string softwarePath, IList<UnifiedLoggingTagAction>? actions)
        {
            return RunUnifiedBatch(nameof(ManageUnifiedLoggingTags), softwarePath, actions,
                "{ \"action\": \"upsert\", \"tagName\": \"Temperature\", \"properties\": { \"DataLog\": \"Process\", \"LoggingMode\": \"OnChange\" } }",
                a => a.TagName,
                (software, action, verb, result) =>
                {
                    var tagName = RequireName(action.TagName, "tagName");
                    var tag = software.Tags.Find(tagName)
                        ?? throw new PortalException(PortalErrorCode.NotFound, $"HMI tag '{tagName}' not found. Use 'unified_get_tags' to list the tags.");

                    var name = string.IsNullOrWhiteSpace(action.LoggingTagName) ? tag.Name : action.LoggingTagName!.Trim();
                    var loggingTag = tag.LoggingTags.Find(name);

                    switch (verb)
                    {
                        case "delete":
                            if (loggingTag == null)
                            {
                                throw new PortalException(PortalErrorCode.NotFound,
                                    $"HMI tag '{tagName}' has no logging tag '{name}'. It has: {DescribeLoggingTagNames(tag)}.");
                            }

                            loggingTag.Delete();

                            return;

                        case "create" when loggingTag != null:
                            throw new PortalException(PortalErrorCode.InvalidParams, $"HMI tag '{tagName}' already has a logging tag '{name}'. Use 'update' or 'upsert'.");

                        case "update" when loggingTag == null:
                            throw new PortalException(PortalErrorCode.NotFound,
                                $"HMI tag '{tagName}' has no logging tag '{name}'. It has: {DescribeLoggingTagNames(tag)}. Use 'upsert' to create it.");

                        case "create":
                        case "update":
                        case "upsert":
                            break;

                        default:
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                $"Unknown action '{action.Action}'. Use 'create', 'update', 'upsert' or 'delete'.");
                    }

                    if (loggingTag == null)
                    {
                        if (software.DataLogs.Count == 0)
                        {
                            throw new PortalException(PortalErrorCode.InvalidState,
                                "There is no data log to archive into. Create one with 'unified_manage_logs' first.");
                        }

                        loggingTag = tag.LoggingTags.Create(name);

                        // Openness is meant to put it into the first data log; it was seen to leave the name empty.
                        if (string.IsNullOrEmpty(loggingTag.DataLog))
                        {
                            loggingTag.DataLog = software.DataLogs.First().Name;
                        }

                        result.Notes.Add($"Created in data log '{loggingTag.DataLog}' (the first one) unless DataLog says otherwise.");
                    }

                    SetLoggingTagProperties(software, loggingTag, action.Properties, result);
                });
        }

        private static string DescribeLoggingTagNames(HmiTag tag)
        {
            var names = tag.LoggingTags.Select(l => l.Name).ToList();

            return names.Count == 0 ? "none" : string.Join(", ", names);
        }

        private static void SetLoggingTagProperties(HmiSoftware software, HmiLoggingTag loggingTag, Dictionary<string, JsonElement>? properties, UnifiedActionResult result)
        {
            if (properties == null)
            {
                return;
            }

            var available = typeof(HmiLoggingTag).GetProperties().Where(p => p.CanWrite).ToList();

            // The order matters (see the header); a rename goes last.
            int Rank(string key)
            {
                if (key.Equals("Name", StringComparison.OrdinalIgnoreCase))
                {
                    return int.MaxValue;
                }

                var index = Array.FindIndex(LoggingTagPropertyOrder, n => n.Equals(key, StringComparison.OrdinalIgnoreCase));

                return index < 0 ? LoggingTagPropertyOrder.Length : index;
            }

            foreach (var entry in properties.OrderBy(p => Rank(p.Key)))
            {
                var property = FindTypedProperty(available, entry.Key, "A logging tag");
                var type = property.PropertyType == typeof(object) ? null : property.PropertyType;
                var value = ConvertHmiValue(entry.Value, type, property.Name);

                // Checked up front: Openness throws for these, and a thrown exception costs the batch its commit.
                if (property.Name == "DataLog" && software.DataLogs.Find(value as string ?? string.Empty) == null)
                {
                    throw new PortalException(PortalErrorCode.NotFound,
                        $"DataLog: data log '{value}' does not exist. Existing: {string.Join(", ", software.DataLogs.Select(l => l.Name))}.");
                }

                if (property.Name == "TriggerTag" && !string.IsNullOrEmpty(value as string) && software.Tags.Find((string)value!) == null)
                {
                    throw new PortalException(PortalErrorCode.NotFound,
                        $"TriggerTag: HMI tag '{value}' does not exist. Use 'unified_get_tags' to list the tags.");
                }

                if (property.Name == "SmoothingMinTime" && value is TimeSpan min)
                {
                    var max = properties.Any(p => p.Key.Equals("SmoothingMaxTime", StringComparison.OrdinalIgnoreCase))
                        ? (TimeSpan)ConvertHmiValue(properties.First(p => p.Key.Equals("SmoothingMaxTime", StringComparison.OrdinalIgnoreCase)).Value, typeof(TimeSpan), "SmoothingMaxTime")!
                        : loggingTag.SmoothingMaxTime;

                    if (min > max)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"SmoothingMinTime ({min}) may not exceed SmoothingMaxTime ({max}); set SmoothingMaxTime as well.");
                    }
                }

                if (property.Name == "Cycle")
                {
                    try
                    {
                        SetTypedProperty(loggingTag, property, value);
                    }
                    catch (Exception ex) when (ex is not PortalException)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Cycle '{value}' is not accepted by TIA Portal ({ex.Message.Trim()}). The form it expects is not known to this server: no logging tag with a cycle exists in the project to read it from. Set LoggingMode to OnChange or OnDemand, or give a Cycle copied from a logging tag made in TIA Portal.");
                    }
                }
                else
                {
                    SetTypedProperty(loggingTag, property, value);
                }

                result.Applied.Add(property.Name);
            }
        }

        #endregion
    }
}
