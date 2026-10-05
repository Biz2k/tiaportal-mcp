using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.Download;
using Siemens.Engineering.HW;

namespace TiaMcpServer.Siemens
{
    // Download to a PLC or a simulated PLC.
    //
    // Callers: the get_download_targets and download_to_plc tools in McpServer.Download.cs.
    // Affected API: DownloadToPlc returns a DownloadOutcome instead of the raw DownloadResult
    // and takes the stop/start switches. Reads/writes no data files. The target has to
    // be reachable already - this server never starts PLCSIM (see AGENTS.md).
    //
    // A download is a dialogue: TIA Portal raises a series of configuration steps ("stop the
    // modules?", "overwrite the system data?") and expects an answer to each before and after
    // loading. The previous implementation answered them through 'dynamic' inside an empty
    // catch, so a step it did not understand silently stayed unanswered and the whole download
    // came back as "DownloadToPlc failed". Here every step is recorded - what it was, which
    // answers it offered, which one was given - and returned to the caller, together with the
    // message tree of the result.
    //
    // The steps are handled by reflection on purpose. Their classes live in
    // Siemens.Engineering.Download.Configurations and differ between TIA Portal versions; a
    // step this server has never heard of must still be reported with its options, not break
    // the build or the call.

    /// <summary>One configuration step TIA Portal raised during a download.</summary>
    public class DownloadStep
    {
        /// <summary>"before" the load or "after" it.</summary>
        public string Phase { get; set; } = string.Empty;

        /// <summary>Class name of the step, e.g. "StopModules". This is the key for 'selections'.</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>What TIA Portal says about the step.</summary>
        public string? Message { get; set; }

        /// <summary>The answers the step offers.</summary>
        public List<string> Options { get; set; } = new List<string>();

        /// <summary>The answer that was given, or the one already set when none was given.</summary>
        public string? Selection { get; set; }

        /// <summary>False when the server had no answer for the step and left it as it was.</summary>
        public bool Answered { get; set; }

        public string? Note { get; set; }
    }

    public class DownloadMessage
    {
        public int Depth { get; set; }

        public string State { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;
    }

    public class DownloadOutcome
    {
        public string State { get; set; } = "Unknown";

        public int ErrorCount { get; set; }

        public int WarningCount { get; set; }

        public string Target { get; set; } = string.Empty;

        public List<DownloadStep> Steps { get; set; } = new List<DownloadStep>();

        public List<DownloadMessage> Messages { get; set; } = new List<DownloadMessage>();
    }

    public partial class Portal
    {
        public List<string> GetDownloadTargets(string softwarePath)
        {
            return Operation.Run(_logger, nameof(GetDownloadTargets), PortalErrorCode.InvalidState, () =>
            {
                var targets = new List<string>();

                foreach (var mode in RequireDownloadProvider(softwarePath).Configuration.Modes)
                {
                    foreach (var pcInterface in mode.PcInterfaces)
                    {
                        foreach (var target in pcInterface.TargetInterfaces)
                        {
                            targets.Add($"{mode.Name} / {pcInterface.Name} / {target.Name}");
                        }
                    }
                }

                return targets;
            },
            ("softwarePath", softwarePath));
        }

        private DownloadProvider RequireDownloadProvider(string softwarePath)
        {
            var softwareContainer = GetSoftwareContainer(softwarePath)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"Software not found at '{softwarePath}'. Use 'get_project_tree' to discover valid software paths.");

            var deviceItem = softwareContainer.Parent as DeviceItem
                ?? throw new PortalException(PortalErrorCode.InvalidState, "Parent of software is not a DeviceItem.");

            return deviceItem.GetService<DownloadProvider>()
                ?? (softwareContainer.Software as IEngineeringServiceProvider)?.GetService<DownloadProvider>()
                ?? ((IEngineeringServiceProvider)softwareContainer).GetService<DownloadProvider>()
                ?? throw new PortalException(PortalErrorCode.NotSupported, "Download is not supported for this device.");
        }

        /// <param name="stopPlc">Allow TIA Portal to stop the CPU when the download requires it.</param>
        /// <param name="startPlc">Start the CPU again after the download.</param>
        /// <param name="selections">
        /// Answers by step type, overriding the defaults - e.g. { "OverwriteSystemData": "Overwrite" }.
        /// The step types and their options are reported in every outcome.
        /// </param>
        public DownloadOutcome DownloadToPlc(
            string softwarePath,
            string modeName,
            string pcInterfaceName,
            string targetInterfaceName,
            bool hardware,
            bool software,
            bool stopPlc = false,
            bool startPlc = false,
            IDictionary<string, string>? selections = null)
        {
            return Operation.Run(_logger, nameof(DownloadToPlc), PortalErrorCode.InvalidState, () =>
            {
                if (!hardware && !software)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams,
                        "Nothing to download: set 'hardware', 'software' or both to true.");
                }

                var downloadProvider = RequireDownloadProvider(softwarePath);
                var modes = downloadProvider.Configuration.Modes.ToList();

                var mode = modes.FirstOrDefault(m => string.Equals(m.Name, modeName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new PortalException(PortalErrorCode.NotFound,
                        $"Mode '{modeName}' not found. Available: {Quote(modes.Select(m => m.Name))}.");

                var pcInterface = mode.PcInterfaces.FirstOrDefault(p => string.Equals(p.Name, pcInterfaceName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new PortalException(PortalErrorCode.NotFound,
                        $"PC interface '{pcInterfaceName}' not found in mode '{modeName}'. Available: {Quote(mode.PcInterfaces.Select(p => p.Name))}.");

                var target = pcInterface.TargetInterfaces.FirstOrDefault(t => string.Equals(t.Name, targetInterfaceName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new PortalException(PortalErrorCode.NotFound,
                        $"Target interface '{targetInterfaceName}' not found on PC interface '{pcInterfaceName}'. Available: {Quote(pcInterface.TargetInterfaces.Select(t => t.Name))}.");

                var options = DownloadOptions.None;
                if (hardware) options |= DownloadOptions.Hardware;
                if (software) options |= DownloadOptions.Software;

                var answers = DefaultDownloadAnswers(stopPlc, startPlc);

                if (selections != null)
                {
                    foreach (var pair in selections)
                    {
                        answers[pair.Key] = new[] { pair.Value };
                    }
                }

                var outcome = new DownloadOutcome
                {
                    Target = $"{mode.Name} / {pcInterface.Name} / {target.Name}"
                };

                var stage = "connecting to the target and preparing the download";

                // The two callbacks below must never let an exception escape. Openness turns an
                // exception thrown inside a download callback into a NonRecoverableException and
                // closes the TIA Portal instance with it - unsaved work included. A 'dry run'
                // that aborted from the first callback did exactly that (2026-10-05). There is no
                // way to preview a download: AnswerDownloadStep catches everything itself.

                try
                {
                    var result = downloadProvider.Download(
                        target,
                        configuration =>
                        {
                            stage = "answering the configuration steps before loading";
                            outcome.Steps.Add(AnswerDownloadStep(configuration, "before", answers));
                            stage = "loading";
                        },
                        configuration =>
                        {
                            stage = "answering the configuration steps after loading";
                            outcome.Steps.Add(AnswerDownloadStep(configuration, "after", answers));
                        },
                        options);

                    outcome.State = result.State.ToString();
                    outcome.ErrorCount = result.ErrorCount;
                    outcome.WarningCount = result.WarningCount;

                    CollectDownloadMessages(result.Messages, 0, outcome.Messages);
                }
                catch (Exception ex) when (ex is not PortalException)
                {
                    var seen = outcome.Steps.Count == 0
                        ? "No configuration step was reached, so the target was probably not reachable: check that the PLC or the PLCSIM instance is running and that its address matches the project."
                        : "Configuration steps seen: " + string.Join("; ", outcome.Steps.Select(DescribeStep)) + ".";

                    // "Download configuration '...StopModules' was unhandled" is how TIA Portal
                    // says that leaving a step at 'NoAction' is not an option for this download.
                    var reason = ErrorText.Describe(ex);
                    var refused = outcome.Steps
                        .Where(s => reason.IndexOf("." + s.Type + "'", StringComparison.OrdinalIgnoreCase) >= 0 && reason.IndexOf("unhandled", StringComparison.OrdinalIgnoreCase) >= 0)
                        .Select(s => s.Type.Equals("StopModules", StringComparison.OrdinalIgnoreCase)
                            ? "This download cannot be loaded while the CPU runs: pass stopPlc=true to let TIA Portal stop it (and startPlc=true to start it again)."
                            : $"TIA Portal needs a decision for '{s.Type}'; pass it in 'selections', e.g. {s.Type}={s.Options.LastOrDefault()}.")
                        .FirstOrDefault();

                    throw new PortalException(PortalErrorCode.InvalidState,
                        $"Download to '{outcome.Target}' failed while {stage}: {reason}. {(refused == null ? string.Empty : refused + " ")}{seen}", null, ex);
                }

                return outcome;
            },
            ("softwarePath", softwarePath), ("target", $"{modeName} / {pcInterfaceName} / {targetInterfaceName}"));
        }

        /// <summary>
        /// The answer given to each known step type, as candidate option names in order of
        /// preference - the first one the step actually offers is used. Names not offered by the
        /// running TIA Portal version are simply skipped.
        /// </summary>
        private static Dictionary<string, string[]> DefaultDownloadAnswers(bool stopPlc, bool startPlc)
        {
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["StopModules"] = stopPlc ? ["StopAll"] : ["NoAction"],
                ["StartModules"] = startPlc ? ["StartModule"] : ["NoAction"],
                ["AllBlocksDownload"] = ["DownloadAllBlocks"],
                ["OverwriteSystemData"] = ["Overwrite"],
                ["ConsistentBlocksDownload"] = ["ConsistentDownload"],
                ["AlarmTextLibrariesDownload"] = ["ConsistentDownload"],
                ["ProtectionLevelChanged"] = ["ContinueDownloading"],
                ["ActiveTestCanBeAborted"] = ["AcceptAndDownload"],
                ["DifferentTargetConfiguration"] = ["AcceptAll"],
                ["CheckBeforeDownload"] = ["AcceptAll"],
                ["LoadIdentificationData"] = ["LoadNothing"],
                ["DataBlockReinitialization"] = stopPlc ? ["StopPlcAndReinitialize"] : ["NoAction"],
                ["InitializeMemory"] = ["AcceptAll"],
                ["TurnOffSequenceBeforeLoadAction"] = ["TurnOffSequenceBeforeLoad"]
            };
        }

        private DownloadStep AnswerDownloadStep(object configuration, string phase, Dictionary<string, string[]> answers)
        {
            var type = configuration.GetType();
            var step = new DownloadStep { Phase = phase, Type = type.Name };

            try
            {
                step.Message = type.GetProperty("Message")?.GetValue(configuration)?.ToString();

                var selection = type.GetProperty("CurrentSelection");

                if (selection == null || !selection.PropertyType.IsEnum)
                {
                    // Password prompts and the like: there is nothing to select.
                    step.Note = type.GetMethod("SetPassword") != null
                        ? "This step asks for a password; the server does not supply passwords. Remove the protection or download from TIA Portal."
                        : "This step offers no selection; it was left as it is.";

                    return step;
                }

                step.Options = Enum.GetNames(selection.PropertyType).ToList();
                step.Selection = selection.GetValue(configuration)?.ToString();

                if (!answers.TryGetValue(type.Name, out var candidates))
                {
                    step.Note = "The server has no default answer for this step. Pass one in 'selections', e.g. " +
                                $"{type.Name}={step.Options.FirstOrDefault()}.";

                    return step;
                }

                var chosen = candidates
                    .Select(c => step.Options.FirstOrDefault(o => o.Equals(c, StringComparison.OrdinalIgnoreCase)))
                    .FirstOrDefault(o => o != null);

                if (chosen == null)
                {
                    step.Note = $"None of the intended answers ({string.Join(", ", candidates)}) is offered by this step.";

                    return step;
                }

                // Only a change is written. Re-assigning the value a step already has is
                // rejected ("The configuration provided is invalid" for StopModules = NoAction);
                // if TIA Portal cannot go on with that value, the download fails as 'unhandled'
                // and the caller is told which switch to set.
                if (selection.CanWrite && !chosen.Equals(step.Selection, StringComparison.OrdinalIgnoreCase))
                {
                    selection.SetValue(configuration, Enum.Parse(selection.PropertyType, chosen));
                }

                step.Selection = chosen;
                step.Answered = true;
            }
            catch (Exception ex)
            {
                // One unreadable step must not hide the others; say what happened and go on.
                step.Note = $"Could not handle this step: {ErrorText.Describe(ex)}";
                _logger?.LogWarning(ex, "Download step {Step} could not be handled", type.Name);
            }

            return step;
        }

        private static string DescribeStep(DownloadStep step)
        {
            var answer = step.Answered ? step.Selection : $"unanswered, options: {string.Join("/", step.Options)}";
            var note = string.IsNullOrEmpty(step.Note) ? string.Empty : $" ({step.Note})";

            return $"{step.Type} [{step.Phase}] = {answer}{note}";
        }

        private static void CollectDownloadMessages(DownloadResultMessageComposition messages, int depth, List<DownloadMessage> sink)
        {
            foreach (var message in messages)
            {
                sink.Add(new DownloadMessage
                {
                    Depth = depth,
                    State = message.State.ToString(),
                    Text = message.Message ?? string.Empty
                });

                // Depth guard: the tree is produced by TIA Portal, not by this server.
                if (depth < 10)
                {
                    CollectDownloadMessages(message.Messages, depth + 1, sink);
                }
            }
        }

        private static string Quote(IEnumerable<string> names)
        {
            return string.Join(", ", names.Select(n => $"'{n}'"));
        }
    }
}
