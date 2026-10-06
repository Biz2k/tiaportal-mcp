using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    [McpServerToolType]
    public static partial class McpServer
    {
        private static IServiceProvider? _services;

        private static Portal? _portal;

        public static ILogger? Logger { get; set; }

        public static Portal Portal
        {
            get
            {
                if (_services !=null)
                {
                    return _services.GetRequiredService<Portal>();
                }
                else
                {
                    if (_portal == null)
                    {
                        _portal = new Portal();
                    }
                    return _portal;
                }
            }
            set
            {
                _portal = value ?? throw new ArgumentNullException(nameof(value), "Portal cannot be null");
            }
        }

        public static void SetServiceProvider(IServiceProvider services)
        {
            _services = services;
        }

        #region portal

        [McpServerTool(Name = "get_tia_instances", Title = "Get TIA Portal instances", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("List the running TIA Portal instances (process id, open project, mode) without connecting. Use it to pick the instance for 'connect' when several are open")]
        public static ResponseTiaInstances GetTiaInstances()
        {
            try
            {
                var instances = Portal.GetTiaInstances();

                return new ResponseTiaInstances
                {
                    Message = instances.Count == 0
                        ? "No TIA Portal is running"
                        : $"{instances.Count} TIA Portal instance(s) running",
                    Items = instances,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure("listing TIA Portal instances", ex);
            }
        }

        [McpServerTool(Name = "connect", Title = "Connect to TIA Portal", Destructive = false, Idempotent = true, OpenWorld = false), Description("Connect to a running TIA Portal; the first one unless processId or projectPath picks another ('get_tia_instances' lists them). Fails when none is running, unless startIfNotRunning is set")]
        public static ResponseConnect Connect(
            [Description("startIfNotRunning: start a new TIA Portal window when none is running (default false)")] bool startIfNotRunning = false,
            [Description("processId: attach to the instance with this process id (see 'get_tia_instances'); 0 means not set")] int processId = 0,
            [Description("projectPath: attach to the instance that has this project open, by full path or file name; empty means not set")] string projectPath = "")
        {
            Logger?.LogInformation("Connecting to TIA Portal...");

            try
            {
                if (Portal.ConnectPortal(startIfNotRunning, processId > 0 ? processId : (int?)null, projectPath))
                {
                    return new ResponseConnect
                    {
                        Message = "Connected to TIA-Portal",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException("Failed to connect to TIA-Portal. Run the 'doctor' tool to check the installation and user group membership.");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolError(ex, ex is PortalException ? null : "Unexpected error connecting to TIA-Portal");
            }
        }

        [McpServerTool(Name = "disconnect", Title = "Disconnect from TIA Portal", Destructive = false, Idempotent = true, OpenWorld = false), Description("Disconnect from TIA-Portal")]
        public static ResponseDisconnect Disconnect()
        {
            try
            {
                if (Portal.DisconnectPortal())
                {
                    return new ResponseDisconnect
                    {
                        Message = "Disconnected from TIA-Portal",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException("Failed disconnecting from TIA-Portal");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"disconnecting from TIA-Portal", ex);
            }
        }

        #endregion

        #region state

        /// <summary>What to do when the server is not connected, naming the TIA Portal instances that run.</summary>
        private static string NotConnectedNote()
        {
            try
            {
                var running = Portal.GetTiaInstances();

                return running.Count == 0
                    ? "Not connected, and no TIA Portal is running. Start it and call 'connect'."
                    : "Not connected: call 'connect' (or 'open_tia_project'). Running: "
                      + string.Join("; ", running.Select(TiaInstanceSelection.Describe)) + ".";
            }
            catch (Exception)
            {
                return "Not connected: call 'connect'.";
            }
        }

        [McpServerTool(Name = "get_state", Title = "Get server state", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get the state of the TIA-Portal MCP server")]
        public static ResponseState GetState()
        {
            try
            {
                var state = Portal.GetState();

                if (state != null)
                {
                    return new ResponseState
                    {
                        Message = "TIA-Portal MCP server state retrieved",
                        IsConnected = state.IsConnected,
                        Project = state.Project,
                        Session = state.Session,
                        AllowWrite = WritePolicy.AllowWrite,
                        Note = state.IsConnected == true ? null : NotConnectedNote(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException("Failed to retrieve TIA-Portal MCP server state");
                }
                

            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"retrieving TIA-Portal MCP server state", ex);
            }
        }

        [McpServerTool(Name = "doctor", Title = "Diagnose the TIA Portal environment", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Diagnose the TIA-Portal environment: connection, open project, active and installed TIA-Portal versions, Openness user group membership")]
        public static ResponseDoctor Doctor()
        {
            Logger?.LogInformation("Running TIA Portal diagnostics...");

            try
            {
                // Fully qualified: 'Diagnostics' alone would collide with the System.Diagnostics namespace.
                var report = TiaMcpServer.Siemens.Diagnostics.Run(Portal, WritePolicy.AllowWrite);

                return new ResponseDoctor
                {
                    Message = "TIA-Portal environment diagnosed",
                    Report = report.Text,
                    IsConnected = report.IsConnected,
                    ActiveTiaMajorVersion = report.ActiveTiaMajorVersion,
                    ProjectName = report.ProjectName,
                    ProjectPath = report.ProjectPath,
                    IsUserInGroup = report.IsUserInGroup,
                    AllowWrite = report.AllowWrite,
                    Installations = report.Installations
                        .Select(i => new ResponseTiaInstallation
                        {
                            MajorVersion = i.MajorVersion,
                            InstallPath = i.InstallPath,
                            EngineeringExists = i.EngineeringExists,
                            PortalExeExists = i.PortalExeExists
                        })
                        .ToList(),
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"diagnosing the TIA-Portal environment", ex);
            }
        }

        #endregion

        #region project/session

        [McpServerTool(Name = "get_project", Title = "Get open project", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get open local project/session")]
        public static ResponseGetProjects GetProjects()
        {
            try
            {
                var list = Portal.GetProjects();

                list.AddRange(Portal.GetSessions());

                var responseList = new List<ResponseProjectInfo>();
                foreach (var project in list)
                {
                    var attributes = Helper.GetAttributeList(project);

                    if (project != null)
                    {
                        responseList.Add(new ResponseProjectInfo
                        {
                            Name = project.Name,
                            Attributes = attributes
                        });
                    }
                }

                return new ResponseGetProjects
                {
                    Message = "Open projects and sessions retrieved",
                    Items = responseList,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"retrieving open projects", ex);
            }
        }

        [McpServerTool(Name = "open_project", Title = "Open project/session", Destructive = false, Idempotent = true, OpenWorld = false), Description("Open a TIA-Portal local project/session")]
        public static ResponseOpenProject OpenProject(
            [Description("path: defines the path where to the project/session")] string path)
        {
            try
            {

                // get project extension
                string extension = Path.GetExtension(path).ToLowerInvariant();

                // use regex to check if extension is .ap\d+ or .als\d+
                if (!Regex.IsMatch(extension, @"^\.ap\d+$") &&
                    !Regex.IsMatch(extension, @"^\.als\d+$"))
                {
                    throw new McpException("Invalid project file extension. Use .apXX for projects or .alsXX for sessions, where XX=18,19,20,....");
                }

                bool success = false;

                if (extension.StartsWith(".ap"))
                {
                    success = Portal.OpenProject(path);
                }
                if (extension.StartsWith(".als"))
                {
                    success = Portal.OpenSession(path);
                }

                if (success)
                {
                    return new ResponseOpenProject
                    {
                        Message = $"Project '{path}' opened",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed to open project '{path}'");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolError(ex, ex is PortalException ? null : $"Unexpected error opening project '{path}'");
            }
        }

        [McpServerTool(Name = "save_project", Title = "Save project", Destructive = true, Idempotent = true, OpenWorld = false), Description("Save the current TIA-Portal local project/session")]
        public static ResponseSaveProject SaveProject()
        {
            try
            {
                if (Portal.IsLocalSession)
                {
                    if (Portal.SaveSession())
                    {
                        return new ResponseSaveProject
                        {
                            Message = "Local session saved",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    }
                    else
                    {
                        throw new McpException("Failed to save local session");
                    }
                }
                else
                {
                    if (Portal.SaveProject())
                    {
                        return new ResponseSaveProject
                        {
                            Message = "Local project saved",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    }
                    else
                    {
                        throw new McpException(Portal.IsConnected()
                            ? "Failed to save project: no project is open in TIA Portal."
                            : "Failed to save project: not connected to TIA Portal. Call 'connect' first.");
                    }
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolError(ex, ex is PortalException ? null : "Unexpected error saving the local project/session");
            }
        }

        [McpServerTool(Name = "save_as_project", Title = "Save project as", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Save the open local project under a new folder and switch TIA Portal to it. The path is the FOLDER of the new project, without an extension: TIA Portal makes <name>.apXX inside it, and the answer gives the full path of that file. A path with a project extension, a relative path, a missing parent folder and a folder that is not empty are refused before anything is written")]
        public static ResponseSaveAsProject SaveAsProject(
            [Description("newProjectPath: absolute path of the new project's folder, without an extension, e.g. 'C:\\Projects\\NewPlant'. The parent folder must exist; the folder itself must not exist or must be empty")] string newProjectPath)
        {
            try
            {
                if (Portal.IsLocalSession)
                {
                    throw new McpException($"Cannot save local session as '{newProjectPath}'");
                }

                var projectFile = Portal.SaveAsProject(newProjectPath);

                return new ResponseSaveAsProject
                {
                    Message = $"Local project saved as '{projectFile}'; TIA Portal now works on that copy",
                    Path = projectFile,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolError(ex, ex is PortalException ? null : $"Unexpected error saving the local project as '{newProjectPath}'");
            }
        }

        [McpServerTool(Name = "close_project", Title = "Close project", Destructive = true, Idempotent = true, OpenWorld = false), Description("Close the current TIA-Portal project/session")]
        public static ResponseCloseProject CloseProject()
        {
            try
            {
                bool success;

                if (Portal.IsLocalSession)
                {
                    success = Portal.CloseSession();
                    if (success)
                    {
                        return new ResponseCloseProject
                        {
                            Message = "Local session closed",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    }
                    else
                    {
                        throw new McpException("Failed closing local session");
                    }
                }
                else
                {
                    success = Portal.CloseProject();
                    if (success)
                    {
                        return new ResponseCloseProject
                        {
                            Message = "Local project closed",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    }
                    else
                    {
                        throw new McpException("Failed closing project");
                    }
                }

            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolError(ex, ex is PortalException ? null : "Unexpected error closing the local project/session");
            }
        }

        #endregion

        #region devices

        [McpServerTool(Name = "get_project_tree", Title = "Get project tree", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Get the project structure: devices, device groups and device items. By default a text tree (about 25 000 characters on a large project: narrow it with 'depth' and 'filter'). With structured = true a flat list of nodes instead, each with the 'path' that 'hw_get_devices', 'hw_*', 'net_*' and 'hw_get_device_item_info' accept and, for an item that carries software, the 'softwarePath' the plc_* and unified_* tools take")]
        public static ResponseProjectTree GetProjectTree(
            [Description("depth: levels shown below the project, 0 = all (default). 1 = devices and groups only, 2 = their top-level items")] int depth = 0,
            [Description("filter: regular expression on names; keeps the matching nodes and the nodes above them. Empty = no filter (default)")] string filter = "",
            [Description("structured: true returns 'nodes' with paths instead of the text tree (default false)")] bool structured = false)
        {
            try
            {
                if (structured)
                {
                    var nodes = Portal.GetProjectNodes(depth, filter);

                    return new ResponseProjectTree
                    {
                        Message = $"Project tree retrieved: {nodes.Count} node(s)",
                        Nodes = nodes,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }

                var tree = Portal.GetProjectTree();

                if (string.IsNullOrEmpty(tree))
                {
                    throw new McpException("Failed retrieving project tree");
                }

                var narrowed = ProjectTreeText.Narrow(tree, depth, filter);
                var cut = narrowed.Total - narrowed.Kept;

                return new ResponseProjectTree
                {
                    Message = cut > 0
                        ? $"Project tree retrieved, narrowed: {narrowed.Kept} of {narrowed.Total} lines shown (depth {depth}, filter '{filter}'). Widen 'depth' or change 'filter' to see more."
                        : "Project tree retrieved",
                    Tree = "```\n" + narrowed.Text + "\n```",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"retrieving project tree", ex);
            }
        }

        #endregion

        #region lookup

        [McpServerTool(Name = "open_tia_project", Title = "Connect/open a project", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Connect to the running TIA Portal if not already connected, open the given project or session, and return the device and PLC software paths the other tools need. Replaces the connect, open_project, get_project_tree sequence. TIA Portal must already be running")]
        public static ResponseOpenTiaProject OpenTiaProject(
            [Description("path: full path of the .apXX project or .alsXX session file on the machine running this server")] string path)
        {
            try
            {
                var connected = Portal.IsConnected();

                if (!connected && !Portal.ConnectPortal())
                {
                    throw new McpException(
                        "Failed to connect to TIA Portal. Run the 'Doctor' tool to check the installation and user group membership.");
                }

                // Reuse the existing tool rather than duplicating its extension validation and
                // project/session branching; it also closes whatever was open first.
                var opened = OpenProject(path);

                var softwarePaths = CollectSoftwarePaths();

                return new ResponseOpenTiaProject
                {
                    Message = $"{opened.Message}. PLC software: " +
                              (softwarePaths.Count > 0 ? string.Join(", ", softwarePaths) : "none found"),
                    ProjectPath = path,
                    WasAlreadyConnected = connected,
                    SoftwarePaths = softwarePaths,
                    Tree = Portal.GetProjectTree(),
                    Meta = Ok(new JsonObject { ["softwareCount"] = softwarePaths.Count })
                };
            }
            catch (PortalException pex)
            {
                throw ToolError(pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"opening '{path}'", ex);
            }
        }

        #endregion

        // From the former McpServer.Preview.cs:
        // Looking before writing.
        //
        // Callers: the MCP host, through tool registration. Affected API: none - PreviewImport is
        // new and no write tool changed. File I/O: reads the caller's import directory; writes
        // nothing, and never touches the project.
        //
        // This is the read-only half of write safety; the other half is the transaction wrapper in
        // Portal.cs (InTransaction), which every write tool now runs inside. It is one tool rather
        // than a 'dryRun' flag on all 39 write tools: the flag would have changed the execution
        // path of every write for a report that only imports really need, and imports are where
        // the collisions actually happen.
        //
        // What it can and cannot know: the object name is taken from the file name, which is how
        // every exporter in this server names its output. A hand-edited file whose content
        // declares a different name than its file name would be reported under the file name.

        #region preview

        [McpServerTool(Name = "preview_import", Title = "Preview import", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Report what importing a directory would create, overwrite or collide with, without touching the project. Checks each file against the objects already in the PLC, including the rule that a PLC data type name must be unique across the whole PLC - importing an existing type name into a different group fails even with importOption 'Override'")]
        public static ResponseImportPreview PreviewImport(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("importPath: directory holding the files to import (.s7dcl source documents or .xml)")] string importPath,
            [Description("kind: what the files contain - 'type' for PLC data types, 'block' for program blocks")] string kind,
            [Description("groupPath: the group the import would target; empty means the root of that area. A leading system folder segment is accepted")] string groupPath = "")
        {
            try
            {
                if (!Directory.Exists(importPath))
                {
                    throw new McpException($"Import directory '{importPath}' does not exist.");
                }

                var isType = kind.Equals("type", StringComparison.OrdinalIgnoreCase);

                if (!isType && !kind.Equals("block", StringComparison.OrdinalIgnoreCase))
                {
                    throw new McpException($"Unknown kind '{kind}'. Use 'type' or 'block'.");
                }

                var names = Directory
                    .GetFiles(importPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => PreviewExtensions.Contains(Path.GetExtension(f)))
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var target = string.IsNullOrEmpty(groupPath) ? "the root" : groupPath;
                var items = new List<ResponseImportPreviewItem>();

                foreach (var name in names)
                {
                    var existing = Portal.ResolveObjectPath(softwarePath, name, isType ? "type" : "block")
                        .FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

                    if (existing == null)
                    {
                        items.Add(new ResponseImportPreviewItem { Name = name, Effect = "create", TargetPath = Join(groupPath, name) });

                        continue;
                    }

                    var sameGroup = string.Equals(
                        Parent(existing.Path),
                        StripLeadingSystemFolder(groupPath),
                        StringComparison.OrdinalIgnoreCase);

                    items.Add(new ResponseImportPreviewItem
                    {
                        Name = name,
                        Effect = sameGroup ? "overwrite" : (isType ? "conflict" : "overwrite-elsewhere"),
                        TargetPath = Join(groupPath, name),
                        ExistingPath = existing.Path,
                        Note = sameGroup
                            ? "Already exists in the target group; importOption 'Override' replaces it."
                            : isType
                                ? "A PLC data type name must be unique across the whole PLC. This import fails even with " +
                                  $"'Override' - target '{Parent(existing.Path)}' instead, or rename the type."
                                : $"A block of this name already exists at '{existing.Path}'; importing here may collide on the block number."
                    });
                }

                var creates = items.Count(i => i.Effect == "create");
                var overwrites = items.Count(i => i.Effect == "overwrite");
                var conflicts = items.Count(i => i.Effect == "conflict" || i.Effect == "overwrite-elsewhere");

                return new ResponseImportPreview
                {
                    Message = $"Importing '{importPath}' into {target} would create {creates}, overwrite {overwrites}, " +
                              $"and hit {conflicts} conflict(s). Nothing was changed.",
                    Items = items,
                    CreateCount = creates,
                    OverwriteCount = overwrites,
                    ConflictCount = conflicts,
                    Meta = Ok(new JsonObject
                    {
                        ["files"] = names.Count,
                        ["create"] = creates,
                        ["overwrite"] = overwrites,
                        ["conflict"] = conflicts
                    })
                };
            }
            catch (PortalException pex)
            {
                throw ToolError(pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"previewing the import of '{importPath}'", ex);
            }
        }

        /// <summary>File kinds an export of this server produces, and therefore an import consumes.</summary>
        private static readonly HashSet<string> PreviewExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".s7dcl", ".xml" };

        private static string Join(string groupPath, string name) =>
            string.IsNullOrEmpty(groupPath) ? name : $"{groupPath.TrimEnd('/')}/{name}";

        private static string Parent(string path)
        {
            var slash = path.LastIndexOf('/');

            return slash < 0 ? string.Empty : path.Substring(0, slash);
        }

        /// <summary>
        /// A groupPath copied from a preservePath export starts with the localised system folder
        /// ('PLC data types', 'Program blocks'), which the resolved paths do not carry.
        /// </summary>
        private static string StripLeadingSystemFolder(string groupPath)
        {
            if (string.IsNullOrEmpty(groupPath) || !groupPath.Contains("/"))
            {
                return string.Empty;
            }

            return groupPath.Substring(groupPath.IndexOf('/') + 1);
        }

        #endregion

        // From the former McpServerWrite.cs:
        // The project-mutating MCP tools (create, rename, delete, import into the project).
        //
        // Callers: registered by Program.BuildTools() only when '--allow-write' was passed, and
        // invoked directly by the write test classes. Affected API: none existing. Data: returns the
        // shared ResponseCreated / ResponseDeleted / ResponseRenamed / ResponseImported /
        // ResponseGenerateBlocks DTOs. The import tools read a caller-supplied file; nothing here
        // writes files. Project changes live in memory until SaveProject (or SaveSession).
        //
        // Conventions, enforced by the Guarded helper below:
        // - WritePolicy.EnsureEnabled runs first, because these are public static methods that the
        // MSTest suite calls directly, bypassing tool registration.
        // - A PortalException is surfaced with its own guidance message; anything else is wrapped.
        // - Destructive = true; Idempotent = true only for renames and deletes-by-name.
        //
        // Filesystem-only exports stay in McpServer: they never modify the project.

        #region plumbing (write)

        /// <summary>
        /// Reminds the caller that Openness edits are in-memory, naming the right save tool for
        /// the current mode: a multiuser local session saves through SaveSession, not SaveProject.
        /// </summary>
        private static string SaveHint =>
            Portal.IsLocalSession
                ? "The change is in memory; call 'SaveSession' to persist it."
                : "The change is in memory; call 'save_project' to persist it.";

        private static T Guarded<T>(string toolName, Func<T> body)
        {
            WritePolicy.EnsureEnabled(toolName);

            try
            {
                // One transaction per tool call: the edits commit together or not at all, and
                // the operator sees a single named entry in the TIA Portal undo stack instead of
                // an unlabelled pile of steps. Falls back to an unwrapped write when TIA Portal
                // refuses exclusive access, so this can never turn a working write into a
                // failure - see Portal.InTransaction in Portal.cs.
                return Portal.InTransaction($"MCP: {toolName}", body);
            }
            catch (PortalException pex)
            {
                // PortalException messages are already written for the caller (what went wrong
                // and which tool lists the valid paths); ToolError adds the code and context.
                throw ToolError(pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolError(ex, $"Unexpected error in '{toolName}'");
            }
        }

        private static T GuardedNoTransaction<T>(string toolName, Func<T> body)
        {
            WritePolicy.EnsureEnabled(toolName);
            try
            {
                return body();
            }
            catch (PortalException pex)
            {
                throw ToolError(pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolError(ex, $"Unexpected error in '{toolName}'");
            }
        }

        /// <summary>
        /// The one place a failure is turned into what the client reads. The MCP SDK forwards
        /// only the message of an McpException - and for any other exception type nothing but
        /// "An error occurred invoking '...'" - so every tool has to leave through here.
        /// </summary>
        /// <param name="prefix">What the tool was doing, for failures that are not a PortalException.</param>
        internal static McpException ToolError(Exception ex, string? prefix = null)
        {
            if (ex is McpException mcp)
            {
                return mcp;
            }

            var text = ErrorText.ForClient(ex, CandidateHint(ex));

            return new McpException(string.IsNullOrEmpty(prefix) ? text : $"{prefix}: {text}", ex);
        }

        /// <summary>Path keys of an error's context that name an object inside a PLC software.</summary>
        private static readonly string[] ObjectPathKeys =
            { "blockPath", "typePath", "tagPath", "tagTablePath", "watchTablePath", "sourcePath", "objectPath", "groupPath", "path", "name" };

        /// <summary>
        /// For a NotFound on an object of a PLC software: the objects of that name or close to it, found with the same
        /// search as 'plc_resolve_object_path', so the caller can correct the path without another call.
        /// </summary>
        private static string? CandidateHint(Exception ex)
        {
            if (ex is not PortalException { Code: PortalErrorCode.NotFound } pex
                || pex.Message.Contains("Did you mean")
                || pex.Data["softwarePath"] is not string softwarePath
                || string.IsNullOrEmpty(softwarePath))
            {
                return null;
            }

            var wanted = ObjectPathKeys.Select(k => pex.Data[k] as string).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            if (wanted == null)
            {
                return null;
            }

            try
            {
                var matches = Portal.ResolveObjectPath(softwarePath, wanted);

                if (matches.Count == 0)
                {
                    return null;
                }

                return "Did you mean: " + string.Join(", ", matches.Take(5).Select(m => $"{m.Kind} '{m.Path}'")) +
                    (matches.Count > 5 ? $" (and {matches.Count - 5} more; 'plc_resolve_object_path' lists them)" : string.Empty) + "?";
            }
            catch (Exception)
            {
                return null; // a hint must never hide the error itself
            }
        }

        /// <summary>
        /// The reason behind an exception, including nested Openness messages - for the
        /// "Unexpected error ...: {reason}" texts, where ex.Message alone is often just a wrapper.
        /// </summary>
        internal static string Why(Exception ex) => ErrorText.Describe(ex);

        /// <summary>
        /// The error a tool raises when its call failed. A <see cref="PortalException"/> is an expected refusal whose message
        /// already says what to do, so it goes out as it is; anything else is an unexpected failure of <paramref name="what"/>.
        /// </summary>
        internal static PortalException ObjectNotFound(string kind, string path, string softwarePath, string pathKey, string listTool)
        {
            var error = new PortalException(PortalErrorCode.NotFound, $"{kind} not found at '{path}' in '{softwarePath}'. Use '{listTool}' to list what exists.");

            error.Data["softwarePath"] = softwarePath;
            error.Data[pathKey] = path;

            return error;
        }

        internal static McpException Failure(string what, Exception ex) =>
            ex is PortalException ? ToolError(ex) : new McpException($"Unexpected error {what}: {Why(ex)}", ex);

        private static JsonObject OkMeta() => new JsonObject
        {
            ["timestamp"] = DateTime.Now,
            ["success"] = true,
            ["pendingSave"] = true
        };

        private static ResponseCreated Created(string kind, string name, string path) => new ResponseCreated
        {
            Kind = kind,
            Name = name,
            Path = path,
            Message = $"{kind} '{name}' created. {SaveHint}",
            Meta = OkMeta()
        };

        private static ResponseDeleted Deleted(string kind, string path) => new ResponseDeleted
        {
            Kind = kind,
            Path = path,
            Message = $"{kind} '{path}' deleted. {SaveHint}",
            Meta = OkMeta()
        };

        private static ResponseRenamed Renamed(string kind, string oldPath, string newName, string newPath) => new ResponseRenamed
        {
            Kind = kind,
            OldPath = oldPath,
            NewName = newName,
            NewPath = newPath,
            Message = $"{kind} '{oldPath}' renamed to '{newName}'. {SaveHint}",
            Meta = OkMeta()
        };

        private static ResponseImported Imported(string kind, string groupPath, string importPath) => new ResponseImported
        {
            Kind = kind,
            GroupPath = groupPath,
            ImportPath = importPath,
            Message = $"{kind} imported from '{importPath}' into '{groupPath}'. {SaveHint}",
            Meta = OkMeta()
        };

        /// <summary>Replaces the last segment of a path, for reporting a rename's new path.</summary>
        private static string ReplaceLeaf(string path, string newName)
        {
            var trimmed = (path ?? string.Empty).Trim('/');
            var index = trimmed.LastIndexOf('/');

            return index < 0 ? newName : trimmed.Substring(0, index + 1) + newName;
        }

        private static string JoinPath(string groupPath, string name) =>
            string.IsNullOrEmpty(groupPath) ? name : $"{groupPath}/{name}";

        #endregion
    }
}
