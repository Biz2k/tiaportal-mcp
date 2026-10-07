using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
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

        public class ResponseAccessibleDevices : ResponseMessage
        {
            public AccessibleDevicesResult? Result { get; set; }
        }

        [McpServerTool(Name = "get_accessible_devices", Title = "Search the network for accessible devices", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
        [Description("Search the network behind a PC interface for devices, as 'Online access > Update accessible devices' in TIA Portal does: name, address, MAC address of each device found - a PLC, a PLCSIM instance. Also returns 'pcAddresses', the addresses of the PC interface itself - a device outside their subnets is found but cannot be loaded - and 'downloadAddresses', the addresses the project gives the PLC. The list of TIA Portal can lag behind: a device started or readdressed a moment ago may be missing or shown at its old address. softwarePath names a PLC of the project, whose download settings give the PC interfaces")]
        public static ResponseAccessibleDevices GetAccessibleDevices(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("pcInterfaceName: PC interface, second part of a target of 'get_download_targets' (e.g. 'Siemens PLCSIM Virtual Ethernet Adapter')")] string pcInterfaceName,
            [Description("modeName: first part of a target (default 'PN/IE')")] string modeName = "PN/IE")
        {
            try
            {
                var result = Portal.GetAccessibleDevices(softwarePath, pcInterfaceName, modeName);

                return new ResponseAccessibleDevices
                {
                    Result = result,
                    Message = $"{result.Devices.Count} device(s) found on '{result.PcInterface}'" + (result.Devices.Count > 0 ? ": " + string.Join(", ", result.Devices.Select(d => $"{d.Name} ({d.Address})")) : string.Empty),
                    Meta = Ok(new JsonObject())
                };
            }
            catch (Exception ex) when (ex is not McpException)
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
            [Description("selections: optional answers that override the defaults, as 'StepType=Option' pairs separated by commas, e.g. 'OverwriteSystemData=Overwrite,StopModules=StopAll'. Step types and their options are listed under 'steps' in every response")] string selections = "",
            [Description("downloadUserManagement: what to do with the user management data (users, roles) when the CPU holds data that differs from the project: 'keep' (default) leaves the CPU's data as it is, 'update' takes the users of the project but keeps the CPU's passwords, 'overwrite' replaces all of it by the project's data and resets the passwords. The same answer can be given as 'UserManagementDownload=<option>' in selections, which wins")] string downloadUserManagement = "keep",
            [Description("targetAddress: the address the device answers at now, when it is not the address of the project - an empty PLCSIM instance, a new PLC, a PLC whose address the project changed. The device has to answer from this PC (see 'pcAddresses' of 'get_accessible_devices'). When hardware is loaded the device takes the address of the project and the rest of the download fails: repeat the call without targetAddress to finish. Empty (default) goes to the address of the project")] string targetAddress = "",
            [Description("passwords: the passwords the PLC asks for during the download, by the type of the step that asks - e.g. {\"ModuleWriteAccessPassword\": \"...\"} for the access level, {\"PlcMasterSecretPassword\": \"...\"} for the protection of confidential PLC configuration data; the key \"*\" answers every step that asks. A step that asked and got none is named under 'steps'. Use only passwords the user of this conversation gave; never make one up, never repeat it in your answer")] Dictionary<string, string>? passwords = null,
            [Description("trustDevice: accept the certificate of the device when TIA Portal cannot verify it (its dialog '... might not be a trustworthy device'): a PLCSIM instance and a PLC with a self-signed certificate show one on the first connection. Default false: the download is then refused and the answer says what TIA Portal found. Set true only after the user confirmed that this is the device they mean")] bool trustDevice = false)
        {
            return GuardedNoTransaction(nameof(DownloadToPlc), () =>
            {
                var outcome = Portal.DownloadToPlc(
                    softwarePath, modeName, pcInterfaceName, targetInterfaceName,
                    hardware, software, stopPlc, startPlc, ParseSelections(selections), downloadUserManagement, targetAddress, passwords, trustDevice);

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
                    Connection = outcome.Connection.Count > 0 ? outcome.Connection : null,
                    Parts = outcome.Messages.Where(m => m.Depth == 0).ToList(),
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

        public IEnumerable<string>? Connection { get; set; }

        /// <summary>
        /// The top-level messages of the result with their own state, errors and warnings. TIA Portal reports the parts of a
        /// download (the hardware configuration, the software) as separate top-level entries; the total above is their sum.
        /// Not checked against a real download (needs a PLC or PLCSIM).
        /// </summary>
        public IEnumerable<DownloadMessage>? Parts { get; set; }

        /// <summary>The message tree of the result, flattened; 'depth' gives the nesting.</summary>
        public IEnumerable<DownloadMessage>? Messages { get; set; }
    }
}
