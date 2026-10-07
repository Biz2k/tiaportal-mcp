using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One change to the interface of a LAD block.</summary>
    public class LadInterfaceAction
    {
        [Description("'add', 'update' (data type, start value, comment, new name) or 'delete'")]
        public string? Action { get; set; }

        [Description("Name of the tag in the interface of the block")]
        public string? Name { get; set; }

        [Description("add: the section the tag goes to - 'input', 'output', 'inout', 'static' (FB only), 'temp' or 'constant'")]
        public string? Section { get; set; }

        [Description("Data type, e.g. Bool, Int, Real, Time, TON_TIME, \"UDT_Motor\", Array[0..7] of Bool. Needed for add")]
        public string? DataType { get; set; }

        [Description("Start value as it is written in the declaration, e.g. 5, TRUE, T#2s, 16#FF; an empty string removes it")]
        public string? StartValue { get; set; }

        [Description("Comment of the tag as plain text, written for every language of the project; an empty string removes it")]
        public string? Comment { get; set; }

        [Description("update: new name of the tag; its uses inside the block (#Name) are renamed with it, callers of the block are not")]
        public string? NewName { get; set; }

        /// <summary>Fields the caller sent that the action does not have. They are refused, not dropped.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Unknown { get; set; }
    }

    /// <summary>
    /// Changes the interface in the declaration of a SIMATIC SD document, tag by tag, so that a caller does not have to
    /// send the whole document for one new input. Works on the text: sections VAR_INPUT ... END_VAR, one member per
    /// "Name : Type := start;" line, the attributes of a member in a { } block before it. Members of a structure are
    /// not reached - only the tags of the block itself. Pure logic.
    /// </summary>
    public static class LadInterface
    {
        public const string Fields = "action, name, section, dataType, startValue, comment, newName";

        private static readonly Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["input"] = "VAR_INPUT", ["output"] = "VAR_OUTPUT", ["inout"] = "VAR_IN_OUT", ["in_out"] = "VAR_IN_OUT",
            ["static"] = "VAR", ["temp"] = "VAR_TEMP", ["constant"] = "VAR CONSTANT"
        };

        /// <summary>The order TIA Portal writes the sections in.</summary>
        private static readonly string[] Order = { "VAR_INPUT", "VAR_OUTPUT", "VAR_IN_OUT", "VAR", "VAR_TEMP", "VAR CONSTANT" };

        private static readonly Regex SectionStart = new Regex(@"^\s*(VAR(?:_INPUT|_OUTPUT|_IN_OUT|_TEMP)?)\b\s*(.*?)\s*$", RegexOptions.Compiled);
        private static readonly Regex Declaration = new Regex(@"^(\s*)(""[^""]+""|[\p{L}_][\p{L}\p{N}_]*)\s*:\s*(.+)$", RegexOptions.Compiled);
        private static readonly Regex ValidName = new Regex(@"^[\p{L}_][\p{L}\p{N}_ ]*$", RegexOptions.Compiled);

        private sealed class Member
        {
            public string Name = string.Empty;
            public string Section = string.Empty;
            public int First;        // the first line of its { } block, or the declaration
            public int Line;         // the line "Name : Type"
            public int Last;         // the line that ends with ';'
        }

        private sealed class Layout
        {
            public List<string> Lines = new List<string>();
            public List<Member> Members = new List<Member>();
            public Dictionary<string, int> SectionEnd = new Dictionary<string, int>(StringComparer.Ordinal);   // header -> line of END_VAR
            public List<KeyValuePair<string, int>> Sections = new List<KeyValuePair<string, int>>();          // header, line of the header
        }

        private static string Bare(string name) => (name ?? string.Empty).Trim().Trim('"');

        private static Layout Read(string head)
        {
            var layout = new Layout { Lines = head.Split('\n').ToList() };
            string? section = null;
            var pragmaFrom = -1;

            for (var i = 0; i < layout.Lines.Count; i++)
            {
                var text = layout.Lines[i];
                var trimmed = text.Trim();

                if (section == null)
                {
                    var start = SectionStart.Match(text);

                    if (start.Success && trimmed.IndexOf(':') < 0)
                    {
                        var qualifier = start.Groups[2].Value;

                        section = start.Groups[1].Value + (qualifier.Equals("CONSTANT", StringComparison.OrdinalIgnoreCase) ? " CONSTANT" : string.Empty);
                        layout.Sections.Add(new KeyValuePair<string, int>(section, i));
                        pragmaFrom = -1;
                    }

                    continue;
                }

                if (trimmed == "END_VAR")
                {
                    if (!layout.SectionEnd.ContainsKey(section))
                    {
                        layout.SectionEnd[section] = i;
                    }

                    section = null;
                    continue;
                }

                if (trimmed.Length == 0)
                {
                    continue;
                }

                if (trimmed.StartsWith("{", StringComparison.Ordinal))
                {
                    pragmaFrom = i;

                    while (i < layout.Lines.Count && !layout.Lines[i].TrimEnd().EndsWith("}", StringComparison.Ordinal))
                    {
                        i++;
                    }

                    continue;
                }

                var declaration = Declaration.Match(text);

                if (!declaration.Success)
                {
                    pragmaFrom = -1;
                    continue;
                }

                var member = new Member { Name = Bare(declaration.Groups[2].Value), Section = section, First = pragmaFrom >= 0 ? pragmaFrom : i, Line = i };

                // A structure or a start value in brackets runs over several lines, up to the ';' at the outer level.
                var depth = 0;
                var last = i;

                for (; last < layout.Lines.Count; last++)
                {
                    var line = Regex.Replace(layout.Lines[last], @"//.*$", string.Empty);

                    depth += Regex.Matches(line, @"\bSTRUCT\b|\(", RegexOptions.IgnoreCase).Count;
                    depth -= Regex.Matches(line, @"\bEND_STRUCT\b|\)", RegexOptions.IgnoreCase).Count;

                    if (depth <= 0 && line.TrimEnd().EndsWith(";", StringComparison.Ordinal))
                    {
                        break;
                    }
                }

                member.Last = Math.Min(last, layout.Lines.Count - 1);
                layout.Members.Add(member);
                i = member.Last;
                pragmaFrom = -1;
            }

            return layout;
        }

        public static string? Verb(LadInterfaceAction action)
        {
            var verb = (action.Action ?? string.Empty).Trim().ToLowerInvariant();

            return verb == "add" || verb == "update" || verb == "delete" ? verb : null;
        }

        /// <returns>One message for every action that cannot be applied; empty when the batch is sound.</returns>
        public static List<string> Check(LadDocument document, IList<LadInterfaceAction>? actions)
        {
            var problems = new List<string>();

            if (actions == null || actions.Count == 0)
            {
                problems.Add("No actions given. Example: [{\"action\": \"add\", \"section\": \"input\", \"name\": \"Enable\", \"dataType\": \"Bool\", \"comment\": \"...\"}].");

                return problems;
            }

            var layout = Read(document.Head);
            var names = new Dictionary<string, Member>(StringComparer.OrdinalIgnoreCase);

            foreach (var member in layout.Members)
            {
                names[member.Name] = member;
            }

            var isFunction = Regex.IsMatch(document.Head, @"(?m)^\s*FUNCTION\s+""");

            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                var at = $"Action {i + 1}";

                if (action == null)
                {
                    problems.Add($"{at}: is empty.");
                    continue;
                }

                if (action.Unknown != null && action.Unknown.Count > 0)
                {
                    problems.Add($"{at}: unknown field(s) {string.Join(", ", action.Unknown.Keys.Select(k => $"'{k}'"))}. The fields are: {Fields}.");
                    continue;
                }

                var verb = Verb(action);

                if (verb == null)
                {
                    problems.Add($"{at}: action '{action.Action}' is not known. Use add, update or delete.");
                    continue;
                }

                var name = Bare(action.Name ?? string.Empty);

                at = $"{at} ({verb} '{name}')";

                if (name.Length == 0)
                {
                    problems.Add($"{at}: 'name' is missing.");
                    continue;
                }

                if (verb == "add")
                {
                    if (!ValidName.IsMatch(name))
                    {
                        problems.Add($"{at}: the name may hold letters, digits, '_' and spaces and starts with a letter or '_'.");
                    }
                    else if (names.ContainsKey(name))
                    {
                        problems.Add($"{at}: the block already has a tag of that name in {names[name].Section}; use update.");
                    }
                    else if (string.IsNullOrWhiteSpace(action.DataType))
                    {
                        problems.Add($"{at}: 'dataType' is missing.");
                    }
                    else if (string.IsNullOrWhiteSpace(action.Section) || !Headers.ContainsKey(action.Section!.Trim()))
                    {
                        problems.Add($"{at}: 'section' takes input, output, inout, static, temp or constant; got '{action.Section}'.");
                    }
                    else if (isFunction && Headers[action.Section!.Trim()] == "VAR")
                    {
                        problems.Add($"{at}: a function (FC) has no static tags; use temp, or inout to keep a value between calls.");
                    }
                    else if (action.NewName != null)
                    {
                        problems.Add($"{at}: 'newName' belongs to update.");
                    }
                    else
                    {
                        names[name] = new Member { Name = name, Section = Headers[action.Section!.Trim()] };
                    }

                    continue;
                }

                if (!names.TryGetValue(name, out var existing))
                {
                    problems.Add($"{at}: the block has no tag of that name. Its tags: {string.Join(", ", names.Keys.Take(40))}{(names.Count > 40 ? " ..." : string.Empty)}.");
                    continue;
                }

                if (verb == "delete")
                {
                    names.Remove(name);
                    continue;
                }

                if (action.Section != null && Headers.TryGetValue(action.Section.Trim(), out var wanted) && !existing.Section.StartsWith(wanted, StringComparison.Ordinal))
                {
                    problems.Add($"{at}: the tag is in {existing.Section}; an update does not move it to another section. Delete it and add it there.");
                    continue;
                }

                if (action.DataType == null && action.StartValue == null && action.Comment == null && action.NewName == null)
                {
                    problems.Add($"{at}: nothing to change - give dataType, startValue, comment or newName.");
                    continue;
                }

                if ((action.DataType != null || action.StartValue != null) && existing.Last > existing.Line)
                {
                    problems.Add($"{at}: the declaration of this tag runs over several lines (a structure or a start value in brackets); its data type and start value are not changed here - pass the whole document to 'plc_replace_source'.");
                    continue;
                }

                if (action.DataType != null && action.DataType.Trim().Length == 0)
                {
                    problems.Add($"{at}: 'dataType' is empty.");
                    continue;
                }

                if (action.NewName != null)
                {
                    var newName = Bare(action.NewName);

                    if (!ValidName.IsMatch(newName))
                    {
                        problems.Add($"{at}: the new name may hold letters, digits, '_' and spaces and starts with a letter or '_'.");
                    }
                    else if (names.ContainsKey(newName) && !string.Equals(newName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        problems.Add($"{at}: the block already has a tag named '{newName}'.");
                    }
                    else
                    {
                        names.Remove(name);
                        names[newName] = new Member { Name = newName, Section = existing.Section, Line = existing.Line, Last = existing.Last };
                    }
                }
            }

            return problems;
        }

        /// <summary>Applies a batch that <see cref="Check"/> found sound.</summary>
        public static void Apply(LadDocument document, IList<LadInterfaceAction> actions, IList<string> cultures)
        {
            foreach (var action in actions)
            {
                var layout = Read(document.Head);
                var name = Bare(action.Name ?? string.Empty);
                var member = layout.Members.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

                switch (Verb(action))
                {
                    case "add":
                        Add(layout, action, name);
                        break;

                    case "delete":
                        layout.Lines.RemoveRange(member!.First, member.Last - member.First + 1);

                        // A section without tags is not written by TIA Portal; left in, it would look like a loss on the import.
                        var header = member.First - 1;

                        while (header >= 0 && layout.Lines[header].Trim().Length == 0)
                        {
                            header--;
                        }

                        var end = member.First;

                        while (end < layout.Lines.Count && layout.Lines[end].Trim().Length == 0)
                        {
                            end++;
                        }

                        if (header >= 0 && end < layout.Lines.Count && SectionStart.IsMatch(layout.Lines[header]) && layout.Lines[header].IndexOf(':') < 0 && layout.Lines[end].Trim() == "END_VAR")
                        {
                            layout.Lines.RemoveRange(header, end - header + 1);
                        }

                        break;

                    case "update":
                        Update(document, layout, member!, action);
                        break;
                }

                document.Head = string.Join("\n", layout.Lines);
            }

            document.AdoptMemberComments(cultures);
        }

        private static string Written(string name) => Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$") ? name : "\"" + name + "\"";

        private static string Line(string indent, string name, string type, string? start, string? comment)
        {
            return indent + name + " : " + type.Trim().TrimEnd(';').Trim()
                   + (string.IsNullOrWhiteSpace(start) ? string.Empty : " := " + start!.Trim().TrimEnd(';').Trim()) + ";"
                   + (string.IsNullOrWhiteSpace(comment) ? string.Empty : "   // " + comment!.Replace("\r", string.Empty).Replace("\n", " ").Trim());
        }

        private static void Add(Layout layout, LadInterfaceAction action, string name)
        {
            var header = Headers[action.Section!.Trim()];
            var line = Line("        ", Written(name), action.DataType!, action.StartValue, action.Comment);

            if (layout.SectionEnd.TryGetValue(header, out var end))
            {
                layout.Lines.Insert(end, line);

                return;
            }

            // A new section, at its place among the others; before the first network when it is the last one.
            var rank = Array.IndexOf(Order, header);
            var before = layout.Sections.Where(s => Array.IndexOf(Order, s.Key.Split(' ')[0] == "VAR" && s.Key != "VAR CONSTANT" ? "VAR" : s.Key) > rank).Select(s => (int?)s.Value).FirstOrDefault();
            int at;

            if (before.HasValue)
            {
                at = before.Value;
            }
            else
            {
                at = layout.Lines.Count;

                while (at > 0 && layout.Lines[at - 1].Trim().Length == 0)
                {
                    at--;
                }
            }

            layout.Lines.InsertRange(at, new[] { "    " + header, line, "    END_VAR" });
        }

        private static void Update(LadDocument document, Layout layout, Member member, LadInterfaceAction action)
        {
            var text = layout.Lines[member.Line];
            var declaration = Declaration.Match(text);
            var indent = declaration.Groups[1].Value;
            var written = declaration.Groups[2].Value;

            if (action.NewName != null)
            {
                var newName = Bare(action.NewName);

                document.RenameLocal(member.Name, newName);
                written = Written(newName);
                layout.Lines[member.Line] = indent + written + text.Substring(declaration.Groups[2].Index + declaration.Groups[2].Length);
                text = layout.Lines[member.Line];
            }

            if (action.DataType != null || action.StartValue != null)
            {
                // One line: "Name : Type := start;   // comment"
                var body = Regex.Replace(Declaration.Match(text).Groups[3].Value, @"\s*//.*$", string.Empty).Trim().TrimEnd(';');
                var cut = body.IndexOf(":=", StringComparison.Ordinal);
                var type = cut >= 0 ? body.Substring(0, cut).Trim() : body.Trim();
                var start = cut >= 0 ? body.Substring(cut + 2).Trim() : null;

                layout.Lines[member.Line] = Line(indent, written, action.DataType ?? type, action.StartValue ?? start, null);
            }

            if (action.Comment == null)
            {
                return;
            }

            // The present comment goes: the id in the { } block of the member, or a // written in this call.
            layout.Lines[member.Last] = Regex.Replace(layout.Lines[member.Last], @"\s*//.*$", string.Empty);

            for (var i = member.Line - 1; i >= member.First && i >= 0; i--)
            {
                var without = Regex.Replace(layout.Lines[i], @"\s*S7_MLC\s*:=\s*""MLC_\w+""\s*;?", string.Empty);

                if (without == layout.Lines[i])
                {
                    continue;
                }

                if (Regex.IsMatch(without, @"^\s*\{\s*\}\s*$") || without.Trim().Length == 0)
                {
                    layout.Lines.RemoveAt(i);
                    member.Line--;
                    member.Last--;
                }
                else
                {
                    layout.Lines[i] = Regex.Replace(without, @";\s*\}", " }");

                    // The entry before the closing bracket of a block of several lines has no ';'.
                    if (i + 1 < layout.Lines.Count && layout.Lines[i + 1].Trim() == "}")
                    {
                        layout.Lines[i] = layout.Lines[i].TrimEnd().TrimEnd(';');
                    }
                }
            }

            for (var i = member.First; i < member.Line; i++)
            {
                if (i + 1 < layout.Lines.Count && layout.Lines[i + 1].Trim() == "}")
                {
                    layout.Lines[i] = layout.Lines[i].TrimEnd().TrimEnd(';');
                }
            }

            if (action.Comment.Trim().Length > 0)
            {
                layout.Lines[member.Last] = layout.Lines[member.Last].TrimEnd() + "   // " + action.Comment.Replace("\r", string.Empty).Replace("\n", " ").Trim();
            }
        }

        /// <summary>The tags of the interface, for an answer: name, section, the declaration line.</summary>
        public static List<string> Describe(LadDocument document)
        {
            var layout = Read(document.Head);

            return layout.Members.Select(m => $"{m.Section}: {layout.Lines[m.Line].Trim()}").ToList();
        }
    }
}
