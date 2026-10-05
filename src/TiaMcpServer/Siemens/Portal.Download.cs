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
}
}
