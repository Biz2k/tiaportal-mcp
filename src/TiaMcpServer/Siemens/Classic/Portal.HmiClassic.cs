using Siemens.Engineering.Hmi;
using System;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    // WinCC Comfort / Advanced / Professional ("classic" WinCC): parked work.
    //
    // Callers: none. No tool is registered for the classic systems at the moment; the unified_*
    // tools refuse an HmiTarget with an explanation. This file keeps the parts that were found
    // to work, so they need not be rediscovered. The findings, including what does NOT work and
    // why, are in docs/hmi-classic-notes.md. Reads and writes no data files.
    //
    // Background: the HMI tools were first written to serve Unified and the classic systems
    // from the same methods. For the classic systems Openness has no object model below the
    // screen - no items, no properties, not even ScreenComposition.Create - so those methods
    // carried a second code path that could not do the job. When the tools were renamed to
    // unified_*, the classic paths were taken out and collected here.

    public partial class Portal
    {
        private HmiTarget RequireClassicHmi(string softwarePath)
        {
            return RequireHmiContainer(softwarePath).Software as HmiTarget
                ?? throw new PortalException(PortalErrorCode.NotSupported,
                    $"'{softwarePath}' is not a WinCC Comfort/Advanced/Professional HMI.");
        }

        /// <summary>Names of all screens, including those in screen folders. Verified on WinCC Advanced V21.</summary>
        public List<string> GetClassicHmiScreens(string softwarePath)
        {
            return Operation.Run(_logger, nameof(GetClassicHmiScreens), PortalErrorCode.InvalidState,
                () =>
                {
                    var names = new List<string>();

                    CollectClassicScreens(RequireClassicHmi(softwarePath).ScreenFolder, names);

                    return names;
                },
                ("softwarePath", softwarePath));
        }

        private static void CollectClassicScreens(dynamic folder, List<string> names)
        {
            foreach (var screen in folder.Screens)
            {
                names.Add((string)screen.Name);
            }

            foreach (var subFolder in folder.Folders)
            {
                CollectClassicScreens(subFolder, names);
            }
        }

        /// <summary>A screen by name, searched through the screen folders; null when there is none.</summary>
        private static object? FindClassicScreen(dynamic folder, string screenName)
        {
            foreach (var screen in folder.Screens)
            {
                if (string.Equals((string)screen.Name, screenName, StringComparison.OrdinalIgnoreCase))
                {
                    return screen;
                }
            }

            foreach (var subFolder in folder.Folders)
            {
                var found = FindClassicScreen(subFolder, screenName);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>Names of all HMI tags across the tag tables. Verified on WinCC Advanced V21.</summary>
        public List<string> GetClassicHmiTags(string softwarePath)
        {
            return Operation.Run(_logger, nameof(GetClassicHmiTags), PortalErrorCode.InvalidState,
                () =>
                {
                    var names = new List<string>();

                    foreach (var table in RequireClassicHmi(softwarePath).TagFolder.TagTables)
                    {
                        foreach (var tag in table.Tags)
                        {
                            names.Add(tag.Name);
                        }
                    }

                    return names;
                },
                ("softwarePath", softwarePath));
        }

        /// <summary>
        /// Names of the classic faceplate types in the project library (FaceplateLibraryType).
        /// Unified faceplates are plain LibraryType entries; see GetLibraryTypes.
        /// </summary>
        public List<string> GetClassicFaceplateTypes()
        {
            return Operation.Run(_logger, nameof(GetClassicFaceplateTypes), PortalErrorCode.InvalidState,
                () =>
                {
                    var names = new List<string>();

                    if (_project == null)
                    {
                        return names;
                    }

                    dynamic library = _project.ProjectLibrary;

                    foreach (var type in library.TypeFolder.Types)
                    {
                        if (((object)type).GetType().Name == "FaceplateLibraryType")
                        {
                            names.Add((string)type.Name);
                        }
                    }

                    return names;
                });
        }
    }
}
