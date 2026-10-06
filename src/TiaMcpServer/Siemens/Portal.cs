using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.Multiuser;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        // closing parantheses for regex characters ommitted, because they are not relevant for regex detection
        private readonly char[] _regexChars = ['.', '^', '$', '*', '+', '?', '(', '[', '{', '\\', '|'];

        private TiaPortal? _portal;

        private ProjectBase? _project;
        public ProjectBase? Project => _project;

        private LocalSession? _session;

        /// <summary>Id of the TIA Portal process the server attached to; 0 when the server started it itself.</summary>
        private int _portalProcessId;

        private readonly ILogger<Portal>? _logger;

        #region ctor

        public Portal(ILogger<Portal>? logger = null)
        {
            _logger = logger;
            Operation.LossHandler = DetectLoss;
        }

        #endregion

        #region helper for mcp server

        public bool ProjectIsValid
        {
            get
            {
                if (_project == null)
                {
                    return false;
                }

                // Check if the project is a valid Project instance
                if ((_session == null) && (_project is Project))
                {
                    return true;
                }

                // If it's a MultiuserProject, we can also check its validity
                if ((_session != null) && (_project is MultiuserProject))
                {
                    return true;
                }

                return false;
            }
        }

        public bool IsLocalSession
        {
            get
            {
                return _session != null;
            }
        }

        public bool IsLocalProject
        {
            get
            {
                return _session == null;
            }
        }

        #endregion

        #region helper for unit tests

        public static bool IsLocalSessionFile(string sessionPath)
        {
            // Check if the path ends with '.als\d+' using regex
            var regex = new Regex(@"\.als\d+$", RegexOptions.IgnoreCase);
            return regex.IsMatch(sessionPath);
        }

        public static bool IsLocalProjectFile(string projectPath)
        {
            // Check if the path ends with '.ap\d+' using regex
            var regex = new Regex(@"\.ap\d+$", RegexOptions.IgnoreCase);
            return regex.IsMatch(projectPath);
        }

        public void Dispose()
        {
            try
            {
                (_project as Project)?.Close();
            }
            catch (Exception)
            {
                // Console.WriteLine($"Error closing the project: {ex.Message}");
            }

            try
            {
                _portal?.Dispose();
            }
            catch (Exception)
            {
                // Console.WriteLine($"Error closing the portal: {ex.Message}");
            }
        }

        #endregion

        #region portal

        /// <summary>
        /// Attaches to a running TIA Portal.
        /// </summary>
        /// <param name="startIfNotRunning">
        /// Start a new TIA Portal (with its window) when none is running. Off by default: a
        /// caller that only wanted to look must not find a new application on the user's
        /// desktop - which is what happened after a TIA Portal crash, when a plain reconnect
        /// silently opened an empty instance (2026-10-05).
        /// </param>
        /// <exception cref="PortalException">InvalidState when no TIA Portal is running and none may be started.</exception>
        public bool ConnectPortal(bool startIfNotRunning = false, int? processId = null, string? projectPath = null)
        {
            _logger?.LogInformation("Connecting to TIA Portal...");

            DropConnection();

            bool running;

            try
            {
                // connect to running TIA Portal
                var processes = TiaPortal.GetProcesses();

                running = processes.Any();

                if (running)
                {
                    var process = processes.First();

                    if (processId.HasValue || !string.IsNullOrWhiteSpace(projectPath))
                    {
                        var picked = TiaInstanceSelection.Pick(processes.Select(ToInstanceInfo).ToList(), processId, projectPath);
                        process = processes.First(p => p.Id == picked.Id);
                    }

                    _portal = process.Attach();
                    _portalProcessId = process.Id;

                    // check for existing local sessions
                    if (_portal.LocalSessions.Any())
                    {
                        _session = _portal.LocalSessions.First();
                        _project = _session.Project;
                    }
                    // checks for existing projects
                    else if (_portal.Projects.Any())
                    {
                        _project = _portal.Projects.First();
                    }

                    return true;
                }
            }
            catch (PortalException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Attaching to TIA Portal failed");

                return false;
            }

            if (processId.HasValue || !string.IsNullOrWhiteSpace(projectPath))
            {
                throw new PortalException(PortalErrorCode.NotFound,
                    "No TIA Portal instance is running, so the requested processId/projectPath cannot be matched. Start TIA Portal, or call 'connect' without them.");
            }

            if (!startIfNotRunning)
            {
                throw new PortalException(PortalErrorCode.InvalidState,
                    "TIA Portal is not running. Start it and try again, or call 'connect' with startIfNotRunning=true to let the server start a new instance.");
            }

            try
            {
                _logger?.LogInformation("Starting a new TIA Portal instance");
                _portal = new TiaPortal(TiaPortalMode.WithUserInterface);

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Starting TIA Portal failed");

                return false;
            }
        }

        /// <summary>The running TIA Portal processes, without attaching to any.</summary>
        public List<TiaInstanceInfo> GetTiaInstances()
        {
            return TiaPortal.GetProcesses().Select(ToInstanceInfo).ToList();
        }

        private static TiaInstanceInfo ToInstanceInfo(TiaPortalProcess process)
        {
            string projectPath;

            try
            {
                projectPath = process.ProjectPath?.FullName ?? string.Empty;
            }
            catch (Exception)
            {
                projectPath = string.Empty;
            }

            return new TiaInstanceInfo { Id = process.Id, ProjectPath = projectPath, Mode = process.Mode.ToString() };
        }

        public bool IsConnected()
        {
            if (_portal != null && _portalProcessId != 0 && !PortalProcessRuns())
            {
                // TIA Portal was closed since the last call (by the user, or by a call that
                // killed it): say so instead of keeping a connection to nothing.
                _logger?.LogWarning("TIA Portal (process {ProcessId}) is no longer running; the connection is dropped", _portalProcessId);
                DropConnection();
            }

            return _portal != null;
        }

        private bool PortalProcessRuns()
        {
            try
            {
                return TiaPortal.GetProcesses().Any(p => p.Id == _portalProcessId);
            }
            catch (Exception)
            {
                // Cannot tell: assume it runs, an actual failure will show soon enough.
                return true;
            }
        }

        /// <summary>Forgets everything that belongs to a TIA Portal that is gone. Does not dispose: the objects are dead.</summary>
        private void DropConnection()
        {
            _portal = null;
            _project = null;
            _session = null;
            _portalProcessId = 0;
            _inTransaction = false;
        }

        /// <summary>
        /// Decides whether an exception means TIA Portal is gone. When it is, the connection is
        /// dropped and the message tells the client what happened and what to do; an ordinary
        /// failure returns null.
        /// </summary>
        private string? DetectLoss(Exception ex)
        {
            var kind = TiaLoss.Classify(ex);

            if (kind == TiaLoss.Kind.None)
            {
                return null;
            }

            var gone = kind == TiaLoss.Kind.Fatal || (_portalProcessId != 0 && !PortalProcessRuns());

            if (gone)
            {
                _logger?.LogError(ex, "TIA Portal is gone ({Kind}); the connection is dropped", kind);
                DropConnection();

                return TiaLoss.GoneMessage(kind);
            }

            return TiaLoss.ClosedProjectMessage();
        }

        public bool DisconnectPortal()
        {
            _logger?.LogInformation("Disconnecting from TIA Portal...");

            try
            {
                _project = null;
                _session = null;

                _portal?.Dispose();
                _portal = null;

                return true;
            }
            catch (Exception)
            {
                // Handle exception if needed, e.g., log it
            }

            return false;
        }

        #endregion

        #region status

        public State GetState()
        {
            _logger?.LogInformation("Getting TIA Portal state...");

            if (IsConnected())
            {
                try
                {
                    // check for existing local sessions
                    if (_portal!.LocalSessions.Any())
                    {
                        _session = _portal.LocalSessions.First();
                        _project = _session.Project;
                    }
                    // checks for existing projects
                    else if (_portal.Projects.Any())
                    {
                        _project = _portal.Projects.First();
                    }
                }
                catch (Exception ex) when (DetectLoss(ex) != null)
                {
                    // TIA Portal went away between the process check and the read: DetectLoss
                    // has dropped the connection, and the state below says "not connected".
                }
            }

            string? project = null;
            string? session = null;

            try
            {
                project = _project?.Name;
                session = _session?.Project.Name;
            }
            catch (Exception ex) when (DetectLoss(ex) != null)
            {
                // The project object is dead: reported as not connected below.
            }

            return new State
            {
                IsConnected = _portal != null,
                Project = project ?? "-",
                Session = session ?? "-"
            };
        }

        #endregion

        #region project

        public List<ProjectBase> GetProjects()
        {
            _logger?.LogInformation("Getting open projects...");

            if (_portal == null)
            {
                _logger?.LogWarning("No TIA Portal instance available.");

                return [];
            }

            var projects = new List<ProjectBase>();

            if (_portal.Projects != null)
            {
                foreach (var project in _portal.Projects)
                {
                    projects.Add(project);
                }
            }

            return projects;
        }

        public bool OpenProject(string projectPath)
        {
            _logger?.LogInformation($"Opening project: {projectPath}");

            if (IsPortalNull())
            {
                return false;
            }

            try
            {
                var projects = GetProjects();
                var projectName = Path.GetFileNameWithoutExtension(projectPath);

                if (!string.IsNullOrEmpty(projectName) && projects.Any(p => p.Name.Equals(projectName)))
                {
                    // Project is already open
                    _project = _portal?.Projects.FirstOrDefault(p => p.Name == projectName);

                    return _project != null;
                }

                if (_project != null)
                {
                    (_project as Project)?.Close();
                    _project = null;
                }

                if (_session != null)
                {
                    _session.Close();
                    _session = null;
                }

                // see [5.3.1 Projekt öffnen, S.113]
                _project = _portal?.Projects.OpenWithUpgrade(new FileInfo(projectPath));

                return _project != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public object? GetProjectInfo()
        {
            _logger?.LogInformation("Getting project info...");

            if (IsPortalNull())
            {
                return null;
            }

            if (IsProjectNull())
            {
                return null;
            }

            var project = _project!;

            var info = new
            {
                Name = project.Name,
                Path = project.Path,
                Type = project.GetType().Name,
                IsMultiuserProject = project is MultiuserProject,
                IsLocalSession = _session != null,
                IsLocalProject = _session == null
            };

            return info;
        }

        public bool SaveProject()
        {
            _logger?.LogInformation("Saving project...");

            if (IsProjectNull())
            {
                return false;
            }

            (_project as Project)?.Save();

            return true;
        }

        public bool SaveAsProject(string path)
        {
            _logger?.LogInformation($"Saving project as: {path}");

            if (IsProjectNull())
            {
                return false;
            }

            var di = new DirectoryInfo(path);

            (_project as Project)?.SaveAs(di);

            return true;
        }

        public bool CloseProject()
        {
            _logger?.LogInformation("Closing project...");

            if (IsProjectNull())
            {
                return false;
            }

            (_project as Project)?.Close();
            _project = null;

            return true;
        }

        #endregion

        #region session

        public List<ProjectBase> GetSessions()
        {
            _logger?.LogInformation("Getting open local sessions...");

            if (IsPortalNull())
            {
                return [];
            }

            var sessions = new List<ProjectBase>();

            if (_portal?.LocalSessions != null)
            {
                foreach (var session in _portal.LocalSessions)
                {
                    sessions.Add(session.Project as ProjectBase);
                }
            }

            return sessions;
        }

        public bool OpenSession(string localSessionPath)
        {
            _logger?.LogInformation($"Opening session: {localSessionPath}");

            if (IsPortalNull())
            {
                return false;
            }

            try
            {
                var sessions = GetSessions();
                var projectName = Path.GetFileNameWithoutExtension(localSessionPath);
                var sessionName = Regex.Replace(projectName, @"_(LS|ES)_\d$", string.Empty, RegexOptions.IgnoreCase);

                if (!string.IsNullOrEmpty(sessionName) && sessions.Any(s => s.Name.Equals(sessionName)))
                {
                    // Session is already open  
                    _session = _portal?.LocalSessions.FirstOrDefault(s => s.Project.Name == sessionName);
                    if (_session != null)
                    {
                        // Correctly cast MultiuserProject to Project  
                        _project = _session.Project;
                        return _project != null;
                    }
                }

                if (_session != null)
                {
                    _project = null;
                    _session?.Close();
                    _session = null;
                }

                if (_project != null)
                {
                    (_project as Project)?.Close();
                    _project = null;
                }

                _session = _portal?.LocalSessions.Open(new FileInfo(localSessionPath));
                if (_session != null)
                {
                    // Correctly cast MultiuserProject to Project  
                    _project = _session.Project;
                    return _project != null;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        public bool SaveSession()
        {
            _logger?.LogInformation("Saving session...");

            if (IsSessionNull())
            {
                return false;
            }

            // Save session
            _session?.Save();

            return true;
        }

        public bool CloseSession()
        {
            _logger?.LogInformation("Closing session...");

            if (IsSessionNull())
            {
                return false;
            }

            _project = null;
            _session?.Close();
            _session = null;

            return true;
        }

        #endregion

        #region project tree

        public string GetProjectTree()
        {
            _logger?.LogInformation("Getting project tree...");

            if (IsProjectNull())
            {
                return string.Empty;
            }

            StringBuilder sb = new();

            sb.AppendLine($"{_project?.Name}");

            var ancestorStates = new List<bool>();
            var sections = new List<Action>();
            
            if (_project?.Devices != null && _project.Devices.Count > 0)
            {
                sections.Add(() => GetProjectTreeDevices(sb, _project.Devices, ancestorStates));
            }
            
            if (_project?.DeviceGroups != null && _project.DeviceGroups.Count > 0)
            {
                sections.Add(() => GetProjectTreeGroups(sb, _project.DeviceGroups, ancestorStates));
            }
            
            if (_project?.UngroupedDevicesGroup != null)
            {
                sections.Add(() => GetProjectTreeUngroupedDeviceGroup(sb, _project.UngroupedDevicesGroup, ancestorStates));
            }
            
            for (int i = 0; i < sections.Count; i++)
            {
                var isLastSection = i == sections.Count - 1;
                if (i == 0)
                {
                    sections[i]();
                }
                else
                {
                    sections[i]();
                }
            }

            return sb.ToString();
        }

        #endregion

        #region private helper

        private bool IsPortalNull()
        {
            if (_portal == null)
            {
                _logger?.LogWarning("No TIA portal available.");

                return true;
            }

            return false;
        }

        /// <summary>Why there is no project: not connected at all (TIA Portal gone or never attached), or connected with nothing open.</summary>
        private string NoProjectMessage => _portal == null
            ? "Not connected to TIA Portal. Start it if it is not running, call 'connect', and 'open_project' if no project is open."
            : "No project is open in TIA Portal. Open one with 'open_project'.";

        private bool IsProjectNull()
        {
            if (_project == null)
            {
                _logger?.LogWarning("No TIA project available.");

                return true;
            }

            return false;
        }

        private bool IsSessionNull()
        {
            if (_session == null)
            {
                _logger?.LogWarning("No TIA session available.");

                return true;
            }

            return false;
        }

        #endregion

        #region get tree ...

        private string GetTreePrefix(List<bool> ancestorStates, bool isLast)
        {
            var prefix = new StringBuilder();
            
            // Build prefix based on ancestor states
            for (int i = 0; i < ancestorStates.Count; i++)
            {
                prefix.Append(ancestorStates[i] ? "    " : "│   ");
            }
            
            // Add current level connector
            prefix.Append(isLast ? "└── " : "├── ");
            return prefix.ToString();
        }

        private void GetProjectTreeDevices(StringBuilder sb, DeviceComposition devices, List<bool> ancestorStates)
        {
            if (devices.Count == 0) return;
            
            // Check if this is the last main section
            var hasOtherSections = (_project?.DeviceGroups != null && _project.DeviceGroups.Count > 0) ||
                                  (_project?.UngroupedDevicesGroup != null);
            var isLastMainSection = !hasOtherSections;
            
            sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastMainSection)}Devices [Collection]");

            var deviceList = devices.ToList();
            var newAncestorStates = new List<bool>(ancestorStates) { isLastMainSection };
            
            for (int i = 0; i < deviceList.Count; i++)
            {
                var device = deviceList[i];
                var isLastDevice = i == deviceList.Count - 1;
                
                sb.AppendLine($"{GetTreePrefix(newAncestorStates, isLastDevice)}{device.Name} [Device: {device.TypeIdentifier}]");

                if (device.DeviceItems != null && device.DeviceItems.Count > 0)
                {
                    GetProjectTreeDeviceItemsRecursive(sb, device.DeviceItems, new List<bool>(newAncestorStates) { isLastDevice });
                }
            }
        }

        private void GetProjectTreeGroups(StringBuilder sb, DeviceUserGroupComposition groups, List<bool> ancestorStates)
        {
            if (groups.Count == 0) return;
            
            var isLastMainSection = _project?.UngroupedDevicesGroup == null;
            
            sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastMainSection)}Groups [Collection]");

            var groupList = groups.ToList();
            var newAncestorStates = new List<bool>(ancestorStates) { isLastMainSection };
            
            for (int i = 0; i < groupList.Count; i++)
            {
                var group = groupList[i];
                var isLastGroup = i == groupList.Count - 1;
                
                sb.AppendLine($"{GetTreePrefix(newAncestorStates, isLastGroup)}{group.Name} [Group]");

                var groupAncestorStates = new List<bool>(newAncestorStates) { isLastGroup };
                
                if (group.Devices != null && group.Devices.Count > 0)
                {
                    GetProjectTreeGroupDevices(sb, group.Devices, groupAncestorStates, group.Groups != null && group.Groups.Count > 0);
                }
                
                if (group.Groups != null && group.Groups.Count > 0)
                {
                    GetProjectTreeSubGroups(sb, group.Groups, groupAncestorStates);
                }
            }
        }

        private void GetProjectTreeGroupDevices(StringBuilder sb, DeviceComposition devices, List<bool> ancestorStates, bool hasSubGroups)
        {
            var deviceList = devices.ToList();
            
            for (int i = 0; i < deviceList.Count; i++)
            {
                var device = deviceList[i];
                var isLastDevice = i == deviceList.Count - 1 && !hasSubGroups;
                
                sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastDevice)}{device.Name} [Device]");
                
                if (device.DeviceItems != null && device.DeviceItems.Count > 0)
                {
                    GetProjectTreeDeviceItemsRecursive(sb, device.DeviceItems, new List<bool>(ancestorStates) { isLastDevice });
                }
            }
        }

        private void GetProjectTreeSubGroups(StringBuilder sb, DeviceUserGroupComposition groups, List<bool> ancestorStates)
        {
            var groupList = groups.ToList();
            
            for (int i = 0; i < groupList.Count; i++)
            {
                var group = groupList[i];
                var isLastGroup = i == groupList.Count - 1;
                
                sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastGroup)}{group.Name} [Subgroup]");
                
                var groupAncestorStates = new List<bool>(ancestorStates) { isLastGroup };
                
                if (group.Devices != null && group.Devices.Count > 0)
                {
                    GetProjectTreeGroupDevices(sb, group.Devices, groupAncestorStates, group.Groups != null && group.Groups.Count > 0);
                }
                
                if (group.Groups != null && group.Groups.Count > 0)
                {
                    GetProjectTreeSubGroups(sb, group.Groups, groupAncestorStates);
                }
            }
        }

        private void GetProjectTreeDeviceItemsRecursive(StringBuilder sb, DeviceItemComposition deviceItems, List<bool> ancestorStates)
        {
            var deviceItemsList = deviceItems.ToList();
            
            for (int i = 0; i < deviceItemsList.Count; i++)
            {
                var deviceItem = deviceItemsList[i];
                var isLastDeviceItem = i == deviceItemsList.Count - 1;
                
                sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastDeviceItem)}{deviceItem.Name} [DeviceItem]");
                
                var itemAncestorStates = new List<bool>(ancestorStates) { isLastDeviceItem };
                
                // Get software first
                GetProjectTreeDeviceItemSoftware(sb, deviceItem, itemAncestorStates);
                
                // Then get items
                if (deviceItem.Items != null && deviceItem.Items.Count > 0)
                {
                    GetProjectTreeItems(sb, deviceItem.Items, itemAncestorStates, deviceItem.DeviceItems != null && deviceItem.DeviceItems.Count > 0);
                }
                
                // Finally get sub-device items
                if (deviceItem.DeviceItems != null && deviceItem.DeviceItems.Count > 0)
                {
                    GetProjectTreeDeviceItemsRecursive(sb, deviceItem.DeviceItems, itemAncestorStates);
                }
            }
        }

        private void GetProjectTreeItems(StringBuilder sb, DeviceItemAssociation items, List<bool> ancestorStates, bool hasSubDeviceItems)
        {
            var itemsList = items.ToList();
            
            for (int i = 0; i < itemsList.Count; i++)
            {
                var subItem = itemsList[i];
                var isLastItem = i == itemsList.Count - 1 && !hasSubDeviceItems;
                
                sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastItem)}{subItem.Name} [Hardware Component]");
            }
        }

        private void GetProjectTreeDeviceItemSoftware(StringBuilder sb, DeviceItem deviceItem, List<bool> ancestorStates)
        {
            var softwareContainer = deviceItem.GetService<SoftwareContainer>();
            var hasSoftware = false;
            
            //PLC software
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                var hasOtherItems = (deviceItem.Items != null && deviceItem.Items.Count > 0) ||
                                   (deviceItem.DeviceItems != null && deviceItem.DeviceItems.Count > 0);
                sb.AppendLine($"{GetTreePrefix(ancestorStates, !hasOtherItems)}PlcSoftware: {plcSoftware.Name} [PLC Program]");
                hasSoftware = true;
            }

            //WinCC HMI software
            if (softwareContainer?.Software is HmiTarget hmiTarget)
            {
                var hasOtherItems = (deviceItem.Items != null && deviceItem.Items.Count > 0) ||
                                   (deviceItem.DeviceItems != null && deviceItem.DeviceItems.Count > 0);
                sb.AppendLine($"{GetTreePrefix(ancestorStates, !hasOtherItems && !hasSoftware)}HmiTarget: {hmiTarget.Name} [HMI Program]");
            }

            //Unified HMI software: dlls will only exist on TIA Portal V19 and newer.
            if (Engineering.TiaMajorVersion >= 19)
                TryGetUnifiedSoftware(sb, deviceItem, ancestorStates, softwareContainer, hasSoftware);
        }

        private bool TryGetUnifiedSoftware(StringBuilder sb, DeviceItem deviceItem, List<bool> ancestorStates, SoftwareContainer? softwareContainer, bool hasSoftware)
        {
            if (softwareContainer?.Software is HmiSoftware hmiSoftware)
            {
                var hasOtherItems = (deviceItem.Items != null && deviceItem.Items.Count > 0) ||
                                    (deviceItem.DeviceItems != null && deviceItem.DeviceItems.Count > 0);
                sb.AppendLine($"{GetTreePrefix(ancestorStates, !hasOtherItems && !hasSoftware)}HmiSoftware: {hmiSoftware.Name} [HMI Program]");
                hasSoftware = true;
            }

            return hasSoftware;
        }

        private void GetProjectTreeUngroupedDeviceGroup(StringBuilder sb, DeviceSystemGroup ungroupedDevicesGroup, List<bool> ancestorStates)
        {
            sb.AppendLine($"{GetTreePrefix(ancestorStates, true)}UngroupedDevicesGroup: {ungroupedDevicesGroup.Name} [System Group]");

            if (ungroupedDevicesGroup.Devices != null && ungroupedDevicesGroup.Devices.Count > 0)
            {
                var deviceList = ungroupedDevicesGroup.Devices.ToList();
                var newAncestorStates = new List<bool>(ancestorStates) { true };
                
                for (int i = 0; i < deviceList.Count; i++)
                {
                    var device = deviceList[i];
                    var isLastDevice = i == deviceList.Count - 1;
                    
                    sb.AppendLine($"{GetTreePrefix(newAncestorStates, isLastDevice)}{device.Name} [{device.TypeIdentifier}]");
                }
            }
        }

        #endregion

        // From the former Portal.Transactions.cs:
        // Atomic, undoable write scopes.
        //
        // Callers: the Guarded helper in McpServer.cs, which wraps every project-mutating
        // tool. Affected API: no signature changes - the wrapper is invisible to callers, it only
        // changes what TIA Portal does with the edits underneath. Reads and writes no data files.
        //
        // Why: before this, a write tool that failed halfway left the project half-changed, and a
        // batch of edits appeared in the TIA Portal undo stack as an unlabelled pile of steps.
        // Siemens.Engineering.ExclusiveAccess.Transaction gives both an all-or-nothing commit and
        // a single named undo entry.
        //
        // Deliberately best-effort: if the running TIA Portal refuses exclusive access - another
        // dialog holds it, or the project does not support transactions - the write still runs,
        // unwrapped, exactly as it did before. Refusing to write at all would be a regression for
        // the sake of a safety net.

        #region transactions

        /// <summary>
        /// Runs <paramref name="body"/> inside a named transaction when TIA Portal allows one.
        /// The transaction commits only if the body returns without throwing, so a failed write
        /// rolls back instead of leaving the project half-edited.
        /// </summary>
        /// <param name="description">
        /// What the operator sees: the exclusive-access banner while it runs, and the undo entry
        /// afterwards. Keep it short and name the tool.
        /// </param>
        /// <summary>
        /// True while a write runs inside a TIA Portal transaction. A batch operation uses it to
        /// say truthfully what a failure left behind: inside a transaction everything is rolled
        /// back, without one the steps that succeeded stay.
        /// </summary>
        private bool _inTransaction;

        public T InTransaction<T>(string description, Func<T> body)
        {
            if (_portal == null || _project is not ITransactionSupport persistence)
            {
                // No portal, or a project that cannot host a transaction: run the write directly
                // rather than failing it.
                return body();
            }

            ExclusiveAccess? access;

            try
            {
                access = _portal.ExclusiveAccess(description);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Exclusive access refused for '{Description}'; writing without a transaction", description);

                return body();
            }

            var failed = false;

            try
            {
                Transaction? transaction;

                try
                {
                    transaction = access.Transaction(persistence, description);
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Transaction refused for '{Description}'; writing without one", description);

                    return body();
                }

                try
                {
                    T result;

                    _inTransaction = true;

                    try
                    {
                        result = body();
                    }
                    finally
                    {
                        _inTransaction = false;
                    }

                    // Only reached when the body did not throw. Without this call the dispose
                    // below rolls the transaction back.
                    transaction.CommitOnDispose();

                    if (access.IsCancellationRequested)
                    {
                        _logger?.LogInformation("The operator requested cancellation during '{Description}'", description);
                    }

                    return result;
                }
                catch (Exception)
                {
                    failed = true;

                    throw;
                }
                finally
                {
                    DisposeAfterWrite(transaction, failed);
                }
            }
            catch (Exception ex) when (ex is not PortalException)
            {
                failed = true;

                // A write that closed TIA Portal ends here with whatever the dead objects throw.
                var translated = Operation.TranslateLoss(ex);

                if (ReferenceEquals(translated, ex))
                {
                    throw;
                }

                throw translated;
            }
            finally
            {
                DisposeAfterWrite(access, failed);
            }
        }

        /// <summary>
        /// Disposes the transaction or the exclusive access. After a failed write the objects may
        /// belong to a TIA Portal that is gone, and a dispose that throws there would replace the
        /// failure the client has to read - so it is only logged then.
        /// </summary>
        private void DisposeAfterWrite(IDisposable disposable, bool writeFailed)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex) when (writeFailed)
            {
                _logger?.LogDebug(ex, "Disposing {Type} after a failed write threw", disposable.GetType().Name);
            }
        }

        /// <summary>Void counterpart of <see cref="InTransaction{T}"/>.</summary>
        public void InTransaction(string description, Action body)
        {
            InTransaction(description, () =>
            {
                body();

                return true;
            });
        }

        #endregion
    }
}
