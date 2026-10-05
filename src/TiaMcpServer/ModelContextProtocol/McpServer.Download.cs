using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TiaMcpServer.Siemens;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        [McpServerTool(Name = "get_download_targets", Title = "Get download targets", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
        [Description("Returns the download targets of a PLC as 'mode / PC interface / target interface' - the three values 'download_to_plc' takes.")]
        public static List<string> GetDownloadTargets(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath)
        {
            try
            {
                return Portal.GetDownloadTargets(softwarePath);
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
            }
        }

        [WriteTool]
        [McpServerTool(Name = "download_to_plc", Title = "Download to PLC", Destructive = true, OpenWorld = false, UseStructuredContent = true)]
        [Description("Download hardware configuration and/or software to a PLC or a simulated PLC. The target must already be running and reachable: this server does not start PLCSIM. Take the three interface values from 'get_download_targets'. There is no preview: a call loads. The CPU is neither stopped nor started unless stopPlc / startPlc say so; a hardware download normally needs stopPlc. The response lists every step, its answer and the messages of the result")]
        public static ResponseDownloadResult DownloadToPlc(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("modeName: download mode, first part of a target (e.g. 'PN/IE')")] string modeName,
            [Description("pcInterfaceName: PC interface, second part of a target (e.g. 'Siemens PLCSIM Virtual Ethernet Adapter')")] string pcInterfaceName,
            [Description("targetInterfaceName: target interface, third part of a target (e.g. '1 X1')")] string targetInterfaceName,
            [Description("hardware: download the hardware configuration")] bool hardware,
            [Description("software: download the software")] bool software,
            [Description("stopPlc: allow the CPU to be stopped when the download requires it (default false)")] bool stopPlc = false,
            [Description("startPlc: start the CPU after the download (default false)")] bool startPlc = false,
            [Description("maxMessages: how many informational result messages to return (default 40); errors and warnings are always returned in full")] int maxMessages = 40,
            [Description("selections: optional answers that override the defaults, as 'StepType=Option' pairs separated by commas, e.g. 'OverwriteSystemData=Overwrite,StopModules=StopAll'. Step types and their options are listed under 'steps' in every response")] string selections = "")
        {
            return GuardedNoTransaction(nameof(DownloadToPlc), () =>
            {
                var outcome = Portal.DownloadToPlc(
                    softwarePath, modeName, pcInterfaceName, targetInterfaceName,
                    hardware, software, stopPlc, startPlc, ParseSelections(selections));

                var unanswered = outcome.Steps.Where(s => !s.Answered).Select(s => s.Type).Distinct().ToList();

                var summary = $"Download to '{outcome.Target}' finished with state '{outcome.State}': {outcome.ErrorCount} error(s), {outcome.WarningCount} warning(s).";

                if (unanswered.Count > 0)
                {
                    summary += $" Steps left at TIA Portal's own preset, because the server has no answer for them: {string.Join(", ", unanswered)} - see 'steps' for the preset and the options, and pass 'selections' to decide differently.";
                }

                return new ResponseDownloadResult
                {
                    Message = summary,
                    State = outcome.State,
                    ErrorCount = outcome.ErrorCount,
                    WarningCount = outcome.WarningCount,
                    Target = outcome.Target,
                    Hardware = hardware,
                    Software = software,
                    Steps = outcome.Steps,
                    Messages = LimitMessages(outcome.Messages, maxMessages)
                };
            });
        }

        /// <summary>
        /// A full download reports one line per loaded block - well over a hundred on a real
        /// PLC. Errors and warnings are all kept; the informational lines are cut off after
        /// <paramref name="maxInformational"/>, with a closing line saying how many were left out.
        /// </summary>
        internal static List<DownloadMessage> LimitMessages(List<DownloadMessage> messages, int maxInformational)
        {
            var limit = Math.Max(0, maxInformational);
            var result = new List<DownloadMessage>();
            var informational = 0;
            var omitted = 0;

            foreach (var message in messages)
            {
                var important = message.State.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0
                                || message.State.IndexOf("Warning", StringComparison.OrdinalIgnoreCase) >= 0;

                if (important || informational < limit)
                {
                    result.Add(message);

                    if (!important)
                    {
                        informational++;
                    }
                }
                else
                {
                    omitted++;
                }
            }

            if (omitted > 0)
            {
                result.Add(new DownloadMessage
                {
                    Depth = 0,
                    State = "Information",
                    Text = $"{omitted} more informational message(s) not shown; raise 'maxMessages' to see them."
                });
            }

            return result;
        }

        /// <summary>Parses "StepType=Option,StepType=Option" into a lookup; an empty text means no overrides.</summary>
        internal static Dictionary<string, string> ParseSelections(string? selections)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in (selections ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split(new[] { '=' }, 2);

                if (parts.Length != 2 || parts[0].Trim().Length == 0 || parts[1].Trim().Length == 0)
                {
                    throw new McpException(
                        $"'{pair.Trim()}' is not a valid selection. Write 'StepType=Option', e.g. 'OverwriteSystemData=Overwrite', and separate several with commas.");
                }

                result[parts[0].Trim()] = parts[1].Trim();
            }

            return result;
        }
    }

    public class ResponseDownloadResult
    {
        public string Message { get; set; } = string.Empty;

        /// <summary>State of the download result.</summary>
        public string State { get; set; } = string.Empty;

        public int ErrorCount { get; set; }

        public int WarningCount { get; set; }

        /// <summary>The target as "mode / PC interface / target interface".</summary>
        public string Target { get; set; } = string.Empty;

        public bool Hardware { get; set; }

        public bool Software { get; set; }

        /// <summary>Every configuration step TIA Portal raised, with its options and the answer given.</summary>
        public IEnumerable<DownloadStep>? Steps { get; set; }

        /// <summary>The message tree of the result, flattened; 'depth' gives the nesting.</summary>
        public IEnumerable<DownloadMessage>? Messages { get; set; }
    }
}
