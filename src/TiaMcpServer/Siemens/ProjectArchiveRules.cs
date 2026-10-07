using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// What 'archive_project' and 'retrieve_project' accept, checked before TIA Portal is asked. Found on V21 (probe of
    /// 2026-10-08 on the 473 MB test project): Project.Archive takes 30 s for 100 MB of archive, does not change
    /// IsModified, and refuses a project with unsaved changes ("save the project before performing 'Archive'"), an
    /// archive that exists ("already exist"); it adds NO extension to the name, so the file name is made here
    /// (<c>.zap21</c> for a <c>.ap21</c> project); it CREATES a missing target folder (refused here); the mode None writes
    /// a folder instead of a file. Retrieve refuses while a project is open ("Another project is already open").
    /// Pure logic: the file system is passed in, so the rules are tested without TIA Portal.
    /// </summary>
    public static class ProjectArchiveRules
    {
        /// <summary>The modes of ProjectArchivationMode in the form a caller writes them; the first is the default.</summary>
        public static readonly string[] Modes = { "compressed", "none", "discardRestorableData", "discardRestorableDataAndCompressed" };

        private static readonly Regex ArchiveExtension = new Regex(@"\.zap\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex ProjectExtension = new Regex(@"\.ap(\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>The name of the ProjectArchivationMode value for the mode a caller wrote (any case; empty means compressed).</summary>
        public static string ParseMode(string? mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
            {
                return "Compressed";
            }

            var found = Modes.FirstOrDefault(m => string.Equals(m, mode!.Trim(), StringComparison.OrdinalIgnoreCase));

            if (found == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Unknown mode '{mode}'. Use one of: {string.Join(", ", Modes)}.");
            }

            return char.ToUpperInvariant(found[0]) + found.Substring(1);
        }

        /// <summary>True for the modes that write a single compressed file; 'none' and 'discardRestorableData' write a folder.</summary>
        public static bool IsFile(string parsedMode) => parsedMode.EndsWith("Compressed", StringComparison.Ordinal);

        /// <summary>
        /// The folder, the name TIA Portal is given and the full path of the archive it will make. A compressed archive gets
        /// the extension of the project (.ap21 -> .zap21) unless the name has one.
        /// </summary>
        public static (string Directory, string Name, string FullPath) CheckArchiveTarget(
            string? targetDirectory, string? name, string parsedMode, string projectFile, Func<string, bool> directoryExists, Func<string, bool> pathExists)
        {
            var directory = CheckAbsolute(targetDirectory, "target directory", @"e.g. 'C:\Backups'");

            if (!directoryExists(directory))
            {
                throw new PortalException(PortalErrorCode.NotFound,
                    $"The folder '{directory}' does not exist. Create it first; TIA Portal would make it, but a mistyped path should not leave a stray folder.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The name of the archive is empty. Pass a file name without a folder, e.g. 'Plant_before_change'.");
            }

            var trimmed = name!.Trim();

            if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The name '{name}' is not a file name (no folder, no characters like \\ / : * ? \" < > |). The folder goes in 'targetDirectory'.");
            }

            if (IsFile(parsedMode) && !ArchiveExtension.IsMatch(trimmed))
            {
                var match = ProjectExtension.Match(projectFile);

                trimmed += ".zap" + (match.Success ? match.Groups[1].Value : "21");
            }

            var full = Path.Combine(directory, trimmed);

            if (pathExists(full))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"'{full}' exists already. Choose another name, so that no archive is overwritten.");
            }

            return (directory, trimmed, full);
        }

        /// <summary>The archive file and the folder to retrieve it into, or a PortalException that says what to change.</summary>
        public static (string Archive, string Directory) CheckRetrieve(
            string? archivePath, string? targetDirectory, Func<string, bool> fileExists, Func<string, bool> directoryExists, Func<string, bool> directoryIsEmpty)
        {
            var archive = CheckAbsolute(archivePath, "archive path", @"e.g. 'C:\Backups\Plant.zap21'");

            if (!fileExists(archive))
            {
                throw new PortalException(PortalErrorCode.NotFound, $"The archive '{archive}' does not exist. Pass the full path of the .zapXX file ('archive_project' gives it).");
            }

            var directory = CheckAbsolute(targetDirectory, "target directory", @"e.g. 'C:\Projects\Restored'");
            var parent = Path.GetDirectoryName(directory);

            if (string.IsNullOrEmpty(parent) || !directoryExists(parent!))
            {
                throw new PortalException(PortalErrorCode.NotFound, $"The parent folder '{parent}' of the target does not exist. Create it first.");
            }

            if (directoryExists(directory) && !directoryIsEmpty(directory))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"The folder '{directory}' exists and is not empty. Choose a new or an empty folder so that nothing is overwritten.");
            }

            return (archive, directory);
        }

        private static string CheckAbsolute(string? path, string what, string example)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The {what} is empty. Pass the full path, {example}.");
            }

            var trimmed = path!.Trim().TrimEnd('\\', '/');

            if (!Path.IsPathRooted(trimmed) || trimmed.Length < 3 || (trimmed[1] != ':' && !trimmed.StartsWith(@"\\", StringComparison.Ordinal)))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{path}' is not an absolute path. Pass the full path, {example}.");
            }

            return trimmed;
        }
    }
}
