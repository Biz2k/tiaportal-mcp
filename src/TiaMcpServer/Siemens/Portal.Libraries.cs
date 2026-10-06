using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Library;
using Siemens.Engineering.Library.MasterCopies;
using Siemens.Engineering.Library.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        public List<ResponseLibraryInfo> GetLibraries()
        {
            var libs = new List<ResponseLibraryInfo>();

            if (_project?.ProjectLibrary != null)
            {
                libs.Add(new ResponseLibraryInfo
                {
                    Name = "ProjectLibrary",
                    Path = "ProjectLibrary",
                    IsProjectLibrary = true
                });
            }

            if (_portal != null)
            {
                foreach (GlobalLibrary gl in _portal.GlobalLibraries)
                {
                    libs.Add(new ResponseLibraryInfo
                    {
                        Name = gl.Name,
                        Path = gl.Path.FullName,
                        IsProjectLibrary = false
                    });
                }
            }

            return libs;
        }

        public void OpenGlobalLibrary(string filePath)
        {
            if (_portal == null) throw new InvalidOperationException("TIA Portal is not connected");
            var file = new FileInfo(filePath);
            if (!file.Exists) throw new FileNotFoundException("Global library file not found", filePath);

            _logger?.LogInformation($"Opening global library: {filePath}");
            _portal.GlobalLibraries.Open(file, OpenMode.ReadOnly);
        }

        public List<ResponseMasterCopyInfo> GetMasterCopies(string libraryName)
        {
            ILibrary? targetLib = null;
            string targetLibName = "";

            if (_project?.ProjectLibrary != null && libraryName.Equals("ProjectLibrary", StringComparison.OrdinalIgnoreCase))
            {
                targetLib = _project.ProjectLibrary;
                targetLibName = "ProjectLibrary";
            }
            else if (_portal != null)
            {
                foreach (GlobalLibrary gl in _portal.GlobalLibraries)
                {
                    if (gl.Name.Equals(libraryName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetLib = gl;
                        targetLibName = gl.Name;
                        break;
                    }
                }
            }

            if (targetLib == null)
            {
                throw new InvalidOperationException($"Library '{libraryName}' not found or not opened");
            }

            var items = new List<ResponseMasterCopyInfo>();
            ProcessMasterCopyFolder(targetLib.MasterCopyFolder, targetLibName, items);
            return items;
        }

        private void ProcessMasterCopyFolder(MasterCopySystemFolder folder, string parentPath, List<ResponseMasterCopyInfo> items)
        {
            foreach (MasterCopy copy in folder.MasterCopies)
            {
                items.Add(new ResponseMasterCopyInfo { Name = copy.Name, Path = $"{parentPath}/{copy.Name}" });
            }

            foreach (MasterCopyUserFolder subFolder in folder.Folders)
            {
                ProcessMasterCopyUserFolder(subFolder, $"{parentPath}/{subFolder.Name}", items);
            }
        }

        private void ProcessMasterCopyUserFolder(MasterCopyUserFolder folder, string parentPath, List<ResponseMasterCopyInfo> items)
        {
            foreach (MasterCopy copy in folder.MasterCopies)
            {
                items.Add(new ResponseMasterCopyInfo { Name = copy.Name, Path = $"{parentPath}/{copy.Name}" });
            }

            foreach (MasterCopyUserFolder subFolder in folder.Folders)
            {
                ProcessMasterCopyUserFolder(subFolder, $"{parentPath}/{subFolder.Name}", items);
            }
        }

        /// <summary>
        /// The types of a library with their versions and the system each belongs to.
        /// </summary>
        public List<LibraryTypeInfo> GetLibraryTypes(string libraryName)
        {
            return Operation.Run(_logger, nameof(GetLibraryTypes), PortalErrorCode.InvalidState,
                () =>
                {
                    ILibrary? library = null;

                    if (string.IsNullOrWhiteSpace(libraryName) || libraryName.Equals("ProjectLibrary", StringComparison.OrdinalIgnoreCase))
                    {
                        if (IsProjectNull())
                        {
                            throw new PortalException(PortalErrorCode.InvalidState, "No project is open in TIA Portal");
                        }

                        library = _project!.ProjectLibrary;
                    }
                    else if (_portal != null)
                    {
                        library = _portal.GlobalLibraries.FirstOrDefault(l => l.Name.Equals(libraryName, StringComparison.OrdinalIgnoreCase));
                    }

                    if (library == null)
                    {
                        throw new PortalException(PortalErrorCode.NotFound,
                            $"Library '{libraryName}' is not open. Use 'get_libraries' to list the libraries, 'open_global_library' to open one.");
                    }

                    var types = new List<LibraryTypeInfo>();

                    CollectLibraryTypes(library.TypeFolder, string.Empty, types);

                    return types;
                },
                ("libraryName", libraryName));
        }

        private static void CollectLibraryTypes(LibraryTypeFolder folder, string path, List<LibraryTypeInfo> types)
        {
            foreach (var type in folder.Types)
            {
                var minimumDeviceVersion = LibraryAttribute(type, "MinimumTargetDeviceVersion");
                var (system, note) = ClassifyLibraryType(type.GetType().Namespace, minimumDeviceVersion);

                var info = new LibraryTypeInfo
                {
                    Name = type.Name,
                    Path = path,
                    Kind = type.GetType().Name,
                    System = system,
                    SystemNote = note,
                    MinimumTargetDeviceVersion = string.IsNullOrEmpty(minimumDeviceVersion) ? null : minimumDeviceVersion,
                    Author = LibraryAttribute(type, "Author"),
                    Status = LibraryAttribute(type, "Status")
                };

                foreach (var version in type.Versions)
                {
                    info.Versions.Add(new LibraryTypeVersionInfo
                    {
                        Version = version.VersionNumber.ToString(),
                        State = version.State.ToString(),
                        IsDefault = version.IsDefault,
                        ContainedType = system == "unified" ? $"V{version.VersionNumber}\\{type.Name}" : null
                    });
                }

                types.Add(info);
            }

            foreach (var subFolder in folder.Folders)
            {
                CollectLibraryTypes(subFolder, path.Length == 0 ? subFolder.Name : $"{path}/{subFolder.Name}", types);
            }
        }

        private static string? LibraryAttribute(IEngineeringObject target, string name)
        {
            try
            {
                return target.GetAttribute(name)?.ToString();
            }
            catch (EngineeringException)
            {
                // Not every kind of type has every attribute.
                return null;
            }
        }

        /// <summary>
        /// Which system a library type belongs to. Openness has dedicated classes for PLC types and
        /// for the classic WinCC faceplates. Everything of WinCC Unified, and everything that is
        /// not tied to a system, arrives as the generic LibraryType; of those, the Unified types
        /// carry a minimum device version and the others (icons, graphics) do not. Verified on
        /// TIA Portal V21 on 2026-10-06.
        /// </summary>
        internal static (string System, string Note) ClassifyLibraryType(string? classNamespace, string? minimumDeviceVersion)
        {
            var typeNamespace = classNamespace ?? string.Empty;

            if (typeNamespace.StartsWith("Siemens.Engineering.SW", StringComparison.Ordinal))
            {
                return ("plc", "PLC type (block or data type).");
            }

            if (typeNamespace.StartsWith("Siemens.Engineering.HmiUnified", StringComparison.Ordinal))
            {
                return ("unified", "WinCC Unified type.");
            }

            if (typeNamespace.StartsWith("Siemens.Engineering.Hmi", StringComparison.Ordinal))
            {
                return ("classic", "WinCC Comfort / Advanced / Professional type. Openness does not say which of the three it was made for.");
            }

            return string.IsNullOrEmpty(minimumDeviceVersion)
                ? ("universal", "Not tied to a system: it has no minimum device version (icons and graphics are of this kind).")
                : ("unified", $"WinCC Unified type (faceplate, script or the like), for devices from version {minimumDeviceVersion}.");
        }

        public void InstantiateMasterCopy(string libraryName, string masterCopyPath, string targetDeviceName, string targetGroupName, string targetType)
        {
            if (IsProjectNull()) throw new InvalidOperationException("Project is not open");

            // Find library
            ILibrary? targetLib = null;
            string targetLibName = "";
            if (_project!.ProjectLibrary != null && libraryName.Equals("ProjectLibrary", StringComparison.OrdinalIgnoreCase))
            {
                targetLib = _project.ProjectLibrary;
                targetLibName = "ProjectLibrary";
            }
            else if (_portal != null)
            {
                foreach (GlobalLibrary gl in _portal.GlobalLibraries)
                {
                    if (gl.Name.Equals(libraryName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetLib = gl;
                        break;
                    }
                }
            }

            if (targetLib == null)
                throw new InvalidOperationException($"Library '{libraryName}' not found");

            // Find MasterCopy
            var masterCopy = FindMasterCopy(targetLib.MasterCopyFolder, masterCopyPath, targetLibName);
            if (masterCopy == null)
                throw new InvalidOperationException($"Master copy '{masterCopyPath}' not found in library '{libraryName}'");

            _logger?.LogInformation($"Instantiating MasterCopy '{masterCopy.Name}' into '{targetDeviceName}/{targetGroupName}' as {targetType}");

            if (targetType.Equals("block", StringComparison.OrdinalIgnoreCase))
            {
                // We need the PlcBlockGroup
                var group = GetPlcBlockGroupByPath(targetDeviceName, targetGroupName);
                if (group == null) throw new InvalidOperationException($"Block group '{targetGroupName}' not found");
                group.Blocks.CreateFrom(masterCopy);
            }
            else if (targetType.Equals("type", StringComparison.OrdinalIgnoreCase))
            {
                // We need the PlcTypeGroup
                var group = GetPlcTypeGroupByPath(targetDeviceName, targetGroupName);
                if (group == null) throw new InvalidOperationException($"Type group '{targetGroupName}' not found");
                group.Types.CreateFrom(masterCopy);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported target type '{targetType}'. Use 'block' or 'type'.");
            }
        }

        private MasterCopy? FindMasterCopy(MasterCopySystemFolder folder, string targetPath, string currentPath)
        {
            foreach (MasterCopy copy in folder.MasterCopies)
            {
                string path = $"{currentPath}/{copy.Name}";
                if (path.Equals(targetPath, StringComparison.OrdinalIgnoreCase)) return copy;
            }

            foreach (MasterCopyUserFolder subFolder in folder.Folders)
            {
                var found = FindMasterCopyUser(subFolder, targetPath, $"{currentPath}/{subFolder.Name}");
                if (found != null) return found;
            }
            return null;
        }

        private MasterCopy? FindMasterCopyUser(MasterCopyUserFolder folder, string targetPath, string currentPath)
        {
            foreach (MasterCopy copy in folder.MasterCopies)
            {
                string path = $"{currentPath}/{copy.Name}";
                if (path.Equals(targetPath, StringComparison.OrdinalIgnoreCase)) return copy;
            }

            foreach (MasterCopyUserFolder subFolder in folder.Folders)
            {
                var found = FindMasterCopyUser(subFolder, targetPath, $"{currentPath}/{subFolder.Name}");
                if (found != null) return found;
            }
            return null;
        }
    }
}
