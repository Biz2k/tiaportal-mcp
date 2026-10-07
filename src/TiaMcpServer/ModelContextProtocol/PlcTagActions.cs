using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One change to a PLC tag of a tag table.</summary>
    public class PlcTagAction
    {
        [Description("'create' (fails if the tag exists), 'update' (fails if it does not), 'upsert' or 'delete'")]
        public string? Action { get; set; }

        [Description("Name of the tag")]
        public string? Name { get; set; }

        [Description("update: new name of the tag")]
        public string? NewName { get; set; }

        [Description("PLC data type such as Bool, Int, Word or the name of a PLC data type. A new tag needs it together with logicalAddress")]
        public string? DataType { get; set; }

        [Description("The same as dataType, under the name 'plc_create_tag' uses")]
        public string? DataTypeName { get; set; }

        [Description("Absolute address such as %I0.0, %QW4 or %M10.1")]
        public string? LogicalAddress { get; set; }

        [Description("Comment of the tag, set for every language of the project; an empty string clears it")]
        public string? Comment { get; set; }

        /// <summary>Fields the caller sent that the action does not have. They are refused, not dropped.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Unknown { get; set; }

        internal string? Type => !string.IsNullOrWhiteSpace(DataType) ? DataType!.Trim() : !string.IsNullOrWhiteSpace(DataTypeName) ? DataTypeName!.Trim() : null;

        internal string? Address => string.IsNullOrWhiteSpace(LogicalAddress) ? null : LogicalAddress!.Trim();
    }

    /// <summary>
    /// Checks a batch of tag actions against the names the table holds, before TIA Portal is touched: after an exception
    /// of Openness the transaction of the call cannot be committed, so everything that can be known beforehand has to
    /// be. Pure logic, so that the tests reach it without TIA Portal.
    /// </summary>
    public static class PlcTagActions
    {
        public const string Fields = "action, name, newName, dataType, logicalAddress, comment";

        /// <summary>The verb of an action in its canonical spelling, or null.</summary>
        public static string? Verb(PlcTagAction action)
        {
            var verb = (action.Action ?? string.Empty).Trim().ToLowerInvariant();

            return verb == "create" || verb == "update" || verb == "upsert" || verb == "delete" ? verb : null;
        }

        /// <param name="existing">The names of the tags of the table now.</param>
        /// <returns>One message for every action that cannot be applied; empty when the batch is sound.</returns>
        public static List<string> Check(IList<PlcTagAction>? actions, IEnumerable<string> existing)
        {
            var problems = new List<string>();

            if (actions == null || actions.Count == 0)
            {
                problems.Add("No actions given. Example: [{\"action\": \"create\", \"name\": \"Motor_On\", \"dataType\": \"Bool\", \"logicalAddress\": \"%M10.0\", \"comment\": \"...\"}].");

                return problems;
            }

            var names = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                var label = $"action {i + 1}";

                if (action == null)
                {
                    problems.Add($"{label}: is null.");

                    continue;
                }

                if (action.Unknown != null && action.Unknown.Count > 0)
                {
                    problems.Add($"{label}: unknown field(s) {string.Join(", ", action.Unknown.Keys.Select(k => $"'{k}'"))}. Fields: {Fields}.");

                    continue;
                }

                var verb = Verb(action);
                var name = (action.Name ?? string.Empty).Trim();

                label = $"{label} ({action.Action} '{name}')";

                if (verb == null)
                {
                    problems.Add($"{label}: unknown action. Use 'create', 'update', 'upsert' or 'delete'.");

                    continue;
                }

                if (name.Length == 0 || name.Contains("/"))
                {
                    problems.Add($"{label}: 'name' is required and must not contain a slash.");

                    continue;
                }

                var exists = names.Contains(name);

                if (verb == "delete")
                {
                    if (!exists)
                    {
                        problems.Add($"{label}: the table has no such tag.");
                    }

                    names.Remove(name);

                    continue;
                }

                if (verb == "create" && exists)
                {
                    problems.Add($"{label}: the tag already exists. Use 'update' or 'upsert'.");

                    continue;
                }

                if (verb == "update" && !exists)
                {
                    problems.Add($"{label}: the table has no such tag. Use 'create' or 'upsert'.");

                    continue;
                }

                if (!exists)
                {
                    // TIA Portal creates a tag either bare or with both the type and the address.
                    if ((action.Type == null) != (action.Address == null))
                    {
                        problems.Add($"{label}: a new tag takes 'dataType' and 'logicalAddress' together (or neither, for a tag with the default type and no address).");

                        continue;
                    }

                    names.Add(name);
                }

                var newName = (action.NewName ?? string.Empty).Trim();

                if (newName.Length > 0 && !string.Equals(newName, name, StringComparison.Ordinal))
                {
                    if (newName.Contains("/"))
                    {
                        problems.Add($"{label}: 'newName' must not contain a slash.");
                    }
                    else if (names.Contains(newName) && !string.Equals(newName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        problems.Add($"{label}: the name '{newName}' is taken in this table.");
                    }
                    else
                    {
                        names.Remove(name);
                        names.Add(newName);
                    }
                }
            }

            return problems;
        }
    }

    /// <summary>
    /// What a PLC tag may be given. Openness stores any text as the data type and as the address of a tag - "NoSuchType",
    /// "garbage", a Bool at %MW4 (probe of 2026-10-07) - and a tag has no validation of its own; the mistake shows at the
    /// next compile, far from the call that made it. So the type and the address are checked here.
    /// </summary>
    public static class PlcTagRules
    {
        /// <summary>The elementary types a tag can have, with the width its address must have: X bit, B, W, D, T timer, C counter, * any.</summary>
        private static readonly Dictionary<string, char> Elementary = new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase)
        {
            ["Bool"] = 'X',
            ["Byte"] = 'B', ["SInt"] = 'B', ["USInt"] = 'B', ["Char"] = 'B',
            ["Word"] = 'W', ["Int"] = 'W', ["UInt"] = 'W', ["Date"] = 'W', ["S5Time"] = 'W', ["WChar"] = 'W',
            ["DWord"] = 'D', ["DInt"] = 'D', ["UDInt"] = 'D', ["Real"] = 'D', ["Time"] = 'D', ["Time_Of_Day"] = 'D', ["TOD"] = 'D',
            ["LWord"] = '*', ["LInt"] = '*', ["ULInt"] = '*', ["LReal"] = '*', ["LTime"] = '*', ["LTime_Of_Day"] = '*', ["LTOD"] = '*',
            ["Date_And_Time"] = '*', ["DT"] = '*', ["LDT"] = '*', ["Date_And_LTime"] = '*', ["DTL"] = '*',
            ["Timer"] = 'T', ["Counter"] = 'C'
        };

        private static readonly System.Text.RegularExpressions.Regex Address = new System.Text.RegularExpressions.Regex(
            @"^%(?:[IQMEA](?:(?<bit>X?\d+\.[0-7])|(?<size>[BWD])\d+)|(?<timer>T)\d+|(?<counter>[CZ])\d+)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <param name="isPlcDataType">Whether the PLC has a PLC data type (UDT) of this name.</param>
        /// <returns>What is wrong with the pair, or null. An empty type or address is not checked: it keeps what the tag has.</returns>
        public static string? Problem(string? dataType, string? address, Func<string, bool> isPlcDataType)
        {
            var type = (dataType ?? string.Empty).Trim();
            var bare = type.Trim('"');
            var width = '*';

            if (type.Length > 0)
            {
                if (Elementary.TryGetValue(bare, out var known))
                {
                    width = known;
                }
                else if (!isPlcDataType(bare))
                {
                    return $"'{type}' is neither an elementary data type a tag can have (Bool, Byte, Word, DWord, Int, DInt, Real, Time ...) nor a PLC data type of this PLC ('plc_get_types' lists them). TIA Portal would store it as it is and report it at the compile.";
                }
            }

            var text = (address ?? string.Empty).Trim();

            if (text.Length == 0)
            {
                return null;
            }

            var match = Address.Match(text);

            if (!match.Success)
            {
                return $"'{text}' is not an absolute address. Examples: %I0.0, %Q4.1, %M10.0 (bit), %MB10, %MW10, %MD10, %IW64, %QW4. TIA Portal would store it as it is.";
            }

            var given = match.Groups["bit"].Success ? 'X'
                : match.Groups["timer"].Success ? 'T'
                : match.Groups["counter"].Success ? 'C'
                : char.ToUpperInvariant(match.Groups["size"].Value[0]);

            if (width == '*' || width == given)
            {
                return null;
            }

            var wanted = width == 'X' ? "a bit address such as %M10.0"
                : width == 'T' ? "a timer address such as %T1"
                : width == 'C' ? "a counter address such as %C1"
                : $"a {(width == 'B' ? "byte" : width == 'W' ? "word" : "double word")} address such as %M{width}10";

            return $"The data type {bare} needs {wanted}; '{text}' is not one. TIA Portal would store the pair and report it at the compile.";
        }
    }
}
