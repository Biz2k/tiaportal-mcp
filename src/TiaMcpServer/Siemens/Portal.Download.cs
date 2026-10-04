using System;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.Download;
using Siemens.Engineering.HW;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        public List<string> GetDownloadTargets(string softwarePath)
        {
            return Operation.Run(_logger, nameof(GetDownloadTargets), PortalErrorCode.InvalidState, () =>
            {
                var softwareContainer = GetSoftwareContainer(softwarePath)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"Software not found at '{softwarePath}'.");

                var deviceItem = softwareContainer.Parent as DeviceItem
                    ?? throw new PortalException(PortalErrorCode.InvalidState, "Parent of software is not a DeviceItem.");

                var downloadProvider = deviceItem.GetService<DownloadProvider>();
                if (downloadProvider == null && softwareContainer.Software is IEngineeringServiceProvider sp)
                {
                    downloadProvider = sp.GetService<DownloadProvider>();
                }
                else if (downloadProvider == null)
                {
                    downloadProvider = ((IEngineeringServiceProvider)softwareContainer).GetService<DownloadProvider>();
                }
                
                if (downloadProvider == null)
                {
                    throw new PortalException(PortalErrorCode.NotSupported, "Download is not supported for this device.");
                }

                var targets = new List<string>();
                var config = downloadProvider.Configuration;
                foreach (var mode in config.Modes)
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

        public DownloadResult? DownloadToPlc(string softwarePath, string modeName, string pcInterfaceName, string targetInterfaceName, bool hardware, bool software)
        {
            return Operation.Run(_logger, nameof(DownloadToPlc), PortalErrorCode.InvalidState, () =>
            {
                var softwareContainer = GetSoftwareContainer(softwarePath)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"Software not found at '{softwarePath}'.");

                var deviceItem = softwareContainer.Parent as DeviceItem
                    ?? throw new PortalException(PortalErrorCode.InvalidState, "Parent of software is not a DeviceItem.");

                var downloadProvider = deviceItem.GetService<DownloadProvider>();
                if (downloadProvider == null && softwareContainer.Software is IEngineeringServiceProvider sp2)
                {
                    downloadProvider = sp2.GetService<DownloadProvider>();
                }
                else if (downloadProvider == null)
                {
                    downloadProvider = ((IEngineeringServiceProvider)softwareContainer).GetService<DownloadProvider>();
                }
                
                if (downloadProvider == null)
                {
                    throw new PortalException(PortalErrorCode.NotSupported, "Download is not supported for this device.");
                }

                var config = downloadProvider.Configuration;
                var mode = config.Modes.FirstOrDefault(m => string.Equals(m.Name, modeName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"Mode '{modeName}' not found.");

                var pcInterface = mode.PcInterfaces.FirstOrDefault(p => string.Equals(p.Name, pcInterfaceName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"PC Interface '{pcInterfaceName}' not found in mode '{modeName}'.");

                var target = pcInterface.TargetInterfaces.FirstOrDefault(t => string.Equals(t.Name, targetInterfaceName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"Target Interface '{targetInterfaceName}' not found in PC interface '{pcInterfaceName}'.");

                DownloadOptions options = DownloadOptions.None;
                if (hardware) options |= DownloadOptions.Hardware;
                if (software) options |= DownloadOptions.Software;

                var result = downloadProvider.Download(target, 
                    preConfig => {
                        // PreConfigureDownload
                        try 
                        {
                            // Try to accept everything dynamically if type is not exposed
                            dynamic c = preConfig;
                            c.AcceptMessages = true;
                            if (c.CheckBeforeDownload != null) {
                                c.CheckBeforeDownload.AcceptAll = true;
                            }
                        } 
                        catch { }
                    }, 
                    postConfig => {
                        // PostConfigureDownload
                    }, 
                    options);
                    
                return result;
            },
            ("softwarePath", softwarePath));
        }
        public string StartPlcSimAndDownloadAll()
        {
            var output = new System.Text.StringBuilder();
            
            output.AppendLine("Loading PLCSIM Advanced API...");
            object manager = null;
            Type managerType = null;
            Type eCpuType = null;
            Type iInstanceType = null;
            Type ePlcInterface = null;
            try
            {
                var asm = System.Reflection.Assembly.LoadFrom(@"C:\Program Files\Siemens\Automation\PLCSIM_V21\resources\bin\wwwroot\assets\lib\runtime\Siemens.Simatic.Simulation.Runtime.Api.x64.dll");
                managerType = asm.GetType("Siemens.Simatic.Simulation.Runtime.SimulationRuntimeManager");
                manager = managerType.GetProperty("LocalRuntimeManagerInstance").GetValue(null, null);
                eCpuType = asm.GetType("Siemens.Simatic.Simulation.Runtime.ECPUType");
                iInstanceType = asm.GetType("Siemens.Simatic.Simulation.Runtime.IInstance");
                ePlcInterface = asm.GetType("Siemens.Simatic.Simulation.Runtime.EPLCInterface");
            }
            catch (Exception ex)
            {
                output.AppendLine($"Failed to load PLCSIM Advanced API: {ex.Message}");
                return output.ToString();
            }

            object selectedInterface = null;
            string selectedInterfaceDesc = null;
            try
            {
                output.AppendLine("Setting NetworkMode to TCPIPMultipleAdapter...");
                var eNetworkMode = managerType.Assembly.GetType("Siemens.Simatic.Simulation.Runtime.ENetworkMode");
                managerType.GetProperty("NetworkMode").SetValue(manager, Enum.Parse(eNetworkMode, "TCPIPMultipleAdapter"), null);

                output.AppendLine("Finding physical interface...");
                var netInterfaces = (System.Collections.IEnumerable)managerType.GetProperty("NetInterfaces").GetValue(manager, null);
                foreach (var ni in netInterfaces)
                {
                    var name = ni.GetType().GetField("interfaceName").GetValue(ni).ToString();
                    var desc = ni.GetType().GetField("interfaceDescription").GetValue(ni).ToString();
                    if (!name.Contains("Virtual") && !name.Contains("vEthernet") && !name.Contains("VMware") && name.Contains("Ethernet"))
                    {
                        selectedInterface = ni;
                        selectedInterfaceDesc = desc;
                        break;
                    }
                }

                if (selectedInterface == null)
                {
                    output.AppendLine("Could not find a suitable physical interface for PLCSIM Advanced.");
                    return output.ToString();
                }

                var ifName = selectedInterface.GetType().GetField("interfaceName").GetValue(selectedInterface).ToString();
                var ifIndex = (uint)selectedInterface.GetType().GetField("interfaceIndex").GetValue(selectedInterface);
                output.AppendLine($"Selected Interface: {ifName} / {selectedInterfaceDesc} (Index: {ifIndex})");

                output.AppendLine("\nAttempting to download all PLCs in the currently open project...");
                var project = Project;
                if (project == null) return output.ToString() + "\nNo open project found.";

                foreach (var device in project.Devices)
                {
                    var deviceItem = device.DeviceItems.FirstOrDefault(di => di.Classification == DeviceItemClassifications.CPU);
                    if (deviceItem != null)
                    {
                        output.AppendLine($"\n--- PLC: {device.Name} ({deviceItem.Name}) ---");
                        
                        string plcIp = null;
                        foreach (var item in device.DeviceItems) {
                            foreach (var subItem in item.DeviceItems) {
                                var netIf = subItem.GetService<global::Siemens.Engineering.HW.Features.NetworkInterface>();
                                if (netIf != null) {
                                    foreach (var node in netIf.Nodes) {
                                        try { 
                                            var addr = node.GetAttribute("Address")?.ToString(); 
                                            if (!string.IsNullOrEmpty(addr) && addr.Contains(".")) {
                                                plcIp = addr;
                                                break;
                                            }
                                        } catch { }
                                    }
                                }
                                if (!string.IsNullOrEmpty(plcIp)) break;
                            }
                            if (!string.IsNullOrEmpty(plcIp)) break;
                        }
                        output.AppendLine($"Found PROFINET IP: {plcIp ?? "None"}");
                        
                        string instanceName = System.Text.RegularExpressions.Regex.Replace(device.Name, "[^a-zA-Z0-9_-]", "");
                        if (string.IsNullOrEmpty(instanceName)) instanceName = "TiaMcpInstance";
                        
                        // Setup Instance
                        try {
                            try {
                                var createInterfaceMethod = managerType.GetMethod("CreateInterface", new Type[] { typeof(string) });
                                var existingInstance = createInterfaceMethod.Invoke(manager, new object[] { instanceName });
                                if (existingInstance != null) {
                                    iInstanceType.GetMethod("PowerOff", new Type[] { typeof(uint) }).Invoke(existingInstance, new object[] { 60000u });
                                    iInstanceType.GetMethod("UnregisterInstance").Invoke(existingInstance, null);
                                }
                            } catch { }

                            output.AppendLine($"Registering {instanceName}...");
                            var cpuTypeVal = Enum.Parse(eCpuType, "CPU1500_Unspecified");
                            var instance = managerType.GetMethod("RegisterInstance", new Type[] { eCpuType, typeof(string) }).Invoke(manager, new object[] { cpuTypeVal, instanceName });
                            
                            output.AppendLine($"Mapping IE1 to {ifIndex}...");
                            var ie1 = Enum.Parse(ePlcInterface, "IE1");
                            iInstanceType.GetMethod("SetNetInterfaceMapping", new Type[] { ePlcInterface, typeof(uint) }).Invoke(instance, new object[] { ie1, ifIndex });
                            
                            output.AppendLine("Configuring Virtual Switch bindings...");
                            try
                            {
                                var setBindingsMethod = managerType.GetMethod("SetNetInterfaceBindings", new Type[] { typeof(uint) });
                                if (setBindingsMethod != null) setBindingsMethod.Invoke(manager, new object[] { 0u });
                                else {
                                    var method = managerType.GetMethod("SetNetInterfaceBindings", Type.EmptyTypes);
                                    if (method != null) method.Invoke(manager, null);
                                }
                            }
                            catch (Exception ex)
                            {
                                output.AppendLine($"SetNetInterfaceBindings warning: {ex.InnerException?.Message ?? ex.Message}");
                            }

                            output.AppendLine($"Powering on...");
                            iInstanceType.GetMethod("PowerOn", new Type[] { typeof(uint) }).Invoke(instance, new object[] { 60000u });
                            output.AppendLine($"Powered on {instanceName}.");
                            System.Threading.Thread.Sleep(5000);
                            
                            if (!string.IsNullOrEmpty(plcIp)) {
                                output.AppendLine($"Setting IP {plcIp}...");
                                try {
                                    var setIpMethod = iInstanceType.GetMethod("SetIPSuite", new Type[] { typeof(uint), typeof(string), typeof(string), typeof(string), typeof(bool) });
                                    if (setIpMethod != null) setIpMethod.Invoke(instance, new object[] { 1u, plcIp, "255.255.255.0", "0.0.0.0", false });
                                    output.AppendLine("SetIPSuite completed.");
                                } catch (Exception ex) {
                                    output.AppendLine($"SetIPSuite warning: {ex.InnerException?.Message ?? ex.Message}");
                                }
                            }
                        } catch (Exception ex) {
                            output.AppendLine($"Failed during setup for {instanceName}: {ex.InnerException?.Message ?? ex.Message}");
                        }

                        try
                        {
                            var downloadProvider = deviceItem.GetService<global::Siemens.Engineering.Download.DownloadProvider>();
                            if (downloadProvider == null)
                            {
                                var swContainer = deviceItem.GetService<global::Siemens.Engineering.HW.Features.SoftwareContainer>();
                                if (swContainer != null && swContainer.Software is global::Siemens.Engineering.IEngineeringServiceProvider provider)
                                {
                                    downloadProvider = provider.GetService<global::Siemens.Engineering.Download.DownloadProvider>();
                                }
                            }

                            if (downloadProvider != null)
                            {
                                var configuration = downloadProvider.Configuration;
                                var modeObj = configuration.Modes.FirstOrDefault(m => m.Name == "PN/IE");
                                if (modeObj != null)
                                {
                                    output.AppendLine("Available PC Interfaces:");
                                    foreach (var pci in modeObj.PcInterfaces) output.AppendLine($"- {pci.Name}");
                                    // Match the physical interface description obtained from PLCSIM Advanced
                                    var pcIfObj = modeObj.PcInterfaces.FirstOrDefault(p => selectedInterfaceDesc != null && p.Name.Contains(selectedInterfaceDesc));
                                    if (pcIfObj == null) pcIfObj = modeObj.PcInterfaces.FirstOrDefault(p => selectedInterfaceDesc != null && selectedInterfaceDesc.Contains(p.Name));
                                    
                                    if (pcIfObj != null)
                                    {
                                        var targetObj = pcIfObj.TargetInterfaces.FirstOrDefault(t => t.Name.Contains("1 X1") || t.Name.Contains("1X1") || t.Name.Contains("X1"));
                                        if (targetObj == null) targetObj = pcIfObj.TargetInterfaces.FirstOrDefault();
                                        
                                        if (targetObj != null)
                                        {
                                            output.AppendLine($"Downloading to {pcIfObj.Name} / {targetObj.Name}...");
                                            var result = downloadProvider.Download(targetObj, pre => {
                                                dynamic p = pre;
                                                p.AcceptMessages = true;
                                            }, post => { }, global::Siemens.Engineering.Download.DownloadOptions.Hardware | global::Siemens.Engineering.Download.DownloadOptions.Software);

                                            output.AppendLine($"Result: State={result.State}, Errors={result.ErrorCount}");
                                            foreach (var msg in result.Messages)
                                            {
                                                output.AppendLine($" - {msg.Message}");
                                            }
                                            
                                            if (result.State == global::Siemens.Engineering.Download.DownloadResultState.Success || 
                                                result.State == global::Siemens.Engineering.Download.DownloadResultState.Warning)
                                            {
                                                output.AppendLine("Attempting to go online...");
                                                try
                                                {
                                                    var onlineProvider = deviceItem.GetService<global::Siemens.Engineering.Online.OnlineProvider>();
                                                    if (onlineProvider == null)
                                                    {
                                                        var swContainer = deviceItem.GetService<global::Siemens.Engineering.HW.Features.SoftwareContainer>();
                                                        if (swContainer != null && swContainer.Software is global::Siemens.Engineering.IEngineeringServiceProvider provider)
                                                        {
                                                            onlineProvider = provider.GetService<global::Siemens.Engineering.Online.OnlineProvider>();
                                                        }
                                                    }
                                                    
                                                    if (onlineProvider != null)
                                                    {
                                                        onlineProvider.GoOnline();
                                                        output.AppendLine("Successfully went online.");
                                                    }
                                                    else
                                                    {
                                                        output.AppendLine("OnlineProvider not available on this PLC.");
                                                    }
                                                }
                                                catch (System.Exception onlineEx)
                                                {
                                                    output.AppendLine($"Failed to go online: {onlineEx.Message}");
                                                }
                                            }
                                        }
                                        else output.AppendLine("No target interface found.");
                                    }
                                    else output.AppendLine($"No PC interface matching '{selectedInterfaceDesc}' found.");
                                }
                                else output.AppendLine("Mode PN/IE not found.");
                            }
                            else output.AppendLine("DownloadProvider not available on this PLC.");
                        }
                        catch (System.Exception ex)
                        {
                            output.AppendLine($"Download error: {ex.Message}");
                        }
                    }
                }

                return output.ToString();
            }
            catch (Exception ex)
            {
                output.AppendLine($"Error setting up PLCSIM Advanced API: {ex.InnerException?.Message ?? ex.Message}");
                return output.ToString();
            }
        }
    }
}
