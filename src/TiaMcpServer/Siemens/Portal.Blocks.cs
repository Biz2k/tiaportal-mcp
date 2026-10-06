using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.Multiuser;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Blocks.Interface;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Siemens.Engineering.Compiler;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        #region blocks

        public PlcBlock? GetBlock(string softwarePath, string blockPath)
        {
            _logger?.LogInformation($"Getting block by path: {blockPath}");

            if (IsProjectNull())
            {
                return null;
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                var blockGroup = plcSoftware?.BlockGroup;

                if (blockGroup != null)
                {
                    var (path, name) = SplitPath(blockPath);

                    var group = GetPlcBlockGroupByPath(softwarePath, path);
                    if (group != null)
                    {
                        return FindByName<PlcBlock>(group.Blocks, name, b => b.Name);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Root-relative path of a block, e.g. "Common/CarrierRegister/GLOBAL_POSITIONING".
        /// The system group ("Program blocks") is deliberately excluded so the result can be fed
        /// straight back into GetBlock/ExportXmlBlock as a blockPath.
        /// </summary>
        public string GetBlockPath(PlcBlock block)
        {
            if (block == null)
            {
                return string.Empty;
            }

            if (block.Parent is PlcBlockGroup parentGroup)
            {
                var groupPath = GetPlcBlockGroupPath(parentGroup, includeSystemRoot: false);
                return JoinLeaf(groupPath, block.Name);
            }

            return block.Name;
        }

        public List<PlcBlock> GetBlocks(string softwarePath, string regexName = "")
        {
            _logger?.LogInformation("Getting blocks...");

            if (IsProjectNull())
            {
                return [];
            }

            // A path that names no PLC software is an error, not an empty list.
            if (GetSoftwareContainer(softwarePath)?.Software is not PlcSoftware)
            {
                throw new PortalException(PortalErrorCode.NotFound, DescribeMissingSoftware(softwarePath, "No PLC software found"));
            }

            var list = new List<PlcBlock>();

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    var group = plcSoftware?.BlockGroup;

                    if (group != null)
                    {
                        GetBlocksRecursive(group, list, regexName);
                    }
                }
            }
            catch (Exception)
            {
                // Console.WriteLine($"Error getting blocks: {ex.Message}");
            }

            return list;
        }

        public PlcBlockGroup? GetBlockRootGroup(string softwarePath)
        {
            _logger?.LogInformation("Getting block root group...");

            if (IsProjectNull())
            {
                return null;
            }

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    return plcSoftware.BlockGroup;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting block root group");
            }

            return null;
        }

        public PlcBlock? ExportXmlBlock(string softwarePath, string blockPath, string exportPath, bool preservePath = false)
        {
            _logger?.LogInformation($"Exporting block by path: {blockPath}");

            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, NoProjectMessage);
                }

                var block = GetBlock(softwarePath, blockPath);

                if (block == null)
                {
                    throw new PortalException(PortalErrorCode.NotFound, "Block not found");
                }

                if (preservePath)
                {
                    var groupPath = "";
                    if (block.Parent is PlcBlockGroup parentGroup)
                    {
                        groupPath = GetPlcBlockGroupPath(parentGroup);
                    }

                    exportPath = Path.Combine(exportPath, groupPath.Replace('/', '\\'), $"{block.Name}.xml");
                }
                else
                {
                    exportPath = Path.Combine(exportPath, $"{block.Name}.xml");
                }

                // TIA Portal never exports inconsistent blocks
                if (!block.IsConsistent)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Block is inconsistent; TIA Portal does not export inconsistent blocks.");
                }

                if (File.Exists(exportPath))
                {
                    File.Delete(exportPath);
                }

                block.Export(new FileInfo(exportPath), ExportOptions.None);

                return block;
            }
            catch (Exception ex)
            {
                //If the exception is already a PortalException, use it; otherwise, wrap it in a new PortalException
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, "Export failed", null, ex);

                pex.Data["softwarePath"] = softwarePath;
                pex.Data["blockPath"] = blockPath;
                pex.Data["exportPath"] = exportPath;

                _logger?.LogError(pex, "ExportXmlBlock failed for {SoftwarePath} {BlockPath} -> {ExportPath}", softwarePath, blockPath, exportPath);
                throw pex;
            }
        }

        /// <summary>
        /// Imports a SimaticML block file into a group. With 'overwrite' a block of the same name is replaced
        /// (wherever it lives, so the result may not be in the target group); without it TIA Portal refuses a
        /// name that is taken. Every failure reaches the caller with its reason.
        /// </summary>
        public bool ImportXmlBlock(string softwarePath, string groupPath, string importPath, bool overwrite = true)
        {
            return Operation.Run(_logger, nameof(ImportXmlBlock), PortalErrorCode.ImportFailed,
                () =>
                {
                    var group = GetPlcBlockGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block group not found at '{groupPath}'. Use 'plc_get_software_tree' to discover valid group paths.");

                    if (!File.Exists(importPath))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Import file '{importPath}' does not exist on the machine running this server.");
                    }

                    var list = group.Blocks.Import(new FileInfo(importPath), overwrite ? ImportOptions.Override : ImportOptions.None);

                    if (list == null || list.Count == 0)
                    {
                        throw new PortalException(PortalErrorCode.ImportFailed, $"TIA Portal imported nothing from '{importPath}'.");
                    }

                    return true;
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath), ("importPath", importPath), ("overwrite", overwrite));
        }

        public IEnumerable<PlcBlock>? ExportXmlBlocks(string softwarePath, string exportPath, string regexName = "", bool preservePath = false)
        {
            _logger?.LogInformation("Exporting blocks...");

            if (IsProjectNull())
            {
                return null;
            }

            var exportList = new List<PlcBlock>();
            var failures = new List<string>();
            
            PlcBlock[] list;

            try
            {
                list = GetBlocks(softwarePath, regexName).ToArray();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to retrieve block list for {SoftwarePath}", softwarePath);
                return exportList;
            }

            for (int k = 0; k < list.Count(); k++)
            {
                var block = list[k];

                _logger?.LogDebug($"- Exporting block {k}/{list.Count()} : {block.Name}");

                string path;
                if (preservePath)
                {
                    var groupPath = "";
                    if (block.Parent is PlcBlockGroup parentGroup)
                    {
                        groupPath = GetPlcBlockGroupPath(parentGroup);
                    }
                    path = Path.Combine(exportPath, groupPath.Replace('/', '\\'), $"{block.Name}.xml");
                }
                else
                {
                    path = Path.Combine(exportPath, $"{block.Name}.xml");
                }

                try
                {
                    if (!block.IsConsistent)
                    {
                        _logger?.LogWarning("Skipping inconsistent block {Name}", block.Name);

                        continue;
                    }

                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    if (File.Exists(path))
                    {
                        try { File.Delete(path); }
                        catch (Exception ioEx)
                        {
                            failures.Add($"{block.Name}: cannot delete existing file ({ioEx.Message})");
                            _logger?.LogError(ioEx, "Delete failed for {File}", path);

                            continue;
                        }
                    }

                    try
                    {
                        block.Export(new FileInfo(path), ExportOptions.None);
                    }
                    catch (LicenseNotFoundException licEx)
                    {
                        failures.Add($"{block.Name}: license not found ({licEx.Message})");
                        _logger?.LogError(licEx, "License issue exporting {Block}", block.Name);

                        continue;
                    }
                    catch (EngineeringTargetInvocationException engEx)
                    {
                        failures.Add($"{block.Name}: target invocation failed ({engEx.Message})");
                        _logger?.LogError(engEx, "TargetInvocationException exporting {Block}", block.Name);

                        continue;
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{block.Name}: export failed ({ex.Message})");
                        _logger?.LogError(ex, "Export failed for {Block}", block.Name);

                        continue;
                    }

                    exportList.Add(block);
                }
                catch (Exception ex)
                {
                    // Catch only truly unexpected wrapper-level errors
                    failures.Add($"{block.Name}: unexpected exception ({ex.Message})");
                    _logger?.LogError(ex, "Unexpected error at block {Block}", block.Name);
                    // continue with next block
                }
            }

            if (failures.Count > 0)
            {
                _logger?.LogWarning($"ExportXmlBlocks completed with {failures.Count} failures out of {list.Count()}. First failure: {failures[0]}");
                // Optionally: _logger?.LogDebug("All failures: {Failures}", string.Join("; ", failures));
            }
            else
            {
                _logger?.LogInformation($"ExportXmlBlocks completed successfully. Exported {exportList.Count} blocks.");
            }

            return exportList;
        }

        #endregion

        #region get...by path

        private PlcBlockGroup? GetPlcBlockGroupByPath(string softwarePath, string groupPath)
        {
            if (_project == null)
            {
                return null;
            }

            var plcSoftware = (GetSoftwareContainer(softwarePath)?.Software as PlcSoftware);

            return plcSoftware?.BlockGroup == null
                ? null
                : WalkGroups<PlcBlockGroup>(plcSoftware.BlockGroup, groupPath, BlockSubgroups, g => g.Name);
        }

        /// <param name="includeSystemRoot">
        /// True (the default) prefixes the system group name, e.g. "Program blocks/1_Tests".
        /// That is the layout the preservePath exports write and Test_415_ImportBlock reads back.
        /// False yields "1_Tests", which round-trips into GetPlcBlockGroupByPath.
        /// </param>
        private string GetPlcBlockGroupPath(PlcBlockGroup group, bool includeSystemRoot = true)
        {
            return BuildGroupPath<PlcBlockGroup>(
                group,
                g => g.Parent as PlcBlockGroup,
                g => g.Name,
                g => g is PlcBlockSystemGroup,
                includeSystemRoot);
        }

        #endregion

        #region get recursive ...

        private bool GetBlocksRecursive(PlcBlockGroup group, List<PlcBlock> list, string regexName = "")
        {
            var before = list.Count;

            WalkRecursive<PlcBlockGroup, PlcBlock>(group, list, g => g.Blocks, BlockSubgroups, b => b.Name, regexName);

            // The previous implementation reported only the last subgroup's outcome; report
            // whether anything at all was collected.
            return list.Count > before;
        }

        #endregion

        // From the former Portal.BlockCrud.cs:
        // Create, rename and delete for program blocks, PLC data types and their groups.
        // The type half of this set (type groups, DeleteType, RenameType) lives in Portal.Types.cs.
        //
        // Callers: the corresponding tools in McpServer.Blocks.cs and McpServer.Types.cs, which are only registered when the
        // server runs with '--allow-write'. Affected API: none existing - all members are new.
        // Reads/writes no data files; these mutate the open project in memory until SaveProject.
        //
        // Openness constraints that shape this file:
        // - PlcBlockComposition has no generic Create(name) and no CreateFrom(PlcBlock), so the
        // only ways to make a block are CreateFB, CreateInstanceDB and XML import.
        // - Only user groups can be created or deleted; the system roots cannot.
        // - Name has a public setter on blocks, types and user groups, so rename is assignment.

        #region guards

        /// <summary>
        /// Know-how protected objects reject edits deep inside Openness with an opaque error;
        /// failing here gives the caller something actionable instead.
        /// </summary>
        private static void EnsureNotKnowHowProtected(PlcBlock block)
        {
            if (block.IsKnowHowProtected)
            {
                throw new PortalException(PortalErrorCode.InvalidState,
                    $"Block '{block.Name}' is know-how protected. Remove the protection in TIA Portal before modifying it.");
            }
        }

        private static void EnsureNotKnowHowProtected(PlcType type)
        {
            if (type.IsKnowHowProtected)
            {
                throw new PortalException(PortalErrorCode.InvalidState,
                    $"Type '{type.Name}' is know-how protected. Remove the protection in TIA Portal before modifying it.");
            }
        }

        private static PlcBlockUserGroup EnsureUserGroup(PlcBlockGroup? group, string groupPath)
        {
            if (group == null)
            {
                throw new PortalException(PortalErrorCode.NotFound,
                    $"Block group not found at '{groupPath}'. Use 'plc_get_software_tree' to discover valid group paths.");
            }

            return group as PlcBlockUserGroup
                ?? throw new PortalException(PortalErrorCode.NotSupported,
                    $"'{groupPath}' is the system group 'Program blocks', which cannot be created, renamed or deleted.");
        }

        private static PlcTypeUserGroup EnsureUserGroup(PlcTypeGroup? group, string groupPath)
        {
            if (group == null)
            {
                throw new PortalException(PortalErrorCode.NotFound,
                    $"Type group not found at '{groupPath}'. Use 'plc_get_software_tree' to discover valid group paths.");
            }

            return group as PlcTypeUserGroup
                ?? throw new PortalException(PortalErrorCode.NotSupported,
                    $"'{groupPath}' is the system group 'PLC data types', which cannot be created, renamed or deleted.");
        }

        private static void EnsureValidName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The name must not be empty.");
            }

            if (name.Contains("/"))
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"'{name}' must be a plain name, not a path: '/' separates path segments.");
            }
        }

        #endregion

        #region groups

        /// <param name="parentGroupPath">Empty creates the group directly under the system root.</param>
        public PlcBlockUserGroup CreateBlockGroup(string softwarePath, string parentGroupPath, string name)
        {
            return Operation.Run(_logger, nameof(CreateBlockGroup), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var parent = GetPlcBlockGroupByPath(softwarePath, parentGroupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block group not found at '{parentGroupPath}'.");

                    return parent.Groups.Create(name);
                },
                ("softwarePath", softwarePath), ("parentGroupPath", parentGroupPath), ("name", name));
        }

        public bool DeleteBlockGroup(string softwarePath, string groupPath)
        {
            return Operation.Run(_logger, nameof(DeleteBlockGroup), PortalErrorCode.DeleteFailed,
                () =>
                {
                    EnsureUserGroup(GetPlcBlockGroupByPath(softwarePath, groupPath), groupPath).Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath));
        }

        #endregion

        #region blocks (create, rename, delete)

        public bool DeleteBlock(string softwarePath, string blockPath)
        {
            return Operation.Run(_logger, nameof(DeleteBlock), PortalErrorCode.DeleteFailed,
                () =>
                {
                    var block = GetBlock(softwarePath, blockPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block not found at '{blockPath}'. Use 'plc_get_blocks' to list the available blocks.");

                    EnsureNotKnowHowProtected(block);
                    block.Delete();

                    return true;
                },
                ("softwarePath", softwarePath), ("blockPath", blockPath));
        }

        public bool RenameBlock(string softwarePath, string blockPath, string newName)
        {
            return Operation.Run(_logger, nameof(RenameBlock), PortalErrorCode.RenameFailed,
                () =>
                {
                    EnsureValidName(newName);

                    var block = GetBlock(softwarePath, blockPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block not found at '{blockPath}'. Use 'plc_get_blocks' to list the available blocks.");

                    EnsureNotKnowHowProtected(block);
                    block.Name = newName;

                    return true;
                },
                ("softwarePath", softwarePath), ("blockPath", blockPath), ("newName", newName));
        }

        #endregion

        #region block numbers

        // Block numbers are unique per kind within a PLC: every data block shares one range,
        // function blocks another, and so on. Openness does not pick a number for us - an
        // instance DB created with autoNumber and number 0 really is created as DB0, which the
        // compiler then rejects - so the server looks up a free one itself.

        private const int MaxBlockNumber = 65535;

        /// <summary>User OBs start at 123; the numbers below belong to the system OB classes.</summary>
        private const int FirstUserObNumber = 123;

        private static string NumberSpace(PlcBlock block)
        {
            return block switch
            {
                OB => "OB",
                FB => "FB",
                FC => "FC",
                _ => "DB"
            };
        }

        private HashSet<int> UsedBlockNumbers(string softwarePath, string numberSpace)
        {
            return new HashSet<int>(
                GetBlocks(softwarePath)
                    .Where(b => NumberSpace(b) == numberSpace)
                    .Select(b => b.Number));
        }

        internal static int NextFreeNumber(HashSet<int> used, string numberSpace)
        {
            for (var number = numberSpace == "OB" ? FirstUserObNumber : 1; number <= MaxBlockNumber; number++)
            {
                if (!used.Contains(number))
                {
                    return number;
                }
            }

            throw new PortalException(PortalErrorCode.InvalidState, $"No free {numberSpace} number is left in this PLC.");
        }

        /// <summary>
        /// The number a new block gets: the caller's when autoNumber is off (validated), else
        /// the first free one of that kind.
        /// </summary>
        private int ResolveBlockNumber(string softwarePath, string numberSpace, bool autoNumber, int number)
        {
            var used = UsedBlockNumbers(softwarePath, numberSpace);

            if (autoNumber)
            {
                return NextFreeNumber(used, numberSpace);
            }

            if (number < 1 || number > MaxBlockNumber)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"Block number {number} is not valid. Pass a number from 1 to {MaxBlockNumber}, or set autoNumber to true. " +
                    $"The next free {numberSpace} number is {NextFreeNumber(used, numberSpace)}.");
            }

            if (used.Contains(number))
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"{numberSpace}{number} already exists in this PLC. The next free {numberSpace} number is {NextFreeNumber(used, numberSpace)}.");
            }

            return number;
        }

        private void EnsureBlockNameIsFree(string softwarePath, string name)
        {
            var existing = GetBlocks(softwarePath).FirstOrDefault(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"A block named '{name}' already exists at '{GetBlockPath(existing)}'. Block names are unique within a PLC.");
            }
        }

        #endregion

        #region create blocks

        /// <summary>Languages a function block can be created in through the SimaticML template.</summary>
        private static readonly ProgrammingLanguage[] TemplateLanguages =
            [ProgrammingLanguage.LAD, ProgrammingLanguage.FBD, ProgrammingLanguage.STL];

        /// <summary>
        /// Creates an empty function block.
        ///
        /// PlcBlockComposition.CreateFB only creates ProDiag blocks - for every other language
        /// Openness answers "The action 'Create block' only supports the programming language
        /// 'ProDiag'". So the route depends on the language: ProDiag through CreateFB, SCL by
        /// generating from a one-block source text, LAD/FBD/STL by importing a minimal SimaticML
        /// document.
        /// </summary>
        public PlcBlock CreateFB(string softwarePath, string groupPath, string name, bool autoNumber = true, int number = 0, string language = "LAD")
        {
            return Operation.Run(_logger, nameof(CreateFB), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    if (!Enum.TryParse<ProgrammingLanguage>(language, true, out var parsedLanguage))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Unknown programming language '{language}'. Supported for a new function block: LAD, FBD, STL, SCL, ProDiag.");
                    }

                    var group = GetPlcBlockGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block group not found at '{groupPath}'. Use 'plc_get_software_tree' to discover valid group paths.");

                    EnsureBlockNameIsFree(softwarePath, name);

                    var resolvedNumber = ResolveBlockNumber(softwarePath, "FB", autoNumber, number);

                    if (parsedLanguage == ProgrammingLanguage.ProDiag)
                    {
                        return group.Blocks.CreateFB(name, autoNumber, resolvedNumber, parsedLanguage);
                    }

                    if (parsedLanguage == ProgrammingLanguage.SCL)
                    {
                        return CreateFbFromSclText(softwarePath, group, name, autoNumber, resolvedNumber);
                    }

                    if (TemplateLanguages.Contains(parsedLanguage))
                    {
                        return ImportBlockTemplate(group, name, autoNumber, resolvedNumber, parsedLanguage);
                    }

                    throw new PortalException(PortalErrorCode.NotSupported,
                        $"An empty function block cannot be created in '{parsedLanguage}' through Openness. " +
                        "Create it in TIA Portal, or import an exported block with 'import_objects'.");
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath), ("name", name), ("language", language));
        }

        private PlcBlock CreateFbFromSclText(string softwarePath, PlcBlockGroup group, string name, bool autoNumber, int number)
        {
            var sclCode =
                $"FUNCTION_BLOCK \"{name}\"\r\n" +
                "{ S7_Optimized_Access := 'TRUE' }\r\n" +
                "VERSION : 0.1\r\n" +
                "BEGIN\r\n" +
                "END_FUNCTION_BLOCK\r\n";

            // A source can only be generated into a user group; the system root is the source's
            // default location, which an empty target selects.
            var target = group is PlcBlockUserGroup ? GetPlcBlockGroupPath(group, false) : string.Empty;

            GenerateFromSclText(softwarePath, sclCode, target, keepOnError: false);

            var block = group.Blocks.Find(name)
                ?? throw new PortalException(PortalErrorCode.CreateFailed,
                    $"Function block '{name}' was generated but could not be found afterwards.");

            // A source text carries no block number, so an explicit one is applied afterwards.
            if (!autoNumber)
            {
                block.AutoNumber = false;
                block.Number = number;
            }

            return block;
        }

        private PlcBlock ImportBlockTemplate(PlcBlockGroup group, string name, bool autoNumber, int number, ProgrammingLanguage language)
        {
            var directory = CreateTempExportDirectory();

            try
            {
                var file = new FileInfo(Path.Combine(directory, "block.xml"));

                File.WriteAllText(file.FullName, BuildFbTemplate(name, number, language.ToString(), Engineering.TiaMajorVersion));
                group.Blocks.Import(file, ImportOptions.None);
            }
            finally
            {
                DeleteTempExportDirectory(directory);
            }

            var block = group.Blocks.Find(name)
                ?? throw new PortalException(PortalErrorCode.CreateFailed,
                    $"Function block '{name}' was imported but could not be found afterwards.");

            // The document pins the number; say whether TIA Portal may renumber it later.
            block.AutoNumber = autoNumber;

            return block;
        }

        /// <summary>
        /// The smallest SimaticML document TIA Portal imports as an empty function block. The
        /// shape was taken from a real V21 export and reduced until the import complained: the
        /// 'Namespace' element is mandatory even when empty ("Missing 'Namespace' identifier
        /// attribute"), the interface sections and one empty network are kept so the block opens
        /// like one created in the editor.
        /// </summary>
        internal static string BuildFbTemplate(string name, int number, string language, int tiaMajorVersion)
        {
            var escapedName = System.Security.SecurityElement.Escape(name);

            return
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<Document>\r\n" +
                $"  <Engineering version=\"V{tiaMajorVersion}\" />\r\n" +
                "  <SW.Blocks.FB ID=\"0\">\r\n" +
                "    <AttributeList>\r\n" +
                "      <Interface>\r\n" +
                "        <Sections xmlns=\"http://www.siemens.com/automation/Openness/SW/Interface/v5\">\r\n" +
                "          <Section Name=\"Input\" />\r\n" +
                "          <Section Name=\"Output\" />\r\n" +
                "          <Section Name=\"InOut\" />\r\n" +
                "          <Section Name=\"Static\" />\r\n" +
                "          <Section Name=\"Temp\" />\r\n" +
                "          <Section Name=\"Constant\" />\r\n" +
                "        </Sections>\r\n" +
                "      </Interface>\r\n" +
                "      <MemoryLayout>Optimized</MemoryLayout>\r\n" +
                $"      <Name>{escapedName}</Name>\r\n" +
                "      <Namespace />\r\n" +
                $"      <Number>{number}</Number>\r\n" +
                $"      <ProgrammingLanguage>{language}</ProgrammingLanguage>\r\n" +
                "    </AttributeList>\r\n" +
                "    <ObjectList>\r\n" +
                "      <SW.Blocks.CompileUnit ID=\"1\" CompositionName=\"CompileUnits\">\r\n" +
                "        <AttributeList>\r\n" +
                "          <NetworkSource />\r\n" +
                $"          <ProgrammingLanguage>{language}</ProgrammingLanguage>\r\n" +
                "        </AttributeList>\r\n" +
                "      </SW.Blocks.CompileUnit>\r\n" +
                "    </ObjectList>\r\n" +
                "  </SW.Blocks.FB>\r\n" +
                "</Document>\r\n";
        }

        /// <param name="instanceOfName">Name of the FB this instance DB is created for.</param>
        public InstanceDB CreateInstanceDB(string softwarePath, string groupPath, string name, string instanceOfName, bool autoNumber = true, int number = 0)
        {
            return Operation.Run(_logger, nameof(CreateInstanceDB), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    if (string.IsNullOrWhiteSpace(instanceOfName))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            "instanceOfName must name the function block this instance DB belongs to.");
                    }

                    var group = GetPlcBlockGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block group not found at '{groupPath}'. Use 'plc_get_software_tree' to discover valid group paths.");

                    var instanceOf = SplitPath(instanceOfName).LeafName;

                    if (!GetBlocks(softwarePath).Any(b => b is FB && b.Name.Equals(instanceOf, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new PortalException(PortalErrorCode.NotFound,
                            $"Function block '{instanceOf}' does not exist in this PLC. Use 'plc_get_blocks' to list the blocks.");
                    }

                    EnsureBlockNameIsFree(softwarePath, name);

                    // Openness takes the number literally even with autoNumber set, so a valid
                    // one is always passed.
                    var resolvedNumber = ResolveBlockNumber(softwarePath, "DB", autoNumber, number);
                    var block = group.Blocks.CreateInstanceDB(name, autoNumber, resolvedNumber, instanceOf);

                    if (block.Number < 1)
                    {
                        // Never leave a DB0 behind: it only shows up later, as a compile error.
                        var created = block.Number;

                        block.Delete();

                        throw new PortalException(PortalErrorCode.CreateFailed,
                            $"TIA Portal created '{name}' with the invalid number {created}; it was removed again. Pass an explicit number with autoNumber false.");
                    }

                    return block;
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath), ("name", name), ("instanceOfName", instanceOfName));
        }

        #endregion

        // From the former Portal.MoveCopy.cs:
        // Copy and move for program blocks and PLC data types.
        // CopyType, MoveType and TransferType live in Portal.Types.cs; the guards and temp-directory
        // helpers below are shared by both.
        //
        // Callers: the CopyBlock / MoveBlock / CopyType / MoveType tools in
        // McpServer.Blocks.cs and McpServer.Types.cs, registered only under '--allow-write'. Affected API: none
        // existing - all members are new. File I/O: each call writes one temporary .xml under the
        // OS temp directory and removes it in a finally block; no caller-visible file is produced.
        //
        // Openness has no move or copy API for blocks and types: PlcBlockComposition.CreateFrom
        // only accepts a library MasterCopy or a library type version, never another block. These
        // operations are therefore composed from export -> import -> (for move) delete the source.
        // Two consequences the caller sees:
        // - the object must be consistent, because TIA Portal refuses to export inconsistent ones;
        // - the exported XML carries the block number, so importing into the same PLC can hit a
        // number collision, which surfaces as ImportFailed rather than being pre-validated.
        // Renaming during a copy is deliberately not offered - it would mean rewriting the XML.

        #region guards

        private static void EnsureConsistent(PlcBlock block)
        {
            if (!block.IsConsistent)
            {
                throw new PortalException(PortalErrorCode.InvalidState,
                    $"Block '{block.Name}' is inconsistent and TIA Portal will not export it. Compile the software first.");
            }
        }

        private static void EnsureConsistent(PlcType type)
        {
            if (!type.IsConsistent)
            {
                throw new PortalException(PortalErrorCode.InvalidState,
                    $"Type '{type.Name}' is inconsistent and TIA Portal will not export it. Compile the software first.");
            }
        }

        private static void EnsureDifferentGroup(string sourceGroupPath, string targetGroupPath, string itemName)
        {
            var source = NormalizeGroupPath(sourceGroupPath);
            var target = NormalizeGroupPath(targetGroupPath);

            if (source.Equals(target, StringComparison.OrdinalIgnoreCase))
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"'{itemName}' already lives in '{(target.Length == 0 ? "the root group" : target)}'. " +
                    "Choose a different target group, or use the rename tool to change its name.");
            }
        }

        #endregion

        #region temp scaffolding

        /// <summary>
        /// A private directory per call, so two concurrent transfers of same-named objects
        /// cannot collide on the intermediate file.
        /// </summary>
        private static string CreateTempExportDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "tiamcp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private void DeleteTempExportDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch (Exception ex)
            {
                // A leftover temp file must never fail an otherwise successful copy or move.
                _logger?.LogWarning(ex, "Could not remove temporary export directory {Directory}", directory);
            }
        }

        #endregion

        #region blocks

        /// <summary>
        /// Copies a block. Block names and numbers are unique within a PLC, so a copy inside the
        /// same PLC needs <paramref name="newName"/> and gets a free number; a copy into another
        /// PLC may keep both.
        /// </summary>
        /// <param name="newName">Name of the copy. Required when source and target PLC are the same.</param>
        /// <param name="targetSoftwarePath">PLC that receives the copy; empty means the source PLC.</param>
        /// <param name="overwrite">Replace a block of the final name that already exists in the target PLC.</param>
        public PlcBlock CopyBlock(string softwarePath, string blockPath, string targetGroupPath, string newName = "", string targetSoftwarePath = "", bool overwrite = false)
        {
            return Operation.Run(_logger, nameof(CopyBlock), PortalErrorCode.ImportFailed,
                () =>
                {
                    var source = GetBlock(softwarePath, blockPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block not found at '{blockPath}'. Use 'plc_get_blocks' to list the available blocks.");

                    EnsureNotKnowHowProtected(source);
                    EnsureConsistent(source);

                    var targetSoftware = string.IsNullOrWhiteSpace(targetSoftwarePath) ? softwarePath : targetSoftwarePath;
                    var samePlc = IsSameSoftware(softwarePath, targetSoftware);
                    var finalName = string.IsNullOrWhiteSpace(newName) ? source.Name : newName.Trim();

                    EnsureValidName(finalName);

                    if (samePlc && finalName.Equals(source.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"A copy of '{source.Name}' inside the same PLC needs a different name: block names are unique within a PLC. " +
                            "Pass 'newName', or pass 'targetSoftwarePath' to copy into another PLC. To change the group only, use 'plc_move_block'.");
                    }

                    var targetGroup = GetPlcBlockGroupByPath(targetSoftware, targetGroupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block group not found at '{targetGroupPath}' in '{targetSoftware}'. Use 'plc_get_software_tree' to discover valid group paths.");

                    var existing = GetBlocks(targetSoftware).FirstOrDefault(b => b.Name.Equals(finalName, StringComparison.OrdinalIgnoreCase));

                    if (existing != null && !overwrite)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"A block named '{finalName}' already exists at '{GetBlockPath(existing)}' in '{targetSoftware}'. " +
                            "Choose another 'newName', or set 'overwrite' to replace it.");
                    }

                    // The exported number travels with the block; give the copy a free one unless
                    // it replaces the block that already owns it.
                    var numberSpace = NumberSpace(source);
                    var used = UsedBlockNumbers(targetSoftware, numberSpace);

                    if (existing != null)
                    {
                        used.Remove(existing.Number);
                    }

                    int? newNumber = used.Contains(source.Number) ? NextFreeNumber(used, numberSpace) : null;
                    var directory = CreateTempExportDirectory();

                    try
                    {
                        var file = new FileInfo(Path.Combine(directory, "block.xml"));

                        source.Export(file, ExportOptions.None);

                        if (newNumber.HasValue || !finalName.Equals(source.Name, StringComparison.Ordinal))
                        {
                            File.WriteAllText(file.FullName, RewriteExportedObject(File.ReadAllText(file.FullName), finalName, newNumber));
                        }

                        targetGroup.Blocks.Import(file, overwrite ? ImportOptions.Override : ImportOptions.None);
                    }
                    finally
                    {
                        DeleteTempExportDirectory(directory);
                    }

                    // With 'overwrite' the replaced block keeps the group it already lived in.
                    return targetGroup.Blocks.Find(finalName)
                        ?? GetBlocks(targetSoftware).FirstOrDefault(b => b.Name.Equals(finalName, StringComparison.OrdinalIgnoreCase))
                        ?? throw new PortalException(PortalErrorCode.ImportFailed,
                            $"Block '{finalName}' was imported into '{targetGroupPath}' but could not be found afterwards.");
                },
                ("softwarePath", softwarePath), ("blockPath", blockPath), ("targetGroupPath", targetGroupPath),
                ("newName", newName), ("targetSoftwarePath", targetSoftwarePath));
        }

        /// <summary>
        /// Moves a block into another group of the same PLC. The original has to go before the
        /// import: with it still in place the import fails on the duplicate name. If the import
        /// then fails, the block is put back where it was.
        /// </summary>
        public PlcBlock MoveBlock(string softwarePath, string blockPath, string targetGroupPath)
        {
            return Operation.Run(_logger, nameof(MoveBlock), PortalErrorCode.ImportFailed,
                () =>
                {
                    var source = GetBlock(softwarePath, blockPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block not found at '{blockPath}'. Use 'plc_get_blocks' to list the available blocks.");

                    EnsureNotKnowHowProtected(source);
                    EnsureConsistent(source);

                    var sourceGroup = source.Parent as PlcBlockGroup
                        ?? throw new PortalException(PortalErrorCode.InvalidState,
                            $"Block '{source.Name}' is not inside a block group and cannot be moved.");

                    var targetGroup = GetPlcBlockGroupByPath(softwarePath, targetGroupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block group not found at '{targetGroupPath}'. Use 'plc_get_software_tree' to discover valid group paths.");

                    EnsureDifferentGroup(GetPlcBlockGroupPath(sourceGroup, false), GetPlcBlockGroupPath(targetGroup, false), source.Name);

                    var name = source.Name;
                    var directory = CreateTempExportDirectory();

                    try
                    {
                        var file = new FileInfo(Path.Combine(directory, "block.xml"));

                        source.Export(file, ExportOptions.None);
                        source.Delete();

                        try
                        {
                            targetGroup.Blocks.Import(file, ImportOptions.None);
                        }
                        catch (Exception importError)
                        {
                            RestoreAfterFailedMove(() => sourceGroup.Blocks.Import(file, ImportOptions.None), "Block", name, importError);

                            throw;
                        }
                    }
                    finally
                    {
                        DeleteTempExportDirectory(directory);
                    }

                    return targetGroup.Blocks.Find(name)
                        ?? throw new PortalException(PortalErrorCode.ImportFailed,
                            $"Block '{name}' was imported into '{targetGroupPath}' but could not be found afterwards.");
                },
                ("softwarePath", softwarePath), ("blockPath", blockPath), ("targetGroupPath", targetGroupPath));
        }

        #endregion

        #region copy and move plumbing

        /// <summary>
        /// Puts a moved object back into its original group after the import into the target
        /// failed. Inside a transaction the rollback would undo the delete anyway; this covers
        /// the case where TIA Portal granted none. If even the restore fails, the caller must
        /// learn that the object is gone - that outranks the original error.
        /// </summary>
        private void RestoreAfterFailedMove(Action restore, string kind, string name, Exception importError)
        {
            try
            {
                restore();
            }
            catch (Exception restoreError)
            {
                _logger?.LogError(restoreError, "{Kind} {Name} could not be restored after a failed move", kind, name);

                throw new PortalException(PortalErrorCode.ImportFailed,
                    $"{kind} '{name}' was removed from its group, the import into the target failed ({ErrorText.Describe(importError)}), " +
                    $"and restoring it failed as well ({ErrorText.Describe(restoreError)}). Undo the change in TIA Portal or close the project without saving.",
                    null, restoreError);
            }
        }

        private bool IsSameSoftware(string softwarePath, string otherSoftwarePath)
        {
            if (softwarePath.Equals(otherSoftwarePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Two spellings can address one PLC ("PLC_1" and "Station/PLC_1").
            var first = GetPlcSoftwareOrThrow(softwarePath);
            var second = GetPlcSoftwareOrThrow(otherSoftwarePath);

            return first.Equals(second);
        }

        /// <summary>
        /// Renames and/or renumbers the object of an exported SimaticML document, so it can be
        /// imported next to its original.
        /// </summary>
        internal static string RewriteExportedObject(string xml, string? newName, int? newNumber)
        {
            var document = System.Xml.Linq.XDocument.Parse(xml, System.Xml.Linq.LoadOptions.PreserveWhitespace);

            var exported = document.Root?.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("SW.", StringComparison.Ordinal))
                ?? throw new PortalException(PortalErrorCode.ImportFailed, "The exported document contains no block or type to copy.");

            var attributes = exported.Element("AttributeList")
                ?? throw new PortalException(PortalErrorCode.ImportFailed, "The exported document has no attribute list to rename the copy in.");

            if (!string.IsNullOrEmpty(newName))
            {
                var name = attributes.Element("Name")
                    ?? throw new PortalException(PortalErrorCode.ImportFailed, "The exported document carries no name to replace.");

                name.Value = newName;
            }

            if (newNumber.HasValue)
            {
                var number = attributes.Element("Number");

                if (number != null)
                {
                    number.Value = newNumber.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            var declaration = document.Declaration == null ? string.Empty : document.Declaration + Environment.NewLine;

            return declaration + document.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
        }

        #endregion

        #region interface

        /// <summary>
        /// The members of a data block: every entry with its attributes (data type, start value,
        /// comment, retain, accessibility - Openness exposes these as attributes rather than
        /// typed properties, so they are reported as-is and stay correct across versions).
        /// Needs no export and works on inconsistent blocks.
        ///
        /// Data blocks only: V21 Openness puts 'Interface' on DataBlock and offers no equivalent
        /// accessor on CodeBlock, so the VAR_INPUT/VAR_OUTPUT declarations of an FB, FC or OB
        /// have to be read from the declaration part of GetBlockSource instead.
        /// </summary>
        public List<InterfaceMember> GetBlockInterface(string softwarePath, string blockPath)
        {
            return Operation.Run(_logger, nameof(GetBlockInterface), PortalErrorCode.NotFound,
                () =>
                {
                    var block = GetBlock(softwarePath, blockPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block not found at '{blockPath}'. Use 'ResolveObjectPath' or 'plc_get_blocks' to find its path.");

                    if (block is not DataBlock dataBlock)
                    {
                        throw new PortalException(PortalErrorCode.NotSupported,
                            $"'{block.Name}' is a {block.GetType().Name}, and Openness exposes an interface only for data blocks. " +
                            "Use 'plc_get_block_source' and read its declaration part instead.");
                    }

                    var members = dataBlock.Interface?.Members
                        ?? throw new PortalException(PortalErrorCode.NotSupported,
                            $"Data block '{block.Name}' exposes no members. Know-how protected blocks hide theirs.");

                    return members.Select(ToInterfaceMember).ToList();
                },
                ("softwarePath", softwarePath), ("blockPath", blockPath));
        }

        private static InterfaceMember ToInterfaceMember(Member member)
        {
            var info = new InterfaceMember { Name = member.Name };

            foreach (var attribute in member.GetAttributeInfos())
            {
                try
                {
                    var value = member.GetAttribute(attribute.Name);

                    info.Attributes[attribute.Name] = value?.ToString() ?? string.Empty;
                }
                catch (Exception)
                {
                    // A member can advertise an attribute it cannot currently produce; skipping
                    // one attribute is better than losing the whole signature.
                }
            }

            info.DataTypeName = info.Attributes.TryGetValue("DataTypeName", out var dataType) ? dataType : null;

            return info;
        }

        #endregion

        #region compile (write)

        public CompilerResult? CompileBlock(string softwarePath, string blockPath)
        {
            return Operation.Run(_logger, nameof(CompileBlock), PortalErrorCode.InvalidState,
                () =>
                {
                    var block = GetBlock(softwarePath, blockPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block not found at '{blockPath}'. Use 'plc_get_blocks' to list the available blocks.");

                    // A block does not implement ICompilable itself; the compiler is a
                    // service it provides, exactly as PlcSoftware does in CompileSoftware.
                    var compilable = block.GetService<ICompilable>() ?? block as ICompilable;

                    if (compilable != null)
                    {
                        return compilable.Compile();
                    }

                    throw new PortalException(PortalErrorCode.NotSupported,
                        $"Block '{blockPath}' offers no compile service. Use 'plc_compile_software' to compile the whole PLC.");
                },
                ("softwarePath", softwarePath), ("blockPath", blockPath));
        }

        public CompilerResult? CompileSoftware(string softwarePath)
        {
            return Operation.Run(_logger, nameof(CompileSoftware), PortalErrorCode.InvalidState,
                () =>
                {
                    var softwareContainer = GetSoftwareContainer(softwarePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            DescribeMissingSoftware(softwarePath));

                    if (softwareContainer.Software is IEngineeringServiceProvider provider && provider.GetService<ICompilable>() is ICompilable compilable)
                    {
                        return compilable.Compile();
                    }
                    else if (softwareContainer.Software is ICompilable swCompilable)
                    {
                        return swCompilable.Compile();
                    }
                    else if (softwareContainer is IEngineeringServiceProvider contProvider && contProvider.GetService<ICompilable>() is ICompilable contService)
                    {
                        return contService.Compile();
                    }
                    else if (softwareContainer is ICompilable containerCompilable)
                    {
                        return containerCompilable.Compile();
                    }
                    throw new PortalException(PortalErrorCode.NotSupported, $"Software '{softwarePath}' is not compilable.");
                },
                ("softwarePath", softwarePath));
        }

        #endregion
    }

    /// <summary>One member of a block interface.</summary>
    public class InterfaceMember
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Convenience copy of the DataTypeName attribute when the member has one.</summary>
        public string? DataTypeName { get; set; }

        /// <summary>Every attribute Openness reports for the member, stringified.</summary>
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
    }
}
