using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One object an external source text declares.</summary>
    public sealed class SourceDeclaration
    {
        /// <summary>"FB", "FC", "OB", "DB" or "TYPE".</summary>
        public string Kind { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Reads which objects an external source text (SCL, DB, UDT) declares, without TIA Portal. TIA Portal generates
    /// whatever a source declares: a text for another block than the one meant would replace or create that other
    /// block. The tool that replaces the code of one object therefore reads the declarations first.
    /// </summary>
    public static class SourceDeclarations
    {
        private static readonly Regex Comments = new Regex(@"\(\*.*?\*\)|//[^\r\n]*", RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex Header = new Regex(
            @"^[ \t]*(FUNCTION_BLOCK|FUNCTION|ORGANIZATION_BLOCK|DATA_BLOCK|TYPE)[ \t]+(?:""([^""\r\n]+)""|([A-Za-z_][A-Za-z0-9_]*))",
            RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static List<SourceDeclaration> Read(string? source)
        {
            var declarations = new List<SourceDeclaration>();
            var text = Comments.Replace(source ?? string.Empty, string.Empty);

            foreach (Match match in Header.Matches(text))
            {
                declarations.Add(new SourceDeclaration
                {
                    Kind = KindOf(match.Groups[1].Value),
                    Name = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value
                });
            }

            return declarations;
        }

        private static string KindOf(string keyword)
        {
            switch (keyword.ToUpperInvariant())
            {
                case "FUNCTION_BLOCK": return "FB";
                case "FUNCTION": return "FC";
                case "ORGANIZATION_BLOCK": return "OB";
                case "DATA_BLOCK": return "DB";
                default: return "TYPE";
            }
        }

        /// <summary>The kind of a block as the source keyword names it, from its Openness class name.</summary>
        public static string KindOfBlockClass(string className)
        {
            switch (className)
            {
                case "FB": return "FB";
                case "FC": return "FC";
                case "OB": return "OB";
                default: return className.EndsWith("DB", StringComparison.Ordinal) ? "DB" : className;
            }
        }
    }
}
