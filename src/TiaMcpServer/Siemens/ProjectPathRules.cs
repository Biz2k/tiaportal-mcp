using System;
using System.IO;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// What 'save_as_project' accepts as the new project location, checked before TIA Portal is asked. The path is the
    /// FOLDER of the new project, without an extension: TIA Portal makes <c>&lt;folder&gt;\&lt;name&gt;.ap21</c> inside it.
    /// A path that ends in the project extension used to create a folder named <c>X.ap21</c> holding <c>X.ap21.ap21</c>,
    /// or fail with "storage medium is no longer available" inside an existing folder (test report, 2026-10-06).
    /// Pure logic: the file system is passed in, so the rules are tested without TIA Portal.
    /// </summary>
    public static class ProjectPathRules
    {
        private static readonly Regex ProjectExtension = new Regex(@"\.(ap|als)\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>The folder to pass to Project.SaveAs, or a PortalException (InvalidParams / NotFound / InvalidState) that says what to change.</summary>
        public static string CheckNewProjectFolder(string? path, Func<string, bool> directoryExists, Func<string, bool> directoryIsEmpty)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "newProjectPath is empty. Pass the folder of the new project, e.g. 'C:\\Projects\\NewPlant'.");
            }

            var trimmed = path!.Trim().TrimEnd('\\', '/');

            if (!Path.IsPathRooted(trimmed) || trimmed.Length < 3 || (trimmed[1] != ':' && !trimmed.StartsWith(@"\\", StringComparison.Ordinal)))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{path}' is not an absolute path. Pass the full path of the new project folder, e.g. 'C:\\Projects\\NewPlant'.");
            }

            var name = Path.GetFileName(trimmed);

            if (ProjectExtension.IsMatch(name))
            {
                var corrected = Path.Combine(Path.GetDirectoryName(trimmed) ?? string.Empty, ProjectExtension.Replace(name, string.Empty));

                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"'{path}' ends in a project extension, but save_as_project takes the FOLDER of the new project: TIA Portal creates the project file '{name}' in it by itself. " +
                    $"Use '{corrected}'; the project file will then be '{Path.Combine(corrected, name)}'.");
            }

            var parent = Path.GetDirectoryName(trimmed);

            if (string.IsNullOrEmpty(parent) || !directoryExists(parent!))
            {
                throw new PortalException(PortalErrorCode.NotFound,
                    $"The parent folder '{parent}' does not exist. Create it first; TIA Portal creates only the last folder of the path.");
            }

            if (directoryExists(trimmed) && !directoryIsEmpty(trimmed))
            {
                throw new PortalException(PortalErrorCode.InvalidState,
                    $"The folder '{trimmed}' exists and is not empty. Choose a new or an empty folder so that nothing is overwritten.");
            }

            return trimmed;
        }
    }
}
