using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One change to the networks of a LAD block.</summary>
    public class LadNetworkAction
    {
        [Description("'replace' (new code for a network), 'insert' (a new network), 'delete', 'move' or 'set_title' (title and comment only)")]
        public string? Action { get; set; }

        [Description("Number of the network, 1-based, AS THE BLOCK IS BEFORE THIS CALL ('plc_get_lad_networks' gives it). Not used by 'insert'")]
        public int? Network { get; set; }

        [Description("insert, move: the network goes after this one, numbered as the block is before this call; 0 puts it first. insert without it appends")]
        public int? After { get; set; }

        [Description("replace, insert: the code of the network - the RUNG ... END_RUNG lines as 'plc_get_lad_networks' shows them; for an SCL network inside the LAD block, SCL statements")]
        public string? Code { get; set; }

        [Description("insert: 'LAD' (default) or 'SCL' for an SCL network inside the LAD block. A replace keeps the language the network has")]
        public string? Language { get; set; }

        [Description("Title of the network as plain text, set for every language of the project; an empty string removes it; leave it out to keep the present one")]
        public string? Title { get; set; }

        [Description("Comment of the network as plain text, several lines allowed; an empty string removes it; leave it out to keep the present one")]
        public string? Comment { get; set; }

        /// <summary>Fields the caller sent that the action does not have. They are refused, not dropped.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Unknown { get; set; }
    }

    /// <summary>
    /// Applies a batch of network actions to a document. Every number in a batch means the block as it was read, so that
    /// an action cannot shift the target of the next one. Pure logic, checked as a whole before anything is changed.
    /// </summary>
    public static class LadNetworkActions
    {
        public const string Fields = "action, network, after, code, language, title, comment";

        /// <summary>The languages a network of a LAD block is written in here: LAD itself and SCL, which TIA Portal allows inside it.</summary>
        private static string? NetworkLanguage(string? language)
        {
            var name = (language ?? string.Empty).Trim().ToUpperInvariant();

            return name == "LAD" || name == "SCL" ? name : null;
        }

        public static string? Verb(LadNetworkAction action)
        {
            var verb = (action.Action ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "_");

            return verb == "replace" || verb == "insert" || verb == "delete" || verb == "move" || verb == "set_title" ? verb : null;
        }

        /// <returns>One message for every action that cannot be applied; empty when the batch is sound.</returns>
        public static List<string> Check(LadDocument document, IList<LadNetworkAction>? actions)
        {
            var problems = new List<string>();

            if (actions == null || actions.Count == 0)
            {
                problems.Add("No actions given. Example: [{\"action\": \"replace\", \"network\": 2, \"code\": \"RUNG wire#powerrail\\n    Contact( #Start )\\n    Coil( #Run )\\nEND_RUNG\"}].");

                return problems;
            }

            var count = document.Networks.Count;
            var changed = new Dictionary<int, string>();
            var moved = new HashSet<int>();

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
                    problems.Add($"{at}: action '{action.Action}' is not known. Use replace, insert, delete, move or set_title.");
                    continue;
                }

                at = $"{at} ({verb})";

                if (verb != "insert")
                {
                    if (action.Network == null || action.Network < 1 || action.Network > count)
                    {
                        problems.Add($"{at}: 'network' has to be a number from 1 to {count} (the block has {count} network(s)); got {(action.Network?.ToString() ?? "nothing")}.");
                        continue;
                    }
                }
                else if (action.Network != null)
                {
                    problems.Add($"{at}: an insert takes 'after', not 'network'.");
                    continue;
                }

                var number = action.Network ?? 0;
                var language = verb == "insert" ? (string.IsNullOrWhiteSpace(action.Language) ? "LAD" : action.Language!) : document.Networks[number - 1].Language;

                if (verb != "insert" && action.Language != null && !string.Equals(action.Language.Trim(), language, StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"{at}: network {number} is written in {language}; 'language' cannot change that. Delete it and insert a new one.");
                    continue;
                }

                if ((verb == "insert" || verb == "move") && action.After != null && (action.After < 0 || action.After > count))
                {
                    problems.Add($"{at}: 'after' has to be a number from 0 to {count}; got {action.After}.");
                    continue;
                }

                if (verb == "move")
                {
                    if (action.After == null)
                    {
                        problems.Add($"{at}: 'after' is missing - the network it goes after, 0 for the first place.");
                    }
                    else if (action.After == number)
                    {
                        problems.Add($"{at}: network {number} cannot go after itself.");
                    }
                    else if (!moved.Add(number))
                    {
                        problems.Add($"{at}: network {number} is moved twice in this call.");
                    }
                    else if (changed.TryGetValue(number, out var other) && other == "delete")
                    {
                        problems.Add($"{at}: network {number} is deleted in this call.");
                    }

                    continue;
                }

                if (verb == "replace" || verb == "insert")
                {
                    if (string.IsNullOrWhiteSpace(action.Code))
                    {
                        problems.Add($"{at}: 'code' is missing - the RUNG ... END_RUNG lines of the network.");
                        continue;
                    }

                    if (NetworkLanguage(language) == null)
                    {
                        problems.Add(verb == "replace"
                            ? $"{at}: network {number} is written in {language}; only LAD and SCL networks are written here."
                            : $"{at}: 'language' takes LAD or SCL; got '{language}'.");
                        continue;
                    }

                    var hasRung = System.Text.RegularExpressions.Regex.IsMatch(action.Code!, @"(?m)^\s*RUNG\b");

                    if (NetworkLanguage(language) == "SCL" && hasRung)
                    {
                        problems.Add($"{at}: the code is LAD (RUNG ...), but the network is an SCL network{(verb == "insert" ? string.Empty : $" - network {number} of this block is written in SCL")}.");
                        continue;
                    }

                    if (NetworkLanguage(language) == "LAD" && !hasRung)
                    {
                        problems.Add($"{at}: the code has no RUNG. A LAD network is made of 'RUNG wire#powerrail ... END_RUNG' blocks; take a network of 'plc_get_lad_networks' as the pattern.");
                        continue;
                    }
                }

                if (verb == "set_title" && action.Title == null && action.Comment == null)
                {
                    problems.Add($"{at}: neither 'title' nor 'comment' is given.");
                    continue;
                }

                if (verb == "insert")
                {
                    continue;
                }

                if (changed.TryGetValue(number, out var earlier))
                {
                    problems.Add($"{at}: network {number} already has a '{earlier}' in this call. Numbers mean the block as it is before the call; put the whole change of a network into one action.");
                }
                else if (verb == "delete" && moved.Contains(number))
                {
                    problems.Add($"{at}: network {number} is moved in this call.");
                }
                else
                {
                    changed[number] = verb;
                }
            }

            if (problems.Count == 0 && changed.Count(c => c.Value == "delete") >= count && !actions.Any(a => Verb(a) == "insert"))
            {
                problems.Add("The call deletes every network; a block keeps at least one.");
            }

            return problems;
        }

        /// <summary>Applies a batch that <see cref="Check"/> found sound.</summary>
        /// <param name="cultures">The languages a new title or comment is written in.</param>
        public static void Apply(LadDocument document, IList<LadNetworkAction> actions, IList<string> cultures)
        {
            var original = document.Networks.ToList();
            var order = document.Networks;
            var lastAfter = new Dictionary<int, LadNetwork>();
            var deleted = new List<LadNetwork>();

            void Place(LadNetwork network, int after)
            {
                order.Remove(network);

                var anchor = lastAfter.TryGetValue(after, out var last) ? last : after == 0 ? null : original[after - 1];
                var index = anchor == null ? 0 : order.IndexOf(anchor) + 1;

                order.Insert(index, network);
                lastAfter[after] = network;
            }

            void Texts(LadNetwork network, LadNetworkAction action)
            {
                if (action.Title != null)
                {
                    document.SetText(network, "S7_NetworkTitle", action.Title, cultures);
                }

                if (action.Comment != null)
                {
                    document.SetText(network, "S7_NetworkComment", action.Comment, cultures);
                }
            }

            foreach (var action in actions)
            {
                var target = action.Network != null ? original[action.Network.Value - 1] : null;

                switch (Verb(action))
                {
                    case "replace":
                        target!.Code = LadDocument.CleanCode(action.Code, target.Language == "LAD");
                        Texts(target, action);
                        break;

                    case "set_title":
                        Texts(target!, action);
                        break;

                    case "delete":
                        deleted.Add(target!);
                        break;

                    case "move":
                        Place(target!, action.After!.Value);
                        break;

                    case "insert":
                        var added = new LadNetwork();

                        added.Attributes["S7_Language"] = NetworkLanguage(action.Language) ?? "LAD";
                        added.Code = LadDocument.CleanCode(action.Code, added.Language == "LAD");
                        Texts(added, action);
                        if (action.After == null)
                        {
                            // Without a place: the end of the block, wherever its last network went.
                            order.Add(added);
                        }
                        else
                        {
                            Place(added, action.After.Value);
                        }
                        break;
                }
            }

            foreach (var network in deleted)
            {
                order.Remove(network);
            }
        }
    }

    /// <summary>One network of a block that is being created.</summary>
    public class LadNewNetwork
    {
        [Description("The code of the network: RUNG wire#powerrail ... END_RUNG with one instruction per line; SCL statements when language is SCL")]
        public string? Code { get; set; }

        [Description("Title of the network as plain text, written for every language of the project")]
        public string? Title { get; set; }

        [Description("Comment of the network as plain text, several lines allowed")]
        public string? Comment { get; set; }

        [Description("'LAD' (default) or 'SCL' for an SCL network inside the LAD block")]
        public string? Language { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Unknown { get; set; }
    }

    /// <summary>One network in an answer.</summary>
    public class LadNetworkInfo
    {
        public int Number { get; set; }

        /// <summary>LAD, or the language of a network of another kind inside the block (SCL).</summary>
        public string? Language { get; set; }

        public string? Title { get; set; }

        public string? Comment { get; set; }

        /// <summary>The title per language; only when the languages differ.</summary>
        public Dictionary<string, string>? Titles { get; set; }

        public string? Code { get; set; }
    }

    /// <summary>What Portal.GetLadNetworks read.</summary>
    public class LadNetworksResult
    {
        public string? Name { get; set; }

        public string? Path { get; set; }

        public string? Kind { get; set; }

        public int? Number { get; set; }

        public int Count { get; set; }

        /// <summary>The declaration of the block - attributes and interface - when it was asked for.</summary>
        public string? Declaration { get; set; }

        public List<LadNetworkInfo> Networks { get; set; } = new List<LadNetworkInfo>();

        public bool Truncated { get; set; }

        public List<string> Notes { get; set; } = new List<string>();
    }

    /// <summary>What a change of a LAD block did: the result of the compile and the networks as they are now.</summary>
    public class LadEditResult : SourceEditResult
    {
        /// <summary>The declaration of the block after a change of its interface.</summary>
        public string? Declaration { get; set; }

        public List<LadNetworkInfo> Networks { get; set; } = new List<LadNetworkInfo>();
    }

    public class ResponseLadNetworks : ResponseMessage
    {
        public string? Name { get; set; }

        public string? Path { get; set; }

        public string? Kind { get; set; }

        public int? Number { get; set; }

        public int Count { get; set; }

        public string? Declaration { get; set; }

        public IEnumerable<LadNetworkInfo>? Networks { get; set; }

        public bool Truncated { get; set; }

        public IEnumerable<string>? Notes { get; set; }
    }

    public class ResponseLadEdit : ResponseSourceEdit
    {
        /// <summary>The declaration of the block after a change of its interface.</summary>
        public string? Declaration { get; set; }

        /// <summary>The networks of the block after the call: number and title.</summary>
        public IEnumerable<LadNetworkInfo>? Networks { get; set; }
    }
}
