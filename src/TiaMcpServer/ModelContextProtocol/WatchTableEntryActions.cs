using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One change to the rows of a PLC watch table. The checks and the edit are in PlcWatchTableEdit.</summary>
    public class WatchTableEntryAction
    {
        public const string Fields = "action, index, name, address, displayFormat, monitorTrigger, modifyTrigger, modifyValue, comment";

        /// <summary>The values of PlcWatchAndForceTableDisplayFormat that a row can be given (not 'Undef').</summary>
        public static readonly string[] DisplayFormats =
        {
            "Any_pointer", "BCD", "Bin", "Bool", "Character", "Character_sequence", "Date", "DATE_AND_TIME", "DEC_sequence", "DEC_signed", "DEC_unsigned", "Hex",
            "Block_number", "Octal", "Pointer", "Float", "Counter", "SIMATIC_Time", "String", "Time", "TIME_OF_DAY", "Unicode_character", "Unicode_character_sequence",
            "Unicode_string", "Symbolic"
        };

        /// <summary>The values of PlcWatchAndForceTablePreDefinedTrigger that a row can be given (not 'Undef').</summary>
        public static readonly string[] Triggers = { "Permanent", "PermanentAtStart", "OnceOnlyAtStart", "PermanentAtEnd", "OnceOnlyAtEnd", "PermanentAtStop", "OnceOnlyAtStop" };

        [Description("'add' (a row), 'delete' (a row) or 'clear' (every row)")]
        public string? Action { get; set; }

        [Description("add: the position of the new row, counted from 0 as 'plc_get_watch_table_info' lists the rows; leave out to add at the end. delete: the row to delete")]
        public int? Index { get; set; }

        [Description("A PLC tag (Motor_On) or a member of a data block (\"DB_Pumps\".Pump1.Speed). The address and, for a tag, the display format come from TIA Portal. A name that is no tag or data block of the PLC is refused. delete: the row with this name")]
        public string? Name { get; set; }

        [Description("An absolute address such as %MW10 or %I0.0 (instead of a name). delete: the row with this address")]
        public string? Address { get; set; }

        [Description("add with an address: how the value is shown, e.g. Hex, Bin, Bool, DEC_signed, Float. A row given by name takes it from the tag")]
        public string? DisplayFormat { get; set; }

        [Description("When the value is read: Permanent (default), PermanentAtStart, OnceOnlyAtStart, PermanentAtEnd, OnceOnlyAtEnd, PermanentAtStop, OnceOnlyAtStop")]
        public string? MonitorTrigger { get; set; }

        [Description("When the modify value is written: the same values as monitorTrigger")]
        public string? ModifyTrigger { get; set; }

        [Description("The value to write, such as 16#10, true or 3.5. It is not written to the PLC: this is the preset of the table. Not for members of data blocks, whose format TIA Portal leaves open")]
        public string? ModifyValue { get; set; }

        [Description("Comment of the row, for every language of the project. With no name and no address the row is a comment line of its own")]
        public string? Comment { get; set; }

        /// <summary>Fields the caller sent that the action does not have. They are refused, not dropped.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Unknown { get; set; }
    }
}
