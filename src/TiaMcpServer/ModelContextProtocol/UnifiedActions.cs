using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One change to an HMI tag of a WinCC Unified HMI.</summary>
    public class UnifiedTagAction
    {
        [Description("'create' (fails if the tag exists), 'update' (fails if it does not), 'upsert' or 'delete'")]
        public string? Action { get; set; }

        [Description("Name of the HMI tag")]
        public string? TagName { get; set; }

        [Description("Tag table a new tag is created in; empty for the default tag table. A tag cannot be moved to another table afterwards")]
        public string? TagTable { get; set; }

        [Description("Properties to set, by name, e.g. {\"DataType\": \"Real\", \"InitialValue\": 1.5} for an internal tag or {\"Connection\": \"HMI_Connection_1\", \"PlcTag\": \"Motor.Speed\"} for a PLC tag. An empty Connection makes the tag internal. 'Name' renames the tag. Comment takes a string (all languages) or {\"en-US\": \"...\"}. DisplayName cannot be set through Openness. Linear scaling (LinearScaling, HmiStartValue, HmiEndValue, PlcStartValue, PlcEndValue) and the substitute value (\"SubstituteValue.SubstituteValueUsage\": None, InvalidValue, RangeViolation, InvalidValueOrRangeViolation; \"SubstituteValue.Value\") work on an external tag only. Limits: \"InitialMaxValue.ValueType\" and \"InitialMinValue.ValueType\" (None, Constant, Tag), then \".Value\" (a number, or a tag name for the type Tag)")]
        public Dictionary<string, JsonElement>? Properties { get; set; }
    }

    /// <summary>One change to a tag table of a WinCC Unified HMI.</summary>
    public class UnifiedTagTableAction
    {
        [Description("'create', 'rename' or 'delete'. Deleting a table deletes the tags in it")]
        public string? Action { get; set; }

        [Description("Name of the tag table")]
        public string? TableName { get; set; }

        [Description("New name, for 'rename'")]
        public string? NewName { get; set; }

        [Description("Tag table group a new table is created in, e.g. 'Pumps' or 'Pumps/Big'; empty for the top level. For 'create' only; the group has to exist ('unified_manage_tag_table_groups' makes one). A table cannot be moved between groups afterwards")]
        public string? Group { get; set; }
    }

    /// <summary>One change to a screen group of a WinCC Unified HMI.</summary>
    public class UnifiedScreenGroupAction
    {
        [Description("'create', 'rename' or 'delete'. Deleting a group deletes the screens in it")]
        public string? Action { get; set; }

        [Description("Path of the group: 'Pumps', or 'Pumps/Big' for a group in a group. For 'create' the parent has to exist")]
        public string? GroupName { get; set; }

        [Description("New name of the group (the name only, not a path), for 'rename'")]
        public string? NewName { get; set; }
    }

    /// <summary>One change to a tag table group of a WinCC Unified HMI.</summary>
    public class UnifiedTagTableGroupAction
    {
        [Description("'create', 'rename' or 'delete'. Deleting a group deletes the tag tables in it and their tags")]
        public string? Action { get; set; }

        [Description("Path of the group: 'Pumps', or 'Pumps/Big' for a group in a group. For 'create' the parent has to exist. Group names are unique in the whole HMI")]
        public string? GroupName { get; set; }

        [Description("New name of the group (the name only, not a path), for 'rename'")]
        public string? NewName { get; set; }
    }

    public class ResponseUnifiedTagTableGroups : ResponseMessage
    {
        public List<UnifiedTagTableGroupInfo>? Items { get; set; }
    }

    public class UnifiedTagTableGroupInfo
    {
        /// <summary>'/'-separated path, the group name for one at the top.</summary>
        public string? Path { get; set; }

        public string? Name { get; set; }

        /// <summary>Tag tables directly in the group.</summary>
        public int TableCount { get; set; }

        /// <summary>Groups directly in the group.</summary>
        public int GroupCount { get; set; }
    }

    public class ResponseUnifiedScreenGroups : ResponseMessage
    {
        public List<UnifiedScreenGroupInfo>? Items { get; set; }
    }

    public class UnifiedScreenGroupInfo
    {
        /// <summary>'/'-separated path, the group name for one at the top.</summary>
        public string? Path { get; set; }

        public string? Name { get; set; }

        /// <summary>Screens directly in the group.</summary>
        public int ScreenCount { get; set; }

        /// <summary>Groups directly in the group.</summary>
        public int GroupCount { get; set; }
    }

    /// <summary>One change to a connection of a WinCC Unified HMI.</summary>
    public class UnifiedConnectionAction
    {
        [Description("'create' (fails if the connection exists), 'update' (fails if it does not), 'upsert' or 'delete'")]
        public string? Action { get; set; }

        [Description("Name of the connection")]
        public string? ConnectionName { get; set; }

        [Description("For a new connection to a PLC of the project (integrated connection): the path of the PLC as the plc_* tools take it, e.g. 'PLC_1' or 'Station_1/PLC_1'. The HMI and the PLC need interfaces on a common subnet. Leave empty for a non-integrated connection that is addressed through driverProperties")]
        public string? Partner { get; set; }

        [Description("With 'partner': interface of the HMI to use, by item name or node name (e.g. 'PROFINET Interface_1' or 'X1'); empty picks the first one that shares a subnet with the PLC")]
        public string? LocalInterface { get; set; }

        [Description("With 'partner': interface of the PLC to use, by item name or node name; empty picks the first one that shares a subnet with the HMI")]
        public string? PartnerInterface { get; set; }

        [Description("Properties to set, by name: CommunicationDriver (e.g. 'SIMATIC S7 1200/1500', 'OPC UA'; not needed with 'partner'), Comment, DisabledAtStartup, Name (renames), InitialAddress")]
        public Dictionary<string, JsonElement>? Properties { get; set; }

        [Description("Driver parameters to set, by name, e.g. {\"Protocol.RemStAddress\": \"192.168.0.10\"}. They depend on the driver; 'unified_get_connections' lists them")]
        public Dictionary<string, string?>? DriverProperties { get; set; }
    }

    public class ResponseUnifiedActions : ResponseMessage
    {
        public List<UnifiedActionResult>? Results { get; set; }

        /// <summary>Number of actions applied. A call either applies all of them or fails as a whole.</summary>
        public int SuccessCount { get; set; }
    }

    public class UnifiedActionResult
    {
        public string? Action { get; set; }

        /// <summary>Name of the tag, tag table or connection the action was about.</summary>
        public string? Name { get; set; }

        /// <summary>"success" or "error".</summary>
        public string? Status { get; set; }

        public string? Error { get; set; }

        /// <summary>What was set.</summary>
        public List<string> Applied { get; set; } = new List<string>();

        /// <summary>Things worth knowing that are not failures.</summary>
        public List<string> Notes { get; set; } = new List<string>();
    }

    public class ResponseUnifiedTagTables : ResponseMessage
    {
        public List<UnifiedTagTableInfo>? Items { get; set; }
    }

    public class UnifiedTagTableInfo
    {
        public string? Name { get; set; }

        /// <summary>Group the table is in, '/'-separated; empty at the top.</summary>
        public string? Group { get; set; }

        public int TagCount { get; set; }
    }
}
