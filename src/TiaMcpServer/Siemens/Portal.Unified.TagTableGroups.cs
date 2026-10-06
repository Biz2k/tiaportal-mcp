using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.HmiUnified.HmiTags;
using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Siemens
{
    // WinCC Unified: tag table groups - read, create, rename, delete, and tables created in them.
    //
    // Callers: unified_get_tag_table_groups, unified_manage_tag_table_groups and the 'group' of
    // unified_manage_tag_tables in McpServer.Unified.cs. Reads and writes no data files. The walk
    // over the tables of all groups is EnumerateTagTables in Portal.Unified.Tags.cs.
    //
    // What Openness does here, as found on TIA Portal V21 (2026-10-06, panel and PC station):
    //   - Table names are unique across the whole HMI, groups included ("The value must be
    //     unique"), so a table is found by its name alone, like a screen. Group names are unique
    //     within the HMI too ("ValueIsNotUnique"); a nested group is written 'Parent/Child'.
    //   - Deleting a group deletes the tables in it and their tags (both gone in the probe).
    //   - A table cannot be moved between groups: HmiTagTable has only Name and Delete.
    //   - Renaming a group is a plain Name write.
    public partial class Portal
    {
        private static IEnumerable<(HmiTagTableGroup Group, string Path)> EnumerateTagTableGroups(HmiSoftware software)
        {
            foreach (var group in software.TagTableGroups)
            {
                yield return (group, group.Name);

                foreach (var entry in EnumerateTagTableGroups(group, group.Name))
                {
                    yield return entry;
                }
            }
        }

        private static IEnumerable<(HmiTagTableGroup Group, string Path)> EnumerateTagTableGroups(HmiTagTableGroup parent, string path)
        {
            foreach (var group in parent.Groups)
            {
                var groupPath = $"{path}/{group.Name}";

                yield return (group, groupPath);

                foreach (var entry in EnumerateTagTableGroups(group, groupPath))
                {
                    yield return entry;
                }
            }
        }

        private static HmiTagTableGroup? FindTagTableGroup(HmiSoftware software, string path)
        {
            var wanted = string.Join("/", SplitGroupPath(path));

            return EnumerateTagTableGroups(software)
                .Where(g => string.Equals(g.Path, wanted, StringComparison.OrdinalIgnoreCase))
                .Select(g => g.Group)
                .FirstOrDefault();
        }

        private static HmiTagTableGroup RequireTagTableGroup(HmiSoftware software, string path)
        {
            return FindTagTableGroup(software, path)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"Tag table group '{path}' not found. Existing: {DescribeTagTableGroups(software)}. Groups in groups are written 'Parent/Child'; 'unified_get_tag_table_groups' lists them.");
        }

        private static string DescribeTagTableGroups(HmiSoftware software)
        {
            var paths = EnumerateTagTableGroups(software).Select(g => g.Path).ToList();

            return paths.Count == 0 ? "none" : string.Join(", ", paths);
        }

        public List<UnifiedTagTableGroupInfo> GetUnifiedTagTableGroups(string softwarePath)
        {
            return Operation.Run(_logger, nameof(GetUnifiedTagTableGroups), PortalErrorCode.InvalidState,
                () => EnumerateTagTableGroups(RequireUnifiedSoftware(softwarePath))
                    .Select(g => new UnifiedTagTableGroupInfo
                    {
                        Path = g.Path,
                        Name = g.Group.Name,
                        TableCount = g.Group.TagTables.Count,
                        GroupCount = g.Group.Groups.Count
                    })
                    .ToList(),
                ("softwarePath", softwarePath));
        }

        public List<UnifiedActionResult> ManageUnifiedTagTableGroups(string softwarePath, IList<UnifiedTagTableGroupAction>? actions)
        {
            return RunUnifiedBatch(nameof(ManageUnifiedTagTableGroups), softwarePath, actions,
                "{ \"action\": \"create\", \"groupName\": \"Pumps\" }",
                a => a.GroupName,
                (software, action, verb, result) =>
                {
                    var path = string.Join("/", SplitGroupPath(RequireName(action.GroupName, "groupName")));

                    if (path.Length == 0)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, "groupName is required.");
                    }

                    switch (verb)
                    {
                        case "create":
                            var steps = SplitGroupPath(path);
                            var name = steps[steps.Length - 1];
                            var parentPath = string.Join("/", steps.Take(steps.Length - 1));

                            if (FindTagTableGroup(software, path) != null)
                            {
                                throw new PortalException(PortalErrorCode.InvalidParams, $"Tag table group '{path}' already exists.");
                            }

                            // Group names are unique in the whole HMI, wherever the group sits.
                            if (EnumerateTagTableGroups(software).Any(g => string.Equals(g.Group.Name, name, StringComparison.OrdinalIgnoreCase)))
                            {
                                throw new PortalException(PortalErrorCode.InvalidParams,
                                    $"A tag table group named '{name}' already exists elsewhere; group names are unique in the whole HMI.");
                            }

                            if (parentPath.Length == 0)
                            {
                                software.TagTableGroups.Create(name);
                            }
                            else
                            {
                                RequireTagTableGroup(software, parentPath).Groups.Create(name);
                            }

                            break;

                        case "rename":
                            var newName = RequireName(action.NewName, "newName");
                            var group = RequireTagTableGroup(software, path);

                            if (EnumerateTagTableGroups(software).Any(g => string.Equals(g.Group.Name, newName, StringComparison.OrdinalIgnoreCase)))
                            {
                                throw new PortalException(PortalErrorCode.InvalidParams, $"A tag table group named '{newName}' already exists; group names are unique in the whole HMI.");
                            }

                            group.Name = newName;
                            result.Applied.Add($"Name = {newName}");

                            break;

                        case "delete":
                            var doomed = RequireTagTableGroup(software, path);
                            var tables = EnumerateTagTables(doomed, path).ToList();
                            var tags = tables.Sum(t => t.Table.Tags.Count);

                            doomed.Delete();

                            if (tables.Count > 0)
                            {
                                result.Notes.Add($"{tables.Count} tag table(s) of the group, nested groups included, were deleted with it, and {tags} tag(s) with them.");
                            }

                            break;

                        default:
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                $"Unknown action '{action.Action}'. Use 'create', 'rename' or 'delete'.");
                    }
                });
        }
    }
}
