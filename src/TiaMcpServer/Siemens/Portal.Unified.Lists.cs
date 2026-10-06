using Siemens.Engineering.HmiUnified;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Siemens
{
    // WinCC Unified: text lists and graphic lists.
    //
    // Callers: the tools unified_get_text_lists, unified_get_graphic_lists and
    // unified_manage_lists in McpServer.Unified.cs.
    //
    // Files: Openness has no object model for the entries of a list - a list object has a name
    // and Delete(), nothing else. Entries are reached only through Export and Import of YAML
    // files, so this code writes and reads temporary files:
    //   <temp>\TiaMcpServer\lists-<guid>\<name>.hmi.yml              the lists with their entries
    //   <temp>\TiaMcpServer\lists-<guid>\<name>.TextLibrary.hmi.yml  text lists only: the texts
    // Each operation creates its own folder and removes it when it is done.
    //
    // The entry forms, as TIA Portal V21 exports them (2026-10-06):
    //   single value   Value: 5 / FromValue: 5 / ToValue: 5   - all three keys, all equal;
    //                  for the value 0 the export leaves all three out
    //   range          Type: Range / Value: 4 / FromValue: 4 / ToValue: 7
    //   from           Type: From  / Value: 100 / FromValue: 100
    //   to             Type: To    / Value: 2 / ToValue: 2
    //   default        IsDefaultEntry: True, no value keys (given ones are discarded)
    // A text entry points at its text as "Text: <library>.<key>", a graphic entry names a
    // project graphic as "Graphic: GraphicLibrary.<name>".
    //
    // What Import does:
    //   - Lists not named in the file are left alone; a list that exists is replaced as a whole.
    //   - Entry names and text keys are renumbered; the ones in the file do not survive.
    //   - An entry in a form it does not know is dropped or loses its value WITHOUT an error.
    //     That is why every write is checked by exporting again and comparing; a difference
    //     fails the call and so rolls it back.
    //   - The name of a graphic is not checked: one that does not exist is stored as it is.
    //
    // A list that is a library type is not among the lists of the HMI and cannot be read: the
    // export of a library type version holds no entries.
    public partial class Portal
    {
        private const string TextLibraryName = "MyTextLibrary";

        private const string GraphicLibraryName = "GraphicLibrary";

        #region read

        /// <param name="kind">"text" or "graphic".</param>
        public List<UnifiedListInfo> GetUnifiedLists(string softwarePath, string kind, string listName = "")
        {
            return Operation.Run(_logger, nameof(GetUnifiedLists), PortalErrorCode.InvalidState,
                () =>
                {
                    var lists = ExportLists(RequireUnifiedSoftware(softwarePath), kind == "graphic");

                    if (string.IsNullOrWhiteSpace(listName))
                    {
                        return lists;
                    }

                    var match = lists.Where(l => string.Equals(l.Name, listName.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

                    return match.Count > 0
                        ? match
                        : throw new PortalException(PortalErrorCode.NotFound,
                            $"There is no {kind} list '{listName}'. Existing: {(lists.Count == 0 ? "none" : string.Join(", ", lists.Select(l => l.Name)))}.");
                },
                ("softwarePath", softwarePath), ("kind", kind), ("listName", listName));
        }

        private static List<UnifiedListInfo> ExportLists(HmiSoftware software, bool graphic)
        {
            var result = new List<UnifiedListInfo>();

            if ((graphic ? software.HmiGraphicLists.Count : software.HmiTextLists.Count) == 0)
            {
                return result;
            }

            WithListFolder(folder =>
            {
                if (graphic)
                {
                    software.HmiGraphicLists.Export(folder, "lists");
                }
                else
                {
                    software.HmiTextLists.Export(folder, "lists");
                }

                var listsFile = Path.Combine(folder.FullName, "lists.hmi.yml");
                var textsFile = Path.Combine(folder.FullName, "lists.TextLibrary.hmi.yml");

                if (!File.Exists(listsFile))
                {
                    throw new PortalException(PortalErrorCode.ExportFailed,
                        $"TIA Portal exported the lists without the expected file 'lists.hmi.yml'. It wrote: {string.Join(", ", folder.GetFiles().Select(f => f.Name))}.");
                }

                result.AddRange(ParseLists(File.ReadAllText(listsFile), File.Exists(textsFile) ? File.ReadAllText(textsFile) : string.Empty));
            });

            return result;
        }

        /// <summary>
        /// Reads an exported lists file, text or graphic. For text lists the second file holds
        /// the texts the entries point at.
        /// </summary>
        internal static List<UnifiedListInfo> ParseLists(string listsYaml, string textsYaml)
        {
            // library -> (languages, key -> texts in the order of the languages)
            var libraries = new Dictionary<string, (List<string> Languages, Dictionary<string, List<string>> Entries)>();

            foreach (var library in SimpleYaml.Parse(textsYaml)["TextLibraries"]?.Children ?? Enumerable.Empty<KeyValuePair<string, YamlNode>>())
            {
                var entries = new Dictionary<string, List<string>>();

                foreach (var entry in library.Value["Entries"]?.Children ?? Enumerable.Empty<KeyValuePair<string, YamlNode>>())
                {
                    entries[entry.Key] = entry.Value["Text"]?.Sequence ?? new List<string>();
                }

                libraries[library.Key] = (library.Value["Languages"]?.Sequence ?? new List<string>(), entries);
            }

            var document = SimpleYaml.Parse(listsYaml);
            var graphic = document["GraphicListContainers"] != null;
            var result = new List<UnifiedListInfo>();

            foreach (var container in (document["TextListContainers"] ?? document["GraphicListContainers"])?.Children ?? Enumerable.Empty<KeyValuePair<string, YamlNode>>())
            {
                foreach (var list in container.Value["ResourceLists"]?.Children ?? Enumerable.Empty<KeyValuePair<string, YamlNode>>())
                {
                    var info = new UnifiedListInfo { Name = list.Key, Kind = graphic ? "graphic" : "text" };

                    foreach (var entry in list.Value["Entries"]?.Children ?? Enumerable.Empty<KeyValuePair<string, YamlNode>>())
                    {
                        var item = ReadListEntry(entry.Value);

                        // "<library>.<key>" for a text, "GraphicLibrary.<name>" for a graphic.
                        var reference = entry.Value[graphic ? "Graphic" : "Text"]?.Scalar ?? string.Empty;
                        var dot = reference.IndexOf('.');

                        if (graphic)
                        {
                            item.Graphic = dot >= 0 ? reference.Substring(dot + 1) : reference;
                        }
                        else
                        {
                            item.Texts = new Dictionary<string, string>();

                            if (dot > 0 && libraries.TryGetValue(reference.Substring(0, dot), out var library)
                                && library.Entries.TryGetValue(reference.Substring(dot + 1), out var texts))
                            {
                                for (var i = 0; i < texts.Count && i < library.Languages.Count; i++)
                                {
                                    if (texts[i].Length > 0)
                                    {
                                        item.Texts[library.Languages[i]] = texts[i];
                                    }
                                }
                            }
                        }

                        info.Entries.Add(item);
                    }

                    result.Add(info);
                }
            }

            return result;
        }

        private static UnifiedListEntryInfo ReadListEntry(YamlNode entry)
        {
            long? Number(string key) =>
                long.TryParse(entry[key]?.Scalar, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : (long?)null;

            if (string.Equals(entry["IsDefaultEntry"]?.Scalar, "True", StringComparison.OrdinalIgnoreCase))
            {
                return new UnifiedListEntryInfo { Type = "default" };
            }

            var type = entry["Type"]?.Scalar;

            switch (type)
            {
                case null:
                case "":
                    // No value keys at all means 0: Export omits them for that value.
                    return new UnifiedListEntryInfo { Type = "value", Value = Number("Value") ?? Number("FromValue") ?? 0 };

                case "Range":
                    return new UnifiedListEntryInfo { Type = "range", From = Number("FromValue") ?? Number("Value") ?? 0, To = Number("ToValue") ?? 0 };

                case "From":
                    return new UnifiedListEntryInfo { Type = "from", From = Number("FromValue") ?? Number("Value") ?? 0 };

                case "To":
                    return new UnifiedListEntryInfo { Type = "to", To = Number("ToValue") ?? Number("Value") ?? 0 };

                default:
                    // A form not seen yet: passed on rather than guessed at.
                    return new UnifiedListEntryInfo { Type = type, Value = Number("Value"), From = Number("FromValue"), To = Number("ToValue") };
            }
        }

        #endregion

        #region write

        public List<UnifiedActionResult> ManageUnifiedLists(string softwarePath, IList<UnifiedListAction>? actions)
        {
            return RunUnifiedBatch(nameof(ManageUnifiedLists), softwarePath, actions,
                "{ \"action\": \"upsert\", \"listName\": \"Modes\", \"entries\": [ { \"value\": 0, \"text\": \"Off\" }, { \"value\": 1, \"text\": \"Auto\" }, { \"default\": true, \"text\": \"?\" } ] }",
                a => a.ListName,
                (software, action, verb, result) =>
                {
                    var name = RequireName(action.ListName, "listName");
                    var kind = (action.Kind ?? string.Empty).Trim().ToLowerInvariant();

                    if (kind != string.Empty && kind != "text" && kind != "graphic")
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, $"kind takes 'text' or 'graphic'; got '{action.Kind}'.");
                    }

                    var graphic = kind == "graphic";
                    var what = graphic ? "Graphic list" : "Text list";
                    var exists = graphic ? software.HmiGraphicLists.Find(name) != null : software.HmiTextLists.Find(name) != null;

                    switch (verb)
                    {
                        case "delete":
                            if (!exists)
                            {
                                throw new PortalException(PortalErrorCode.NotFound, $"{what} '{name}' not found.");
                            }

                            if (graphic)
                            {
                                software.HmiGraphicLists.Find(name).Delete();
                            }
                            else
                            {
                                software.HmiTextLists.Find(name).Delete();
                            }

                            return;

                        case "create" when exists:
                            throw new PortalException(PortalErrorCode.InvalidParams, $"{what} '{name}' already exists. Use 'update' or 'upsert'.");

                        case "update" when !exists:
                            throw new PortalException(PortalErrorCode.NotFound,
                                $"{what} '{name}' not found. Use '{(graphic ? "unified_get_graphic_lists" : "unified_get_text_lists")}' to list them, or 'upsert' to create it.");

                        case "create":
                        case "update":
                        case "upsert":
                            break;

                        default:
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                $"Unknown action '{action.Action}'. Use 'create', 'update', 'upsert' or 'delete'.");
                    }

                    var languages = _project!.LanguageSettings.ActiveLanguages.Select(l => l.Culture.Name).ToList();
                    var wanted = BuildListEntries(action.Entries, languages, graphic);

                    WithListFolder(folder =>
                    {
                        var (listsYaml, textsYaml) = WriteList(name, wanted, languages, graphic);

                        File.WriteAllText(Path.Combine(folder.FullName, "import.hmi.yml"), listsYaml, new UTF8Encoding(false));

                        if (textsYaml != null)
                        {
                            File.WriteAllText(Path.Combine(folder.FullName, "import.TextLibrary.hmi.yml"), textsYaml, new UTF8Encoding(false));
                        }

                        if (!(graphic ? software.HmiGraphicLists.Import(folder, "import") : software.HmiTextLists.Import(folder, "import")))
                        {
                            throw new PortalException(PortalErrorCode.ImportFailed, $"TIA Portal did not import {what.ToLowerInvariant()} '{name}' and gave no reason.");
                        }
                    });

                    // Import drops what it does not understand without saying so.
                    var written = ExportLists(software, graphic).FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase))
                        ?? throw new PortalException(PortalErrorCode.ImportFailed, $"{what} '{name}' is not there after the import.");

                    var difference = DescribeListDifference(wanted, written.Entries);

                    if (difference != null)
                    {
                        throw new PortalException(PortalErrorCode.ImportFailed,
                            $"TIA Portal imported {what.ToLowerInvariant()} '{name}' differently from what was asked: {difference}.");
                    }

                    result.Applied.Add($"{wanted.Count} entr{(wanted.Count == 1 ? "y" : "ies")}");
                    result.Notes.Add(exists ? "Replaced: the entries it had before are gone." : "Created.");

                    if (graphic)
                    {
                        result.Notes.Add("TIA Portal does not check the graphic names: one that is not among the project graphics is stored and shows nothing.");
                    }
                });
        }

        /// <summary>Checks the entries of a request and brings them into the form the lists are read in.</summary>
        internal static List<UnifiedListEntryInfo> BuildListEntries(List<UnifiedListEntry>? entries, List<string> languages, bool graphic)
        {
            if (entries == null || entries.Count == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    "entries is required: a list is written as a whole, e.g. [ { \"value\": 0, \"text\": \"Off\" }, { \"value\": 1, \"text\": \"On\" } ].");
            }

            var result = new List<UnifiedListEntryInfo>();

            foreach (var entry in entries)
            {
                var item = new UnifiedListEntryInfo();

                if (entry.Default)
                {
                    if (entry.Value != null || entry.From != null || entry.To != null)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            "The default entry takes no 'value', 'from' or 'to': it stands for every value no other entry covers.");
                    }

                    item.Type = "default";
                }
                else if (entry.Value != null)
                {
                    if (entry.From != null || entry.To != null)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"The entry for value {entry.Value} also has 'from' or 'to'. Give either 'value', or 'from' and/or 'to'.");
                    }

                    item.Type = "value";
                    item.Value = entry.Value;
                }
                else if (entry.From != null && entry.To != null)
                {
                    if (entry.From > entry.To)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, $"The range {entry.From}..{entry.To} is empty: 'from' is above 'to'.");
                    }

                    item.Type = "range";
                    item.From = entry.From;
                    item.To = entry.To;
                }
                else if (entry.From != null)
                {
                    item.Type = "from";
                    item.From = entry.From;
                }
                else if (entry.To != null)
                {
                    item.Type = "to";
                    item.To = entry.To;
                }
                else
                {
                    throw new PortalException(PortalErrorCode.InvalidParams,
                        "Every entry needs 'value', or 'from' and/or 'to', or 'default': true.");
                }

                if (graphic)
                {
                    if (string.IsNullOrWhiteSpace(entry.Graphic))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, $"The {DescribeListEntry(item)} needs 'graphic', the name of a project graphic.");
                    }

                    item.Graphic = entry.Graphic!.Trim();
                }
                else
                {
                    item.Texts = new Dictionary<string, string>();

                    switch (entry.Text.ValueKind)
                    {
                        case JsonValueKind.String:
                            foreach (var language in languages)
                            {
                                item.Texts[language] = entry.Text.GetString() ?? string.Empty;
                            }

                            break;

                        case JsonValueKind.Object:
                            foreach (var text in entry.Text.EnumerateObject())
                            {
                                var language = languages.FirstOrDefault(l => l.Equals(text.Name, StringComparison.OrdinalIgnoreCase))
                                    ?? throw new PortalException(PortalErrorCode.NotFound,
                                        $"The project has no language '{text.Name}'. Available: {string.Join(", ", languages)}.");

                                item.Texts[language] = text.Value.GetString() ?? string.Empty;
                            }

                            break;

                        default:
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                $"The {DescribeListEntry(item)} needs 'text': a string, or {{ \"en-US\": \"...\" }}.");
                    }
                }

                result.Add(item);
            }

            if (result.Count(e => e.Type == "default") > 1)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "A list has one default entry at most.");
            }

            var duplicate = result.GroupBy(ListEntryKey).FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The {DescribeListEntry(duplicate.First())} is given more than once.");
            }

            return result;
        }

        private static string ListEntryKey(UnifiedListEntryInfo entry) => $"{entry.Type}|{entry.Value}|{entry.From}|{entry.To}";

        private static string DescribeListEntry(UnifiedListEntryInfo entry)
        {
            switch (entry.Type)
            {
                case "default": return "default entry";
                case "value": return $"entry for value {entry.Value}";
                case "range": return $"entry for {entry.From}..{entry.To}";
                case "from": return $"entry for {entry.From} and above";
                case "to": return $"entry for {entry.To} and below";
                default: return $"entry of type '{entry.Type}'";
            }
        }

        /// <summary>
        /// The files Import takes for one list, in the form TIA Portal itself exports. The second
        /// one, the texts, is null for a graphic list.
        /// </summary>
        internal static (string Lists, string? Texts) WriteList(string name, List<UnifiedListEntryInfo> entries, List<string> languages, bool graphic)
        {
            var lists = new StringBuilder();
            var texts = graphic ? null : new StringBuilder();
            var prefix = graphic ? "Graphic" : "Text";

            lists.Append("#Version: 2.0\n\n").Append(prefix).Append("ListContainers:\n  Device").Append(prefix).Append("List:\n");
            lists.Append("    ResourceListType: ").Append(prefix).Append("List\n    ResourceLists:\n");
            lists.Append("      ").Append(Regex.IsMatch(name, @"^[A-Za-z0-9_][A-Za-z0-9_\-]*$") ? name : SimpleYaml.Quote(name)).Append(":\n        Entries:\n");

            if (texts != null)
            {
                texts.Append("#Version: 2.0\n\nTextLibraries:\n  ").Append(TextLibraryName).Append(":\n    Type: Text\n    Languages:\n");

                foreach (var language in languages)
                {
                    texts.Append("    - ").Append(language).Append('\n');
                }

                texts.Append("    DefaultLanguage: ").Append(languages.FirstOrDefault() ?? "en-US").Append("\n    Entries:\n");
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                string Number(long? value) => (value ?? 0).ToString(CultureInfo.InvariantCulture);

                void Key(string key, string value) => lists.Append("            ").Append(key).Append(": ").Append(value).Append('\n');

                lists.Append("          ").Append(prefix).Append("_list_entry_").Append(i).Append(":\n");

                switch (entry.Type)
                {
                    case "default":
                        Key("IsDefaultEntry", "True");
                        break;

                    case "range":
                        Key("Type", "Range");
                        Key("Value", Number(entry.From));
                        Key("FromValue", Number(entry.From));
                        Key("ToValue", Number(entry.To));
                        break;

                    case "from":
                        Key("Type", "From");
                        Key("Value", Number(entry.From));
                        Key("FromValue", Number(entry.From));
                        break;

                    case "to":
                        Key("Type", "To");
                        Key("Value", Number(entry.To));
                        Key("ToValue", Number(entry.To));
                        break;

                    default:
                        // All three keys, all equal: the only form of a single value Import keeps.
                        Key("Value", Number(entry.Value));
                        Key("FromValue", Number(entry.Value));
                        Key("ToValue", Number(entry.Value));
                        break;
                }

                if (texts == null)
                {
                    Key("Graphic", SimpleYaml.Quote($"{GraphicLibraryName}.{entry.Graphic}"));

                    continue;
                }

                Key("Text", $"{TextLibraryName}.Text_{i}");

                texts.Append("      Text_").Append(i).Append(":\n        Text:\n");

                foreach (var language in languages)
                {
                    string? text = null;

                    entry.Texts?.TryGetValue(language, out text);

                    texts.Append("        - ").Append(SimpleYaml.Quote(text)).Append('\n');
                }
            }

            return (lists.ToString(), texts?.ToString());
        }

        /// <summary>What differs between the entries asked for and the entries found, or null.</summary>
        internal static string? DescribeListDifference(List<UnifiedListEntryInfo> wanted, List<UnifiedListEntryInfo> found)
        {
            if (wanted.Count != found.Count)
            {
                return $"{wanted.Count} entries were sent, {found.Count} arrived";
            }

            foreach (var entry in wanted)
            {
                var label = DescribeListEntry(entry);
                var match = found.FirstOrDefault(f => ListEntryKey(f) == ListEntryKey(entry));

                if (match == null)
                {
                    return $"the {label} is missing";
                }

                if (entry.Graphic != null && match.Graphic != entry.Graphic)
                {
                    return $"the {label} has the graphic '{match.Graphic}' instead of '{entry.Graphic}'";
                }

                foreach (var text in (entry.Texts ?? new Dictionary<string, string>()).Where(t => t.Value.Length > 0))
                {
                    string? actual = null;

                    if (match.Texts == null || !match.Texts.TryGetValue(text.Key, out actual) || actual != text.Value)
                    {
                        return $"the {label} has the text '{actual}' for {text.Key} instead of '{text.Value}'";
                    }
                }
            }

            return null;
        }

        /// <summary>Runs an action with a temporary folder of its own and removes the folder afterwards.</summary>
        private static void WithListFolder(Action<DirectoryInfo> action)
        {
            var folder = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "TiaMcpServer", "lists-" + Guid.NewGuid().ToString("N")));

            folder.Create();

            try
            {
                action(folder);
            }
            finally
            {
                try
                {
                    folder.Delete(true);
                }
                catch (IOException)
                {
                    // Left for the system to clean up; the next call uses a folder of its own.
                }
            }
        }

        #endregion
    }
}
