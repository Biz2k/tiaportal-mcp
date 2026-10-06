using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>One running TIA Portal process, as 'get_tia_instances' reports it.</summary>
    public sealed class TiaInstanceInfo
    {
        public int Id { get; set; }

        /// <summary>Full path of the open project, empty when none is open.</summary>
        public string ProjectPath { get; set; } = string.Empty;

        public string Mode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Chooses the TIA Portal process 'connect' attaches to. Pure logic, kept out of Portal so it
    /// can be tested without TIA Portal.
    /// </summary>
    public static class TiaInstanceSelection
    {
        /// <summary>
        /// No criteria: the only instance that has a project open; when none has one, the first instance; when several have,
        /// an error that lists them. With
        /// <paramref name="processId"/> the process with that id; with <paramref name="projectPath"/>
        /// the one whose open project has that path or file name.
        /// </summary>
        /// <exception cref="PortalException">InvalidParams when both criteria are given or nothing / more than one matches.</exception>
        public static TiaInstanceInfo Pick(IReadOnlyList<TiaInstanceInfo> instances, int? processId, string? projectPath)
        {
            var hasPath = !string.IsNullOrWhiteSpace(projectPath);

            if (processId.HasValue && hasPath)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "Pass either processId or projectPath, not both.");
            }

            if (!processId.HasValue && !hasPath)
            {
                // No criteria: the instance that has a project open is the one meant. A new, empty instance (a second
                // window, or one the server started) must not win just by being first in the list.
                var withProject = instances.Where(i => !string.IsNullOrEmpty(i.ProjectPath)).ToList();

                if (withProject.Count > 1)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams,
                        $"{withProject.Count} TIA Portal instances have a project open, so 'connect' cannot tell which one is meant. " +
                        $"Pass processId or projectPath. Running: {string.Join("; ", instances.Select(Describe))}.");
                }

                return withProject.Count == 1 ? withProject[0] : instances[0];
            }

            var matches = processId.HasValue
                ? instances.Where(i => i.Id == processId.Value).ToList()
                : instances.Where(i => PathMatches(i.ProjectPath, projectPath!)).ToList();

            if (matches.Count == 1)
            {
                return matches[0];
            }

            var what = processId.HasValue ? $"process {processId}" : $"project '{projectPath}'";
            var running = string.Join("; ", instances.Select(Describe));

            throw new PortalException(PortalErrorCode.NotFound,
                (matches.Count == 0 ? $"No TIA Portal instance for {what}. " : $"{matches.Count} TIA Portal instances match {what}; use processId. ") +
                $"Running: {running}.");
        }

        public static string Describe(TiaInstanceInfo i)
        {
            return $"process {i.Id} ({(string.IsNullOrEmpty(i.ProjectPath) ? "no project" : i.ProjectPath)}, {i.Mode})";
        }

        private static bool PathMatches(string actual, string wanted)
        {
            if (string.IsNullOrEmpty(actual))
            {
                return false;
            }

            var a = actual.Replace('/', '\\');
            var w = wanted.Trim().Replace('/', '\\');

            return a.Equals(w, StringComparison.OrdinalIgnoreCase)
                || System.IO.Path.GetFileName(a).Equals(w, StringComparison.OrdinalIgnoreCase)
                || System.IO.Path.GetFileNameWithoutExtension(a).Equals(w, StringComparison.OrdinalIgnoreCase);
        }
    }
}
