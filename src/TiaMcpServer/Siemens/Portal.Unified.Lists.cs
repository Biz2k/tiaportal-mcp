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
    // unified_manage_text_lists in McpServer.Unified.cs.
    //
    // Files: Openness has no object model for the entries of a list - a list object has a name
    // and Delete(), nothing else. Entries are reached only through Export and Import of YAML
    // files, so this code writes and reads temporary files:
    //   <temp>\TiaMcpServer\lists-<guid>\<name>.hmi.yml              the lists with their entries
    //   <temp>\TiaMcpServer\lists-<guid>\<name>.TextLibrary.hmi.yml  the texts, per language
    // Each operation creates its own folder and removes it when it is done.
    //
    // What Import does, as found on TIA Portal V21 (2026-10-06):
    //   - Lists not named in the file are left alone; a list that exists is replaced as a whole.
    //   - Entry names and text keys are renumbered; the ones in the file do not survive.
    //   - An entry is kept only as a single value with Value, FromValue and ToValue all equal.
    //     Anything else - a range, a lone Value - is dropped or loses its value WITHOUT an
    //     error. That is why every write is checked by exporting again and comparing; a
    //     difference fails the call and so rolls it back.
    //   - Export leaves the three value keys out when the value is 0: an entry without them
    //     is the entry for 0, not a "default" entry. How a default entry is written is unknown.
    //   - The format of value ranges and of graphic lists is not known: the test project had
    //     neither, and there is no way to create one through Openness to look at.
    public partial class Portal
    {
        private const string TextLibraryName = "MyTextLibrary";

        #region read

        public List<UnifiedTextListInfo> GetUnifiedTextLists(string softwarePath, string listName = "")
        {
            return Operation.Run(_logger, nameof(GetUnifiedTextLists), PortalErrorCode.InvalidState,
                () =>
                {
                    var software = RequireUnifiedSoftware(softwarePath);
                    var lists = ExportTextLists(software);

                    if (string.IsNullOrWhiteSpace(listName))
                    {
                        return lists;
                    }

                    var match = lists.Where(l => string.Equals(l.Name, listName.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

                    return match.Count > 0
                        ? match
                        : throw new PortalException(PortalErrorCode.NotFound,
                            $"Text list '{listName}' not found. Existing: {string.Join(", ", lists.Select(l => l.Name))}.");
                },
                ("softwarePath", softwarePath), ("listName", listName));
        }

        public (List<string> Names, Dictionary<string, string> Export) GetUnifiedGraphicLists(string softwarePath)
        {
            return Operation.Run(_logger, nameof(GetUnifiedGraphicLists), PortalErrorCode.InvalidState,
                () =>
                {
                    var software = RequireUnifiedSoftware(softwarePath);
                    var names = software.HmiGraphicLists.Select(l => l.Name).ToList();
                    var export = new Dictionary<string, string>();

                    if (names.Count > 0)
                    {
                        WithListFolder(folder =>
                        {
                            software.HmiGraphicLists.Export(folder, "graphiclists");

                            foreach (var file in folder.GetFiles())
                            {
                                export[file.Name] = File.ReadAllText(file.FullName);
                            }
                        });
                    }

                    return (names, export);
                },
                ("softwarePath", softwarePath));
        }

        private static List<UnifiedTextListInfo> ExportTextLists(HmiSoftware software)
        {
            var result = new List<UnifiedTextListInfo>();

            if (software.HmiTextLists.Count == 0)
            {
                return result;
            }

            WithListFolder(folder =>
            {
                software.HmiTextLists.Export(folder, "textlists");

                var listsFile = Path.Combine(folder.FullName, "textlists.hmi.yml");
                var textsFile = Path.Combine(folder.FullName, "textlists.TextLibrary.hmi.yml");

                if (!File.Exists(listsFile))
                {
                    throw new PortalException(PortalErrorCode.ExportFailed,
                        $"TIA Portal exported the text lists without the expected file 'textlists.hmi.yml'. It wrote: {string.Join(", ", folder.GetFiles().Select(f => f.Name))}.");
                }

                result.AddRange(ParseTextLists(File.ReadAllText(listsFile), File.Exists(textsFile) ? File.ReadAllText(textsFile) : string.Empty));
            });

            return result;
        }

        /// <summary>Joins the two exported files: the lists with their entries, and the texts the entries point at.</summary>
        internal static List<UnifiedTextListInfo> ParseTextLists(string listsYaml, string textsYaml)
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

            var result = new List<UnifiedTextListInfo>();

            foreach (var container in SimpleYaml.Parse(listsYaml)["TextListContainers"]?.Children ?? Enumerable.Empty<KeyValuePair<string, YamlNode>>())
            {
                foreach (var list in container.Value["ResourceLists"]?.Children ?? Enumerable.Empty<KeyValuePair<string, YamlNode>>())
                {
                    var info = new UnifiedTextListInfo { Name = list.Key };

                    foreach (var entry in list.Value["Entries"]?.Children ?? Enumerable.Empty<KeyValuePair<string, YamlNode>>())
                    {
                        var value = entry.Value["Value"]?.Scalar;
                        var from = entry.Value["FromValue"]?.Scalar;
                        var to = entry.Value["ToValue"]?.Scalar;

                        var item = new UnifiedTextListEntryInfo
                        {
                            // No value keys at all means 0: Export omits them for that value.
                            Value = long.TryParse(value ?? from, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0,
                            Range = from != null && to != null && from != to ? $"{from}..{to}" : null
                        };

                        // "Library.Key"
                        var reference = entry.Value["Text"]?.Scalar ?? string.Empty;
                        var dot = reference.IndexOf('.');

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

                        info.Entries.Add(item);
                    }

                    result.Add(info);
                }
            }

            return result;
        }

        #endregion

        #region write

        public List<UnifiedActionResult> ManageUnifiedTextLists(string softwarePath, IList<UnifiedTextListAction>? actions)
        {
            return RunUnifiedBatch(nameof(ManageUnifiedTextLists), softwarePath, actions,
                "{ \"action\": \"upsert\", \"listName\": \"Modes\", \"entries\": [ { \"value\": 0, \"text\": \"Off\" }, { \"value\": 1, \"text\": \"Auto\" } ] }",
                a => a.ListName,
                (software, action, verb, result) =>
                {
                    var name = RequireName(action.ListName, "listName");
                    var kind = (action.Kind ?? string.Empty).Trim().ToLowerInvariant();

                    if (kind != string.Empty && kind != "text" && kind != "graphic")
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, $"kind takes 'text' or 'graphic'; got '{action.Kind}'.");
                    }

                    if (kind == "graphic")
                    {
                        if (verb != "delete")
                        {
                            throw new PortalException(PortalErrorCode.NotSupported,
                                "A graphic list can only be deleted here. Openness reaches its entries through a file format that could not be examined; create it in TIA Portal.");
                        }

                        var graphicList = software.HmiGraphicLists.Find(name)
                            ?? throw new PortalException(PortalErrorCode.NotFound, $"Graphic list '{name}' not found.");

                        graphicList.Delete();

                        return;
                    }

                    var list = software.HmiTextLists.Find(name);

                    switch (verb)
                    {
                        case "delete":
                            if (list == null)
                            {
                                throw new PortalException(PortalErrorCode.NotFound, $"Text list '{name}' not found.");
                            }

                            list.Delete();

                            return;

                        case "create" when list != null:
                            throw new PortalException(PortalErrorCode.InvalidParams, $"Text list '{name}' already exists. Use 'update' or 'upsert'.");

                        case "update" when list == null:
                            throw new PortalException(PortalErrorCode.NotFound,
                                $"Text list '{name}' not found. Use 'unified_get_text_lists' to list them, or 'upsert' to create it.");

                        case "create":
                        case "update":
                        case "upsert":
                            break;

                        default:
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                $"Unknown action '{action.Action}'. Use 'create', 'update', 'upsert' or 'delete'.");
                    }

                    var languages = _project!.LanguageSettings.ActiveLanguages.Select(l => l.Culture.Name).ToList();
                    var wanted = BuildTextListEntries(action.Entries, languages);

                    WithListFolder(folder =>
                    {
                        var (listsYaml, textsYaml) = WriteTextList(name, wanted, languages);

                        File.WriteAllText(Path.Combine(folder.FullName, "import.hmi.yml"), listsYaml, new UTF8Encoding(false));
                        File.WriteAllText(Path.Combine(folder.FullName, "import.TextLibrary.hmi.yml"), textsYaml, new UTF8Encoding(false));

                        if (!software.HmiTextLists.Import(folder, "import"))
                        {
                            throw new PortalException(PortalErrorCode.ImportFailed, $"TIA Portal did not import text list '{name}' and gave no reason.");
                        }
                    });

                    // Import drops what it does not understand without saying so.
                    var written = ExportTextLists(software).FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase))
                        ?? throw new PortalException(PortalErrorCode.ImportFailed, $"Text list '{name}' is not there after the import.");

                    var difference = DescribeTextListDifference(wanted, written.Entries);

                    if (difference != null)
                    {
                        throw new PortalException(PortalErrorCode.ImportFailed,
                            $"TIA Portal imported text list '{name}' differently from what was asked: {difference}.");
                    }

                    result.Applied.Add($"{wanted.Count} entr{(wanted.Count == 1 ? "y" : "ies")}");
                    result.Notes.Add(list == null ? "Created." : "Replaced: the entries it had before are gone.");
                });
        }

        /// <summary>Checks the entries of a request and spreads each text over the languages.</summary>
        internal static List<UnifiedTextListEntryInfo> BuildTextListEntries(List<UnifiedTextListEntry>? entries, List<string> languages)
        {
            if (entries == null || entries.Count == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    "entries is required: a text list is written as a whole, e.g. [ { \"value\": 0, \"text\": \"Off\" }, { \"value\": 1, \"text\": \"On\" } ].");
            }

            if (entries.Any(e => e.Value == null))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "Every entry needs 'value', the number it stands for.");
            }

            var duplicate = entries.GroupBy(e => e.Value).FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Value {duplicate.Key} is given more than once.");
            }

            var result = new List<UnifiedTextListEntryInfo>();

            foreach (var entry in entries)
            {
                var item = new UnifiedTextListEntryInfo { Value = entry.Value!.Value };

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
                            $"The entry for value {entry.Value} needs 'text': a string, or {{ \"en-US\": \"...\" }}.");
                }

                result.Add(item);
            }

            return result;
        }

        /// <summary>The two files Import takes for one list, in the form TIA Portal itself exports.</summary>
        internal static (string Lists, string Texts) WriteTextList(string name, List<UnifiedTextListEntryInfo> entries, List<string> languages)
        {
            var lists = new StringBuilder();
            var texts = new StringBuilder();

            lists.Append("#Version: 2.0\n\nTextListContainers:\n  DeviceTextList:\n    ResourceListType: TextList\n    ResourceLists:\n");
            lists.Append("      ").Append(Regex.IsMatch(name, @"^[A-Za-z0-9_][A-Za-z0-9_\-]*$") ? name : SimpleYaml.Quote(name)).Append(":\n        Entries:\n");

            texts.Append("#Version: 2.0\n\nTextLibraries:\n  ").Append(TextLibraryName).Append(":\n    Type: Text\n    Languages:\n");

            foreach (var language in languages)
            {
                texts.Append("    - ").Append(language).Append('\n');
            }

            texts.Append("    DefaultLanguage: ").Append(languages.FirstOrDefault() ?? "en-US").Append("\n    Entries:\n");

            for (var i = 0; i < entries.Count; i++)
            {
                lists.Append("          Text_list_entry_").Append(i).Append(":\n");

                // All three keys, all equal: the only form of a value Import keeps.
                var value = entries[i].Value.ToString(CultureInfo.InvariantCulture);

                lists.Append("            Value: ").Append(value).Append('\n');
                lists.Append("            FromValue: ").Append(value).Append('\n');
                lists.Append("            ToValue: ").Append(value).Append('\n');

                lists.Append("            Text: ").Append(TextLibraryName).Append(".Text_").Append(i).Append('\n');

                texts.Append("      Text_").Append(i).Append(":\n        Text:\n");

                foreach (var language in languages)
                {
                    texts.Append("        - ").Append(SimpleYaml.Quote(entries[i].Texts.TryGetValue(language, out var text) ? text : string.Empty)).Append('\n');
                }
            }

            return (lists.ToString(), texts.ToString());
        }

        /// <summary>What differs between the entries asked for and the entries found, or null.</summary>
        internal static string? DescribeTextListDifference(List<UnifiedTextListEntryInfo> wanted, List<UnifiedTextListEntryInfo> found)
        {
            if (wanted.Count != found.Count)
            {
                return $"{wanted.Count} entries were sent, {found.Count} arrived";
            }

            foreach (var entry in wanted)
            {
                var label = $"the entry for value {entry.Value}";
                var match = found.FirstOrDefault(f => f.Value == entry.Value && f.Range == null);

                if (match == null)
                {
                    return $"{label} is missing";
                }

                foreach (var text in entry.Texts.Where(t => t.Value.Length > 0))
                {
                    if (!match.Texts.TryGetValue(text.Key, out var actual) || actual != text.Value)
                    {
                        return $"{label} has the text '{actual}' for {text.Key} instead of '{text.Value}'";
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
