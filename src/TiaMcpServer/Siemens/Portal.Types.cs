using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.Multiuser;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks.Interface;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        #region types

        public PlcType? GetType(string softwarePath, string typePath)
        {
            _logger?.LogInformation($"Getting type by path: {typePath}");

            if (IsProjectNull())
            {
                return null;
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                var typeGroup = plcSoftware?.TypeGroup;

                if (typeGroup != null)
                {
                    var (path, name) = SplitPath(typePath);

                    var group = GetPlcTypeGroupByPath(softwarePath, path);
                    if (group != null)
                    {
                        return FindByName<PlcType>(group.Types, name, t => t.Name);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Root-relative path of a PLC data type, the GetBlockPath counterpart.
        /// </summary>
        public string GetTypePath(PlcType type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            if (type.Parent is PlcTypeGroup parentGroup)
            {
                var groupPath = GetPlcTypeGroupPath(parentGroup, includeSystemRoot: false);
                return JoinLeaf(groupPath, type.Name);
            }

            return type.Name;
        }

        public List<PlcType> GetTypes(string softwarePath, string regexName = "")
        {
            _logger?.LogInformation("Getting types...");

            if (IsProjectNull())
            {
                return [];
            }

            // A path that names no PLC software is an error, not an empty list.
            if (GetSoftwareContainer(softwarePath)?.Software is not PlcSoftware)
            {
                throw new PortalException(PortalErrorCode.NotFound, DescribeMissingSoftware(softwarePath, "No PLC software found"));
            }

            var list = new List<PlcType>();

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    var group = plcSoftware?.TypeGroup;

                    if (group != null)
                    {
                        GetTypesRecursive(group, list, regexName);
                    }
                }
            }
            catch (Exception)
            {
                // Console.WriteLine($"Error getting user defined types: {ex.Message}");
            }

            return list;
        }

        public PlcType? ExportXmlType(string softwarePath, string typePath, string exportPath, bool preservePath = false)
        {
            _logger?.LogInformation($"Exporting type by path: {typePath}");

            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, NoProjectMessage);
                }

                var type = GetType(softwarePath, typePath);

                if (type == null)
                {
                    throw new PortalException(PortalErrorCode.NotFound, "Type not found");
                }

                // TIA Portal never exports inconsistent types
                if (!type.IsConsistent)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Type is inconsistent; TIA Portal does not export inconsistent types.");
                }

                if (preservePath)
                {
                    var groupPath = "";
                    if (type.Parent is PlcTypeGroup parentGroup)
                    {
                        groupPath = GetPlcTypeGroupPath(parentGroup);
                    }

                    exportPath = Path.Combine(exportPath, groupPath.Replace('/', '\\'), $"{type.Name}.xml");
                }
                else
                {
                    exportPath = Path.Combine(exportPath, $"{type.Name}.xml");
                }

                if (File.Exists(exportPath))
                {
                    File.Delete(exportPath);
                }

                type.Export(new FileInfo(exportPath), ExportOptions.None);

                return type;
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, "Export failed", null, ex);

                if (!pex.Data.Contains("softwarePath")) pex.Data["softwarePath"] = softwarePath;
                if (!pex.Data.Contains("typePath")) pex.Data["typePath"] = typePath;
                if (!pex.Data.Contains("exportPath")) pex.Data["exportPath"] = exportPath;

                _logger?.LogError(pex, "ExportXmlType failed for {SoftwarePath} {TypePath} -> {ExportPath}", softwarePath, typePath, exportPath);
                throw pex;
            }
        }

        /// <summary>Imports a SimaticML type file into a group; see <see cref="ImportXmlBlock"/> for 'overwrite'.</summary>
        public bool ImportXmlType(string softwarePath, string groupPath, string importPath, bool overwrite = true)
        {
            return Operation.Run(_logger, nameof(ImportXmlType), PortalErrorCode.ImportFailed,
                () =>
                {
                    var group = GetPlcTypeGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Type group not found at '{groupPath}'. Use 'plc_get_software_tree' to discover valid group paths.");

                    if (!File.Exists(importPath))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Import file '{importPath}' does not exist on the machine running this server.");
                    }

                    var list = group.Types.Import(new FileInfo(importPath), overwrite ? ImportOptions.Override : ImportOptions.None);

                    if (list == null || list.Count == 0)
                    {
                        throw new PortalException(PortalErrorCode.ImportFailed, $"TIA Portal imported nothing from '{importPath}'.");
                    }

                    return true;
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath), ("importPath", importPath), ("overwrite", overwrite));
        }

        public IEnumerable<PlcType>? ExportXmlTypes(string softwarePath, string exportPath, string regexName = "", bool preservePath = false)
        {
            _logger?.LogInformation("Exporting types...");

            if (IsProjectNull())
            {
                return null;
            }

            var exportList = new List<PlcType>();
            var failures = new List<string>();

            PlcType[] list;

            try
            {
                list = GetTypes(softwarePath, regexName).ToArray();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to retrieve type list for {SoftwarePath}", softwarePath);
                return exportList;
            }

            for (int i = 0; i < list.Count(); i++)
            {
                var type = list[i];

                _logger?.LogDebug("- Exporting type {Index}/{Total} : {Name}", i, list.Count(), type.Name);

                string path;
                if (preservePath)
                {
                    var groupPath = "";
                    if (type.Parent is PlcTypeGroup parentGroup)
                    {
                        groupPath = GetPlcTypeGroupPath(parentGroup);
                    }
                    path = Path.Combine(exportPath, groupPath.Replace('/', '\\'), $"{type.Name}.xml");
                }
                else
                {
                    path = Path.Combine(exportPath, $"{type.Name}.xml");
                }

                try
                {
                    if (!type.IsConsistent)
                    {
                        _logger?.LogWarning("Skipping inconsistent type {Name}", type.Name);
                        continue;
                    }

                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    if (File.Exists(path))
                    {
                        try
                        {
                            File.Delete(path);
                        }
                        catch (Exception ioEx)
                        {
                            failures.Add($"{type.Name}: cannot delete existing file ({ioEx.Message})");
                            _logger?.LogError(ioEx, "Delete failed for {File}", path);
                            continue;
                        }
                    }

                    try
                    {
                        type.Export(new FileInfo(path), ExportOptions.None);
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{type.Name}: export failed ({ex.Message})");
                        _logger?.LogError(ex, "Export failed for type {Type}", type.Name);
                        continue;
                    }

                    exportList.Add(type);
                }
                catch (Exception ex)
                {
                    failures.Add($"{type.Name}: unexpected exception ({ex.Message})");
                    _logger?.LogError(ex, "Unexpected error at type {Type}", type.Name);
                }
            }

            if (failures.Count > 0)
            {
                _logger?.LogWarning($"ExportXmlTypes completed with {failures.Count} failures out of {list.Count()}. First failure: {failures[0]}");
            }
            else
            {
                _logger?.LogInformation($"ExportXmlTypes completed successfully. Exported {exportList.Count} types.");
            }

            return exportList;
        }

        #endregion

        #region get...by path

        private PlcTypeGroup? GetPlcTypeGroupByPath(string softwarePath, string groupPath)
        {
            if (_project == null)
            {
                return null;
            }

            var plcSoftware = (GetSoftwareContainer(softwarePath)?.Software as PlcSoftware);

            return plcSoftware?.TypeGroup == null
                ? null
                : WalkGroups<PlcTypeGroup>(plcSoftware.TypeGroup, groupPath, TypeSubgroups, g => g.Name);
        }

        /// <param name="includeSystemRoot">See GetPlcBlockGroupPath.</param>
        private string GetPlcTypeGroupPath(PlcTypeGroup group, bool includeSystemRoot = true)
        {
            return BuildGroupPath<PlcTypeGroup>(
                group,
                g => g.Parent as PlcTypeGroup,
                g => g.Name,
                g => g is PlcTypeSystemGroup,
                includeSystemRoot);
        }

        #endregion

        #region get recursive ...

        private bool GetTypesRecursive(PlcTypeGroup group, List<PlcType> list, string regexName = "")
        {
            var before = list.Count;

            WalkRecursive<PlcTypeGroup, PlcType>(group, list, g => g.Types, TypeSubgroups, t => t.Name, regexName);

            return list.Count > before;
        }

        #endregion

        #region groups

        public PlcTypeUserGroup CreateTypeGroup(string softwarePath, string parentGroupPath, string name)
        {
            return Operation.Run(_logger, nameof(CreateTypeGroup), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var parent = GetPlcTypeGroupByPath(softwarePath, parentGroupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Type group not found at '{parentGroupPath}'.");

                    return parent.Groups.Create(name);
                },
                ("softwarePath", softwarePath), ("parentGroupPath", parentGroupPath), ("name", name));
        }

        public bool DeleteTypeGroup(string softwarePath, string groupPath)
        {
            return Operation.Run(_logger, nameof(DeleteTypeGroup), PortalErrorCode.DeleteFailed,
                () =>
                {
                    EnsureUserGroup(GetPlcTypeGroupByPath(softwarePath, groupPath), groupPath).Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath));
        }

        #endregion

        #region types (delete, rename)

        public bool DeleteType(string softwarePath, string typePath)
        {
            return Operation.Run(_logger, nameof(DeleteType), PortalErrorCode.DeleteFailed,
                () =>
                {
                    var type = GetType(softwarePath, typePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Type not found at '{typePath}'. Use 'plc_get_types' to list the available types.");

                    EnsureNotKnowHowProtected(type);
                    type.Delete();

                    return true;
                },
                ("softwarePath", softwarePath), ("typePath", typePath));
        }

        public bool RenameType(string softwarePath, string typePath, string newName)
        {
            return Operation.Run(_logger, nameof(RenameType), PortalErrorCode.RenameFailed,
                () =>
                {
                    EnsureValidName(newName);

                    var type = GetType(softwarePath, typePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Type not found at '{typePath}'. Use 'plc_get_types' to list the available types.");

                    EnsureNotKnowHowProtected(type);
                    type.Name = newName;

                    return true;
                },
                ("softwarePath", softwarePath), ("typePath", typePath), ("newName", newName));
        }

        #endregion

        #region types

        /// <summary>
        /// Copies a PLC data type. A type name is unique across the whole PLC, so a copy inside
        /// the same PLC needs <paramref name="newName"/>; a copy into another PLC may keep it.
        /// </summary>
        /// <param name="newName">Name of the copy. Required when source and target PLC are the same.</param>
        /// <param name="targetSoftwarePath">PLC that receives the copy; empty means the source PLC.</param>
        /// <param name="overwrite">Replace a type of the final name that already exists in the target PLC.</param>
        public PlcType CopyType(string softwarePath, string typePath, string targetGroupPath, string newName = "", string targetSoftwarePath = "", bool overwrite = false)
        {
            return Operation.Run(_logger, nameof(CopyType), PortalErrorCode.ImportFailed,
                () =>
                {
                    var source = GetType(softwarePath, typePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Type not found at '{typePath}'. Use 'plc_get_types' to list the available types.");

                    EnsureNotKnowHowProtected(source);
                    EnsureConsistent(source);

                    var targetSoftware = string.IsNullOrWhiteSpace(targetSoftwarePath) ? softwarePath : targetSoftwarePath;
                    var samePlc = IsSameSoftware(softwarePath, targetSoftware);
                    var finalName = string.IsNullOrWhiteSpace(newName) ? source.Name : newName.Trim();

                    EnsureValidName(finalName);

                    if (samePlc && finalName.Equals(source.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"A copy of '{source.Name}' inside the same PLC needs a different name: a PLC data type name is unique across the whole PLC. " +
                            "Pass 'newName', or pass 'targetSoftwarePath' to copy into another PLC. To change the group only, use 'plc_move_type'.");
                    }

                    var targetGroup = GetPlcTypeGroupByPath(targetSoftware, targetGroupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Type group not found at '{targetGroupPath}' in '{targetSoftware}'. Use 'plc_get_software_tree' to discover valid group paths.");

                    var existing = GetTypes(targetSoftware).FirstOrDefault(t => t.Name.Equals(finalName, StringComparison.OrdinalIgnoreCase));

                    if (existing != null && !overwrite)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"A type named '{finalName}' already exists at '{GetTypePath(existing)}' in '{targetSoftware}'. " +
                            "Choose another 'newName', or set 'overwrite' to replace it.");
                    }

                    var directory = CreateTempExportDirectory();

                    try
                    {
                        var file = new FileInfo(Path.Combine(directory, "type.xml"));

                        source.Export(file, ExportOptions.None);

                        if (!finalName.Equals(source.Name, StringComparison.Ordinal))
                        {
                            File.WriteAllText(file.FullName, RewriteExportedObject(File.ReadAllText(file.FullName), finalName, null));
                        }

                        targetGroup.Types.Import(file, overwrite ? ImportOptions.Override : ImportOptions.None);
                    }
                    finally
                    {
                        DeleteTempExportDirectory(directory);
                    }

                    // With 'overwrite' the replaced type keeps the group it already lived in.
                    return targetGroup.Types.Find(finalName)
                        ?? GetTypes(targetSoftware).FirstOrDefault(t => t.Name.Equals(finalName, StringComparison.OrdinalIgnoreCase))
                        ?? throw new PortalException(PortalErrorCode.ImportFailed,
                            $"Type '{finalName}' was imported into '{targetGroupPath}' but could not be found afterwards.");
                },
                ("softwarePath", softwarePath), ("typePath", typePath), ("targetGroupPath", targetGroupPath),
                ("newName", newName), ("targetSoftwarePath", targetSoftwarePath));
        }

        /// <summary>
        /// Moves a PLC data type into another group of the same PLC: export, delete the
        /// original, import into the target. See MoveBlock for why the delete comes first and
        /// how a failed import is undone.
        /// </summary>
        public PlcType MoveType(string softwarePath, string typePath, string targetGroupPath)
        {
            return Operation.Run(_logger, nameof(MoveType), PortalErrorCode.ImportFailed,
                () =>
                {
                    var source = GetType(softwarePath, typePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Type not found at '{typePath}'. Use 'plc_get_types' to list the available types.");

                    EnsureNotKnowHowProtected(source);
                    EnsureConsistent(source);

                    var sourceGroup = source.Parent as PlcTypeGroup
                        ?? throw new PortalException(PortalErrorCode.InvalidState,
                            $"Type '{source.Name}' is not inside a type group and cannot be moved.");

                    var targetGroup = GetPlcTypeGroupByPath(softwarePath, targetGroupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Type group not found at '{targetGroupPath}'. Use 'plc_get_software_tree' to discover valid group paths.");

                    EnsureDifferentGroup(GetPlcTypeGroupPath(sourceGroup, false), GetPlcTypeGroupPath(targetGroup, false), source.Name);

                    var name = source.Name;
                    var directory = CreateTempExportDirectory();

                    try
                    {
                        var file = new FileInfo(Path.Combine(directory, "type.xml"));

                        source.Export(file, ExportOptions.None);
                        source.Delete();

                        try
                        {
                            targetGroup.Types.Import(file, ImportOptions.None);
                        }
                        catch (Exception importError)
                        {
                            MoveRecovery.Restore(_logger, () => sourceGroup.Types.Import(file, ImportOptions.None), "Type", name, importError);

                            throw;
                        }
                    }
                    finally
                    {
                        DeleteTempExportDirectory(directory);
                    }

                    return targetGroup.Types.Find(name)
                        ?? throw new PortalException(PortalErrorCode.ImportFailed,
                            $"Type '{name}' was imported into '{targetGroupPath}' but could not be found afterwards.");
                },
                ("softwarePath", softwarePath), ("typePath", typePath), ("targetGroupPath", targetGroupPath));
        }

        #endregion
    }
}
