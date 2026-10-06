using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>Narrows the text tree of 'get_project_tree' by depth and by a name filter, line by line.</summary>
    internal static class ProjectTreeText
    {
        /// <summary>
        /// The level of a tree line: 0 for the project name, 1 for the first branch, and so on. Each level adds four
        /// characters of prefix ahead of the connector.
        /// </summary>
        internal static int Level(string line)
        {
            var connector = line.IndexOfAny(new[] { '└', '├' });

            return connector < 0 ? 0 : connector / 4 + 1;
        }

        /// <summary>
        /// Keeps the lines down to <paramref name="depth"/> levels below the project (0 = all), and, with a
        /// <paramref name="filter"/>, the lines that match it together with the lines above them. Returns the text and
        /// how many lines were cut.
        /// </summary>
        internal static (string Text, int Total, int Kept) Narrow(string tree, int depth, string? filter)
        {
            var lines = tree.Replace("\r\n", "\n").Split('\n').Where(l => l.Length > 0).ToList();
            var levels = lines.Select(Level).ToList();
            var keep = new bool[lines.Count];

            for (var i = 0; i < lines.Count; i++)
            {
                keep[i] = depth <= 0 || levels[i] <= depth;
            }

            if (!string.IsNullOrWhiteSpace(filter))
            {
                Regex pattern;

                try
                {
                    pattern = new Regex(filter, RegexOptions.IgnoreCase);
                }
                catch (ArgumentException ex)
                {
                    throw new TiaMcpServer.Siemens.PortalException(TiaMcpServer.Siemens.PortalErrorCode.InvalidParams, $"'{filter}' is not a regular expression: {ex.Message}");
                }

                var inDepth = (bool[])keep.Clone();

                keep = new bool[lines.Count];
                keep[0] = lines.Count > 0;

                for (var i = 1; i < lines.Count; i++)
                {
                    var connector = lines[i].IndexOfAny(new[] { '└', '├' });

                    if (!inDepth[i] || !pattern.IsMatch(lines[i].Substring(connector < 0 ? 0 : connector + 4)))
                    {
                        continue;
                    }

                    keep[i] = true;

                    // The lines above: the nearest earlier line of each lower level.
                    var level = levels[i];

                    for (var j = i - 1; j >= 0 && level > 1; j--)
                    {
                        if (levels[j] < level)
                        {
                            keep[j] = true;
                            level = levels[j];
                        }
                    }
                }
            }

            var kept = lines.Where((_, i) => keep[i]).ToList();

            return (string.Join("\n", kept), lines.Count, kept.Count);
        }
    }
}
