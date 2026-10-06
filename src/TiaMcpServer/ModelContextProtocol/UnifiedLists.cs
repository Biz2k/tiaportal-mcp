using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One change to a text list or graphic list of a WinCC Unified HMI.</summary>
    public class UnifiedListAction
    {
        [Description("'create' (fails if the list exists), 'update' (fails if it does not), 'upsert' or 'delete'. Writing a list replaces all of its entries")]
        public string? Action { get; set; }

        [Description("Name of the list")]
        public string? ListName { get; set; }

        [Description("'text' (default) or 'graphic'")]
        public string? Kind { get; set; }

        [Description("The entries of the list, all of them: writing a list replaces what it had")]
        public List<UnifiedListEntry>? Entries { get; set; }
    }

    public class UnifiedListEntry
    {
        [Description("A single value the entry stands for. Give either 'value', or 'from' and/or 'to', or 'default'")]
        public long? Value { get; set; }

        [Description("Lower bound: with 'to' a range from..to, alone 'this value and above'")]
        public long? From { get; set; }

        [Description("Upper bound: with 'from' a range from..to, alone 'this value and below'")]
        public long? To { get; set; }

        [Description("true for the default entry, shown for every value no other entry covers; it takes no value. One per list")]
        public bool Default { get; set; }

        [Description("Text list: the text - a string for every project language, or {\"en-US\": \"...\", \"de-DE\": \"...\"} for single languages")]
        public JsonElement Text { get; set; }

        [Description("Graphic list: name of the graphic in the project graphics, e.g. 'Pump_On'. TIA Portal does not check that it exists")]
        public string? Graphic { get; set; }
    }

    public class ResponseUnifiedLists : ResponseMessage
    {
        public List<UnifiedListInfo>? Items { get; set; }
    }

    public class UnifiedListInfo
    {
        public string? Name { get; set; }

        /// <summary>"text" or "graphic".</summary>
        public string? Kind { get; set; }

        public List<UnifiedListEntryInfo> Entries { get; set; } = new List<UnifiedListEntryInfo>();
    }

    public class UnifiedListEntryInfo
    {
        /// <summary>"value", "range", "from", "to" or "default"; anything else is what TIA Portal exported, unread.</summary>
        public string? Type { get; set; }

        /// <summary>The single value of a "value" entry.</summary>
        public long? Value { get; set; }

        /// <summary>Lower bound of a "range" or "from" entry.</summary>
        public long? From { get; set; }

        /// <summary>Upper bound of a "range" or "to" entry.</summary>
        public long? To { get; set; }

        /// <summary>Text list: text by language; languages without a text are left out.</summary>
        public Dictionary<string, string>? Texts { get; set; }

        /// <summary>Graphic list: name of the graphic.</summary>
        public string? Graphic { get; set; }
    }
}
