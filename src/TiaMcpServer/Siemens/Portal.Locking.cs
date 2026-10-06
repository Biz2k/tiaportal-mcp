using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using System;
using System.Collections.Generic;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Siemens
{
    // The shared lock around the reads that did not go through Operation.Run.
    //
    // All Openness traffic is serialized by one lock (see Operation.cs). The methods below were written before that
    // lock existed and return their result or null without decorating errors, so they keep their bodies as private
    // '...Unlocked' methods and take the lock here. The lock is reentrant: a locked method may call another one.
    // Without it, a tool reading the project tree could run while save_project or close_project ran, and touch an
    // object that was just closed (task 22, 2026-10-06).
    public partial class Portal
    {
        /// <summary>Throws InvalidState with the reason when no project is open: for a read that would otherwise answer with an empty list.</summary>
        public void EnsureProjectOpen() => Operation.Locked(() =>
        {
            if (_project == null)
            {
                throw new PortalException(PortalErrorCode.InvalidState, NoProjectMessage);
            }
        });

        public bool ConnectPortal(bool startIfNotRunning = false, int? processId = null, string? projectPath = null) => Operation.Locked(() => ConnectPortalUnlocked(startIfNotRunning, processId, projectPath));
        public bool DisconnectPortal() => Operation.Locked(() => DisconnectPortalUnlocked());
        public State GetState() => Operation.Locked(() => GetStateUnlocked());
        public List<ProjectBase> GetProjects() => Operation.Locked(() => GetProjectsUnlocked());
        public object? GetProjectInfo() => Operation.Locked(() => GetProjectInfoUnlocked());
        public List<ProjectBase> GetSessions() => Operation.Locked(() => GetSessionsUnlocked());
        public string GetProjectTree() => Operation.Locked(() => GetProjectTreeUnlocked());
        public List<Device> GetDevices(string regexName = "") => Operation.Locked(() => GetDevicesUnlocked(regexName));
        public Device? GetDevice(string devicePath) => Operation.Locked(() => GetDeviceUnlocked(devicePath));
        public DeviceItem? GetDeviceItem(string deviceItemPath) => Operation.Locked(() => GetDeviceItemUnlocked(deviceItemPath));
        public PlcSoftware? GetPlcSoftware(string softwarePath) => Operation.Locked(() => GetPlcSoftwareUnlocked(softwarePath));
        public CompilerResult? CompileSoftware(string softwarePath, string password = "") => Operation.Locked(() => CompileSoftwareUnlocked(softwarePath, password));
        public SoftwareContainer? GetSoftwareContainer(string softwarePath) => Operation.Locked(() => GetSoftwareContainerUnlocked(softwarePath));
        public string GetSoftwareTree(string softwarePath, string sections = "all") => Operation.Locked(() => GetSoftwareTreeUnlocked(softwarePath, sections));
    }
}
