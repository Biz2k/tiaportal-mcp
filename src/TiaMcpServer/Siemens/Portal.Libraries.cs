using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Library;
using Siemens.Engineering.Library.MasterCopies;
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
