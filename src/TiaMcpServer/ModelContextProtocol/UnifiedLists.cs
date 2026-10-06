using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One change to a text or graphic list of a WinCC Unified HMI.</summary>
    public class UnifiedTextListAction
    {
        [Description("'create' (fails if the list exists), 'update' (fails if it does not), 'upsert' or 'delete'. Writing a list replaces all of its entries")]
        public string? Action { get; set; }

        [Description("Name of the list")]
        public string? ListName { get; set; }

        [Description("'text' (default) or 'graphic'. A graphic list can only be deleted here")]
        public string? Kind { get; set; }

        [Description("The entries of the list, all of them: writing a list replaces what it had")]
        public List<UnifiedTextListEntry>? Entries { get; set; }
    }

    public class UnifiedTextListEntry
    {
        [Description("The value the entry stands for; required")]
        public long? Value { get; set; }

        [Description("The text: a string for every project language, or {\"en-US\": \"...\", \"de-DE\": \"...\"} for single languages")]
        public JsonElement Text { get; set; }
    }

    public class ResponseUnifiedTextLists : ResponseMessage
    {
        public List<UnifiedTextListInfo>? Items { get; set; }
    }

    public class UnifiedTextListInfo
    {
        public string? Name { get; set; }

        public List<UnifiedTextListEntryInfo> Entries { get; set; } = new List<UnifiedTextListEntryInfo>();
    }

    public class UnifiedTextListEntryInfo
    {
        /// <summary>The value the entry stands for.</summary>
        public long Value { get; set; }

        /// <summary>Set when the entry covers a range of values rather than one.</summary>
        public string? Range { get; set; }

        /// <summary>Text by language; languages without a text are left out.</summary>
        public Dictionary<string, string> Texts { get; set; } = new Dictionary<string, string>();
    }

    public class ResponseUnifiedGraphicLists : ResponseMessage
    {
        /// <summary>Names of the graphic lists.</summary>
        public List<string>? Items { get; set; }

        /// <summary>The lists as TIA Portal exports them (YAML), by file name; empty when there are none.</summary>
        public Dictionary<string, string>? Export { get; set; }
    }
}
