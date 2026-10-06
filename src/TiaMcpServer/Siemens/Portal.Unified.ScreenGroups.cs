using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.HmiUnified.UI.ScreenGroup;
using Siemens.Engineering.HmiUnified.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Siemens
{
    // WinCC Unified: screen groups, and the walk over every screen of an HMI.
    //
    // Callers: the tools unified_get_screen_groups and unified_manage_screen_groups, and every
    // tool that names a screen (through FindUnifiedScreen in Portal.Unified.cs). Reads and
    // writes no data files.
    //
    // HmiSoftware.Screens holds only the screens at the top level; the others sit in
    // HmiSoftware.ScreenGroups[].Screens and, in nested groups, in .Groups[].Screens. Looking
    // only at the top level made the screens of a group invisible to every tool (found
    // 2026-10-06: 14 of the 74 screens of the test panel were seen).
    //
    // What Openness does here, as found on TIA Portal V21 (2026-10-06):
    //   - Screen names are unique across the whole HMI, groups included: creating a screen
    //     whose name is taken, in the same group or another, fails with "ValueIsNotUnique".
    //     So a screen is found by its name alone. The same holds for group names within one
    //     parent group (group names are checked here, a failed Create would cost the batch
    //     its commit).
    //   - Creating a group or a screen takes a name only; there is no way to move a screen
    //     between groups.
    //   - HmiScreen.DisplayName is a MultilingualText whose items take the same
    //     "<body><p>text</p></body>" document as the texts of screen items; a plain string is
    //     rejected with "invalid format". Writing it is harmless, unlike HmiTag.DisplayName.
    //   - Width, Height, ScreenNumber, BackColor and BackGraphic are written without complaint;
    //     BackGraphic accepts a name that does not exist, as the other graphic references do.
    public partial class Portal
    {
        /// <summary>Every screen of the HMI with the '/'-separated path of its group, empty at the top level.</summary>
        private static IEnumerable<(HmiScreen Screen, string Group)> EnumerateUnifiedScreens(HmiSoftware software)
        {
            foreach (var screen in software.Screens)
            {
                yield return (screen, string.Empty);
            }

            foreach (var group in software.ScreenGroups)
            {
                foreach (var entry in EnumerateUnifiedScreens(group, group.Name))
                {
                    yield return entry;
                }
            }
        }

        private static IEnumerable<(HmiScreen Screen, string Group)> EnumerateUnifiedScreens(HmiScreenGroup group, string path)
        {
            foreach (var screen in group.Screens)
            {
                yield return (screen, path);
            }

            foreach (var subGroup in group.Groups)
            {
                foreach (var entry in EnumerateUnifiedScreens(subGroup, $"{path}/{subGroup.Name}"))
                {
                    yield return entry;
                }
            }
        }

        private static IEnumerable<(HmiScreenGroup Group, string Path)> EnumerateScreenGroups(HmiSoftware software)
        {
            foreach (var group in software.ScreenGroups)
            {
                yield return (group, group.Name);

                foreach (var entry in EnumerateScreenGroups(group, group.Name))
                {
                    yield return entry;
                }
            }
        }

        private static IEnumerable<(HmiScreenGroup Group, string Path)> EnumerateScreenGroups(HmiScreenGroup parent, string path)
        {
            foreach (var group in parent.Groups)
            {
                var groupPath = $"{path}/{group.Name}";

                yield return (group, groupPath);

                foreach (var entry in EnumerateScreenGroups(group, groupPath))
                {
                    yield return entry;
                }
            }
        }

        private static string[] SplitGroupPath(string path)
        {
            return path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();
        }

        private static HmiScreenGroup? FindScreenGroup(HmiSoftware software, string path)
        {
            var wanted = string.Join("/", SplitGroupPath(path));

            return EnumerateScreenGroups(software)
                .Where(g => string.Equals(g.Path, wanted, StringComparison.OrdinalIgnoreCase))
                .Select(g => g.Group)
                .FirstOrDefault();
        }

        private static HmiScreenGroup RequireScreenGroup(HmiSoftware software, string path)
        {
            return FindScreenGroup(software, path)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"Screen group '{path}' not found. Existing: {DescribeScreenGroups(software)}. Groups in groups are written 'Parent/Child'; 'unified_get_screen_groups' lists them.");
        }

        private static string DescribeScreenGroups(HmiSoftware software)
        {
            var paths = EnumerateScreenGroups(software).Select(g => g.Path).ToList();

            return paths.Count == 0 ? "none" : string.Join(", ", paths);
        }

        public List<UnifiedScreenGroupInfo> GetUnifiedScreenGroups(string softwarePath)
        {
            return Operation.Run(_logger, nameof(GetUnifiedScreenGroups), PortalErrorCode.InvalidState,
                () => EnumerateScreenGroups(RequireUnifiedSoftware(softwarePath))
                    .Select(g => new UnifiedScreenGroupInfo
                    {
                        Path = g.Path,
                        Name = g.Group.Name,
                        ScreenCount = g.Group.Screens.Count,
                        GroupCount = g.Group.Groups.Count
                    })
                    .ToList(),
                ("softwarePath", softwarePath));
        }

        public List<UnifiedActionResult> ManageUnifiedScreenGroups(string softwarePath, IList<UnifiedScreenGroupAction>? actions)
        {
            return RunUnifiedBatch(nameof(ManageUnifiedScreenGroups), softwarePath, actions,
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

                            if (FindScreenGroup(software, path) != null)
                            {
                                throw new PortalException(PortalErrorCode.InvalidParams, $"Screen group '{path}' already exists.");
                            }

                            // The parent has to exist: creating it silently would turn a typo
                            // into a new group.
                            if (parentPath.Length == 0)
                            {
                                software.ScreenGroups.Create(name);
                            }
                            else
                            {
                                RequireScreenGroup(software, parentPath).Groups.Create(name);
                            }

                            break;

                        case "rename":
                            var newName = RequireName(action.NewName, "newName");
                            var group = RequireScreenGroup(software, path);
                            var siblings = group.Parent is HmiScreenGroup parent
                                ? parent.Groups.Select(g => g.Name)
                                : software.ScreenGroups.Select(g => g.Name);

                            if (siblings.Any(n => string.Equals(n, newName, StringComparison.OrdinalIgnoreCase)))
                            {
                                throw new PortalException(PortalErrorCode.InvalidParams, $"A screen group named '{newName}' already exists in the same place.");
                            }

                            group.Name = newName;
                            result.Applied.Add($"Name = {newName}");

                            break;

                        case "delete":
                            var doomed = RequireScreenGroup(software, path);
                            var screens = EnumerateUnifiedScreens(doomed, path).Count();

                            doomed.Delete();

                            if (screens > 0)
                            {
                                result.Notes.Add($"{screens} screen(s) of the group, nested groups included, were deleted with it.");
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
