using global::ModelContextProtocol.Protocol;
using global::ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// The compact face of the server ('--compact'): instead of one MCP tool per operation the client sees a handful of
    /// group tools, each taking the name of an operation and its arguments, and 'tia_help' that gives the parameters of
    /// an operation. Why: a client that lets the user decide per tool (always allow / ask / block) shows one row per
    /// tool, and 150 rows are no choice at all; fifteen groups cut along what a user wants to decide - reading, changing,
    /// deleting, downloading, protection - are. It also keeps 150 parameter lists out of the context of the model.
    /// The operations themselves are the same tools, called through the same code.
    /// </summary>
    public static class ToolGroups
    {
        public const string Help = "tia_help";

        public sealed class Group
        {
            public Group(string name, string title, bool readOnly, string summary)
            {
                Name = name;
                Title = title;
                ReadOnly = readOnly;
                Summary = summary;
            }

            public string Name { get; }

            public string Title { get; }

            public bool ReadOnly { get; }

            public string Summary { get; }
        }

        /// <summary>The groups, in the order a client lists them. With 'tia_help' they are fifteen; a test keeps it so.</summary>
        public static readonly IReadOnlyList<Group> All = new[]
        {
            new Group("project_read", "Project: connect and read", true, "Connection to TIA Portal, state and diagnostics, the open project and its tree, libraries, export to files"),
            new Group("project_write", "Project: open, save, close, create, import", false, "Open, create, save and close a project, open a global library, import objects from files, take a master copy"),
            new Group("plc_read", "PLC program: read", true, "Read the PLC software: blocks and their code, types, tags, constants, watch tables, sources, references"),
            new Group("plc_write", "PLC program: create and change", false, "Create and change blocks, code, types, tags, constants, tables and sources; compile"),
            new Group("plc_delete", "PLC program: delete", false, "Delete blocks, types, tags, constants, tables, sources and their groups"),
            new Group("hw_read", "Hardware and network: read", true, "Read devices, modules, their parameters, the catalog, subnets and connections"),
            new Group("hw_write", "Hardware and network: create and change", false, "Create devices, plug modules, set parameters of hardware, connect to subnets and IO systems, create connections"),
            new Group("hw_delete", "Hardware and network: delete", false, "Delete devices, subnets and connections; take an interface off its subnet"),
            new Group("hmi_read", "WinCC Unified: read", true, "Read screens, screen items, tags, alarms, logs, scripts, lists, connections and runtime settings of a Unified device"),
            new Group("hmi_write", "WinCC Unified: create, change and delete", false, "Create, change and delete screens, screen items, tags, alarms, logs, scripts, lists and connections; compile"),
            new Group("plc_download", "Download to the PLC", false, "Load the project into a PLC"),
            new Group("security_read", "Protection and users: read", true, "Read how PLCs are protected and the users, groups, roles and password policy of the project"),
            new Group("security_protection", "Protection: passwords of PLCs and blocks", false, "Protection of the PLC configuration data, access level and its passwords, display password, know-how and write protection of blocks"),
            new Group("security_users", "Protection: users, roles, password policy", false, "Users of the project, of the web server and of the OPC UA server, user groups, roles and their rights, password policy")
        };

        private static readonly HashSet<string> ProjectWrite = new HashSet<string>(StringComparer.Ordinal)
        {
            "open_tia_project", "open_project", "create_project", "save_project", "save_as_project", "close_project",
            "open_global_library", "import_objects", "instantiate_master_copy"
        };

        /// <summary>The group of a tool, from its name and from whether it changes the project.</summary>
        public static string GroupOf(string toolName, bool writes)
        {
            bool Has(string part) => toolName.IndexOf(part, StringComparison.Ordinal) >= 0;

            if (toolName.StartsWith("sec_", StringComparison.Ordinal))
            {
                return !writes ? "security_read" : Has("user") || Has("role") || Has("password_policy") ? "security_users" : "security_protection";
            }

            if (toolName.StartsWith("plc_", StringComparison.Ordinal))
            {
                return !writes ? "plc_read" : Has("_delete_") ? "plc_delete" : "plc_write";
            }

            if (toolName.StartsWith("hw_", StringComparison.Ordinal) || toolName.StartsWith("net_", StringComparison.Ordinal))
            {
                return !writes ? "hw_read" : Has("_delete_") || Has("_disconnect_") ? "hw_delete" : "hw_write";
            }

            if (toolName.StartsWith("unified_", StringComparison.Ordinal))
            {
                return writes ? "hmi_write" : "hmi_read";
            }

            if (toolName == "download_to_plc")
            {
                return "plc_download";
            }

            return ProjectWrite.Contains(toolName) ? "project_write" : "project_read";
        }

        /// <summary>
        /// Wraps the tools into group tools. <paramref name="tools"/> are the tools the start flags left ('--read-only',
        /// '--tools'); a group none of them belongs to is not made.
        /// </summary>
        public static List<McpServerTool> Build(IReadOnlyList<(McpServerTool Tool, bool Writes)> tools)
        {
            var byGroup = tools.GroupBy(t => GroupOf(t.Tool.ProtocolTool.Name, t.Writes)).ToDictionary(g => g.Key, g => g.Select(t => t.Tool).ToList());
            var groupOfTool = tools.ToDictionary(t => t.Tool.ProtocolTool.Name, t => GroupOf(t.Tool.ProtocolTool.Name, t.Writes), StringComparer.Ordinal);
            var result = new List<McpServerTool> { new HelpTool(tools.Select(t => t.Tool).ToList(), groupOfTool) };

            foreach (var group in All)
            {
                if (byGroup.TryGetValue(group.Name, out var members))
                {
                    result.Add(new GroupTool(group, members, groupOfTool));
                }
            }

            return result;
        }

        /// <summary>The operation a call of a group tool means; null for a call of anything else. For the gate.</summary>
        internal static string? InnerToolName(CallToolRequestParams? call)
        {
            if (call?.Arguments != null && All.Any(g => g.Name == call.Name) && call.Arguments.TryGetValue("tool", out var tool) && tool.ValueKind == JsonValueKind.String)
            {
                return tool.GetString();
            }

            return null;
        }

        private static string TitleOf(Tool tool)
        {
            var title = tool.Title ?? tool.Annotations?.Title;

            if (!string.IsNullOrWhiteSpace(title))
            {
                return title!;
            }

            var text = tool.Description ?? string.Empty;
            var end = text.IndexOf(". ", StringComparison.Ordinal);

            return end > 0 ? text.Substring(0, end) : text;
        }

        private static CallToolResult Error(string text) =>
            new CallToolResult { IsError = true, Content = new List<ContentBlock> { new TextContentBlock { Text = text } } };

        private sealed class GroupTool : McpServerTool
        {
            private readonly Group _group;
            private readonly Dictionary<string, McpServerTool> _members;
            private readonly Dictionary<string, string> _groupOfTool;

            public GroupTool(Group group, List<McpServerTool> members, Dictionary<string, string> groupOfTool)
            {
                _group = group;
                _members = members.ToDictionary(m => m.ProtocolTool.Name, StringComparer.Ordinal);
                _groupOfTool = groupOfTool;

                var description = new StringBuilder();

                description.Append(group.Summary).Append(". Give 'tool' (one of the names below) and 'arguments' (the parameters of that tool). ")
                    .Append("Before the first call of a tool get its parameters with '").Append(Help).Append("'. Tools:");

                foreach (var member in members)
                {
                    description.Append("\n- ").Append(member.ProtocolTool.Name).Append(": ").Append(TitleOf(member.ProtocolTool));
                }

                var schema = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["tool"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(members.Select(m => (JsonNode)m.ProtocolTool.Name).ToArray()), ["description"] = "Name of the tool to call" },
                        ["arguments"] = new JsonObject { ["type"] = "object", ["description"] = $"The parameters of that tool, by name, as '{Help}' lists them", ["additionalProperties"] = true }
                    },
                    ["required"] = new JsonArray("tool")
                };

                ProtocolTool = new Tool
                {
                    Name = group.Name,
                    Title = group.Title,
                    Description = description.ToString(),
                    InputSchema = JsonSerializer.SerializeToElement(schema),
                    Annotations = new ToolAnnotations
                    {
                        Title = group.Title,
                        ReadOnlyHint = group.ReadOnly,
                        DestructiveHint = !group.ReadOnly,
                        IdempotentHint = group.ReadOnly,
                        OpenWorldHint = false
                    }
                };
            }

            public override Tool ProtocolTool { get; }

            public override IReadOnlyList<object> Metadata { get; } = Array.Empty<object>();

            public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
            {
                var call = request.Params;
                var name = call?.Arguments != null && call.Arguments.TryGetValue("tool", out var tool) && tool.ValueKind == JsonValueKind.String ? tool.GetString() : null;

                if (string.IsNullOrWhiteSpace(name))
                {
                    return Error($"'tool' is missing. '{_group.Name}' takes the name of a tool and its arguments: {{\"tool\": \"<name>\", \"arguments\": {{...}}}}. Tools: {string.Join(", ", _members.Keys)}.");
                }

                if (!_members.TryGetValue(name!.Trim(), out var member))
                {
                    return Error(_groupOfTool.TryGetValue(name.Trim(), out var other)
                        ? $"'{name}' is a tool of '{other}', not of '{_group.Name}': call '{other}' with it."
                        : $"'{_group.Name}' has no tool '{name}'. Tools: {string.Join(", ", _members.Keys)}. If it is a tool of the server at all, it was left out by a start flag ('--read-only', '--tools').");
                }

                var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

                if (call!.Arguments!.TryGetValue("arguments", out var given))
                {
                    // some models send the object as a string of JSON
                    if (given.ValueKind == JsonValueKind.String)
                    {
                        try
                        {
                            given = JsonDocument.Parse(given.GetString() ?? "{}").RootElement.Clone();
                        }
                        catch (JsonException)
                        {
                            return Error($"'arguments' has to be an object with the parameters of '{name}', e.g. {{\"tool\": \"{name}\", \"arguments\": {{...}}}}.");
                        }
                    }

                    if (given.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in given.EnumerateObject())
                        {
                            arguments[property.Name] = property.Value.Clone();
                        }
                    }
                    else if (given.ValueKind != JsonValueKind.Null && given.ValueKind != JsonValueKind.Undefined)
                    {
                        return Error($"'arguments' has to be an object with the parameters of '{name}', e.g. {{\"tool\": \"{name}\", \"arguments\": {{...}}}}.");
                    }
                }

                var stray = call.Arguments.Keys.Where(k => k != "tool" && k != "arguments").ToList();

                if (stray.Count > 0)
                {
                    return Error($"Unknown field(s) {string.Join(", ", stray)}: the parameters of '{name}' go inside 'arguments', e.g. {{\"tool\": \"{name}\", \"arguments\": {{\"{stray[0]}\": ...}}}}.");
                }

                request.Params = new CallToolRequestParams { Name = member.ProtocolTool.Name, Arguments = arguments, Meta = call.Meta };
                request.MatchedPrimitive = member;

                return await member.InvokeAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }

        private sealed class HelpTool : McpServerTool
        {
            private readonly Dictionary<string, McpServerTool> _tools;
            private readonly Dictionary<string, string> _groupOfTool;

            public HelpTool(List<McpServerTool> tools, Dictionary<string, string> groupOfTool)
            {
                _tools = tools.ToDictionary(t => t.ProtocolTool.Name, StringComparer.Ordinal);
                _groupOfTool = groupOfTool;

                var schema = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["tools"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" }, ["description"] = "Names of the tools, e.g. [\"plc_get_blocks\", \"plc_replace_source\"]" }
                    },
                    ["required"] = new JsonArray("tools")
                };

                ProtocolTool = new Tool
                {
                    Name = Help,
                    Title = "Help: parameters of the tools",
                    Description = "The full description and the parameters of tools of this server, several at once. The tools are called through the group tools (project_read, plc_read, plc_write, ...): " +
                                  "each lists its tools by name and takes {\"tool\": \"<name>\", \"arguments\": {...}}. Read the help of a tool before its first call. Reads nothing from TIA Portal",
                    InputSchema = JsonSerializer.SerializeToElement(schema),
                    Annotations = new ToolAnnotations { Title = "Help: parameters of the tools", ReadOnlyHint = true, DestructiveHint = false, IdempotentHint = true, OpenWorldHint = false }
                };
            }

            public override Tool ProtocolTool { get; }

            public override IReadOnlyList<object> Metadata { get; } = Array.Empty<object>();

            public override ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
            {
                var names = new List<string>();

                if (request.Params?.Arguments != null && request.Params.Arguments.TryGetValue("tools", out var given))
                {
                    if (given.ValueKind == JsonValueKind.Array)
                    {
                        names.AddRange(given.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!.Trim()));
                    }
                    else if (given.ValueKind == JsonValueKind.String)
                    {
                        names.AddRange(given.GetString()!.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));
                    }
                }

                if (names.Count == 0)
                {
                    return new ValueTask<CallToolResult>(Error("'tools' is missing: give the names of the tools, e.g. {\"tools\": [\"plc_get_blocks\"]}. The group tools list the names."));
                }

                var result = new JsonArray();
                var unknown = new List<string>();

                foreach (var name in names.Distinct())
                {
                    if (!_tools.TryGetValue(name, out var tool))
                    {
                        unknown.Add(name);
                        continue;
                    }

                    result.Add(new JsonObject
                    {
                        ["tool"] = name,
                        ["callWith"] = _groupOfTool[name],
                        ["description"] = tool.ProtocolTool.Description,
                        ["arguments"] = JsonNode.Parse(tool.ProtocolTool.InputSchema.GetRawText())
                    });
                }

                var answer = new JsonObject { ["tools"] = result };

                if (unknown.Count > 0)
                {
                    answer["unknown"] = new JsonArray(unknown.Select(n => (JsonNode)n).ToArray());
                    answer["note"] = "The unknown names are no tools of this server as it was started; the group tools list the names.";
                }

                return new ValueTask<CallToolResult>(new CallToolResult
                {
                    IsError = result.Count == 0,
                    Content = new List<ContentBlock> { new TextContentBlock { Text = answer.ToJsonString() } }
                });
            }
        }
    }
}
