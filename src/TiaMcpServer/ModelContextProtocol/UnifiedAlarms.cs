using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One change to an alarm of a WinCC Unified HMI.</summary>
    public class UnifiedAlarmAction
    {
        [Description("'create' (fails if the alarm exists), 'update' (fails if it does not), 'upsert' or 'delete'")]
        public string? Action { get; set; }

        [Description("Name of the alarm")]
        public string? AlarmName { get; set; }

        [Description("Kind of a new alarm: 'discrete' (default; raised by a bit of a tag) or 'analog' (raised by a limit of a tag). An existing alarm is found by its name")]
        public string? Type { get; set; }

        [Description("Properties to set, by name. Both kinds: RaisedStateTag (HMI tag), AlarmClass, EventText, InfoText, Priority, Area, Origin, Id, EventText1..EventText9, Name (renames). Discrete: RaisedStateTagBitNumber, TriggerMode ('OnRisingEdge'/'OnFallingEdge'), AcknowledgmentStateTag, AcknowledgmentStateTagBitNumber, AcknowledgmentControlTag, AcknowledgmentControlTagBitNumber. Analog: Condition ('UpperLimit', 'LowerLimit', 'Equal', ...), ConditionValue. A text takes a string (all languages) or {\"en-US\": \"...\"}")]
        public Dictionary<string, JsonElement>? Properties { get; set; }
    }

    /// <summary>One change to an alarm class of a WinCC Unified HMI.</summary>
    public class UnifiedAlarmClassAction
    {
        [Description("'create' (fails if the class exists), 'update' (fails if it does not), 'upsert' or 'delete'. A system class cannot be deleted")]
        public string? Action { get; set; }

        [Description("Name of the alarm class")]
        public string? ClassName { get; set; }

        [Description("Properties to set, by name: Priority (0-255), StateMachine (e.g. 'RaiseClear', 'RaiseClearRequiresAcknowledgement'), Log, Name (renames). The look of a state is set as '<State>.<Property>' with State one of RaisedState, ClearedState, AcknowledgedState, AcknowledgedClearedState and Property one of BackColor, TextColor (\"#RRGGBB\") or Flashing, e.g. {\"RaisedState.BackColor\": \"#FF0000\"}")]
        public Dictionary<string, JsonElement>? Properties { get; set; }
    }

    public class ResponseUnifiedAlarms : ResponseMessage
    {
        public List<UnifiedAlarmInfo>? Items { get; set; }
    }

    public class UnifiedAlarmInfo
    {
        public string? Name { get; set; }

        /// <summary>"discrete" or "analog".</summary>
        public string? Type { get; set; }

        public uint Id { get; set; }

        public string? AlarmClass { get; set; }

        /// <summary>HMI tag that raises the alarm; null when none is set.</summary>
        public string? RaisedStateTag { get; set; }

        /// <summary>Discrete alarm: bit of the tag.</summary>
        public uint? RaisedStateTagBitNumber { get; set; }

        /// <summary>Discrete alarm: "OnRisingEdge" or "OnFallingEdge".</summary>
        public string? TriggerMode { get; set; }

        /// <summary>Analog alarm: the comparison, e.g. "UpperLimit".</summary>
        public string? Condition { get; set; }

        /// <summary>Analog alarm: the limit.</summary>
        public string? ConditionValue { get; set; }

        public byte Priority { get; set; }

        public string? Area { get; set; }

        public string? Origin { get; set; }

        /// <summary>Alarm text by language, as plain text; languages without a text are left out.</summary>
        public Dictionary<string, string>? EventText { get; set; }

        /// <summary>Info text by language; languages without a text are left out.</summary>
        public Dictionary<string, string>? InfoText { get; set; }
    }

    public class ResponseUnifiedAlarmClasses : ResponseMessage
    {
        public List<UnifiedAlarmClassInfo>? Items { get; set; }
    }

    public class UnifiedAlarmClassInfo
    {
        public string? Name { get; set; }

        public uint Id { get; set; }

        /// <summary>True for the classes TIA Portal provides; they cannot be deleted.</summary>
        public bool IsSystem { get; set; }

        public byte Priority { get; set; }

        public string? StateMachine { get; set; }

        public string? Log { get; set; }

        /// <summary>The look of each alarm state: state name to BackColor, TextColor and Flashing.</summary>
        public Dictionary<string, UnifiedAlarmStateLook>? States { get; set; }
    }

    public class UnifiedAlarmStateLook
    {
        public string? BackColor { get; set; }

        public string? TextColor { get; set; }

        public bool Flashing { get; set; }
    }
}
