using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;

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

        [Description("Name of the HMI tag that is archived")]
        public string? TagName { get; set; }

        [Description("Name of the logging tag on that HMI tag; empty uses the name of the HMI tag. An HMI tag can have several logging tags, one per data log")]
        public string? LoggingTagName { get; set; }

        [Description("Properties by name: DataLog (name of an existing data log; a new logging tag starts in the first data log), LoggingMode (Cyclic, OnDemand, OnChange), Cycle (needed with Cyclic; TIA Portal checks its format), CycleFactor, AggregationMode (NoAggregation, Minimum, Maximum, MinimumWithTimeStamp, MaximumWithTimeStamp, Sum, TimeAverageStepped, Average, End), AggregationDelay (\"hh:mm:ss\"), SmoothingMode (NoSmoothing, Value, CompareValues, ValueRelative, SwingingDoor), SmoothingDeltaValue, SmoothingMinTime and SmoothingMaxTime (\"hh:mm:ss\"; the minimum may not exceed the maximum), LimitScope (NoLimitsUsed, Greater, Less, GreaterOrEqual, LessOrEqual, WithinLimits, WithinOrEqualLimits, OutsideLimits, OutsideOrEqualLimits), HighLimit, LowLimit, TriggerMode (None, RisingEdge, FallingEdge, RisingAndFallingEdge), TriggerTag (an existing HMI tag), TriggerTagBitNumber, Source. 'Name' renames the logging tag")]
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
}
