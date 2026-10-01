using Siemens.Engineering;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.Multiuser;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.ExternalSources;
// Imported rather than written inline: TiaMcpServer.Siemens.Engineering shadows the
// Siemens.Engineering namespace, so a fully qualified Siemens.Engineering.SW.Types.PlcType
// does not resolve from inside this namespace.
using Siemens.Engineering.SW.Types;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        // From the former Portal.ExternalSources.cs:
        // PLC external source files (read side).
        //
        // Callers: the GetExternalSources / GetExternalSourceInfo tools in
        // McpServer.Documents.ExternalSources.cs. Affected API: none existing - all members are new.
        // Reads/writes no data files: the source files themselves live inside the project.
        //
        // PlcExternalSource exposes only Name and Parent as typed properties, so anything else a
        // caller needs (file path, timestamps) has to come from the generic attribute bag via
        // Helper.GetAttributeList. No attribute name is hard-coded here for that reason.

        #region external sources (read)

        public PlcExternalSourceSystemGroup? GetExternalSourceRootGroup(string softwarePath)
        {
            return Operation.Run(_logger, nameof(GetExternalSourceRootGroup), PortalErrorCode.NotFound,
                () => GetPlcSoftwareOrThrow(softwarePath).ExternalSourceGroup,
                ("softwarePath", softwarePath));
        }

        public PlcExternalSourceGroup? GetExternalSourceGroupByPath(string softwarePath, string groupPath)
        {
            return Operation.Run(_logger, nameof(GetExternalSourceGroupByPath), PortalErrorCode.NotFound,
                () =>
                {
                    var root = GetPlcSoftwareOrThrow(softwarePath).ExternalSourceGroup;

                    return root == null
                        ? null
                        : WalkGroups<PlcExternalSourceGroup>(root, groupPath, g => g.Groups, g => g.Name);
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath));
        }

        /// <summary>Root-relative path of an external source, e.g. "SourceGroup1/Source_1".</summary>
        public string GetExternalSourcePath(PlcExternalSource source)
        {
            if (source == null)
            {
                return string.Empty;
            }

            if (source.Parent is PlcExternalSourceGroup group)
            {
                var groupPath = BuildGroupPath<PlcExternalSourceGroup>(
                    group,
                    g => g.Parent as PlcExternalSourceGroup,
                    g => g.Name,
                    g => g is PlcExternalSourceSystemGroup,
                    includeSystemRoot: false);

                return string.IsNullOrEmpty(groupPath) ? source.Name : $"{groupPath}/{source.Name}";
            }

            return source.Name;
        }

        public List<PlcExternalSource> GetExternalSources(string softwarePath, string regexName = "")
        {
            return Operation.Run(_logger, nameof(GetExternalSources), PortalErrorCode.NotFound,
                () =>
                {
                    var list = new List<PlcExternalSource>();
                    var root = GetPlcSoftwareOrThrow(softwarePath).ExternalSourceGroup;

                    if (root != null)
                    {
                        WalkRecursive<PlcExternalSourceGroup, PlcExternalSource>(
                            root, list, g => g.ExternalSources, g => g.Groups, s => s.Name, regexName);
                    }

                    return list;
                },
                ("softwarePath", softwarePath), ("regexName", regexName));
        }

        public PlcExternalSource? GetExternalSource(string softwarePath, string sourcePath)
        {
            return Operation.Run(_logger, nameof(GetExternalSource), PortalErrorCode.NotFound,
                () =>
                {
                    var (groupPath, sourceName) = SplitPath(sourcePath);

                    return GetExternalSourceGroupByPath(softwarePath, groupPath)?.ExternalSources.Find(sourceName);
                },
                ("softwarePath", softwarePath), ("sourcePath", sourcePath));
        }

        #endregion

        #region external sources (write)

        public PlcExternalSource CreateExternalSourceFromFile(string softwarePath, string groupPath, string name, string filePath)
        {
            return Operation.Run(_logger, nameof(CreateExternalSourceFromFile), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var group = GetExternalSourceGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"External source group not found at '{groupPath}'.");

                    if (!File.Exists(filePath))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Source file '{filePath}' does not exist on the machine running this server.");
                    }

                    return group.ExternalSources.CreateFromFile(name, filePath);
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath), ("name", name), ("filePath", filePath));
        }

        public bool DeleteExternalSource(string softwarePath, string sourcePath)
        {
            return Operation.Run(_logger, nameof(DeleteExternalSource), PortalErrorCode.DeleteFailed,
                () =>
                {
                    var source = GetExternalSource(softwarePath, sourcePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"External source not found at '{sourcePath}'.");

                    source.Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("sourcePath", sourcePath));
        }

        public PlcExternalSourceUserGroup CreateExternalSourceGroup(string softwarePath, string parentGroupPath, string name)
        {
            return Operation.Run(_logger, nameof(CreateExternalSourceGroup), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var parent = GetExternalSourceGroupByPath(softwarePath, parentGroupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"External source group not found at '{parentGroupPath}'.");

                    return parent.Groups.Create(name);
                },
                ("softwarePath", softwarePath), ("parentGroupPath", parentGroupPath), ("name", name));
        }

        public bool DeleteExternalSourceGroup(string softwarePath, string groupPath)
        {
            return Operation.Run(_logger, nameof(DeleteExternalSourceGroup), PortalErrorCode.DeleteFailed,
                () =>
                {
                    var group = GetExternalSourceGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"External source group not found at '{groupPath}'.");

                    var userGroup = group as PlcExternalSourceUserGroup
                        ?? throw new PortalException(PortalErrorCode.NotSupported,
                            $"'{groupPath}' is the system group 'External source files', which cannot be deleted.");

                    userGroup.Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath));
        }

        /// <summary>
        /// Compiles an external source into blocks. An empty target generates into the source's
        /// default location; a target must resolve to a block USER group - the system root does
        /// not bind to the PlcBlockUserGroup overload.
        /// </summary>
        public List<string> GenerateBlocksFromSource(string softwarePath, string sourcePath, string targetGroupPath = "", bool keepOnError = false)
        {
            return Operation.Run(_logger, nameof(GenerateBlocksFromSource), PortalErrorCode.CreateFailed,
                () =>
                {
                    var source = GetExternalSource(softwarePath, sourcePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"External source not found at '{sourcePath}'.");

                    var option = keepOnError ? GenerateBlockOption.KeepOnError : GenerateBlockOption.None;

                    // Verified against the V21 reference assembly: the overloads return
                    // IList<IEngineeringObject>, which holds PlcBlock and PlcType instances.
                    IList<IEngineeringObject> generated;

                    if (string.IsNullOrWhiteSpace(targetGroupPath))
                    {
                        generated = source.GenerateBlocksFromSource(option);
                    }
                    else
                    {
                        var target = GetPlcBlockGroupByPath(softwarePath, targetGroupPath) as PlcBlockUserGroup
                            ?? throw new PortalException(PortalErrorCode.InvalidParams,
                                $"'{targetGroupPath}' must be a block user group. Blocks cannot be generated into the 'Program blocks' system root; " +
                                "leave targetGroupPath empty to use the source's default location.");

                        generated = source.GenerateBlocksFromSource(target, option);
                    }

                    var names = new List<string>();

                    foreach (var item in generated)
                    {
                        if (item is PlcBlock block)
                        {
                            names.Add(block.Name);
                        }
                        else if (item is PlcType type)
                        {
                            names.Add(type.Name);
                        }
                        else
                        {
                            names.Add(item?.ToString() ?? string.Empty);
                        }
                    }

                    return names;
                },
                ("softwarePath", softwarePath), ("sourcePath", sourcePath), ("targetGroupPath", targetGroupPath));
        }

        #endregion
    }
}
