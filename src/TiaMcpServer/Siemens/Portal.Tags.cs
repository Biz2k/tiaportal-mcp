using Siemens.Engineering;
using Siemens.Engineering.SW.ExternalSources;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.WatchAndForceTables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using TiaMcpServer.ModelContextProtocol;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        // From the former Portal.Tags.cs:
        // PLC tag tables, tags and constants (read side).
        //
        // Callers: the GetTagTables / GetTagTableInfo / GetTags / GetTagInfo / GetConstants /
        // ExportXmlTagTable tools in McpServer.cs. Affected API: none existing - all members are new.
        // ExportXmlTagTable writes one .xml file to the path the caller supplies; nothing else here
        // touches the file system.
        //
        // Path shape: tag table paths are root-relative, e.g. "TagGroup1/Table1"; tag paths append
        // the tag, e.g. "TagGroup1/Table1/Tag_1". The system root ("PLC tags", localised by TIA
        // Portal) is not part of a reported path, so every path here round-trips back into these
        // resolvers. The preservePath export layout does prefix it - the same convention block and
        // type exports follow - and the resolvers accept it back as an optional leading segment.

        #region groups

        public PlcTagTableSystemGroup? GetTagTableRootGroup(string softwarePath)
        {
            return Operation.Run(_logger, nameof(GetTagTableRootGroup), PortalErrorCode.NotFound,
                () => GetPlcSoftwareOrThrow(softwarePath).TagTableGroup,
                ("softwarePath", softwarePath));
        }

        public PlcTagTableGroup? GetTagTableGroupByPath(string softwarePath, string groupPath)
        {
            return Operation.Run(_logger, nameof(GetTagTableGroupByPath), PortalErrorCode.NotFound,
                () =>
                {
                    var root = GetPlcSoftwareOrThrow(softwarePath).TagTableGroup;

                    return root == null
                        ? null
                        : WalkGroups<PlcTagTableGroup>(
                            root, StripTagTableSystemRoot(root, groupPath), g => g.Groups, g => g.Name);
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath));
        }

        /// <summary>Root-relative path of a tag table, e.g. "TagGroup1/Table1".</summary>
        /// <param name="includeSystemRoot">
        /// True prefixes the system group name as TIA Portal reports it in the current interface
        /// language (e.g. "PLC tags/TagGroup1/Table1"), which is the layout the preservePath
        /// exports write, matching the block and type exports. False (the default) yields a path
        /// that round-trips back into GetTagTable and GetTagTableGroupByPath.
        /// </param>
        public string GetTagTablePath(PlcTagTable table, bool includeSystemRoot = false)
        {
            if (table == null)
            {
                return string.Empty;
            }

            if (table.Parent is PlcTagTableGroup parentGroup)
            {
                var groupPath = BuildGroupPath<PlcTagTableGroup>(
                    parentGroup,
                    g => g.Parent as PlcTagTableGroup,
                    g => g.Name,
                    g => g is PlcTagTableSystemGroup,
                    includeSystemRoot);

                return JoinLeaf(groupPath, table.Name);
            }

            return table.Name;
        }

        /// <summary>
        /// Drops a leading system root segment ("PLC tags", or its translation in the current
        /// TIA Portal interface language) so a group path taken from the export layout resolves
        /// like the root-relative form.
        /// </summary>
        private static string StripTagTableSystemRoot(PlcTagTableSystemGroup root, string groupPath)
        {
            groupPath = NormalizeGroupPath(groupPath);

            if (groupPath.Length == 0)
            {
                return groupPath;
            }

            var separator = groupPath.IndexOf('/');
            var head = separator < 0 ? groupPath : groupPath.Substring(0, separator);

            if (!head.Equals(root.Name, StringComparison.OrdinalIgnoreCase))
            {
                return groupPath;
            }

            return separator < 0 ? string.Empty : groupPath.Substring(separator + 1);
        }

        #endregion

        #region tag tables

        public List<PlcTagTable> GetTagTables(string softwarePath, string regexName = "")
        {
            return Operation.Run(_logger, nameof(GetTagTables), PortalErrorCode.NotFound,
                () =>
                {
                    var list = new List<PlcTagTable>();
                    var root = GetPlcSoftwareOrThrow(softwarePath).TagTableGroup;

                    if (root != null)
                    {
                        WalkRecursive<PlcTagTableGroup, PlcTagTable>(
                            root, list, g => g.TagTables, g => g.Groups, t => t.Name, regexName);
                    }

                    return list;
                },
                ("softwarePath", softwarePath), ("regexName", regexName));
        }

        public PlcTagTable? GetTagTable(string softwarePath, string tagTablePath)
        {
            return Operation.Run(_logger, nameof(GetTagTable), PortalErrorCode.NotFound,
                () =>
                {
                    var (groupPath, tableName) = SplitPath(tagTablePath);
                    var group = GetTagTableGroupByPath(softwarePath, groupPath);

                    return group?.TagTables.Find(tableName);
                },
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath));
        }

        /// <summary>
        /// Exports one tag table to '&lt;exportPath&gt;/&lt;table&gt;.xml', or, with
        /// <paramref name="preservePath"/>, to
        /// '&lt;exportPath&gt;/PLC tags/&lt;groups&gt;/&lt;table&gt;.xml' - the system group name
        /// as TIA Portal reports it in the current interface language, the same way block and
        /// type exports mirror "Program blocks" and "PLC data types".
        /// Filesystem only - it does not modify the project, so it is not gated behind '--allow-write'.
        /// </summary>
        public PlcTagTable? ExportXmlTagTable(string softwarePath, string tagTablePath, string exportPath, bool preservePath = false)
        {
            return Operation.Run(_logger, nameof(ExportXmlTagTable), PortalErrorCode.ExportFailed,
                () =>
                {
                    var table = GetTagTable(softwarePath, tagTablePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Tag table not found at '{tagTablePath}'. Use 'plc_get_tag_tables' to list the available tables.");

                    var target = preservePath
                        ? Path.Combine(exportPath, GetTagTablePath(table, includeSystemRoot: true).Replace('/', '\\') + ".xml")
                        : Path.Combine(exportPath, $"{table.Name}.xml");

                    var directory = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    if (File.Exists(target))
                    {
                        File.Delete(target);
                    }

                    table.Export(new FileInfo(target), ExportOptions.None);

                    return table;
                },
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath), ("exportPath", exportPath));
        }

        #endregion

        #region tags and constants

        /// <param name="tagTablePath">Empty searches every tag table of the PLC.</param>
        public List<PlcTag> GetTags(string softwarePath, string tagTablePath = "", string regexName = "")
        {
            return Operation.Run(_logger, nameof(GetTags), PortalErrorCode.NotFound,
                () => SelectFromTables<PlcTag>(softwarePath, tagTablePath, t => t.Tags, t => t.Name, regexName),
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath), ("regexName", regexName));
        }

        /// <param name="tagPath">"Table/Tag" or "Group/Table/Tag" - the table part is required.</param>
        public PlcTag? GetTag(string softwarePath, string tagPath)
        {
            return Operation.Run(_logger, nameof(GetTag), PortalErrorCode.NotFound,
                () =>
                {
                    var (tablePath, tagName) = SplitPath(tagPath);

                    if (string.IsNullOrEmpty(tablePath))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"'{tagPath}' does not name a tag table. Use 'TableName/TagName', or 'Group/TableName/TagName'.");
                    }

                    var table = GetTagTable(softwarePath, tablePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Tag table not found at '{tablePath}'. Use 'plc_get_tag_tables' to list the available tables.");

                    return table.Tags.Find(tagName);
                },
                ("softwarePath", softwarePath), ("tagPath", tagPath));
        }

        public List<PlcUserConstant> GetUserConstants(string softwarePath, string tagTablePath = "", string regexName = "")
        {
            return Operation.Run(_logger, nameof(GetUserConstants), PortalErrorCode.NotFound,
                () => SelectFromTables<PlcUserConstant>(softwarePath, tagTablePath, t => t.UserConstants, c => c.Name, regexName),
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath), ("regexName", regexName));
        }

        public List<PlcSystemConstant> GetSystemConstants(string softwarePath, string tagTablePath = "", string regexName = "")
        {
            return Operation.Run(_logger, nameof(GetSystemConstants), PortalErrorCode.NotFound,
                () => SelectFromTables<PlcSystemConstant>(softwarePath, tagTablePath, t => t.SystemConstants, c => c.Name, regexName),
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath), ("regexName", regexName));
        }

        /// <summary>
        /// Collects one member collection across either a single tag table or every table of the
        /// PLC, applying the optional name regex.
        /// </summary>
        private List<T> SelectFromTables<T>(
            string softwarePath,
            string tagTablePath,
            Func<PlcTagTable, IEnumerable<T>> selector,
            Func<T, string> name,
            string regexName)
        {
            var tables = NormalizeGroupPath(tagTablePath).Length == 0
                ? GetTagTables(softwarePath)
                : new List<PlcTagTable>
                  {
                      GetTagTable(softwarePath, tagTablePath)
                          ?? throw new PortalException(PortalErrorCode.NotFound,
                              $"Tag table not found at '{tagTablePath}'. Use 'plc_get_tag_tables' to list the available tables.")
                  };

            var result = new List<T>();

            foreach (var table in tables)
            {
                foreach (var item in selector(table))
                {
                    if (string.IsNullOrEmpty(regexName) || MatchesRegex(name(item), regexName))
                    {
                        result.Add(item);
                    }
                }
            }

            return result;
        }

        /// <summary>An invalid pattern excludes the item rather than failing the whole call.</summary>
        private static bool MatchesRegex(string value, string pattern)
        {
            try
            {
                return Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        #endregion

        // From the former Portal.WatchTables.cs:
        // PLC watch and force tables (read side).
        //
        // Callers: the GetWatchTables / GetWatchTableInfo / GetForceTables / ExportXmlWatchTable tools
        // in McpServer.Tags.cs. Affected API: none existing - all members are new.
        // ExportXmlWatchTable writes one .xml to the caller-supplied path; nothing else does file I/O.
        //
        // Openness asymmetry to be aware of: watch tables can be created and deleted, force tables
        // cannot - PlcForceTableComposition has no Create and PlcForceTable has no Delete, because
        // the force table is system-owned (one per PLC). Only reads live here either way.

        #region watch tables

        public PlcWatchAndForceTableSystemGroup? GetWatchTableRootGroup(string softwarePath)
        {
            return Operation.Run(_logger, nameof(GetWatchTableRootGroup), PortalErrorCode.NotFound,
                () => GetPlcSoftwareOrThrow(softwarePath).WatchAndForceTableGroup,
                ("softwarePath", softwarePath));
        }

        public PlcWatchAndForceTableGroup? GetWatchTableGroupByPath(string softwarePath, string groupPath)
        {
            return Operation.Run(_logger, nameof(GetWatchTableGroupByPath), PortalErrorCode.NotFound,
                () =>
                {
                    var root = GetPlcSoftwareOrThrow(softwarePath).WatchAndForceTableGroup;

                    return root == null
                        ? null
                        : WalkGroups<PlcWatchAndForceTableGroup>(root, groupPath, g => g.Groups, g => g.Name);
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath));
        }

        /// <summary>Root-relative path of a watch table, e.g. "WatchGroup1/WatchTable_1".</summary>
        public string GetWatchTablePath(PlcWatchTable table)
        {
            return table == null ? string.Empty : BuildTablePath(table.Parent, table.Name);
        }

        /// <summary>Root-relative path of a force table.</summary>
        public string GetForceTablePath(PlcForceTable table)
        {
            return table == null ? string.Empty : BuildTablePath(table.Parent, table.Name);
        }

        private static string BuildTablePath(IEngineeringObject? parent, string name)
        {
            if (!(parent is PlcWatchAndForceTableGroup group))
            {
                return name;
            }

            var groupPath = BuildGroupPath<PlcWatchAndForceTableGroup>(
                group,
                g => g.Parent as PlcWatchAndForceTableGroup,
                g => g.Name,
                g => g is PlcWatchAndForceTableSystemGroup,
                includeSystemRoot: false);

            return JoinLeaf(groupPath, name);
        }

        public List<PlcWatchTable> GetWatchTables(string softwarePath, string regexName = "")
        {
            return Operation.Run(_logger, nameof(GetWatchTables), PortalErrorCode.NotFound,
                () =>
                {
                    var list = new List<PlcWatchTable>();
                    var root = GetPlcSoftwareOrThrow(softwarePath).WatchAndForceTableGroup;

                    if (root != null)
                    {
                        WalkRecursive<PlcWatchAndForceTableGroup, PlcWatchTable>(
                            root, list, g => g.WatchTables, g => g.Groups, t => t.Name, regexName);
                    }

                    return list;
                },
                ("softwarePath", softwarePath), ("regexName", regexName));
        }

        public PlcWatchTable? GetWatchTable(string softwarePath, string watchTablePath)
        {
            return Operation.Run(_logger, nameof(GetWatchTable), PortalErrorCode.NotFound,
                () =>
                {
                    var (groupPath, tableName) = SplitPath(watchTablePath);

                    return GetWatchTableGroupByPath(softwarePath, groupPath)?.WatchTables.Find(tableName);
                },
                ("softwarePath", softwarePath), ("watchTablePath", watchTablePath));
        }

        public List<PlcForceTable> GetForceTables(string softwarePath, string regexName = "")
        {
            return Operation.Run(_logger, nameof(GetForceTables), PortalErrorCode.NotFound,
                () =>
                {
                    var list = new List<PlcForceTable>();
                    var root = GetPlcSoftwareOrThrow(softwarePath).WatchAndForceTableGroup;

                    if (root != null)
                    {
                        WalkRecursive<PlcWatchAndForceTableGroup, PlcForceTable>(
                            root, list, g => g.ForceTables, g => g.Groups, t => t.Name, regexName);
                    }

                    return list;
                },
                ("softwarePath", softwarePath), ("regexName", regexName));
        }

        public PlcForceTable? GetForceTable(string softwarePath, string forceTablePath)
        {
            return Operation.Run(_logger, nameof(GetForceTable), PortalErrorCode.NotFound,
                () =>
                {
                    var (groupPath, tableName) = SplitPath(forceTablePath);

                    return GetWatchTableGroupByPath(softwarePath, groupPath)?.ForceTables.Find(tableName);
                },
                ("softwarePath", softwarePath), ("forceTablePath", forceTablePath));
        }

        /// <summary>
        /// Exports one watch table to '&lt;exportPath&gt;/&lt;table&gt;.xml'. Filesystem only, so
        /// it is not gated behind '--allow-write'.
        /// </summary>
        public PlcWatchTable? ExportXmlWatchTable(string softwarePath, string watchTablePath, string exportPath, bool preservePath = false)
        {
            return Operation.Run(_logger, nameof(ExportXmlWatchTable), PortalErrorCode.ExportFailed,
                () =>
                {
                    var table = GetWatchTable(softwarePath, watchTablePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Watch table not found at '{watchTablePath}'. Use 'plc_get_watch_tables' to list the available tables.");

                    var target = preservePath
                        ? Path.Combine(exportPath, GetWatchTablePath(table).Replace('/', '\\') + ".xml")
                        : Path.Combine(exportPath, $"{table.Name}.xml");

                    var directory = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    if (File.Exists(target))
                    {
                        File.Delete(target);
                    }

                    table.Export(new FileInfo(target), ExportOptions.None);

                    return table;
                },
                ("softwarePath", softwarePath), ("watchTablePath", watchTablePath), ("exportPath", exportPath));
        }

        #endregion

        // From the former Portal.Write.cs:
        // Create, update and delete for tag tables, tags, user constants, watch tables and external
        // sources. Block and type CRUD lives in Portal.Blocks.cs and Portal.Types.cs.
        // The external source create/delete members live in Portal.Sources.cs.
        //
        // Callers: the corresponding tools in McpServer.Tags.cs, registered only under
        // '--allow-write'. Affected API: none existing - all members are new. File reads: the
        // import methods and CreateExternalSourceFromFile read a caller-supplied file; nothing here
        // writes files. Project changes stay in memory until SaveProject / SaveSession.
        //
        // Openness constraints reflected here: system constants and force tables are read-only
        // (no Create/Delete exists for them), and the default tag table cannot be deleted.

        #region tag tables

        public PlcTagTable CreateTagTable(string softwarePath, string groupPath, string name)
        {
            return Operation.Run(_logger, nameof(CreateTagTable), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var group = GetTagTableGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Tag table group not found at '{groupPath}'.");

                    return group.TagTables.Create(name);
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath), ("name", name));
        }

        public bool DeleteTagTable(string softwarePath, string tagTablePath)
        {
            return Operation.Run(_logger, nameof(DeleteTagTable), PortalErrorCode.DeleteFailed,
                () =>
                {
                    var table = RequireTagTable(softwarePath, tagTablePath);

                    if (table.IsDefault)
                    {
                        throw new PortalException(PortalErrorCode.NotSupported,
                            $"'{tagTablePath}' is the default tag table and cannot be deleted.");
                    }

                    table.Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath));
        }

        public bool RenameTagTable(string softwarePath, string tagTablePath, string newName)
        {
            return Operation.Run(_logger, nameof(RenameTagTable), PortalErrorCode.RenameFailed,
                () =>
                {
                    EnsureValidName(newName);
                    RequireTagTable(softwarePath, tagTablePath).Name = newName;
                    return true;
                },
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath), ("newName", newName));
        }

        public PlcTagTableUserGroup CreateTagTableGroup(string softwarePath, string parentGroupPath, string name)
        {
            return Operation.Run(_logger, nameof(CreateTagTableGroup), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var parent = GetTagTableGroupByPath(softwarePath, parentGroupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Tag table group not found at '{parentGroupPath}'.");

                    return parent.Groups.Create(name);
                },
                ("softwarePath", softwarePath), ("parentGroupPath", parentGroupPath), ("name", name));
        }

        public bool DeleteTagTableGroup(string softwarePath, string groupPath)
        {
            return Operation.Run(_logger, nameof(DeleteTagTableGroup), PortalErrorCode.DeleteFailed,
                () =>
                {
                    var group = GetTagTableGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Tag table group not found at '{groupPath}'.");

                    var userGroup = group as PlcTagTableUserGroup
                        ?? throw new PortalException(PortalErrorCode.NotSupported,
                            $"'{groupPath}' is the system group 'PLC tags', which cannot be deleted.");

                    userGroup.Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath));
        }

        public bool ImportXmlTagTable(string softwarePath, string groupPath, string importPath, bool overwrite = true)
        {
            return Operation.Run(_logger, nameof(ImportXmlTagTable), PortalErrorCode.ImportFailed,
                () =>
                {
                    var group = GetTagTableGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Tag table group not found at '{groupPath}'.");

                    if (!File.Exists(importPath))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Import file '{importPath}' does not exist on the machine running this server.");
                    }

                    group.TagTables.Import(
                        new FileInfo(importPath),
                        overwrite ? ImportOptions.Override : ImportOptions.None);

                    return true;
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath), ("importPath", importPath));
        }

        #endregion

        #region tags and user constants

        public PlcTag CreateTag(string softwarePath, string tagTablePath, string name, string dataTypeName, string logicalAddress)
        {
            return Operation.Run(_logger, nameof(CreateTag), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var table = RequireTagTable(softwarePath, tagTablePath);

                    if (!string.IsNullOrWhiteSpace(dataTypeName))
                    {
                        RequireSoundTag(softwarePath, name, dataTypeName, logicalAddress, true);
                    }

                    return string.IsNullOrWhiteSpace(dataTypeName)
                        ? table.Tags.Create(name)
                        : table.Tags.Create(name, dataTypeName, logicalAddress);
                },
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath), ("name", name),
                ("dataTypeName", dataTypeName), ("logicalAddress", logicalAddress));
        }

        /// <summary>
        /// Applies a batch of changes to the tags of one table. The batch is checked as a whole first
        /// (PlcTagActions.Check): the caller runs this in a transaction, and after an exception of Openness that
        /// transaction cannot be committed, so a batch is applied completely or not at all.
        /// </summary>
        public List<UnifiedActionResult> ManagePlcTags(string softwarePath, string tagTablePath, IList<PlcTagAction>? actions)
        {
            return Operation.Run(_logger, nameof(ManagePlcTags), PortalErrorCode.InvalidState,
                () =>
                {
                    var table = RequireTagTable(softwarePath, tagTablePath);
                    var problems = PlcTagActions.Check(actions, table.Tags.Select(t => t.Name));

                    if (problems.Count > 0)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"{problems.Count} problem(s) in the actions; nothing was changed. " + string.Join(" | ", problems));
                    }

                    var results = new List<UnifiedActionResult>();

                    foreach (var action in actions!)
                    {
                        var verb = PlcTagActions.Verb(action)!;
                        var name = action.Name!.Trim();
                        var result = new UnifiedActionResult { Action = verb, Name = name, Status = "success" };

                        Progress(results.Count + 1, actions.Count, $"{tagTablePath}: {verb} '{name}'");

                        var tag = table.Tags.Find(name);

                        results.Add(result);

                        if (verb == "delete")
                        {
                            tag!.Delete();

                            continue;
                        }

                        // The type and the address as the tag will have them, checked as a pair before the write.
                        if (action.Type != null || action.Address != null)
                        {
                            RequireSoundTag(softwarePath, name, action.Type ?? tag?.DataTypeName, action.Address ?? tag?.LogicalAddress, action.Type != null);
                        }

                        if (tag == null)
                        {
                            tag = action.Type == null ? table.Tags.Create(name) : table.Tags.Create(name, action.Type, action.Address);
                            result.Notes.Add("Created.");
                        }
                        else
                        {
                            if (action.Type != null)
                            {
                                tag.DataTypeName = action.Type;
                                result.Applied.Add("dataType");
                            }

                            if (action.Address != null)
                            {
                                tag.LogicalAddress = action.Address;
                                result.Applied.Add("logicalAddress");
                            }
                        }

                        if (action.Comment != null)
                        {
                            foreach (var item in tag.Comment.Items)
                            {
                                item.Text = action.Comment;
                            }

                            result.Applied.Add("comment");
                        }

                        var newName = (action.NewName ?? string.Empty).Trim();

                        if (newName.Length > 0 && !string.Equals(newName, name, StringComparison.Ordinal))
                        {
                            EnsureValidName(newName);
                            tag.Name = newName;
                            result.Applied.Add("newName");
                            result.Name = newName;
                        }
                    }

                    return results;
                },
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath));
        }

        /// <summary>Refuses a data type or an address TIA Portal would store without a word (PlcTagRules).</summary>
        /// <param name="typeIsNew">False when the type is the one the tag already has: then only the address is judged against it.</param>
        private void RequireSoundTag(string softwarePath, string name, string? dataType, string? address, bool typeIsNew)
        {
            var problem = PlcTagRules.Problem(dataType, address,
                typeName => !typeIsNew || GetTypes(softwarePath).Any(t => string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase)));

            if (problem != null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Tag '{name}': {problem}");
            }
        }

        /// <summary>Every optional argument left null keeps the tag's current value.</summary>
        public bool UpdateTag(
            string softwarePath,
            string tagPath,
            string? newName = null,
            string? dataTypeName = null,
            string? logicalAddress = null,
            bool? externalAccessible = null,
            bool? externalVisible = null,
            bool? externalWritable = null)
        {
            return Operation.Run(_logger, nameof(UpdateTag), PortalErrorCode.RenameFailed,
                () =>
                {
                    var tag = GetTag(softwarePath, tagPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Tag not found at '{tagPath}'. Use 'plc_get_tags' to list the available tags.");

                    if (!string.IsNullOrWhiteSpace(dataTypeName) || logicalAddress != null)
                    RequireSoundTag(softwarePath, tag.Name,
                        string.IsNullOrWhiteSpace(dataTypeName) ? tag.DataTypeName : dataTypeName,
                        logicalAddress == null ? tag.LogicalAddress : logicalAddress,
                        !string.IsNullOrWhiteSpace(dataTypeName));

                    // Applied before the rename so a later lookup by the new name is not needed.
                    if (dataTypeName != null) tag.DataTypeName = dataTypeName;
                    if (logicalAddress != null) tag.LogicalAddress = logicalAddress;
                    if (externalAccessible.HasValue) tag.ExternalAccessible = externalAccessible.Value;
                    if (externalVisible.HasValue) tag.ExternalVisible = externalVisible.Value;
                    if (externalWritable.HasValue) tag.ExternalWritable = externalWritable.Value;

                    if (newName != null)
                    {
                        EnsureValidName(newName);
                        tag.Name = newName;
                    }

                    return true;
                },
                ("softwarePath", softwarePath), ("tagPath", tagPath));
        }

        public bool DeleteTag(string softwarePath, string tagPath)
        {
            return Operation.Run(_logger, nameof(DeleteTag), PortalErrorCode.DeleteFailed,
                () =>
                {
                    var tag = GetTag(softwarePath, tagPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Tag not found at '{tagPath}'. Use 'plc_get_tags' to list the available tags.");

                    tag.Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("tagPath", tagPath));
        }

        public PlcUserConstant CreateUserConstant(string softwarePath, string tagTablePath, string name, string dataTypeName, string value)
        {
            return Operation.Run(_logger, nameof(CreateUserConstant), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var table = RequireTagTable(softwarePath, tagTablePath);

                    return string.IsNullOrWhiteSpace(dataTypeName)
                        ? table.UserConstants.Create(name)
                        : table.UserConstants.Create(name, dataTypeName, value);
                },
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath), ("name", name));
        }

        /// <summary>Every optional argument left null keeps the constant's current value.</summary>
        public bool UpdateUserConstant(
            string softwarePath,
            string tagTablePath,
            string name,
            string? newName = null,
            string? dataTypeName = null,
            string? value = null)
        {
            return Operation.Run(_logger, nameof(UpdateUserConstant), PortalErrorCode.RenameFailed,
                () =>
                {
                    var constant = RequireUserConstant(softwarePath, tagTablePath, name);

                    if (dataTypeName != null) constant.DataTypeName = dataTypeName;
                    if (value != null) constant.Value = value;

                    if (newName != null)
                    {
                        EnsureValidName(newName);
                        constant.Name = newName;
                    }

                    return true;
                },
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath), ("name", name));
        }

        public bool DeleteUserConstant(string softwarePath, string tagTablePath, string name)
        {
            return Operation.Run(_logger, nameof(DeleteUserConstant), PortalErrorCode.DeleteFailed,
                () =>
                {
                    RequireUserConstant(softwarePath, tagTablePath, name).Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("tagTablePath", tagTablePath), ("name", name));
        }

        #endregion

        #region watch tables

        public PlcWatchTable CreateWatchTable(string softwarePath, string groupPath, string name)
        {
            return Operation.Run(_logger, nameof(CreateWatchTable), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var group = GetWatchTableGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Watch table group not found at '{groupPath}'.");

                    return group.WatchTables.Create(name);
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath), ("name", name));
        }

        public bool RenameWatchTable(string softwarePath, string watchTablePath, string newName)
        {
            return Operation.Run(_logger, nameof(RenameWatchTable), PortalErrorCode.RenameFailed,
                () =>
                {
                    EnsureValidName(newName);
                    RequireWatchTable(softwarePath, watchTablePath).Name = newName;
                    return true;
                },
                ("softwarePath", softwarePath), ("watchTablePath", watchTablePath), ("newName", newName));
        }

        public bool DeleteWatchTable(string softwarePath, string watchTablePath)
        {
            return Operation.Run(_logger, nameof(DeleteWatchTable), PortalErrorCode.DeleteFailed,
                () =>
                {
                    RequireWatchTable(softwarePath, watchTablePath).Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("watchTablePath", watchTablePath));
        }

        public PlcWatchAndForceTableUserGroup CreateWatchTableGroup(string softwarePath, string parentGroupPath, string name)
        {
            return Operation.Run(_logger, nameof(CreateWatchTableGroup), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var parent = GetWatchTableGroupByPath(softwarePath, parentGroupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Watch table group not found at '{parentGroupPath}'.");

                    return parent.Groups.Create(name);
                },
                ("softwarePath", softwarePath), ("parentGroupPath", parentGroupPath), ("name", name));
        }

        public bool DeleteWatchTableGroup(string softwarePath, string groupPath)
        {
            return Operation.Run(_logger, nameof(DeleteWatchTableGroup), PortalErrorCode.DeleteFailed,
                () =>
                {
                    var group = GetWatchTableGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Watch table group not found at '{groupPath}'.");

                    var userGroup = group as PlcWatchAndForceTableUserGroup
                        ?? throw new PortalException(PortalErrorCode.NotSupported,
                            $"'{groupPath}' is the system group 'Watch and force tables', which cannot be deleted.");

                    userGroup.Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath));
        }

        public bool ImportWatchTable(string softwarePath, string groupPath, string importPath, bool overwrite = true)
        {
            return Operation.Run(_logger, nameof(ImportWatchTable), PortalErrorCode.ImportFailed,
                () =>
                {
                    var group = GetWatchTableGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Watch table group not found at '{groupPath}'.");

                    if (!File.Exists(importPath))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Import file '{importPath}' does not exist on the machine running this server.");
                    }

                    group.WatchTables.Import(
                        new FileInfo(importPath),
                        overwrite ? ImportOptions.Override : ImportOptions.None);

                    return true;
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath), ("importPath", importPath));
        }

        /// <summary>
        /// Adds and deletes the rows of a watch table. TIA Portal has no call for it (see <see cref="PlcWatchTableEdit"/>), so the table is
        /// exported, edited as text and imported over itself; the result is read back and compared with what was asked for, and a
        /// difference fails the call (and with it the transaction). Rows are counted from 0, comment rows included.
        /// </summary>
        public List<UnifiedActionResult> ManageWatchTableEntries(string softwarePath, string watchTablePath, IList<WatchTableEntryAction>? actions)
        {
            return Operation.Run(_logger, nameof(ManageWatchTableEntries), PortalErrorCode.InvalidState,
                () =>
                {
                    var software = GetPlcSoftwareOrThrow(softwarePath);
                    var table = GetWatchTable(softwarePath, watchTablePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound, $"Watch table not found at '{watchTablePath}'. Use 'plc_get_watch_tables' to list the available tables.");

                    if (!table.IsConsistent)
                    {
                        throw new PortalException(PortalErrorCode.InvalidState, $"Watch table '{watchTablePath}' is inconsistent, and TIA Portal exports only a consistent table. Open it in TIA Portal and correct the rows with errors (or 'plc_manage_watch_table_entries' with a 'clear' after deleting the table content by hand).");
                    }

                    // the import replaces the table: the object read here is disposed afterwards, so its name is kept
                    var tableName = table.Name;
                    var folder = Path.Combine(Path.GetTempPath(), "tiamcp-watch-" + Guid.NewGuid().ToString("N").Substring(0, 8));

                    Directory.CreateDirectory(folder);

                    try
                    {
                        var exported = Path.Combine(folder, "table.xml");

                        table.Export(new FileInfo(exported), ExportOptions.None);

                        var document = XDocument.Load(exported);
                        var rows = PlcWatchTableEdit.ReadRows(document);
                        var needsSymbols = actions != null && actions.Any(a => a?.Name != null);
                        HashSet<string>? symbols = null;

                        if (needsSymbols)
                        {
                            symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                            foreach (var tag in GetTags(softwarePath))
                            {
                                symbols.Add(tag.Name);
                            }

                            foreach (var block in GetBlocks(softwarePath))
                            {
                                symbols.Add(block.Name);
                            }
                        }

                        var problems = PlcWatchTableEdit.Check(actions, rows, symbols == null ? (Func<string, bool>?)null : symbols.Contains);

                        if (problems.Count > 0)
                        {
                            throw new PortalException(PortalErrorCode.InvalidParams, $"{problems.Count} problem(s) in the actions; nothing was changed. " + string.Join(" | ", problems));
                        }

                        var languages = RequireProject().LanguageSettings.ActiveLanguages.Select(l => l.Culture.Name).ToList();

                        PlcWatchTableEdit.Apply(document, actions!, languages);

                        var expected = PlcWatchTableEdit.ReadRows(document);
                        var edited = Path.Combine(folder, "edited.xml");

                        File.WriteAllText(edited, document.Declaration + Environment.NewLine + document.ToString(), new UTF8Encoding(false));

                        var (groupPath, _) = SplitPath(watchTablePath);
                        var group = GetWatchTableGroupByPath(softwarePath, groupPath)!;

                        Progress(1, 2, $"{watchTablePath}: import of {expected.Count} row(s)");

                        try
                        {
                            group.WatchTables.Import(new FileInfo(edited), ImportOptions.Override);
                        }
                        catch (Exception ex) when (ex is not PortalException && ErrorText.Describe(ex).IndexOf("read-only", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                "TIA Portal refused a field of a row as read-only, so nothing is kept. 'displayFormat' goes only with an 'address' (a tag gives its own), and 'modifyValue' only on a row that has a display format - a tag or an absolute address, not a member of a data block. TIA Portal said: " + ErrorText.Describe(ex));
                        }

                        var after = group.WatchTables.Find(tableName)
                            ?? throw new PortalException(PortalErrorCode.InvalidState, $"After the import the watch table '{watchTablePath}' is gone; nothing is kept.");

                        var actual = new List<PlcWatchTableEdit.Row>();

                        foreach (var entry in after.Entries)
                        {
                            actual.Add(entry is PlcWatchTableEntry w
                                ? new PlcWatchTableEdit.Row
                                {
                                    Kind = "Watch", Name = w.Name, Address = w.Address, DisplayFormat = w.DisplayFormat.ToString(), MonitorTrigger = w.MonitorTrigger.ToString(),
                                    ModifyTrigger = w.ModifyTrigger.ToString(), ModifyValue = w.ModifyValue
                                }
                                : new PlcWatchTableEdit.Row { Kind = "Comment" });
                        }

                        var differences = PlcWatchTableEdit.Compare(expected, actual);

                        if (!after.IsConsistent)
                        {
                            differences.Add("the table is inconsistent after the import");
                        }

                        if (differences.Count > 0)
                        {
                            throw new PortalException(PortalErrorCode.InvalidState, "The watch table does not hold what was asked for, so nothing is kept: " + string.Join("; ", differences) + ".");
                        }

                        return actions!.Select(a => new UnifiedActionResult
                        {
                            Action = a.Action!.Trim().ToLowerInvariant(),
                            Name = a.Name ?? a.Address ?? (a.Index != null ? $"row {a.Index}" : null),
                            Status = "success",
                            Notes = { $"The table now has {actual.Count} row(s)." }
                        }).ToList();
                    }
                    finally
                    {
                        try
                        {
                            Directory.Delete(folder, true);
                        }
                        catch (IOException)
                        {
                        }
                    }
                },
                ("softwarePath", softwarePath), ("watchTablePath", watchTablePath));
        }

        #endregion

        #region lookup guards

        private PlcTagTable RequireTagTable(string softwarePath, string tagTablePath)
        {
            return GetTagTable(softwarePath, tagTablePath)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"Tag table not found at '{tagTablePath}'. Use 'plc_get_tag_tables' to list the available tables.");
        }

        private PlcWatchTable RequireWatchTable(string softwarePath, string watchTablePath)
        {
            return GetWatchTable(softwarePath, watchTablePath)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"Watch table not found at '{watchTablePath}'. Use 'plc_get_watch_tables' to list the available tables.");
        }

        private PlcUserConstant RequireUserConstant(string softwarePath, string tagTablePath, string name)
        {
            return RequireTagTable(softwarePath, tagTablePath).UserConstants.Find(name)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"User constant '{name}' not found in tag table '{tagTablePath}'. " +
                    "System constants are read-only and cannot be modified through Openness.");
        }

        #endregion
    }
}
