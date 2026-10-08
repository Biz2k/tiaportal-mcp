using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Changes of the rows of a PLC watch table, made on the SimaticML of the table. Why this way: found on V21 (probe of
    /// 2026-10-08) - PlcTableCommentEntryComposition.Create() makes a COMMENT row, nothing else; the rows of a watch table
    /// (PlcWatchTableEntry) have no setters, SetAttribute refuses every attribute ("is not supported by type
    /// PlcTableCommentEntry"), and ModifyIntention cannot be set at all. What works is the import: export the table, edit
    /// the XML, import it with ImportOptions.Override into the same group - the table keeps its name, its group and the
    /// other tables; rows (watch rows and comment rows) come in the order of the XML.
    /// What the import does with a row (all found by the same probe):
    ///  - Name (a PLC tag as "Tag" or a data block member as "DB".a.b, quoted or bare) or Address (%MW10) is enough; the other
    ///    of the two is filled by TIA Portal, and for a tag so are DisplayFormat and the triggers;
    ///  - a Name that no tag or block has does not fail: the row is DROPPED and the table becomes inconsistent. So the
    ///    names are checked before, and the table is read back after;
    ///  - DisplayFormat of a row given by Name is read-only (it comes from the data type) - the import fails with "cannot be set
    ///    because it is read-only"; so is ModifyValue on a row whose format is Undef (members of data blocks);
    ///  - a row may carry a comment (MultilingualText "Comment"); a comment row is a PlcTableCommentEntry.
    /// Pure logic: no TIA Portal needed, the tests work on exported text.
    /// </summary>
    public static class PlcWatchTableEdit
    {
        public const string TableElement = "SW.WatchAndForceTables.PlcWatchTable";
        public const string RowElement = "SW.WatchAndForceTables.PlcWatchTableEntry";
        public const string CommentRowElement = "SW.WatchAndForceTables.PlcTableCommentEntry";

        /// <summary>One row of a watch table as the XML states it. A field the XML leaves out is null.</summary>
        public sealed class Row
        {
            /// <summary>"Watch" or "Comment".</summary>
            public string Kind { get; set; } = "Watch";

            public string? Name { get; set; }

            public string? Address { get; set; }

            public string? DisplayFormat { get; set; }

            public string? MonitorTrigger { get; set; }

            public string? ModifyTrigger { get; set; }

            public string? ModifyValue { get; set; }

            /// <summary>The first non-empty text of the comment.</summary>
            public string? Comment { get; set; }
        }

        /// <summary>The rows of the table in the XML, in order.</summary>
        public static List<Row> ReadRows(XDocument document)
        {
            return RowElements(document).Select(ToRow).ToList();
        }

        /// <summary>A name without quotes and in a form two names are compared in.</summary>
        public static string Normalize(string? name) => (name ?? string.Empty).Replace("\"", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();

        /// <summary>The first part of a symbolic name, which has to be a tag or a data block: "HMI".Pumps.CP_1 gives HMI.</summary>
        public static string Root(string name)
        {
            var text = name.Trim();

            if (text.StartsWith("\"", StringComparison.Ordinal))
            {
                var end = text.IndexOf('"', 1);

                return end > 1 ? text.Substring(1, end - 1) : text.Substring(1);
            }

            var stop = text.IndexOfAny(new[] { '.', '[' });

            return stop > 0 ? text.Substring(0, stop) : text;
        }

        /// <param name="rows">The rows of the table now.</param>
        /// <param name="symbolExists">Whether a tag or a block of that name exists in the PLC; null skips the check.</param>
        /// <returns>One message for every action that cannot be applied; empty when the batch is sound.</returns>
        public static List<string> Check(IList<WatchTableEntryAction>? actions, IEnumerable<Row> rows, Func<string, bool>? symbolExists)
        {
            var problems = new List<string>();

            if (actions == null || actions.Count == 0)
            {
                problems.Add("No actions given. Example: [{\"action\": \"add\", \"name\": \"Motor_On\", \"modifyValue\": \"true\"}, {\"action\": \"add\", \"address\": \"%MW10\", \"displayFormat\": \"Hex\"}].");

                return problems;
            }

            var model = rows.Select(r => new Row { Kind = r.Kind, Name = r.Name, Address = r.Address }).ToList();

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
                    problems.Add($"{label}: unknown field(s) {string.Join(", ", action.Unknown.Keys.Select(k => $"'{k}'"))}. Fields: {WatchTableEntryAction.Fields}.");

                    continue;
                }

                var verb = (action.Action ?? string.Empty).Trim().ToLowerInvariant();

                label += $" ({action.Action})";

                if (verb == "clear")
                {
                    if (HasRowFields(action) || action.Index != null)
                    {
                        problems.Add($"{label}: 'clear' takes no other field.");

                        continue;
                    }

                    model.Clear();

                    continue;
                }

                if (verb == "delete")
                {
                    var problem = CheckDelete(action, model);

                    if (problem != null)
                    {
                        problems.Add($"{label}: {problem}");
                    }

                    continue;
                }

                if (verb != "add")
                {
                    problems.Add($"{label}: unknown action. Use 'add', 'delete' or 'clear'.");

                    continue;
                }

                var added = CheckAdd(action, model.Count, symbolExists, out var row);

                if (added != null)
                {
                    problems.Add($"{label}: {added}");

                    continue;
                }

                model.Insert(action.Index ?? model.Count, row!);
            }

            return problems;
        }

        /// <summary>Applies actions that <see cref="Check"/> accepted to the XML of the table.</summary>
        /// <param name="languages">The cultures of the project: a comment is written for each of them.</param>
        public static void Apply(XDocument document, IList<WatchTableEntryAction> actions, IList<string> languages)
        {
            var table = TableOf(document);
            var list = table.Element("ObjectList");

            if (list == null)
            {
                list = new XElement("ObjectList");
                table.Add(list);
            }

            var next = 1000;

            foreach (var action in actions)
            {
                var verb = action.Action!.Trim().ToLowerInvariant();

                if (verb == "clear")
                {
                    list.Elements().Where(IsRow).Remove();

                    continue;
                }

                var rows = list.Elements().Where(IsRow).ToList();

                if (verb == "delete")
                {
                    var target = action.Index != null ? rows[action.Index.Value] : rows[Matches(rows.Select(ToRow).ToList(), action)[0]];

                    target.Remove();

                    continue;
                }

                var element = NewRow(action, languages, ref next);

                if (action.Index == null || action.Index.Value >= rows.Count)
                {
                    if (rows.Count == 0)
                    {
                        list.Add(element);
                    }
                    else
                    {
                        rows[rows.Count - 1].AddAfterSelf(element);
                    }
                }
                else
                {
                    rows[action.Index.Value].AddBeforeSelf(element);
                }
            }
        }

        /// <summary>
        /// Compares what TIA Portal holds with what the XML asked for; one message per difference. A field the XML does not state
        /// is not compared (TIA Portal fills it).
        /// </summary>
        public static List<string> Compare(IList<Row> expected, IList<Row> actual)
        {
            var differences = new List<string>();

            if (expected.Count != actual.Count)
            {
                var missing = expected.Where(e => e.Kind == "Watch" && !actual.Any(a => a.Kind == "Watch" && (e.Name != null ? Same(e.Name, a.Name) : Same(e.Address, a.Address))))
                    .Select(e => e.Name ?? e.Address ?? "?").ToList();

                differences.Add($"the table holds {actual.Count} row(s), {expected.Count} were asked for" + (missing.Count > 0 ? $"; TIA Portal dropped: {string.Join(", ", missing)} (a name that is no tag or data block of the PLC is dropped without an error)" : string.Empty));

                return differences;
            }

            for (var i = 0; i < expected.Count; i++)
            {
                var e = expected[i];
                var a = actual[i];

                if (e.Kind != a.Kind)
                {
                    differences.Add($"row {i}: is a {a.Kind} row, a {e.Kind} row was asked for");

                    continue;
                }

                foreach (var (field, want, got) in new[]
                         {
                             ("name", e.Name, a.Name), ("address", e.Address, a.Address), ("displayFormat", e.DisplayFormat, a.DisplayFormat),
                             ("monitorTrigger", e.MonitorTrigger, a.MonitorTrigger), ("modifyTrigger", e.ModifyTrigger, a.ModifyTrigger), ("modifyValue", e.ModifyValue, a.ModifyValue)
                         })
                {
                    if (want != null && !Same(want, got))
                    {
                        differences.Add($"row {i}: {field} is '{got}', '{want}' was asked for");
                    }
                }
            }

            return differences;
        }

        private static bool Same(string? a, string? b) => Normalize(a) == Normalize(b);

        private static bool HasRowFields(WatchTableEntryAction a) =>
            a.Name != null || a.Address != null || a.DisplayFormat != null || a.MonitorTrigger != null || a.ModifyTrigger != null || a.ModifyValue != null || a.Comment != null;

        private static string? CheckDelete(WatchTableEntryAction a, List<Row> model)
        {
            if (a.DisplayFormat != null || a.MonitorTrigger != null || a.ModifyTrigger != null || a.ModifyValue != null || a.Comment != null)
            {
                return "'delete' takes 'index', 'name' or 'address' and nothing else.";
            }

            var given = (a.Index != null ? 1 : 0) + (a.Name != null ? 1 : 0) + (a.Address != null ? 1 : 0);

            if (given != 1)
            {
                return "give exactly one of 'index', 'name' or 'address'.";
            }

            if (a.Index != null)
            {
                if (a.Index < 0 || a.Index >= model.Count)
                {
                    return model.Count == 0 ? "the table has no rows." : $"index {a.Index} is outside the rows 0..{model.Count - 1}.";
                }

                model.RemoveAt(a.Index.Value);

                return null;
            }

            var found = Matches(model, a);

            if (found.Count == 0)
            {
                return $"the table has no row with {(a.Name != null ? "name" : "address")} '{a.Name ?? a.Address}'.";
            }

            if (found.Count > 1)
            {
                return $"{found.Count} rows have {(a.Name != null ? "name" : "address")} '{a.Name ?? a.Address}' (indexes {string.Join(", ", found)}); delete by 'index'.";
            }

            model.RemoveAt(found[0]);

            return null;
        }

        private static List<int> Matches(IList<Row> rows, WatchTableEntryAction a)
        {
            var found = new List<int>();

            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].Kind == "Watch" && (a.Name != null ? Same(rows[i].Name, a.Name) : Same(rows[i].Address, a.Address)))
                {
                    found.Add(i);
                }
            }

            return found;
        }

        private static string? CheckAdd(WatchTableEntryAction a, int count, Func<string, bool>? symbolExists, out Row? row)
        {
            row = null;

            var name = a.Name?.Trim();
            var address = a.Address?.Trim();

            if (a.Index != null && (a.Index < 0 || a.Index > count))
            {
                return $"index {a.Index} is outside 0..{count} (the table has {count} row(s); leave 'index' out to add at the end).";
            }

            if (name != null && address != null)
            {
                return "give 'name' (a tag or a data block member) or 'address' (such as %MW10), not both.";
            }

            if ((name != null && name.Length == 0) || (address != null && address.Length == 0))
            {
                return "'name' / 'address' is empty.";
            }

            if (name == null && address == null)
            {
                if (string.IsNullOrEmpty(a.Comment))
                {
                    return "give 'name' or 'address' for a watch row, or 'comment' alone for a comment row.";
                }

                if (a.DisplayFormat != null || a.MonitorTrigger != null || a.ModifyTrigger != null || a.ModifyValue != null)
                {
                    return "a comment row takes nothing but 'comment'.";
                }

                row = new Row { Kind = "Comment", Comment = a.Comment };

                return null;
            }

            if (name != null && a.DisplayFormat != null)
            {
                return "a row given by name takes its display format from the data type of the tag; 'displayFormat' goes with 'address'.";
            }

            var bad = BadEnum("displayFormat", a.DisplayFormat, WatchTableEntryAction.DisplayFormats)
                      ?? BadEnum("monitorTrigger", a.MonitorTrigger, WatchTableEntryAction.Triggers)
                      ?? BadEnum("modifyTrigger", a.ModifyTrigger, WatchTableEntryAction.Triggers);

            if (bad != null)
            {
                return bad;
            }

            if (name != null && symbolExists != null && !symbolExists(Root(name)))
            {
                return $"'{Root(name)}' is neither a tag nor a data block of the PLC (TIA Portal would drop the row without an error). 'plc_get_tags' and 'plc_get_blocks' list them.";
            }

            row = new Row
            {
                Kind = "Watch",
                Name = name,
                Address = address,
                DisplayFormat = Canonical(a.DisplayFormat, WatchTableEntryAction.DisplayFormats),
                MonitorTrigger = Canonical(a.MonitorTrigger, WatchTableEntryAction.Triggers),
                ModifyTrigger = Canonical(a.ModifyTrigger, WatchTableEntryAction.Triggers),
                ModifyValue = a.ModifyValue,
                Comment = a.Comment
            };

            return null;
        }

        private static string? BadEnum(string field, string? value, string[] allowed) =>
            value == null || Canonical(value, allowed) != null ? null : $"'{value}' is not a value of {field}. Use one of: {string.Join(", ", allowed)}.";

        private static string? Canonical(string? value, string[] allowed) =>
            value == null ? null : allowed.FirstOrDefault(v => string.Equals(v, value.Trim(), StringComparison.OrdinalIgnoreCase));

        private static XElement NewRow(WatchTableEntryAction a, IList<string> languages, ref int next)
        {
            var isWatch = a.Name != null || a.Address != null;
            var element = new XElement(isWatch ? RowElement : CommentRowElement, new XAttribute("ID", (next++).ToString()), new XAttribute("CompositionName", "Entries"));

            if (isWatch)
            {
                var attributes = new XElement("AttributeList");

                void Add(string field, string? value)
                {
                    if (value != null)
                    {
                        attributes.Add(new XElement(field, value));
                    }
                }

                Add("Name", a.Name?.Trim());
                Add("Address", a.Address?.Trim());
                Add("DisplayFormat", Canonical(a.DisplayFormat, WatchTableEntryAction.DisplayFormats));
                Add("MonitorTrigger", Canonical(a.MonitorTrigger, WatchTableEntryAction.Triggers));
                Add("ModifyTrigger", Canonical(a.ModifyTrigger, WatchTableEntryAction.Triggers));
                Add("ModifyValue", a.ModifyValue);
                element.Add(attributes);
            }

            if (!string.IsNullOrEmpty(a.Comment))
            {
                var items = new XElement("ObjectList");
                var text = new XElement("MultilingualText", new XAttribute("ID", (next++).ToString()), new XAttribute("CompositionName", "Comment"), items);

                foreach (var language in languages)
                {
                    items.Add(new XElement("MultilingualTextItem", new XAttribute("ID", (next++).ToString()), new XAttribute("CompositionName", "Items"),
                        new XElement("AttributeList", new XElement("Culture", language), new XElement("Text", a.Comment))));
                }

                element.Add(new XElement("ObjectList", text));
            }

            return element;
        }

        private static XElement TableOf(XDocument document) =>
            document.Root?.Element(TableElement) ?? throw new PortalException(PortalErrorCode.InvalidState, "The export of the watch table has no table element; the format of the export changed.");

        private static bool IsRow(XElement e) => e.Name.LocalName == RowElement || e.Name.LocalName == CommentRowElement;

        private static IEnumerable<XElement> RowElements(XDocument document) => TableOf(document).Element("ObjectList")?.Elements().Where(IsRow) ?? Enumerable.Empty<XElement>();

        private static Row ToRow(XElement e)
        {
            string? Attribute(string name) => e.Element("AttributeList")?.Element(name)?.Value;

            return new Row
            {
                Kind = e.Name.LocalName == RowElement ? "Watch" : "Comment",
                Name = Attribute("Name"),
                Address = Attribute("Address"),
                DisplayFormat = Attribute("DisplayFormat"),
                MonitorTrigger = Attribute("MonitorTrigger"),
                ModifyTrigger = Attribute("ModifyTrigger"),
                ModifyValue = Attribute("ModifyValue"),
                Comment = e.Descendants("Text").Select(t => t.Value).FirstOrDefault(t => t.Length > 0)
            };
        }
    }
}
