using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// The areas a tool belongs to, for the '--tools' start flag: a client that works on one part of a project
    /// does not need the descriptions of all the tools in its context. The area follows from the name of the
    /// tool in one place here, not from an attribute on every method.
    /// </summary>
    public static class ToolSets
    {
        public const string Plc = "plc";
        public const string Hw = "hw";
        public const string Unified = "unified";
        public const string Library = "library";
        public const string Transfer = "transfer";
        public const string Download = "download";

        /// <summary>Protection, passwords and users: the sec_* tools.</summary>
        public const string Security = "security";

        /// <summary>SINAMICS drives through Startdrive: the drive_* tools.</summary>
        public const string Drive = "drive";

        /// <summary>The areas that can be named after '--tools', in the order the documentation lists them.</summary>
        public static readonly IReadOnlyList<string> Areas = new[] { Plc, Hw, Unified, Library, Transfer, Download, Security, Drive };

        /// <summary>Connection, project and diagnostics: registered whatever '--tools' says.</summary>
        private static readonly HashSet<string> Always = new HashSet<string>(StringComparer.Ordinal)
        {
            "connect", "disconnect", "get_state", "get_tia_instances", "doctor",
            "open_tia_project", "open_project", "create_project", "get_project", "save_project", "save_as_project", "archive_project", "retrieve_project", "close_project",
            "get_project_tree", "get_installed_software"
        };

        private static readonly Dictionary<string, string> Named = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["get_libraries"] = Library,
            ["get_library_types"] = Library,
            ["get_master_copies"] = Library,
            ["instantiate_master_copy"] = Library,
            ["open_global_library"] = Library,
            ["export_objects"] = Transfer,
            ["import_objects"] = Transfer,
            ["preview_import"] = Transfer,
            ["download_to_plc"] = Download,
            ["get_download_targets"] = Download,
            ["get_accessible_devices"] = Download
        };

        /// <summary>The areas switched on, or null when every area is (no '--tools', or 'all').</summary>
        public static IReadOnlyList<string>? Enabled { get; set; }

        /// <summary>The area of a tool; null for a tool that is always registered or that no rule covers.</summary>
        public static string? AreaOf(string toolName)
        {
            if (Named.TryGetValue(toolName, out var area))
            {
                return area;
            }

            if (toolName.StartsWith("plc_", StringComparison.Ordinal))
            {
                return Plc;
            }

            if (toolName.StartsWith("hw_", StringComparison.Ordinal) || toolName.StartsWith("net_", StringComparison.Ordinal))
            {
                return Hw;
            }

            if (toolName.StartsWith("drive_", StringComparison.Ordinal))
            {
                return Drive;
            }

            if (toolName.StartsWith("sec_", StringComparison.Ordinal))
            {
                return Security;
            }

            if (toolName.StartsWith("unified_", StringComparison.Ordinal))
            {
                return Unified;
            }

            return null;
        }

        /// <summary>True for a tool that every '--tools' value keeps.</summary>
        public static bool IsAlways(string toolName) => Always.Contains(toolName);

        /// <summary>True when the tool is registered with the given areas (null means all).</summary>
        public static bool IsRegistered(string toolName, IReadOnlyCollection<string>? areas)
        {
            if (areas == null)
            {
                return true;
            }

            var area = AreaOf(toolName);

            // A tool without an area is one of the always-registered ones; Test_2501 keeps a new tool from slipping in unsorted.
            return area == null || areas.Contains(area);
        }

        /// <summary>
        /// Reads the value of '--tools': names separated by commas, 'all' for no limit. Names that are not an area
        /// are returned in <paramref name="unknown"/>.
        /// </summary>
        public static IReadOnlyList<string>? Parse(string? value, out IReadOnlyList<string> unknown)
        {
            var names = (value ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(n => n.Trim().ToLowerInvariant())
                .Where(n => n.Length > 0)
                .Distinct()
                .ToList();

            unknown = names.Where(n => n != "all" && !Areas.Contains(n)).ToList();

            if (names.Count == 0 || names.Contains("all"))
            {
                return null;
            }

            return Areas.Where(names.Contains).ToList();
        }

        public static string Describe(IReadOnlyCollection<string>? areas) =>
            areas == null ? "all" : string.Join(",", areas);

        /// <summary>The text printed to stderr when '--tools' holds something that is not an area.</summary>
        public static string UnknownText(IReadOnlyCollection<string> unknown) =>
            $"Unknown tool area for --tools: {string.Join(", ", unknown)}. Valid areas: {string.Join(", ", Areas)}, all.";
    }
}
