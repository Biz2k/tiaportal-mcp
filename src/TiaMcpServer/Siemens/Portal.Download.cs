using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.Connection;
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

        public int ErrorCount { get; set; }

        public int WarningCount { get; set; }
    }

    public class DownloadOutcome
    {
        public string State { get; set; } = "Unknown";

        public int ErrorCount { get; set; }

        public int WarningCount { get; set; }

        public string Target { get; set; } = string.Empty;

        public List<DownloadStep> Steps { get; set; } = new List<DownloadStep>();

        /// <summary>What the device asked while the connection was made - its certificate, a password - and what was answered.</summary>
        public List<string> Connection { get; set; } = new List<string>();

        public List<DownloadMessage> Messages { get; set; } = new List<DownloadMessage>();
    }

    /// <summary>A device the search on a PC interface found ("Online access > Update accessible devices").</summary>
    public class AccessibleDevice
    {
        public string? Name { get; set; }

        public string? Address { get; set; }

        public string? DeviceSeries { get; set; }

        public string? MacAddress { get; set; }
    }

    public class AccessibleDevicesResult
    {
        public string? PcInterface { get; set; }

        /// <summary>The IPv4 addresses of that adapter of the PC, with prefix length - a device outside all of them is found by the search but cannot be loaded.</summary>
        public List<string> PcAddresses { get; set; } = new List<string>();

        public List<string> Notes { get; set; } = new List<string>();

        public List<AccessibleDevice> Devices { get; set; } = new List<AccessibleDevice>();

        /// <summary>The addresses 'download_to_plc' takes as targetAddress: those TIA Portal knows on the subnets of this PC interface.</summary>
        public List<string> DownloadAddresses { get; set; } = new List<string>();
    }

    public partial class Portal
    {
        /// <summary>
        /// The search of "Online access": ConfigurationPcInterface.GetAccessibleDevices(). Found on V21 (2026-10-07):
        /// it finds a PLCSIM instance whose address the PC cannot even ping (the adapter sat in another IP subnet),
        /// within a second.
        /// </summary>
        public AccessibleDevicesResult GetAccessibleDevices(string softwarePath, string pcInterfaceName, string modeName)
        {
            return Operation.Run(_logger, nameof(GetAccessibleDevices), PortalErrorCode.InvalidState, () =>
            {
                var pcInterface = RequirePcInterface(RequireDownloadProvider(softwarePath), string.IsNullOrWhiteSpace(modeName) ? "PN/IE" : modeName, pcInterfaceName);
                var result = new AccessibleDevicesResult { PcInterface = pcInterface.Name };

                foreach (var device in pcInterface.GetAccessibleDevices())
                {
                    result.Devices.Add(new AccessibleDevice { Name = device.Name, Address = device.Address, DeviceSeries = string.IsNullOrEmpty(device.DeviceSeries) ? null : device.DeviceSeries, MacAddress = device.MACAddress });
                }

                result.DownloadAddresses = SubnetAddresses(pcInterface).Select(a => a.Address).Distinct().ToList();

                var own = PcAddressesOf(pcInterface.Name);

                result.PcAddresses = own.Select(a => $"{a.Address}/{a.Prefix}").ToList();

                foreach (var device in result.Devices)
                {
                    if (own.Count > 0 && System.Net.IPAddress.TryParse(device.Address, out var ip) && !own.Any(a => SameSubnet(a.Address, ip, a.Prefix)))
                    {
                        result.Notes.Add($"'{device.Name}' at {device.Address} is in none of the subnets of this PC interface ({string.Join(", ", result.PcAddresses)}): the search finds it, a download cannot connect to it. " +
                                         "Either the PC interface gets an address in the subnet of the device (the user's to do; the download dialog of TIA Portal adds one by itself), or the device and the PLC of the project get an address in a subnet of the PC interface.");
                    }
                }

                return result;
            },
            ("softwarePath", softwarePath), ("pcInterfaceName", pcInterfaceName));
        }

        /// <summary>The IPv4 addresses Windows has on the adapter TIA Portal calls by this name (its description).</summary>
        private static List<(System.Net.IPAddress Address, int Prefix)> PcAddressesOf(string pcInterfaceName)
        {
            var result = new List<(System.Net.IPAddress, int)>();

            try
            {
                foreach (var adapter in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (!string.Equals(adapter.Description, pcInterfaceName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foreach (var address in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if (address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            result.Add((address.Address, address.PrefixLength));
                        }
                    }
                }
            }
            catch (Exception)
            {
                // the addresses are a hint; the search does not depend on them
            }

            return result;
        }

        /// <summary>Whether something answers a ping at the address; false for anything that is no IPv4 address.</summary>
        private static bool Answers(string address)
        {
            try
            {
                if (!System.Net.IPAddress.TryParse(address, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return false;
                }

                using (var ping = new System.Net.NetworkInformation.Ping())
                {
                    return ping.Send(ip, 1000)?.Status == System.Net.NetworkInformation.IPStatus.Success;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool SameSubnet(System.Net.IPAddress a, System.Net.IPAddress b, int prefix)
        {
            var x = a.GetAddressBytes();
            var y = b.GetAddressBytes();

            for (var bit = 0; bit < prefix && bit < 32; bit++)
            {
                var mask = (byte)(0x80 >> (bit % 8));

                if ((x[bit / 8] & mask) != (y[bit / 8] & mask))
                {
                    return false;
                }
            }

            return true;
        }

        private static ConfigurationPcInterface RequirePcInterface(DownloadProvider provider, string modeName, string pcInterfaceName)
        {
            var modes = provider.Configuration.Modes.ToList();
            var mode = modes.FirstOrDefault(m => string.Equals(m.Name, modeName, StringComparison.OrdinalIgnoreCase))
                ?? throw new PortalException(PortalErrorCode.NotFound, $"Mode '{modeName}' not found. Available: {Quote(modes.Select(m => m.Name))}.");

            return mode.PcInterfaces.FirstOrDefault(p => string.Equals(p.Name, pcInterfaceName, StringComparison.OrdinalIgnoreCase))
                ?? throw new PortalException(PortalErrorCode.NotFound, $"PC interface '{pcInterfaceName}' not found in mode '{modeName}'. Available: {Quote(mode.PcInterfaces.Select(p => p.Name))}.");
        }

        /// <summary>The addresses TIA Portal itself offers on the subnets of a PC interface, gateways included.</summary>
        private static List<ConfigurationAddress> SubnetAddresses(ConfigurationPcInterface pcInterface)
        {
            var addresses = new List<ConfigurationAddress>();

            foreach (var subnet in pcInterface.Subnets)
            {
                addresses.AddRange(subnet.Addresses);

                foreach (var gateway in subnet.Gateways)
                {
                    addresses.AddRange(gateway.Addresses);
                }
            }

            return addresses;
        }

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
                    DescribeMissingSoftware(softwarePath));

            var deviceItem = softwareContainer.Parent as DeviceItem
                ?? throw new PortalException(PortalErrorCode.InvalidState, "Parent of software is not a DeviceItem.");

            return deviceItem.GetService<DownloadProvider>()
                ?? (softwareContainer.Software as IEngineeringServiceProvider)?.GetService<DownloadProvider>()
                ?? ((IEngineeringServiceProvider)softwareContainer).GetService<DownloadProvider>()
                ?? throw new PortalException(PortalErrorCode.NotSupported, "Download is not supported for this device.");
        }

        /// <param name="stopPlc">Allow TIA Portal to stop the CPU when the download requires it.</param>
        /// <param name="startPlc">Start the CPU again after the download.</param>
        /// <param name="userManagement">The answer to the step 'UserManagementDownload': keep, update or overwrite (see <see cref="DownloadUserManagement"/>).</param>
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
            IDictionary<string, string>? selections = null,
            string userManagement = "keep",
            string? targetAddress = null,
            IDictionary<string, string>? passwords = null,
            bool trustDevice = false)
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

                // Checked before the download starts: a wrong word must not cost a download.
                answers["UserManagementDownload"] = new[] { DownloadUserManagement.OptionFor(userManagement) };

                if (selections != null)
                {
                    foreach (var pair in selections)
                    {
                        answers[pair.Key] = new[] { pair.Value };
                    }
                }

                // An address TIA Portal offers on the subnets of the PC interface is a configuration of its own. Any other
                // address is made with target.Addresses.Create(...) and goes into the SECOND argument of Download - handed
                // over as the configuration itself it closed TIA Portal (2026-10-07, with a device that was not reachable).
                ConfigurationAddress? address = null;

                // A device that sits at another address than the project gives it - found by the search - is loaded with
                // the overload Download(targetInterface, customAddress, ...).
                ConfigurationAddress? custom = null;

                if (!string.IsNullOrWhiteSpace(targetAddress))
                {
                    var wanted = targetAddress!.Trim();
                    var known = SubnetAddresses(pcInterface);

                    address = known.FirstOrDefault(a => string.Equals(a.Address, wanted, StringComparison.OrdinalIgnoreCase));

                    if (address == null)
                    {
                        // Only a device that answers at the address is tried. The search is no proof of that: it finds
                        // devices the PC cannot reach by IP, and after a download changed the address of a PLCSIM instance
                        // it went on naming the old one (2026-10-07).
                        if (!Answers(wanted))
                        {
                            var own = PcAddressesOf(pcInterface.Name).Select(a => $"{a.Address}/{a.Prefix}").ToList();

                            throw new PortalException(PortalErrorCode.NotFound,
                                $"No device answers at '{targetAddress}' from this PC, so nothing was loaded. PC interface '{pcInterface.Name}' has {(own.Count == 0 ? "no IPv4 address" : string.Join(", ", own))}: " +
                                "a device outside these subnets is found by 'get_accessible_devices' but cannot be loaded until the PC interface has an address in its subnet " +
                                "(the user's to add; the download dialog of TIA Portal adds one by itself) or the device is given an address in one of them.");
                        }

                        custom = target.Addresses.Create(wanted);
                    }
                }

                var outcome = new DownloadOutcome
                {
                    Target = $"{mode.Name} / {pcInterface.Name} / {target.Name}" + (address == null && custom == null ? string.Empty : $" at {(address ?? custom)!.Address}")
                };

                var stage = "connecting to the target and preparing the download";

                // The two callbacks below must never let an exception escape. Openness turns an
                // exception thrown inside a download callback into a NonRecoverableException and
                // closes the TIA Portal instance with it - unsaved work included. A 'dry run'
                // that aborted from the first callback did exactly that (2026-10-05). There is no
                // way to preview a download: AnswerDownloadStep catches everything itself.

                // What the device asks while TIA Portal connects to it: whether its certificate is trusted (the dialog
                // "... might not be a trustworthy device"), a password for reading. Without an answer the connection is
                // refused ("Connect to module ... failed"). Like every Openness callback this one must never throw.
                global::Siemens.Engineering.Online.OnlineConfigurationDelegate legitimation = configuration =>
                {
                    try
                    {
                        if (configuration is global::Siemens.Engineering.Online.Configurations.TlsVerificationConfiguration tls)
                        {
                            var info = (tls.VerificationInfo ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();

                            if (trustDevice)
                            {
                                tls.CurrentSelection = global::Siemens.Engineering.Online.Configurations.TlsVerificationConfigurationSelection.Trusted;
                                outcome.Connection.Add($"The certificate of '{tls.PlcName}' was accepted as trusted, as trustDevice says. TIA Portal about it: {info}");
                            }
                            else
                            {
                                outcome.Connection.Add($"'{tls.PlcName}' shows a certificate TIA Portal cannot verify, and it was NOT accepted: {info} If the user confirms that this is the device they mean, repeat the call with trustDevice=true.");
                            }
                        }
                        else if (configuration is global::Siemens.Engineering.Online.Configurations.OnlinePasswordConfiguration password)
                        {
                            var name = configuration.GetType().Name;
                            var given = passwords?.FirstOrDefault(p => string.Equals(p.Key?.Trim(), name, StringComparison.OrdinalIgnoreCase)).Value
                                        ?? passwords?.FirstOrDefault(p => p.Key?.Trim() == "*").Value;

                            if (string.IsNullOrEmpty(given))
                            {
                                outcome.Connection.Add($"The device asks for a password to connect ({name}) and none was given: pass it in 'passwords' as {{\"{name}\": \"...\"}}.");
                            }
                            else
                            {
                                password.SetPassword(Secret(given, "password"));
                                outcome.Connection.Add($"The password for {name} given in 'passwords' was supplied.");
                            }
                        }
                        else
                        {
                            outcome.Connection.Add($"The device asks for '{configuration.GetType().Name}' to connect; the server has no answer for it.");
                        }
                    }
                    catch (Exception ex)
                    {
                        outcome.Connection.Add($"Could not answer '{configuration?.GetType().Name}': {ErrorText.Describe(ex)}");
                    }
                };

                downloadProvider.Configuration.OnlineLegitimation += legitimation;

                try
                {
                    DownloadConfigurationDelegate before = configuration =>
                    {
                        stage = "answering the configuration steps before loading";
                        outcome.Steps.Add(AnswerDownloadStep(configuration, "before", answers, passwords));
                        stage = "loading";
                    };

                    DownloadConfigurationDelegate after = configuration =>
                    {
                        stage = "answering the configuration steps after loading";
                        outcome.Steps.Add(AnswerDownloadStep(configuration, "after", answers, passwords));
                    };

                    var result = custom != null
                        ? downloadProvider.Download(target, custom, before, after, options)
                        : downloadProvider.Download(address != null ? (IConfiguration)address : target, before, after, options);

                    outcome.State = result.State.ToString();
                    outcome.ErrorCount = result.ErrorCount;
                    outcome.WarningCount = result.WarningCount;

                    CollectDownloadMessages(result.Messages, 0, outcome.Messages);

                    if (custom != null && hardware && outcome.ErrorCount > 0)
                    {
                        outcome.Messages.Insert(0, new DownloadMessage
                        {
                            State = "Information",
                            Text = $"The download went to '{custom.Address}', which is not the address of the project. Seen on V21: the hardware configuration is loaded, the device takes the address of the project, " +
                                   "and the rest of the download fails because the device is no longer at the address it was reached at. Repeat the download without 'targetAddress' to finish it."
                        });
                    }
                }
                catch (Exception ex) when (ex is not PortalException)
                {
                    var seen = outcome.Steps.Count == 0
                        ? "No configuration step was reached, so the target was probably not reachable: check that the PLC or the PLCSIM instance is running and that its address matches the project ('get_accessible_devices' shows what is on the network). If the search finds the device and the download still cannot connect, the PC interface has no IP address in the subnet of the device: the download dialog of TIA Portal adds one by itself (\"An additional IP address was added\"), Openness does not - the user adds it to the adapter, or loads once from TIA Portal."
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

                    if (outcome.Connection.Count > 0)
                    {
                        seen = string.Join(" ", outcome.Connection);
                    }

                    // The reasons TIA Portal names itself are not a matter of reachability.
                    if (reason.IndexOf("is not compatible with the module configured offline", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        seen = "The device was reached, but it is another kind of CPU than the project has: a PLCSIM instance has to be created for the family of the CPU (an ET 200SP CPU needs an ET 200SP instance, not an S7-1500 one). Nothing was loaded.";
                    }
                    else if (reason.IndexOf("Compilation of hardware configuration", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        seen = "The device was reached, but the hardware configuration of the project does not compile, so nothing was loaded. Compile the hardware in TIA Portal to see the errors - a common one after changing an address: a connection whose two ends are now in different subnets.";
                    }

                    throw new PortalException(PortalErrorCode.InvalidState,
                        $"Download to '{outcome.Target}' failed while {stage}: {reason}. {(refused == null ? string.Empty : refused + " ")}{seen}", null, ex);
                }
                finally
                {
                    try
                    {
                        downloadProvider.Configuration.OnlineLegitimation -= legitimation;
                    }
                    catch (Exception)
                    {
                        // the provider may be gone with the connection
                    }
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

        private DownloadStep AnswerDownloadStep(object configuration, string phase, Dictionary<string, string[]> answers, IDictionary<string, string>? passwords = null)
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
                    var setPassword = type.GetMethod("SetPassword");

                    if (setPassword == null)
                    {
                        step.Note = "This step offers no selection; it was left as it is.";

                        return step;
                    }

                    // by the type of the step, or '*' for every step that asks
                    var given = passwords?.FirstOrDefault(p => string.Equals(p.Key?.Trim(), type.Name, StringComparison.OrdinalIgnoreCase)).Value
                                ?? passwords?.FirstOrDefault(p => p.Key?.Trim() == "*").Value;

                    if (string.IsNullOrEmpty(given))
                    {
                        step.Note = $"This step asks for a password and none was given. Ask the user for it and pass it in 'passwords' as {{\"{type.Name}\": \"...\"}}.";

                        return step;
                    }

                    setPassword.Invoke(configuration, new object[] { Secret(given, "password") });
                    step.Answered = true;
                    step.Note = "The password given in 'passwords' was supplied.";

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
                    Text = message.Message ?? string.Empty,
                    ErrorCount = message.ErrorCount,
                    WarningCount = message.WarningCount
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
