using Siemens.Engineering;
using Siemens.Engineering.CrossReference;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.Multiuser;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;
using Siemens.Engineering.Safety;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        // From the former Portal.Resolve.cs:
        // Generic path resolution and traversal shared by the PLC software areas.
        //
        // Callers: the private resolvers in Portal.Blocks.cs and Portal.Types.cs (GetPlcBlockGroupByPath, GetPlcTypeGroupByPath,
        // GetPlcBlockGroupPath, GetPlcTypeGroupPath, GetBlocksRecursive, GetTypesRecursive) and the
        // tag, watch table, external source and cross reference members.
        // Affected API: none - every member here is private to Portal. Reads/writes no data files.
        //
        // PlcSoftware exposes five look-alike group hierarchies (BlockGroup, TypeGroup,
        // TagTableGroup, WatchAndForceTableGroup, ExternalSourceGroup) that share no common base
        // type, so the shape is captured with generics plus selector delegates rather than
        // inheritance.

        #region resolve

        /// <summary>
        /// Resolves a software path to its PlcSoftware, throwing rather than returning null so
        /// callers wrapped in Operation.Run get a decorated PortalException.
        /// </summary>
        private PlcSoftware GetPlcSoftwareOrThrow(string softwarePath)
        {
            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, NoProjectMessage);
            }

            var container = GetSoftwareContainer(softwarePath);

            if (container?.Software is not PlcSoftware plcSoftware)
            {
                throw new PortalException(
                    PortalErrorCode.NotFound,
                    $"No PLC software found at '{softwarePath}'. Use 'GetProjectTree' to discover valid software paths.");
            }

            return plcSoftware;
        }

        /// <summary>
        /// Canonical form of a group or object path: '/'-separated, without empty or '.' segments.
        /// "", ".", "/" and "./" (and null) all mean the top folder of the software structure and
        /// come back as the empty string; "./A//B/" comes back as "A/B". Backslashes are accepted
        /// as separators.
        /// </summary>
        private static string NormalizeGroupPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var segments = path!
                .Replace('\\', '/')
                .Split(['/'], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0 && s != ".");

            return string.Join("/", segments);
        }

        /// <summary>
        /// How a '/' that is part of a name is written inside a path. TIA Portal allows the
        /// slash in group, table and station names ("Inputs/Outputs", "S7-1500/ET200MP
        /// station_1"), and '/' is also the path separator, so a path built from such a name
        /// could not be parsed back. Percent-encoding was chosen because NormalizeGroupPath
        /// already gives a meaning to both '\' (separator) and '//' (empty segment).
        /// </summary>
        private const string EscapedSlash = "%2F";

        /// <summary>One name as a path segment: "Inputs/Outputs" becomes "Inputs%2FOutputs".</summary>
        internal static string EscapeSegment(string? name)
        {
            return (name ?? string.Empty).Replace("/", EscapedSlash);
        }

        /// <summary>The name a path segment stands for; the inverse of <see cref="EscapeSegment"/>.</summary>
        internal static string UnescapeSegment(string? segment)
        {
            return Regex.Replace(segment ?? string.Empty, EscapedSlash, "/", RegexOptions.IgnoreCase);
        }

        /// <summary>The segments of a path, still escaped.</summary>
        internal static string[] PathSegments(string? path)
        {
            return NormalizeGroupPath(path).Split(['/'], StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Walks a '/'-separated group path down from <paramref name="root"/>.
        /// An empty path returns the root itself. Returns null when a segment does not match.
        /// </summary>
        private static T? WalkGroups<T>(
            T root,
            string groupPath,
            Func<T, IEnumerable<T>> children,
            Func<T, string> name)
            where T : class
        {
            var segments = PathSegments(groupPath);

            return segments.Length == 0 ? root : WalkNamed(children(root), segments, 0, children, name);
        }

        /// <summary>
        /// Resolves <paramref name="segments"/> from <paramref name="index"/> on against a set
        /// of named nodes and their descendants.
        ///
        /// A segment is matched as written first. If that leads nowhere, it is joined with the
        /// following segments by '/', which is how a path reads when the caller copied a name
        /// containing a slash without escaping it: "Inputs/Outputs/AI_Handler" still finds the
        /// group "Inputs/Outputs". The as-written reading wins, so a real "Inputs" group with an
        /// "Outputs" subgroup is never shadowed; the escaped form addresses the other one.
        /// </summary>
        internal static T? WalkNamed<T>(
            IEnumerable<T> nodes,
            string[] segments,
            int index,
            Func<T, IEnumerable<T>> children,
            Func<T, string> name)
            where T : class
        {
            var candidates = nodes.Where(n => n != null).ToList();

            for (var take = 1; index + take <= segments.Length; take++)
            {
                var wanted = string.Join("/", segments.Skip(index).Take(take).Select(UnescapeSegment));
                var node = candidates.FirstOrDefault(n => name(n).Equals(wanted, StringComparison.OrdinalIgnoreCase));

                if (node == null)
                {
                    continue;
                }

                if (index + take == segments.Length)
                {
                    return node;
                }

                var deeper = WalkNamed(children(node), segments, index + take, children, name);

                if (deeper != null)
                {
                    return deeper;
                }
            }

            return null;
        }

        /// <summary>
        /// Builds the '/'-separated path of a group by walking Parent upwards.
        /// </summary>
        /// <param name="includeSystemRoot">
        /// True keeps the system group name (e.g. "Program blocks") as the first segment, which
        /// is what the preservePath export layout expects and what Test_415_ImportBlock asserts.
        /// False yields a root-relative path that round-trips back into the ...ByPath resolvers.
        /// </param>
        private static string BuildGroupPath<T>(
            T group,
            Func<T, T?> parent,
            Func<T, string> name,
            Func<T, bool> isSystemRoot,
            bool includeSystemRoot)
            where T : class
        {
            if (group == null)
            {
                return string.Empty;
            }

            var segments = new List<string>();
            T? current = group;

            while (current != null)
            {
                if (isSystemRoot(current))
                {
                    if (includeSystemRoot)
                    {
                        segments.Insert(0, EscapeSegment(name(current)));
                    }

                    break;
                }

                segments.Insert(0, EscapeSegment(name(current)));

                try
                {
                    current = parent(current);
                }
                catch (Exception)
                {
                    // The parent chain leaves the group hierarchy (or Openness refuses the
                    // access); the path collected so far is the best answer available.
                    break;
                }
            }

            return string.Join("/", segments);
        }

        /// <summary>
        /// Depth-first collection of every item in a group tree, optionally filtered by a regex
        /// on the item name. An invalid regex skips items rather than throwing, matching the
        /// behaviour of the resolvers this replaces.
        /// </summary>
        private static void WalkRecursive<TGroup, TItem>(
            TGroup group,
            List<TItem> sink,
            Func<TGroup, IEnumerable<TItem>> items,
            Func<TGroup, IEnumerable<TGroup>> groups,
            Func<TItem, string> name,
            string regexName = "")
            where TGroup : class
        {
            foreach (var item in items(group))
            {
                if (item == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(regexName))
                {
                    try
                    {
                        if (!Regex.IsMatch(name(item), regexName, RegexOptions.IgnoreCase))
                        {
                            continue;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Invalid regex pattern - skip this item.
                        continue;
                    }
                }

                sink.Add(item);
            }

            foreach (var subgroup in groups(group))
            {
                WalkRecursive(subgroup, sink, items, groups, name, regexName);
            }
        }

        /// <summary>
        /// First available translation of a MultilingualText, or null when there is none.
        /// </summary>
        private static string? Text(MultilingualText? text)
        {
            return text?.Items?.FirstOrDefault()?.Text;
        }

        /// <summary>
        /// Splits "Group/Sub/Leaf" into ("Group/Sub", "Leaf"). A path without '/' yields an
        /// empty group path, meaning the system root. The group path stays escaped, ready for
        /// WalkGroups; the leaf is returned as the plain name to compare objects against.
        /// </summary>
        internal static (string GroupPath, string LeafName) SplitPath(string path)
        {
            var trimmed = NormalizeGroupPath(path);
            var index = trimmed.LastIndexOf('/');

            return index < 0
                ? (string.Empty, UnescapeSegment(trimmed))
                : (trimmed.Substring(0, index), UnescapeSegment(trimmed.Substring(index + 1)));
        }

        /// <summary>Appends an object name to the (already escaped) path of its group.</summary>
        internal static string JoinLeaf(string groupPath, string leafName)
        {
            var leaf = EscapeSegment(leafName);

            return string.IsNullOrEmpty(groupPath) ? leaf : $"{groupPath}/{leaf}";
        }

        /// <summary>
        /// Finds an object by the last segment of a path: the exact name first, and only if
        /// nothing has that name is the segment tried as a regular expression. The old order -
        /// regex whenever the name contained a regex character - made "A5.01" match "A5x01".
        /// </summary>
        private T? FindByName<T>(IEnumerable<T> items, string leafName, Func<T, string> name)
            where T : class
        {
            var list = items.Where(i => i != null).ToList();
            var exact = list.FirstOrDefault(i => name(i).Equals(leafName, StringComparison.OrdinalIgnoreCase));

            if (exact != null || leafName.IndexOfAny(_regexChars) < 0)
            {
                return exact;
            }

            try
            {
                var regex = new Regex(leafName, RegexOptions.IgnoreCase);

                return list.FirstOrDefault(i => regex.IsMatch(name(i)));
            }
            catch (ArgumentException)
            {
                // Not a valid pattern, and no object carries it as a literal name.
                return null;
            }
        }

        #endregion

        #region selectors for the block and type hierarchies

        private static IEnumerable<PlcBlockGroup> BlockSubgroups(PlcBlockGroup group) => group.Groups;

        private static IEnumerable<PlcTypeGroup> TypeSubgroups(PlcTypeGroup group) => group.Groups;

        #endregion
    }
}
