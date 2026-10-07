using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One network of a block, as the SIMATIC SD document has it.</summary>
    public sealed class LadNetwork
    {
        /// <summary>The number the network had in the block that was read; 0 for a network that is new.</summary>
        public int Original { get; set; }

        /// <summary>The attributes before NETWORK: S7_Language, S7_NetworkTitle, S7_NetworkComment and whatever else is there.</summary>
        public SortedDictionary<string, string> Attributes { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal);

        /// <summary>The lines between NETWORK and END_NETWORK, as they are.</summary>
        public string Code { get; set; } = string.Empty;

        public string Language => Attributes.TryGetValue("S7_Language", out var language) ? language : string.Empty;

        public string? TitleId => Attributes.TryGetValue("S7_NetworkTitle", out var id) ? id : null;

        public string? CommentId => Attributes.TryGetValue("S7_NetworkComment", out var id) ? id : null;
    }

    /// <summary>One text of the resource file (.s7res): an id and the text per language.</summary>
    public sealed class LadText
    {
        public string Id { get; set; } = string.Empty;

        /// <summary>The lines below "- id:", as TIA Portal wrote them. Kept and written back untouched.</summary>
        public List<string> Raw { get; } = new List<string>();

        /// <summary>The same texts decoded, by culture name.</summary>
        public Dictionary<string, string> ByCulture { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The SIMATIC SD document of a block (.s7dcl and .s7res) taken apart into the declaration, the networks and the
    /// texts, and put together again. No Openness here.
    ///
    /// Found on TIA Portal V21 (2026-10-07):
    ///   - A network is "{ attributes } NETWORK ... END_NETWORK"; a LAD network holds RUNG ... END_RUNG lines.
    ///   - Titles and comments are not in the document: it holds an id (S7_NetworkTitle := "MLC_3sB") and the resource
    ///     file the texts per language. A plain text in place of the id is refused, and so is an id without a file.
    ///   - TIA Portal itself writes resource files it cannot read back: networks that were copied share one id, and the
    ///     file then has that id several times with different texts ("The resource file contains corrupted data").
    ///     The n-th use of an id in the document belongs to its n-th entry in the file; with the repeats renamed the
    ///     import passes and the texts come back the same.
    ///   - TIA Portal writes a local tag whose name has other than ASCII letters without quotes - Contact( #Пуск ) -
    ///     and refuses that on the import ("no viable alternative at input '#?'"); it takes #"Пуск". The LAD networks
    ///     are therefore read and written with such names quoted.
    /// </summary>
    public sealed class LadDocument
    {
        private static readonly Regex Attribute = new Regex(@"(S7_\w+)\s*:=\s*""([^""]*)""", RegexOptions.Compiled);
        private static readonly Regex TextReference = new Regex(@"(:=\s*"")(MLC_\w+)("")", RegexOptions.Compiled);
        private static readonly Regex EntryId = new Regex(@"^(\s*-\s+id:\s*)(\S+)\s*$", RegexOptions.Compiled);
        private static readonly Regex LocalReference = new Regex(@"#(?:""[^""\r\n]*""|[\p{L}\p{N}_]+)(?:\.(?:""[^""\r\n]*""|%?[\p{L}\p{N}_]+))*", RegexOptions.Compiled);
        private static readonly Regex Segment = new Regex(@"(?<=[#.])[\p{L}\p{N}_]+", RegexOptions.Compiled);
        private static readonly Regex CultureLine = new Regex(@"^ {4}([A-Za-z]{2,3}(?:-[A-Za-z0-9]+)*):(?: (.*))?$", RegexOptions.Compiled);

        /// <summary>Everything before the first network: the block attributes, the declaration line, the interface.</summary>
        public string Head { get; set; } = string.Empty;

        /// <summary>Everything after the last network: END_FUNCTION_BLOCK and the like.</summary>
        public string Tail { get; set; } = string.Empty;

        public List<LadNetwork> Networks { get; } = new List<LadNetwork>();

        public List<LadText> Texts { get; } = new List<LadText>();

        private int _newIds;

        #region reading

        public static LadDocument Parse(string declaration, string? resources)
        {
            var document = new LadDocument();
            var text = (declaration ?? string.Empty).TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n');
            var resourceText = (resources ?? string.Empty).TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n');

            MakeIdsUnique(ref text, ref resourceText);

            document.ReadResources(resourceText);

            var lines = text.Split('\n');
            var position = 0;
            var headEnd = -1;
            var lastEnd = -1;

            while (position < lines.Length)
            {
                if (lines[position].Trim() != "NETWORK")
                {
                    position++;
                    continue;
                }

                var attributesFrom = AttributesStart(lines, position, lastEnd + 1);

                if (headEnd < 0)
                {
                    headEnd = attributesFrom;
                }

                var end = position + 1;

                while (end < lines.Length && lines[end].Trim() != "END_NETWORK")
                {
                    end++;
                }

                if (end >= lines.Length)
                {
                    throw new FormatException($"The document has a NETWORK at line {position + 1} without END_NETWORK.");
                }

                var network = new LadNetwork { Original = document.Networks.Count + 1 };

                foreach (Match match in Attribute.Matches(string.Join("\n", lines.Skip(attributesFrom).Take(position - attributesFrom))))
                {
                    network.Attributes[match.Groups[1].Value] = match.Groups[2].Value;
                }

                network.Code = string.Join("\n", lines.Skip(position + 1).Take(end - position - 1));

                if (network.Language == "LAD")
                {
                    network.Code = QuoteLocalNames(network.Code);
                }
                document.Networks.Add(network);

                lastEnd = end;
                position = end + 1;
            }

            if (headEnd < 0)
            {
                throw new FormatException("The document has no NETWORK.");
            }

            document.Head = string.Join("\n", lines.Take(headEnd)).TrimEnd();
            document.Tail = string.Join("\n", lines.Skip(lastEnd + 1)).Trim();

            return document;
        }

        /// <summary>The line the attribute block before a NETWORK starts at; the NETWORK line itself when there is none.</summary>
        private static int AttributesStart(string[] lines, int network, int notBefore)
        {
            var line = network - 1;

            while (line >= notBefore && lines[line].Trim().Length == 0)
            {
                line--;
            }

            if (line < notBefore)
            {
                return network;
            }

            var trimmed = lines[line].Trim();

            if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
            {
                return line;
            }

            if (trimmed != "}")
            {
                return network;
            }

            while (line >= notBefore && lines[line].Trim() != "{")
            {
                line--;
            }

            return line < notBefore ? network : line;
        }

        /// <summary>Renames the repeats of an id, in the document and in the resources alike, when both have it equally often.</summary>
        private static void MakeIdsUnique(ref string declaration, ref string resources)
        {
            var entries = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var line in resources.Split('\n'))
            {
                var match = EntryId.Match(line);

                if (match.Success)
                {
                    entries[match.Groups[2].Value] = entries.TryGetValue(match.Groups[2].Value, out var count) ? count + 1 : 1;
                }
            }

            var uses = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (Match match in TextReference.Matches(declaration))
            {
                uses[match.Groups[2].Value] = uses.TryGetValue(match.Groups[2].Value, out var count) ? count + 1 : 1;
            }

            var repeated = new HashSet<string>(entries.Where(e => e.Value > 1 && uses.TryGetValue(e.Key, out var used) && used == e.Value).Select(e => e.Key), StringComparer.Ordinal);

            if (repeated.Count == 0)
            {
                return;
            }

            var taken = new HashSet<string>(entries.Keys.Concat(uses.Keys), StringComparer.Ordinal);
            var names = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var id in repeated)
            {
                var list = new List<string> { id };

                for (var n = 2; n <= entries[id]; n++)
                {
                    var name = id + "x" + n;

                    while (!taken.Add(name))
                    {
                        name += "x";
                    }

                    list.Add(name);
                }

                names[id] = list;
            }

            var seen = new Dictionary<string, int>(StringComparer.Ordinal);

            declaration = TextReference.Replace(declaration, match =>
            {
                var id = match.Groups[2].Value;

                if (!repeated.Contains(id))
                {
                    return match.Value;
                }

                seen[id] = seen.TryGetValue(id, out var count) ? count + 1 : 1;

                return match.Groups[1].Value + names[id][seen[id] - 1] + match.Groups[3].Value;
            });

            seen.Clear();

            resources = string.Join("\n", resources.Split('\n').Select(line =>
            {
                var match = EntryId.Match(line);

                if (!match.Success || !repeated.Contains(match.Groups[2].Value))
                {
                    return line;
                }

                var id = match.Groups[2].Value;

                seen[id] = seen.TryGetValue(id, out var count) ? count + 1 : 1;

                return match.Groups[1].Value + names[id][seen[id] - 1];
            }));
        }

        private void ReadResources(string resources)
        {
            LadText? entry = null;

            foreach (var line in resources.Split('\n'))
            {
                var match = EntryId.Match(line);

                if (match.Success)
                {
                    entry = new LadText { Id = match.Groups[2].Value };
                    Texts.Add(entry);
                }
                else if (entry != null)
                {
                    entry.Raw.Add(line);
                }
            }

            foreach (var text in Texts)
            {
                while (text.Raw.Count > 0 && text.Raw[text.Raw.Count - 1].Trim().Length == 0 && !IsKeptBlank(text.Raw))
                {
                    text.Raw.RemoveAt(text.Raw.Count - 1);
                }

                Decode(text);
            }
        }

        /// <summary>The blank lines at the end of a "|+" text belong to it.</summary>
        private static bool IsKeptBlank(List<string> raw)
        {
            for (var i = raw.Count - 1; i >= 0; i--)
            {
                var match = CultureLine.Match(raw[i]);

                if (match.Success)
                {
                    return Regex.IsMatch(match.Groups[2].Value, @"^[|>]\d*\+\d*\s*$");
                }
            }

            return false;
        }

        private static void Decode(LadText text)
        {
            string? culture = null;
            var first = string.Empty;
            var rest = new List<string>();

            void Close()
            {
                if (culture != null)
                {
                    text.ByCulture[culture] = Scalar(first, rest);
                }
            }

            foreach (var line in text.Raw)
            {
                var match = CultureLine.Match(line);

                if (match.Success)
                {
                    Close();
                    culture = match.Groups[1].Value;
                    first = match.Groups[2].Value;
                    rest = new List<string>();
                }
                else
                {
                    rest.Add(line);
                }
            }

            Close();
        }

        /// <summary>A YAML scalar as TIA Portal writes it: plain, quoted, or a block ("|-", "|+", "|2-").</summary>
        private static string Scalar(string first, List<string> rest)
        {
            var head = first.Trim();
            var block = Regex.Match(head, @"^([|>])(\d*)([+-]?)(\d*)$");

            if (block.Success)
            {
                var digits = block.Groups[2].Value + block.Groups[4].Value;
                var filled = rest.Where(l => l.Trim().Length > 0).ToList();
                var indent = digits.Length > 0
                    ? 4 + int.Parse(digits)
                    : filled.Count > 0 ? filled.Min(l => l.Length - l.TrimStart(' ').Length) : 0;
                var body = string.Join(block.Groups[1].Value == ">" ? " " : "\n", rest.Select(l => l.Length >= indent ? l.Substring(indent) : l.TrimStart(' ')));

                return block.Groups[3].Value == "+" ? body : body.TrimEnd('\n', ' ');
            }

            var folded = new StringBuilder(head);

            foreach (var line in rest)
            {
                folded.Append(line.Trim().Length == 0 ? "\n" : " " + line.Trim());
            }

            var value = folded.ToString().Trim();

            if (value.Length >= 2 && value[0] == '\'' && value[value.Length - 1] == '\'')
            {
                return value.Substring(1, value.Length - 2).Replace("''", "'");
            }

            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                return Regex.Replace(value.Substring(1, value.Length - 2), @"\\(.)", m => m.Groups[1].Value == "n" ? "\n" : m.Groups[1].Value == "t" ? "\t" : m.Groups[1].Value);
            }

            return value;
        }

        /// <summary>The text behind an id: in the first of the wanted languages that has one, else in any.</summary>
        public string? TextOf(string? id, IEnumerable<string>? cultures = null)
        {
            var entry = id == null ? null : Texts.FirstOrDefault(t => t.Id == id);

            if (entry == null)
            {
                return null;
            }

            foreach (var culture in cultures ?? Enumerable.Empty<string>())
            {
                if (entry.ByCulture.TryGetValue(culture, out var text) && text.Trim().Length > 0)
                {
                    return text.TrimEnd();
                }
            }

            return entry.ByCulture.Values.FirstOrDefault(t => t.Trim().Length > 0)?.TrimEnd();
        }

        /// <summary>The texts behind an id per language, when they are not all the same.</summary>
        public Dictionary<string, string>? TextsOf(string? id)
        {
            var entry = id == null ? null : Texts.FirstOrDefault(t => t.Id == id);

            if (entry == null || entry.ByCulture.Values.Select(v => v.TrimEnd()).Distinct().Count() < 2)
            {
                return null;
            }

            return entry.ByCulture.ToDictionary(p => p.Key, p => p.Value.TrimEnd());
        }

        #endregion

        #region changing

        /// <summary>Sets the title or the comment of a network in every given language; an empty text removes it.</summary>
        public void SetText(LadNetwork network, string attribute, string? text, IEnumerable<string> cultures)
        {
            var value = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd();

            if (value.Length == 0)
            {
                network.Attributes.Remove(attribute);

                return;
            }

            network.Attributes[attribute] = AddText(value, cultures);
        }

        /// <summary>Adds a text in every given language and returns the id the document refers to it by.</summary>
        public string AddText(string text, IEnumerable<string> cultures)
        {
            var value = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd();
            var entry = new LadText { Id = NewId() };

            foreach (var culture in cultures)
            {
                entry.ByCulture[culture] = value;

                if (value.IndexOf('\n') < 0)
                {
                    entry.Raw.Add($"    {culture}: '{value.Replace("'", "''")}'");
                }
                else
                {
                    entry.Raw.Add($"    {culture}: |2-");
                    entry.Raw.AddRange(value.Split('\n').Select(line => "      " + line));
                }
            }

            Texts.Add(entry);

            return entry.Id;
        }

        private string NewId()
        {
            string id;

            do
            {
                id = "MLC_mcp" + (++_newIds);
            }
            while (Texts.Any(t => t.Id == id));

            return id;
        }

        /// <summary>Puts the parts of a local tag reference that TIA Portal does not read unquoted into quotes: #Пуск.Член becomes #"Пуск"."Член".</summary>
        public static string QuoteLocalNames(string code)
        {
            return LocalReference.Replace(code ?? string.Empty, reference =>
                Segment.Replace(reference.Value, part => part.Value.Any(c => c > 127) ? "\"" + part.Value + "\"" : part.Value));
        }

        /// <summary>
        /// The code of a network as a caller may pass it: with or without the NETWORK lines and the attribute block
        /// around it. An empty code becomes the empty rung TIA Portal itself writes.
        /// </summary>
        public static string CleanCode(string? code, bool lad = true)
        {
            if (lad)
            {
                code = QuoteLocalNames(code ?? string.Empty);
            }

            var lines = (code ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
            var open = lines.FindIndex(l => l.Trim() == "NETWORK");

            if (open >= 0)
            {
                var close = lines.FindLastIndex(l => l.Trim() == "END_NETWORK");

                lines = lines.Skip(open + 1).Take((close > open ? close : lines.Count) - open - 1).ToList();
            }

            while (lines.Count > 0 && lines[0].Trim().Length == 0)
            {
                lines.RemoveAt(0);
            }

            while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
            }

            if (lines.Count == 0)
            {
                return "        RUNG wire#powerrail\n        END_RUNG";
            }

            // Indented like the rest of the document; TIA Portal does not need it, a reader does.
            var indent = lines.Where(l => l.Trim().Length > 0).Min(l => l.Length - l.TrimStart(' ', '\t').Length);

            return string.Join("\n", lines.Select(l => l.Trim().Length == 0 ? string.Empty : "        " + l.Substring(indent).TrimEnd()));
        }

        #endregion

        #region comments of interface members

        private static readonly Regex MemberWithComment = new Regex(@"^(\s*)([^/{}]+?;)[ \t]*//[ \t]*(.+?)\s*$", RegexOptions.Compiled);
        private static readonly Regex MemberLine = new Regex(@"^\s*[^/{}\s][^/{}]*:[^/{}]*;\s*$", RegexOptions.Compiled);
        private static readonly Regex CommentPragma = new Regex(@"S7_MLC\s*:=\s*""(MLC_\w+)""\s*;?", RegexOptions.Compiled);

        /// <summary>
        /// A member written as "Start : Bool;   // text" gets the text as its comment. TIA Portal drops a // comment of
        /// the declaration on the import without a word (2026-10-07); the comment of a member is a text of the
        /// resource file, referred to by { S7_MLC := "id" } before the member.
        /// </summary>
        public void AdoptMemberComments(IList<string> cultures)
        {
            var lines = Head.Split('\n').ToList();

            for (var i = 0; i < lines.Count; i++)
            {
                var match = MemberWithComment.Match(lines[i]);

                if (!match.Success || lines[i].IndexOf(':') < 0)
                {
                    continue;
                }

                var indent = match.Groups[1].Value;
                var id = AddText(match.Groups[3].Value, cultures);

                lines[i] = indent + match.Groups[2].Value;

                var before = i - 1;

                while (before >= 0 && lines[before].Trim().Length == 0)
                {
                    before--;
                }

                var previous = before >= 0 ? lines[before].Trim() : string.Empty;

                if (previous.StartsWith("{", StringComparison.Ordinal) && previous.EndsWith("}", StringComparison.Ordinal) && previous.Length > 2)
                {
                    // { S7_X := "..." } of this member: the comment joins it.
                    var inner = CommentPragma.Replace(previous.Substring(1, previous.Length - 2), string.Empty).Trim().TrimEnd(';').Trim();

                    lines[before] = indent + "{ " + (inner.Length > 0 ? inner + "; " : string.Empty) + $"S7_MLC := \"{id}\" }}";
                }
                else if (previous == "}")
                {
                    var open = before;

                    while (open >= 0 && lines[open].Trim() != "{")
                    {
                        open--;
                    }

                    if (open >= 0)
                    {
                        for (var k = open + 1; k < before; k++)
                        {
                            if (CommentPragma.IsMatch(lines[k]))
                            {
                                lines.RemoveAt(k);
                                before--;
                                i--;
                                break;
                            }
                        }

                        if (!lines[before - 1].TrimEnd().EndsWith(";", StringComparison.Ordinal) && lines[before - 1].Trim() != "{")
                        {
                            lines[before - 1] = lines[before - 1].TrimEnd() + ";";
                        }

                        lines.Insert(before, indent + $"    S7_MLC := \"{id}\"");
                        i++;
                    }
                }
                else
                {
                    lines.Insert(i, indent + $"{{ S7_MLC := \"{id}\" }}");
                    i++;
                }
            }

            Head = string.Join("\n", lines);
        }

        /// <summary>The declaration for a reader: the comment of a member as "// text" behind it, not as an id before it.</summary>
        public string HeadWithComments(IEnumerable<string>? cultures)
        {
            var lines = Head.Split('\n').ToList();
            string? pending = null;

            for (var i = 0; i < lines.Count; i++)
            {
                var pragma = CommentPragma.Match(lines[i]);

                if (pragma.Success && lines[i].IndexOf("S7_BlockTitle", StringComparison.Ordinal) < 0)
                {
                    pending = TextOf(pragma.Groups[1].Value, cultures);

                    var rest = CommentPragma.Replace(lines[i], string.Empty);

                    if (Regex.IsMatch(rest, @"^\s*\{\s*\}\s*$") || rest.Trim().Length == 0)
                    {
                        lines.RemoveAt(i);
                        i--;
                    }
                    else
                    {
                        lines[i] = Regex.Replace(rest, @";\s*\}", " }").TrimEnd();
                    }

                    continue;
                }

                if (pending != null && MemberLine.IsMatch(lines[i]))
                {
                    lines[i] = lines[i].TrimEnd() + "   // " + pending.Replace("\n", " ");
                    pending = null;
                }
            }

            return string.Join("\n", lines);
        }

        #endregion

        /// <summary>Renames a tag of the block where the networks use it: #Old and #"Old", as a whole name only.</summary>
        public void RenameLocal(string oldName, string newName)
        {
            var written = Regex.IsMatch(newName, @"^[A-Za-z_][A-Za-z0-9_]*$") ? newName : "\"" + newName + "\"";
            var pattern = new Regex(@"#(?:""" + Regex.Escape(oldName) + @"""|" + Regex.Escape(oldName) + @"(?![\p{L}\p{N}_]))", RegexOptions.IgnoreCase);

            foreach (var network in Networks)
            {
                network.Code = pattern.Replace(network.Code, "#" + written);
            }
        }

        #region a new block

        /// <summary>The sections of an interface a caller may pass: VAR_INPUT ... END_VAR and the like, nothing else.</summary>
        public static string? InterfaceProblem(string? declaration)
        {
            var text = declaration ?? string.Empty;

            foreach (var word in new[] { "NETWORK", "FUNCTION", "FUNCTION_BLOCK", "ORGANIZATION_BLOCK", "END_FUNCTION", "END_FUNCTION_BLOCK", "BEGIN" })
            {
                if (Regex.IsMatch(text, @"(?m)^\s*" + word + @"\b"))
                {
                    return $"'interface' holds only the sections of the block interface (VAR_INPUT ... END_VAR, VAR_OUTPUT, VAR_IN_OUT, VAR, VAR_TEMP, VAR CONSTANT); it has a line starting with {word}.";
                }
            }

            var opened = Regex.Matches(text, @"(?m)^\s*VAR(_[A-Z_]+)?\b").Count;
            var closed = Regex.Matches(text, @"(?m)^\s*END_VAR\b").Count;

            return opened != closed ? $"'interface' has {opened} VAR section(s) and {closed} END_VAR." : null;
        }

        /// <summary>A document for a block that does not exist yet.</summary>
        /// <param name="kind">"FB" or "FC".</param>
        public static LadDocument Create(string kind, string name, string? returnType, int number, string? declaration, string? title, IList<string> cultures)
        {
            var document = new LadDocument();
            var attributes = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["S7_Optimized"] = "TRUE",
                ["S7_PreferredLanguage"] = "LAD",
                ["S7_Version"] = "0.1"
            };

            if (number > 0)
            {
                attributes["S7_BlockNumber"] = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                attributes["S7_BlockTitle"] = document.AddText(title!, cultures);
            }

            var head = new StringBuilder("{\n");

            head.Append(string.Join(";\n", attributes.Select(a => $"    {a.Key} := \"{a.Value}\""))).Append("\n}\n");
            head.Append(kind == "FC" ? $"FUNCTION \"{name}\" : {(string.IsNullOrWhiteSpace(returnType) ? "Void" : returnType!.Trim())}" : $"FUNCTION_BLOCK \"{name}\"");

            var lines = (declaration ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Where(l => l.Trim().Length > 0).ToList();

            if (lines.Count > 0)
            {
                var indent = lines.Min(l => l.Length - l.TrimStart(' ', '\t').Length);

                head.Append('\n').Append(string.Join("\n", lines.Select(l => "    " + l.Substring(indent).TrimEnd())));
            }

            document.Head = head.ToString() + "\n";
            document.Tail = kind == "FC" ? "END_FUNCTION" : "END_FUNCTION_BLOCK";
            document.AdoptMemberComments(cultures);

            return document;
        }

        #endregion

        #region writing

        public string RenderDeclaration()
        {
            var text = new StringBuilder();

            text.Append(Head).Append('\n');

            foreach (var network in Networks)
            {
                if (network.Attributes.Count == 1)
                {
                    var only = network.Attributes.First();

                    text.Append($"    {{ {only.Key} := \"{only.Value}\" }}\n");
                }
                else if (network.Attributes.Count > 1)
                {
                    text.Append("    {\n");
                    text.Append(string.Join(";\n", network.Attributes.Select(a => $"        {a.Key} := \"{a.Value}\"")));
                    text.Append("\n    }\n");
                }

                text.Append("    NETWORK\n").Append(network.Code).Append("\n    END_NETWORK\n");
            }

            text.Append(Tail).Append('\n');

            return text.ToString().Replace("\n", "\r\n");
        }

        /// <summary>The resource file for the document as it is now: only the texts it still refers to. Null when there are none.</summary>
        public string? RenderResources()
        {
            var used = new HashSet<string>(TextReference.Matches(RenderDeclaration()).Cast<Match>().Select(m => m.Groups[2].Value), StringComparer.Ordinal);
            var kept = Texts.Where(t => used.Contains(t.Id)).ToList();

            if (kept.Count == 0)
            {
                return null;
            }

            var text = new StringBuilder("MultiLingualTexts:\n");

            foreach (var entry in kept)
            {
                text.Append("  - id: ").Append(entry.Id).Append('\n');

                foreach (var line in entry.Raw)
                {
                    text.Append(line).Append('\n');
                }
            }

            return text.ToString().Replace("\n", "\r\n");
        }

        /// <summary>The lines of a declaration that carry meaning, for comparing what was sent with what TIA Portal made of it.</summary>
        public static List<string> MeaningfulLines(string head)
        {
            return (head ?? string.Empty).Replace("\r\n", "\n").Split('\n')
                .Select(l => Regex.Replace(l.Trim(), @"\s+", " ").TrimEnd(';', ','))
                .Where(l => l.Length > 0 && !l.StartsWith("S7_BlockNumber", StringComparison.Ordinal))
                .ToList();
        }

        /// <summary>The lines of the first declaration the second does not have.</summary>
        public static List<string> LinesMissing(string sent, string result)
        {
            var have = MeaningfulLines(result).GroupBy(l => l, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            var missing = new List<string>();

            foreach (var line in MeaningfulLines(sent))
            {
                if (have.TryGetValue(line, out var count) && count > 0)
                {
                    have[line] = count - 1;
                }
                else
                {
                    missing.Add(line);
                }
            }

            return missing;
        }

        #endregion
    }
}
