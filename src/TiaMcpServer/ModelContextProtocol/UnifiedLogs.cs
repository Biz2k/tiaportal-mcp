using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One change to a data log or alarm log of a WinCC Unified HMI.</summary>
    public class UnifiedLogAction
    {
        [Description("'create' (fails if a log of that name exists), 'update' (fails if it does not), 'upsert' or 'delete'")]
        public string? Action { get; set; }

        [Description("'data' (data log) or 'alarm' (alarm log). Needed for 'create' and 'upsert' of a log that does not exist yet; log names are unique across both kinds, so for an existing log it may be left out")]
        public string? Type { get; set; }

        [Description("Name of the log. Names are unique across data logs and alarm logs")]
        public string? LogName { get; set; }

        [Description("Settings by name: Settings.LogMaxSize, Settings.LogTimePeriod, Settings.StorageDevice, Settings.StorageFolder, Segment.SegmentMaxSize, Segment.SegmentStartTime, Segment.SegmentTimePeriod, Backup.BackupMode, Backup.PrimaryPath; the part before the dot may be left out. 'Name' renames the log; the logging tags that use it follow. A duration (LogTimePeriod, SegmentTimePeriod) is an object {\"days\": 7, \"hours\": 0, \"minutes\": 0, \"seconds\": 0}; parts that are left out become 0. SegmentStartTime is an ISO date and time, e.g. \"2026-01-02T03:04:05\". StorageDevice is one of Default, Local, SDX51, USBX61, USBX62; which of them an HMI accepts depends on the device, and the error says when it is not this one. BackupMode is NoBackup or PrimaryPath")]
        public Dictionary<string, JsonElement>? Properties { get; set; }
    }

    /// <summary>One change to a logging tag (the archiving of an HMI tag into a data log).</summary>
    public class UnifiedLoggingTagAction
    {
        [Description("'create' (fails if the logging tag exists), 'update' (fails if it does not), 'upsert' or 'delete'")]
        public string? Action { get; set; }

        [Description("Path of the process tag that is archived: an HMI tag, or for a structured tag the member that is archived, e.g. \"AI_DB_CP10-U1.field_input_EUF\"")]
        public string? TagName { get; set; }

        [Description("Name of the logging tag; empty uses the name of the HMI tag (the first part of tagName), as TIA Portal does. A tag can have several logging tags, one per data log")]
        public string? LoggingTagName { get; set; }

        [Description("Properties by name: DataLog (name of an existing data log; a new logging tag starts in the first data log), LoggingMode (Cyclic, OnDemand, OnChange), Cycle (needed with Cyclic: T500ms, T1s, T2s, T5s or T10s; a cyclic logging tag may not be faster than 500 ms; T100ms and T250ms are for other modes), CycleFactor, AggregationMode (NoAggregation, Minimum, Maximum, MinimumWithTimeStamp, MaximumWithTimeStamp, Sum, TimeAverageStepped, Average, End), AggregationDelay (\"hh:mm:ss\"), SmoothingMode (NoSmoothing, Value, CompareValues, ValueRelative, SwingingDoor), SmoothingDeltaValue, SmoothingMinTime and SmoothingMaxTime (\"hh:mm:ss\"; the minimum may not exceed the maximum), LimitScope (NoLimitsUsed, Greater, Less, GreaterOrEqual, LessOrEqual, WithinLimits, WithinOrEqualLimits, OutsideLimits, OutsideOrEqualLimits), HighLimit, LowLimit, TriggerMode (None, RisingEdge, FallingEdge, RisingAndFallingEdge), TriggerTag (an existing tag as TIA Portal writes it: xReset, Tag.Member, or \"Tag-Name\".Member for special characters; used with TriggerMode and the mode OnDemand), TriggerTagBitNumber, Source. 'Name' renames the logging tag")]
        public Dictionary<string, JsonElement>? Properties { get; set; }
    }

    public class ResponseUnifiedLogs : ResponseMessage
    {
        public List<UnifiedLogInfo>? Items { get; set; }
    }

    public class UnifiedLogInfo
    {
        /// <summary>"data", "alarm" or "audit" (audit trails can only be read).</summary>
        public string? Type { get; set; }

        public string? Name { get; set; }

        public uint LogMaxSize { get; set; }

        public UnifiedLogDuration? LogTimePeriod { get; set; }

        public string? StorageDevice { get; set; }

        public string? StorageFolder { get; set; }

        public uint SegmentMaxSize { get; set; }

        /// <summary>ISO date and time.</summary>
        public string? SegmentStartTime { get; set; }

        public UnifiedLogDuration? SegmentTimePeriod { get; set; }

        public string? BackupMode { get; set; }

        public string? PrimaryPath { get; set; }

        /// <summary>Logging tags that archive into this log (data logs only).</summary>
        public int LoggingTagCount { get; set; }
    }

    public class UnifiedLogDuration
    {
        public uint Days { get; set; }

        public uint Hours { get; set; }

        public uint Minutes { get; set; }

        public uint Seconds { get; set; }

        public uint Ticks { get; set; }

        public static UnifiedLogDuration Parse(JsonElement value, string name)
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                throw new TiaMcpServer.Siemens.PortalException(TiaMcpServer.Siemens.PortalErrorCode.InvalidParams,
                    $"{name} takes a duration as an object, e.g. {{\"days\": 7, \"hours\": 12}}; got {value.GetRawText()}.");
            }

            var duration = new UnifiedLogDuration();

            foreach (var part in value.EnumerateObject())
            {
                if (part.Value.ValueKind != JsonValueKind.Number || !part.Value.TryGetUInt32(out var number))
                {
                    throw new TiaMcpServer.Siemens.PortalException(TiaMcpServer.Siemens.PortalErrorCode.InvalidParams, $"{name}: '{part.Name}' takes a whole number of at least 0; got {part.Value.GetRawText()}.");
                }

                switch (part.Name.ToLowerInvariant())
                {
                    case "days": duration.Days = number; break;
                    case "hours": duration.Hours = number; break;
                    case "minutes": duration.Minutes = number; break;
                    case "seconds": duration.Seconds = number; break;
                    case "ticks": duration.Ticks = number; break;
                    default:
                        throw new TiaMcpServer.Siemens.PortalException(TiaMcpServer.Siemens.PortalErrorCode.InvalidParams, $"{name} has no part '{part.Name}'. Parts: days, hours, minutes, seconds, ticks.");
                }
            }

            return duration;
        }
    }

    public class ResponseUnifiedLoggingTags : ResponseMessage
    {
        public List<UnifiedLoggingTagInfo>? Items { get; set; }

        /// <summary>True when there were more logging tags than the limit; narrow the filters.</summary>
        public bool Truncated { get; set; }
    }

    public class UnifiedLoggingTagInfo
    {
        public string? TagName { get; set; }

        public string? Name { get; set; }

        public string? DataLog { get; set; }

        public string? LoggingMode { get; set; }

        public string? Cycle { get; set; }

        public uint CycleFactor { get; set; }

        public string? AggregationMode { get; set; }

        public string? AggregationDelay { get; set; }

        public string? SmoothingMode { get; set; }

        public double SmoothingDeltaValue { get; set; }

        public string? SmoothingMinTime { get; set; }

        public string? SmoothingMaxTime { get; set; }

        public string? LimitScope { get; set; }

        public string? HighLimit { get; set; }

        public string? LowLimit { get; set; }

        public string? TriggerMode { get; set; }

        public string? TriggerTag { get; set; }

        public uint TriggerTagBitNumber { get; set; }

        public string? Source { get; set; }
    }

    /// <summary>
    /// The path of an HMI tag as TIA Portal writes it: segments separated by dots, a segment with
    /// special characters in double quotes, e.g. <c>"HMI_Analog_Valves_VM-16".CMD_Mode</c>. A member
    /// of a structured tag is the next segment.
    /// </summary>
    public static class UnifiedTagPath
    {
        public static List<string> Split(string? path)
        {
            var segments = new List<string>();
            var current = new System.Text.StringBuilder();
            var quoted = false;

            foreach (var c in path ?? string.Empty)
            {
                if (c == '"')
                {
                    quoted = !quoted;
                }
                else if (c == '.' && !quoted)
                {
                    segments.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            segments.Add(current.ToString().Trim());

            return segments;
        }
    }

    /// <summary>The cycle of a logging tag, named like "T500ms" or "T5s" after the cycles of the project.</summary>
    public static class UnifiedLogCycle
    {
        /// <summary>The cycles TIA Portal V21 was seen to accept (2026-10-06).</summary>
        public const string KnownCycles = "T100ms, T250ms, T500ms, T1s, T2s, T5s, T10s";

        /// <summary>The shortest cycle a cyclic logging tag may have; shorter ones fail TIA Portal's consistency check.</summary>
        public const int MinimumCyclicMilliseconds = 500;

        private static readonly Regex Pattern = new Regex(@"^T(\d+)(ms|s)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>The length of a cycle such as "T500ms", or null when it does not have that form.</summary>
        public static int? Milliseconds(string? cycle)
        {
            var match = Pattern.Match((cycle ?? string.Empty).Trim());

            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var number))
            {
                return null;
            }

            return match.Groups[2].Value.Equals("s", StringComparison.OrdinalIgnoreCase) ? number * 1000 : number;
        }
    }
}
