using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TiaMcpServer.Siemens
{
    // A reader and a scalar writer for the YAML subset that TIA Portal uses when it exports
    // WinCC Unified text lists (*.hmi.yml): nested mappings, sequences of scalars, and scalars
    // that are plain, single-quoted or double-quoted on one line.
    //
    // Callers: Portal.Unified.Lists.cs. Not a general YAML parser - anchors, flow collections,
    // folded texts and sequences of mappings are not handled, and a line that does not fit is
    // reported instead of guessed at. Literal block texts ("|-") are read: TIA Portal writes a
    // text with line breaks that way.

    /// <summary>A node of the parsed document: a mapping, a sequence of scalars, or a scalar.</summary>
    internal sealed class YamlNode
    {
        public string? Scalar { get; set; }

        public List<string>? Sequence { get; set; }

        public List<KeyValuePair<string, YamlNode>>? Mapping { get; set; }

        /// <summary>The child under a key, or null.</summary>
        public YamlNode? this[string key] => Mapping?.FirstOrDefault(p => p.Key == key).Value;

        public IEnumerable<KeyValuePair<string, YamlNode>> Children => Mapping ?? Enumerable.Empty<KeyValuePair<string, YamlNode>>();
    }

    internal static class SimpleYaml
    {
        public static YamlNode Parse(string text)
        {
            var root = new YamlNode { Mapping = new List<KeyValuePair<string, YamlNode>>() };

            // Open mappings with the indent of their keys; the innermost is on top.
            var stack = new Stack<(int Indent, YamlNode Node)>();
            stack.Push((-1, root));

            (int Indent, YamlNode Node)? lastKey = null;
            var lines = text.Replace("\r\n", "\n").Split('\n');

            for (var index = 0; index < lines.Length; index++)
            {
                var raw = lines[index];
                var lineNumber = index + 1;

                var line = raw.TrimEnd();
                var content = line.TrimStart();

                if (content.Length == 0 || content[0] == '#')
                {
                    continue;
                }

                var indent = line.Length - content.Length;

                if (content == "-" || content.StartsWith("- ", StringComparison.Ordinal))
                {
                    // A sequence may sit at the indent of its key or deeper.
                    if (lastKey == null || indent < lastKey.Value.Indent || lastKey.Value.Node.Mapping != null || lastKey.Value.Node.Scalar != null)
                    {
                        throw Unsupported(lineNumber, raw, "a list item without a key before it");
                    }

                    lastKey.Value.Node.Sequence ??= new List<string>();
                    var item = content.Length > 1 ? content.Substring(2).Trim() : string.Empty;

                    lastKey.Value.Node.Sequence.Add(IsBlock(item) ? ReadBlock(lines, ref index, indent, item) : DecodeScalar(item, lineNumber, raw));

                    continue;
                }

                var (key, rest) = SplitKey(content, lineNumber, raw);

                while (stack.Peek().Indent >= indent)
                {
                    stack.Pop();
                }

                var parent = stack.Peek().Node;

                if (parent.Mapping == null)
                {
                    if (parent.Sequence != null || parent.Scalar != null)
                    {
                        throw Unsupported(lineNumber, raw, "a key under a value that is not a mapping");
                    }

                    parent.Mapping = new List<KeyValuePair<string, YamlNode>>();
                }

                var node = new YamlNode();

                if (rest.Length > 0)
                {
                    node.Scalar = IsBlock(rest) ? ReadBlock(lines, ref index, indent, rest) : DecodeScalar(rest, lineNumber, raw);
                }

                parent.Mapping.Add(new KeyValuePair<string, YamlNode>(key, node));

                lastKey = (indent, node);

                if (rest.Length == 0)
                {
                    stack.Push((indent, node));
                }
            }

            return root;
        }

        /// <summary>A literal block text: "|", "|-" or "|+" with the text on the lines below.</summary>
        private static bool IsBlock(string scalar)
        {
            return scalar == "|" || scalar == "|-" || scalar == "|+";
        }

        /// <summary>
        /// Reads the lines of a literal block: every following line that is blank or indented
        /// deeper than the line the block started on. TIA Portal writes a text with line breaks
        /// this way.
        /// </summary>
        private static string ReadBlock(string[] lines, ref int index, int parentIndent, string header)
        {
            var block = new List<string>();
            var blockIndent = -1;

            while (index + 1 < lines.Length)
            {
                var next = lines[index + 1];
                var trimmed = next.TrimStart();
                var indent = next.Length - trimmed.Length;

                if (trimmed.Length > 0 && indent <= parentIndent)
                {
                    break;
                }

                if (trimmed.Length > 0 && blockIndent < 0)
                {
                    blockIndent = indent;
                }

                block.Add(trimmed.Length == 0 ? string.Empty : next.Substring(Math.Min(blockIndent, indent)).TrimEnd('\r'));
                index++;
            }

            // "|+" keeps the blank lines at the end, the other two drop them.
            if (header != "|+")
            {
                while (block.Count > 0 && block[block.Count - 1].Length == 0)
                {
                    block.RemoveAt(block.Count - 1);
                }
            }

            var text = string.Join("\n", block);

            return header == "|-" || block.Count == 0 ? text : text + "\n";
        }

        private static (string Key, string Value) SplitKey(string content, int lineNumber, string raw)
        {
            if (content[0] == '\'' || content[0] == '"')
            {
                var end = ClosingQuote(content, lineNumber, raw);
                var after = content.Substring(end + 1).TrimStart();

                if (after.Length == 0 || after[0] != ':')
                {
                    throw Unsupported(lineNumber, raw, "a quoted text that is not a key");
                }

                return (DecodeScalar(content.Substring(0, end + 1), lineNumber, raw), after.Substring(1).Trim());
            }

            // The key ends at the first ": " or at a trailing ":".
            var index = content.IndexOf(": ", StringComparison.Ordinal);

            if (index < 0 && content.EndsWith(":", StringComparison.Ordinal))
            {
                index = content.Length - 1;
            }

            if (index <= 0)
            {
                throw Unsupported(lineNumber, raw, "a line that is neither 'key: value' nor a list item");
            }

            return (content.Substring(0, index).Trim(), content.Substring(index + 1).Trim());
        }

        private static int ClosingQuote(string text, int lineNumber, string raw)
        {
            var quote = text[0];

            for (var i = 1; i < text.Length; i++)
            {
                if (quote == '"' && text[i] == '\\')
                {
                    i++;
                }
                else if (text[i] == quote)
                {
                    if (quote == '\'' && i + 1 < text.Length && text[i + 1] == '\'')
                    {
                        i++;
                    }
                    else
                    {
                        return i;
                    }
                }
            }

            throw Unsupported(lineNumber, raw, "a quoted text that does not end on its line");
        }

        private static string DecodeScalar(string text, int lineNumber, string raw)
        {
            if (text.Length == 0)
            {
                return string.Empty;
            }

            if (text[0] == '|' || text[0] == '>' || text[0] == '[' || text[0] == '{' || text[0] == '&' || text[0] == '*')
            {
                throw Unsupported(lineNumber, raw, "a block text, flow collection or anchor");
            }

            if (text[0] == '\'')
            {
                var end = ClosingQuote(text, lineNumber, raw);

                return text.Substring(1, end - 1).Replace("''", "'");
            }

            if (text[0] != '"')
            {
                // Plain: a " #" starts a comment.
                var comment = text.IndexOf(" #", StringComparison.Ordinal);

                return comment >= 0 ? text.Substring(0, comment).TrimEnd() : text;
            }

            var close = ClosingQuote(text, lineNumber, raw);
            var result = new StringBuilder();

            for (var i = 1; i < close; i++)
            {
                if (text[i] != '\\')
                {
                    result.Append(text[i]);

                    continue;
                }

                i++;

                switch (text[i])
                {
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case '0': result.Append('\0'); break;
                    case 'x':
                        result.Append((char)int.Parse(text.Substring(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 2;
                        break;
                    case 'u':
                        result.Append((char)int.Parse(text.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: result.Append(text[i]); break;
                }
            }

            return result.ToString();
        }

        /// <summary>A scalar as double-quoted YAML, safe for any text.</summary>
        public static string Quote(string? text)
        {
            var result = new StringBuilder("\"");

            foreach (var c in text ?? string.Empty)
            {
                switch (c)
                {
                    case '\\': result.Append("\\\\"); break;
                    case '"': result.Append("\\\""); break;
                    case '\n': result.Append("\\n"); break;
                    case '\r': break;
                    case '\t': result.Append("\\t"); break;
                    default: result.Append(c); break;
                }
            }

            return result.Append('"').ToString();
        }

        private static PortalException Unsupported(int lineNumber, string line, string what)
        {
            return new PortalException(PortalErrorCode.NotSupported,
                $"The exported list file has {what} in line {lineNumber}: '{line.Trim()}'. The server reads only the plain form TIA Portal normally writes.");
        }
    }
}
