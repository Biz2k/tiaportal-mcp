using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>The trigger of a script dynamization: when the script runs.</summary>
    public class UnifiedTriggerSpec
    {
        /// <summary>One of <see cref="UnifiedDynamizationSpec.TriggerTypes"/>, spelled as TIA Portal does.</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>The tags of the trigger 'Tags'; empty for the other types.</summary>
        public List<string> Tags { get; } = new List<string>();

        /// <summary>The name of the cycle of the trigger 'CustomCycle'.</summary>
        public string? Cycle { get; set; }
    }

    /// <summary>One row of the table that maps the value of a tag to a value of the property.</summary>
    public class UnifiedMappingEntrySpec
    {
        /// <summary>Range tables: the limits, a number each. Single-bit tables use <see cref="Bit"/> instead.</summary>
        public object? From { get; set; }

        public object? To { get; set; }

        /// <summary>Single-bit tables: the row for the bit value 0 or 1.</summary>
        public int? Bit { get; set; }

        public JsonElement Value { get; set; }

        public bool HasValue { get; set; }

        public JsonElement Alternate { get; set; }

        public bool HasAlternate { get; set; }

        public bool? Flashing { get; set; }

        public string? Rate { get; set; }
    }

    /// <summary>The table of a value converter. Type: 'range', 'singlebit' or 'none' (which switches the table off).</summary>
    public class UnifiedMappingSpec
    {
        public string Type { get; set; } = string.Empty;

        public List<UnifiedMappingEntrySpec> Entries { get; } = new List<UnifiedMappingEntrySpec>();
    }

    /// <summary>What an event (or a change of a property) does: the script and how it runs.</summary>
    public class UnifiedEventSpec
    {
        /// <summary>The code; null means the handler is to be removed.</summary>
        public string? Script { get; set; }

        public bool? Async { get; set; }

        public string? GlobalDefinitions { get; set; }
    }

    /// <summary>
    /// Reads the options that the dynamization and event objects of 'unified_manage_items' take
    /// beside their main key, and refuses what is wrong before anything reaches Openness - an
    /// Openness exception would cost the whole batch its commit. Pure logic, so that the tests
    /// need no TIA Portal.
    /// </summary>
    public static class UnifiedDynamizationSpec
    {
        /// <summary>The kinds of dynamization that carry options. 'tag' and 'script' also exist without any.</summary>
        public static readonly string[] MainKeys = { "tag", "script", "expression", "flashing" };

        public static readonly string[] TriggerTypes =
        {
            "Disabled", "T100ms", "T250ms", "T500ms", "T1s", "T2s", "T5s", "T10s", "CustomCycle", "Tags", "AutomaticTags"
        };

        public static readonly string[] FlashingRates = { "Slow", "Medium", "Fast" };

        public static readonly string[] FlashingConditions = { "Never", "Always", "RangeViolation" };

        /// <summary>The key of the property object that decides the kind of dynamization, or null if none does.</summary>
        public static string? FindMainKey(IEnumerable<string> keys)
        {
            return keys.FirstOrDefault(k => MainKeys.Contains(k, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>The keys beside the main one, each checked against what that kind of dynamization takes.</summary>
        public static Dictionary<string, JsonElement> Options(JsonElement obj, string mainKey, params string[] allowed)
        {
            var options = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

            foreach (var part in obj.EnumerateObject())
            {
                if (part.Name.Equals(mainKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!allowed.Contains(part.Name, StringComparer.OrdinalIgnoreCase))
                {
                    throw Invalid($"'{part.Name}' is not an option of '{mainKey}'. Options: {(allowed.Length == 0 ? "none" : string.Join(", ", allowed))}.");
                }

                options[part.Name] = part.Value;
            }

            return options;
        }

        public static bool ReadBool(JsonElement value, string name)
        {
            if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
            {
                throw Invalid($"'{name}' takes true or false; got {value.GetRawText()}.");
            }

            return value.GetBoolean();
        }

        public static string ReadText(JsonElement value, string name)
        {
            if (value.ValueKind != JsonValueKind.String)
            {
                throw Invalid($"'{name}' takes a string; got {value.GetRawText()}.");
            }

            return value.GetString()!;
        }

        /// <summary>"T1s", or { "type": "Tags", "tags": ["A", "B"] }, or { "type": "CustomCycle", "cycle": "Custom cycle" }.</summary>
        public static UnifiedTriggerSpec ParseTrigger(JsonElement value)
        {
            var spec = new UnifiedTriggerSpec();

            if (value.ValueKind == JsonValueKind.String)
            {
                spec.Type = CanonicalTriggerType(value.GetString());

                RequireNoMissingParts(spec);

                return spec;
            }

            if (value.ValueKind != JsonValueKind.Object)
            {
                throw Invalid($"'trigger' takes a type name or an object {{\"type\": ..., \"tags\": [...], \"cycle\": ...}}; got {value.GetRawText()}.");
            }

            string? type = null;

            foreach (var part in value.EnumerateObject())
            {
                switch (part.Name.ToLowerInvariant())
                {
                    case "type":
                        type = ReadText(part.Value, "trigger.type");

                        break;

                    case "tags":
                        if (part.Value.ValueKind != JsonValueKind.Array)
                        {
                            throw Invalid($"'trigger.tags' takes a list of tag names; got {part.Value.GetRawText()}.");
                        }

                        foreach (var tag in part.Value.EnumerateArray())
                        {
                            spec.Tags.Add(ReadText(tag, "trigger.tags"));
                        }

                        break;

                    case "cycle":
                        spec.Cycle = ReadText(part.Value, "trigger.cycle");

                        break;

                    default:
                        throw Invalid($"'trigger' has no part '{part.Name}'. Parts: type, tags, cycle.");
                }
            }

            spec.Type = CanonicalTriggerType(type);

            RequireNoMissingParts(spec);

            if (spec.Type != "Tags" && spec.Tags.Count > 0)
            {
                throw Invalid($"'trigger.tags' belongs to the type 'Tags'; the type is '{spec.Type}'.");
            }

            if (spec.Type != "CustomCycle" && spec.Cycle != null)
            {
                throw Invalid($"'trigger.cycle' belongs to the type 'CustomCycle'; the type is '{spec.Type}'. The cycles T100ms .. T10s are types of their own.");
            }

            return spec;
        }

        private static string CanonicalTriggerType(string? type)
        {
            var match = TriggerTypes.FirstOrDefault(t => t.Equals(type?.Trim(), StringComparison.OrdinalIgnoreCase));

            return match ?? throw Invalid($"Trigger type '{type}' does not exist. Types: {string.Join(", ", TriggerTypes)}.");
        }

        private static void RequireNoMissingParts(UnifiedTriggerSpec spec)
        {
            if (spec.Type == "Tags" && spec.Tags.Count == 0)
            {
                throw Invalid("The trigger 'Tags' needs 'tags': the tags whose change runs the script, e.g. {\"type\": \"Tags\", \"tags\": [\"Tag_1\"]}.");
            }

            if (spec.Type == "CustomCycle" && string.IsNullOrWhiteSpace(spec.Cycle))
            {
                throw Invalid("The trigger 'CustomCycle' needs 'cycle': the name of a cycle of the HMI, e.g. {\"type\": \"CustomCycle\", \"cycle\": \"Custom cycle\"}.");
            }
        }

        /// <summary>{ "type": "range", "entries": [ { "from": 0, "to": 30, "value": "#00FF00" }, ... ] }.</summary>
        public static UnifiedMappingSpec ParseMapping(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                throw Invalid($"'mapping' takes an object {{\"type\": \"range\", \"entries\": [...]}}; got {value.GetRawText()}.");
            }

            var spec = new UnifiedMappingSpec();
            string? type = null;
            JsonElement? entries = null;

            foreach (var part in value.EnumerateObject())
            {
                switch (part.Name.ToLowerInvariant())
                {
                    case "type":
                        type = ReadText(part.Value, "mapping.type").Trim().ToLowerInvariant();

                        break;

                    case "entries":
                        entries = part.Value;

                        break;

                    default:
                        throw Invalid($"'mapping' has no part '{part.Name}'. Parts: type, entries.");
                }
            }

            if (type != "range" && type != "singlebit" && type != "none")
            {
                throw Invalid($"mapping.type '{type}' is not supported. Use 'range' (rows with from, to, value), 'singlebit' (rows for bit 0 and 1) or 'none'. " +
                              "The bitmask and expression tables are not offered: creating an expression row closes TIA Portal, and a bitmask row cannot choose its mask.");
            }

            spec.Type = type;

            if (type == "none")
            {
                if (entries != null)
                {
                    throw Invalid("mapping.type 'none' switches the table off and takes no entries.");
                }

                return spec;
            }

            if (entries == null || entries.Value.ValueKind != JsonValueKind.Array || entries.Value.GetArrayLength() == 0)
            {
                throw Invalid($"mapping.type '{type}' needs 'entries': a list with at least one row.");
            }

            foreach (var row in entries.Value.EnumerateArray())
            {
                spec.Entries.Add(ParseEntry(row, type));
            }

            if (type == "singlebit")
            {
                var bits = spec.Entries.Select(e => e.Bit).ToList();

                if (bits.Distinct().Count() != bits.Count)
                {
                    throw Invalid("The 'singlebit' table has one row per bit value (0 and 1); a bit is named twice.");
                }
            }

            return spec;
        }

        private static UnifiedMappingEntrySpec ParseEntry(JsonElement row, string type)
        {
            if (row.ValueKind != JsonValueKind.Object)
            {
                throw Invalid($"A mapping row is an object; got {row.GetRawText()}.");
            }

            var entry = new UnifiedMappingEntrySpec();

            foreach (var part in row.EnumerateObject())
            {
                switch (part.Name.ToLowerInvariant())
                {
                    case "from":
                    case "to":
                        if (type != "range")
                        {
                            throw Invalid($"'{part.Name}' belongs to the rows of a 'range' table; a 'singlebit' row names its 'bit'.");
                        }

                        var number = ReadNumber(part.Value, part.Name);

                        if (part.Name.Equals("from", StringComparison.OrdinalIgnoreCase))
                        {
                            entry.From = number;
                        }
                        else
                        {
                            entry.To = number;
                        }

                        break;

                    case "bit":
                        if (type != "singlebit")
                        {
                            throw Invalid("'bit' belongs to the rows of a 'singlebit' table; a 'range' row has 'from' and 'to'.");
                        }

                        if (part.Value.ValueKind != JsonValueKind.Number || !part.Value.TryGetInt32(out var bit) || (bit != 0 && bit != 1))
                        {
                            throw Invalid($"'bit' is 0 or 1; got {part.Value.GetRawText()}.");
                        }

                        entry.Bit = bit;

                        break;

                    case "value":
                        entry.Value = part.Value;
                        entry.HasValue = true;

                        break;

                    case "alternate":
                    case "alternatevalue":
                        entry.Alternate = part.Value;
                        entry.HasAlternate = true;

                        break;

                    case "flashing":
                        entry.Flashing = ReadBool(part.Value, "flashing");

                        break;

                    case "rate":
                        entry.Rate = CanonicalRate(ReadText(part.Value, "rate"));

                        break;

                    default:
                        throw Invalid($"A mapping row has no part '{part.Name}'. Parts: {(type == "range" ? "from, to" : "bit")}, value, alternate, flashing, rate.");
                }
            }

            if (!entry.HasValue)
            {
                throw Invalid($"A mapping row needs 'value': what the property shows for it. Row: {row.GetRawText()}.");
            }

            if (type == "range")
            {
                if (entry.From == null || entry.To == null)
                {
                    throw Invalid($"A 'range' row needs 'from' and 'to'. Row: {row.GetRawText()}.");
                }

                if (Convert.ToDouble(entry.From) > Convert.ToDouble(entry.To))
                {
                    throw Invalid($"A 'range' row has 'from' above 'to'. Row: {row.GetRawText()}.");
                }
            }
            else if (entry.Bit == null)
            {
                throw Invalid($"A 'singlebit' row needs 'bit' (0 or 1). Row: {row.GetRawText()}.");
            }

            return entry;
        }

        public static string CanonicalRate(string rate)
        {
            return FlashingRates.FirstOrDefault(r => r.Equals(rate.Trim(), StringComparison.OrdinalIgnoreCase))
                   ?? throw Invalid($"Flashing rate '{rate}' does not exist. Rates: {string.Join(", ", FlashingRates)}.");
        }

        public static string CanonicalCondition(string condition)
        {
            return FlashingConditions.FirstOrDefault(c => c.Equals(condition.Trim(), StringComparison.OrdinalIgnoreCase))
                   ?? throw Invalid($"Flashing condition '{condition}' does not exist. Conditions: {string.Join(", ", FlashingConditions)}.");
        }

        /// <summary>A whole number stays whole (an int when it fits), anything else is a double.</summary>
        private static object ReadNumber(JsonElement value, string name)
        {
            if (value.ValueKind != JsonValueKind.Number)
            {
                throw Invalid($"'{name}' takes a number; got {value.GetRawText()}.");
            }

            if (value.TryGetInt32(out var whole))
            {
                return whole;
            }

            return value.GetDouble();
        }

        /// <summary>
        /// An event of an item: a script as a string, null (or an empty string) to remove it, or
        /// { "script": "code", "async": true, "globalDefinitions": "const k = 5;" }.
        /// </summary>
        public static UnifiedEventSpec ParseEvent(JsonElement value, string name)
        {
            var spec = new UnifiedEventSpec();

            switch (value.ValueKind)
            {
                case JsonValueKind.Null:
                    return spec;

                case JsonValueKind.String:
                    spec.Script = string.IsNullOrWhiteSpace(value.GetString()) ? null : value.GetString();

                    return spec;

                case JsonValueKind.Object:
                    foreach (var part in value.EnumerateObject())
                    {
                        switch (part.Name.ToLowerInvariant())
                        {
                            case "script":
                                spec.Script = string.IsNullOrWhiteSpace(ReadText(part.Value, name + ".script")) ? null : part.Value.GetString();

                                break;

                            case "async":
                                spec.Async = ReadBool(part.Value, name + ".async");

                                break;

                            case "globaldefinitions":
                                spec.GlobalDefinitions = ReadText(part.Value, name + ".globalDefinitions");

                                break;

                            default:
                                throw Invalid($"Event '{name}' has no part '{part.Name}'. Parts: script, async, globalDefinitions.");
                        }
                    }

                    if (spec.Script == null && (spec.Async != null || spec.GlobalDefinitions != null))
                    {
                        throw Invalid($"Event '{name}' gives 'async' or 'globalDefinitions' without a 'script'.");
                    }

                    return spec;

                default:
                    throw Invalid($"Event '{name}' takes the script as a string, null to remove it, or an object {{\"script\": ..., \"async\": ..., \"globalDefinitions\": ...}}; got {value.GetRawText()}.");
            }
        }

        /// <summary>The key of a property event: "ProcessValue" (a change of the value) or "ProcessValue.QualityCodeChange".</summary>
        public static (string Property, string Type) SplitPropertyEvent(string key)
        {
            var dot = key.LastIndexOf('.');

            if (dot < 0)
            {
                return (key.Trim(), "Change");
            }

            var type = key.Substring(dot + 1).Trim();
            var canonical = new[] { "Change", "QualityCodeChange" }.FirstOrDefault(t => t.Equals(type, StringComparison.OrdinalIgnoreCase));

            if (canonical == null)
            {
                // Not an event type: the dot belongs to the property name.
                return (key.Trim(), "Change");
            }

            return (key.Substring(0, dot).Trim(), canonical);
        }

        private static PortalException Invalid(string message)
        {
            return new PortalException(PortalErrorCode.InvalidParams, message);
        }
    }
}
