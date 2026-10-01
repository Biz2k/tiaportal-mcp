using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        #region blocks

        [McpServerTool(Name = "GetBlockInfo", Title = "Get block info", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get a block info, which is located in the plc software")]
        public static ResponseBlockInfo GetBlockInfo(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: defines the path in the project structure to the block")] string blockPath)
        {
            try
            {
                var block = Portal.GetBlock(softwarePath, blockPath);
                if (block != null)
                {
                    var attributes = Helper.GetAttributeList(block);

                    return new ResponseBlockInfo
                    {
                        Path = Portal.GetBlockPath(block),
                        Message = $"Block info retrieved from '{blockPath}' in '{softwarePath}'",
                        Name = block.Name,
                        TypeName = block.GetType().Name,
                        Namespace = block.Namespace,
                        ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage),block.ProgrammingLanguage),
                        MemoryLayout = Enum.GetName(typeof(MemoryLayout), block.MemoryLayout),
                        IsConsistent = block.IsConsistent,
                        HeaderName = block.HeaderName,
                        ModifiedDate = block.ModifiedDate,
                        IsKnowHowProtected = block.IsKnowHowProtected,
                        Attributes = attributes,
                        Description = block.ToString(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Block not found at '{blockPath}' in '{softwarePath}'");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving block info from '{blockPath}' in '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "GetBlocks", Title = "Get blocks", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get a list of blocks, which are located in plc software")]
        public static ResponseBlocks GetBlocks(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "")
        {
            try
            {
                var list = Portal.GetBlocks(softwarePath, regexName);

                var responseList = new List<ResponseBlockInfo>();
                foreach (var block in list)
                {
                    if (block != null)
                    {
                        var attributes = Helper.GetAttributeList(block);

                        responseList.Add(new ResponseBlockInfo
                        {
                            Path = Portal.GetBlockPath(block),
                            Name = block.Name,
                            TypeName = block.GetType().Name,
                            Namespace = block.Namespace,
                            ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), block.ProgrammingLanguage),
                            MemoryLayout = Enum.GetName(typeof(MemoryLayout), block.MemoryLayout),
                            IsConsistent = block.IsConsistent,
                            HeaderName = block.HeaderName,
                            ModifiedDate = block.ModifiedDate,
                            IsKnowHowProtected = block.IsKnowHowProtected,
                            Attributes = attributes,
                            Description = block.ToString()
                        });
                    }
                }

                if (list != null)
                {
                    return new ResponseBlocks
                    {
                        Message = $"Blocks with regex '{regexName}' retrieved from '{softwarePath}'",
                        Items = responseList,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed retrieving blocks with regex '{regexName}' in '{softwarePath}'");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving blocks with regex '{regexName}' in '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "GetBlocksWithHierarchy", Title = "Get blocks with hierarchy", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get a list of all blocks with their group hierarchy from the plc software.")]
        public static ResponseBlocksWithHierarchy GetBlocksWithHierarchy(
        [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath)
        {
            try
            {
                var rootGroup = Portal.GetBlockRootGroup(softwarePath);
                if (rootGroup != null)
                {
                    var hierarchy = Helper.BuildBlockHierarchy(rootGroup, Portal);
                    return new ResponseBlocksWithHierarchy
                    {
                        Message = $"Block hierarchy retrieved from '{softwarePath}'",
                        Root = hierarchy,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    // Specific failure: root group could not be resolved
                    throw new McpException($"Block root group not found for '{softwarePath}'");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                // Generic unexpected failure wrapper
                throw new McpException($"Unexpected error retrieving block hierarchy for '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "ExportBlock", Title = "Export block to XML", Destructive = true, Idempotent = true, OpenWorld = false), Description("Export a block from plc software to file")]
        public static ResponseExportBlock ExportBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: full path to the block in the project structure, e.g. 'Group/Subgroup/Name' (single names are ambiguous)")] string blockPath,
            [Description("exportPath: defines the path where to export the block")] string exportPath,
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            try
            {
                var block = Portal.ExportBlock(softwarePath, blockPath, exportPath, preservePath);
                if (block != null)
                {
                    return new ResponseExportBlock
                    {
                        Message = $"Block exported from '{blockPath}' to '{exportPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                // Should not be reachable because Portal.ExportBlock throws on failure
                throw new McpException($"Failed exporting block from '{blockPath}' to '{exportPath}'");
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                // Map known portal errors to sharper MCP errors and messages.
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        {
                            var suggestionNote = string.Empty;
                            // If the path has no '/', it may be incomplete; build suggestions using Portal's regex search and path resolver
                            if (!string.IsNullOrEmpty(blockPath) && !blockPath.Contains('/'))
                            {
                                try
                                {
                                    var escaped = Regex.Escape(blockPath);
                                    var blocks = Portal.GetBlocks(softwarePath, $"^{escaped}$");
                                    if (blocks == null || blocks.Count == 0)
                                    {
                                        blocks = Portal.GetBlocks(softwarePath, escaped);
                                    }

                                    var candidates = blocks
                                        .Take(10)
                                        .Select(b => Portal.GetBlockPath(b))
                                        .Where(p => !string.IsNullOrWhiteSpace(p))
                                        .Distinct(StringComparer.OrdinalIgnoreCase)
                                        .ToList();

                                    if (candidates.Count > 0)
                                    {
                                        suggestionNote = $" Did you mean: {string.Join(", ", candidates)}?";
                                    }
                                }
                                catch
                                {
                                    // Best-effort suggestions only
                                }
                            }

                            var msg = $"Block not found.{suggestionNote}".Trim();
                            throw new McpException(msg);
                        }

                    case TiaMcpServer.Siemens.PortalErrorCode.ExportFailed:
                        {
                            // Relay underlying portal error with concise reason; log full details
                            var reason = pex.InnerException?.Message?.Trim();
                            var msg = "Failed to export block.";
                            if (!string.IsNullOrEmpty(reason)) msg += $" Reason: {reason}";

                            Logger?.LogError(pex, "MCP ExportBlock failed for {SoftwarePath} {BlockPath} -> {ExportPath}",
                                pex.Data?["softwarePath"], pex.Data?["blockPath"], pex.Data?["exportPath"]);

                            throw new McpException(msg);
                        }

                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        {
                            throw new McpException(pex.Message);
                        }
                }

                // Fallback
                throw new McpException(pex.Message);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting block from '{blockPath}' to '{exportPath}': {ex.Message}", ex);
            }
        }

        private static string BuildBlockPathSuggestion(string softwarePath, string blockPath)
        {
            if (string.IsNullOrEmpty(blockPath) || blockPath.Contains('/')) return string.Empty;
            try
            {
                var escaped = Regex.Escape(blockPath);
                var blocks = Portal.GetBlocks(softwarePath, $"^{escaped}$");
                if (blocks == null || blocks.Count == 0)
                {
                    blocks = Portal.GetBlocks(softwarePath, escaped);
                }

                var candidates = blocks
                    .Take(10)
                    .Select(b =>
                    {
                        var name = b.Name;
                        var parts = new List<string> { name };
                        var parent = b.Parent;
                        while (parent != null)
                        {
                            if (parent is PlcBlockSystemGroup) break;
                            if (parent is PlcBlockGroup grp)
                            {
                                parts.Insert(0, grp.Name);
                                parent = grp.Parent;
                            }
                            else break;
                        }
                        if (parts.Count > 1) parts.RemoveAt(0);
                        return string.Join("/", parts);
                    })
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return candidates.Count > 0 ? $" Did you mean: {string.Join(", ", candidates)}?" : string.Empty;
            }
            catch
            {
                return string.Empty; // best effort only
            }
        }

        [McpServerTool(Name = "ImportBlock", Title = "Import block from XML", Destructive = true, Idempotent = true, OpenWorld = false), Description("Import a block file to plc software")]
        public static ResponseImportBlock ImportBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: defines the path in the project structure to the group, where to import the block")] string groupPath,
            [Description("importPath: defines the path of the xml file from where to import the block")] string importPath)
        {
            try
            {
                if (Portal.ImportBlock(softwarePath, groupPath, importPath))
                {
                    return new ResponseImportBlock
                    {
                        Message = $"Block imported from '{importPath}' to '{groupPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed importing block from '{importPath}' to '{groupPath}'");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error importing block from '{importPath}' to '{groupPath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "ExportBlocks", Title = "Export blocks to XML", Destructive = true, Idempotent = true, OpenWorld = false), Description("Export all blocks from the plc software to path")]
        public static async Task<ResponseExportBlocks> ExportBlocks(
            IProgress<ProgressNotificationValue> progress,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: defines the path where to export the blocks")] string exportPath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "",
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            var startTime = DateTime.Now;
            
            try
            {
                // First, get the list of blocks to determine total count
                Logger?.LogInformation($"Starting export of blocks from '{softwarePath}' to '{exportPath}'");
                
                var allBlocks = await Task.Run(() => Portal.GetBlocks(softwarePath, regexName));
                var totalBlocks = allBlocks?.Count ?? 0;

                if (totalBlocks == 0)
                {
                    progress.Report(new ProgressNotificationValue { Progress = 0, Total = 0, Message = "No blocks found to export" });
                    
                    return new ResponseExportBlocks
                    {
                        Message = $"No blocks found with regex '{regexName}' in '{softwarePath}'",
                        Items = new List<ResponseBlockInfo>(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalBlocks"] = 0,
                            ["exportedBlocks"] = 0,
                            ["duration"] = (DateTime.Now - startTime).TotalSeconds
                        }
                    };
                }

                // Send initial progress notification
                progress.Report(new ProgressNotificationValue { Progress = 0, Total = totalBlocks, Message = $"Starting export of {totalBlocks} blocks..." });

                // Export blocks asynchronously
                var exportedBlocks = await Task.Run(() => Portal.ExportBlocks(softwarePath, exportPath, regexName, preservePath));

                // Build list of inconsistent (skipped) blocks for reporting
                var inconsistentInfos = new List<ResponseBlockInfo>();
                if (allBlocks != null)
                {
                    foreach (var b in allBlocks)
                    {
                        if (b != null && b.IsConsistent == false)
                        {
                            var attrs = Helper.GetAttributeList(b);
                            inconsistentInfos.Add(new ResponseBlockInfo
                            {
                                Path = Portal.GetBlockPath(b),
                                Name = b.Name,
                                TypeName = b.GetType().Name,
                                Namespace = b.Namespace,
                                ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), b.ProgrammingLanguage),
                                MemoryLayout = Enum.GetName(typeof(MemoryLayout), b.MemoryLayout),
                                IsConsistent = b.IsConsistent,
                                HeaderName = b.HeaderName,
                                ModifiedDate = b.ModifiedDate,
                                IsKnowHowProtected = b.IsKnowHowProtected,
                                Attributes = attrs,
                                Description = b.ToString()
                            });
                        }
                    }
                }
                
                // Send progress update after export completion
                if (exportedBlocks != null)
                {
                    var exportedCount = exportedBlocks.Count();
                    progress.Report(new ProgressNotificationValue { Progress = exportedCount, Total = totalBlocks, Message = $"Exported {exportedCount} of {totalBlocks} blocks" });
                }

                if (exportedBlocks != null)
                {
                    var responseList = new List<ResponseBlockInfo>();
                    var processedCount = 0;
                    
                    foreach (var block in exportedBlocks)
                    {
                        if (block != null)
                        {
                            var attributes = Helper.GetAttributeList(block);

                            responseList.Add(new ResponseBlockInfo
                            {
                                Path = Portal.GetBlockPath(block),
                                Name = block.Name,
                                TypeName = block.GetType().Name,
                                Namespace = block.Namespace,
                                ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), block.ProgrammingLanguage),
                                MemoryLayout = Enum.GetName(typeof(MemoryLayout), block.MemoryLayout),
                                IsConsistent = block.IsConsistent,
                                HeaderName = block.HeaderName,
                                ModifiedDate = block.ModifiedDate,
                                IsKnowHowProtected = block.IsKnowHowProtected,
                                Attributes = attributes,
                                Description = block.ToString()
                            });
                        }
                        processedCount++;
                    }

                    // Send final progress notification
                    progress.Report(new ProgressNotificationValue { Progress = processedCount, Total = totalBlocks, Message = $"Export completed: {processedCount} blocks exported successfully" });

                    var duration = (DateTime.Now - startTime).TotalSeconds;
                    Logger?.LogInformation($"Export completed: {processedCount} blocks exported in {duration:F2} seconds");

                    return new ResponseExportBlocks
                    {
                        Message = $"Export completed: {processedCount} blocks with regex '{regexName}' exported from '{softwarePath}' to '{exportPath}'",
                        Items = responseList,
                        Inconsistent = inconsistentInfos,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalBlocks"] = totalBlocks,
                            ["exportedBlocks"] = processedCount,
                            ["inconsistentBlocks"] = inconsistentInfos.Count,
                            ["duration"] = duration
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting blocks with '{regexName}' from '{softwarePath}' to {exportPath}");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                // Send error progress notification if we have a progress token
                progress.Report(new ProgressNotificationValue { Progress = 0, Total = 0, Message = $"Export failed: {ex.Message}" });
                
                Logger?.LogError(ex, $"Failed exporting blocks with '{regexName}' from '{softwarePath}' to {exportPath}");
                throw new McpException($"Unexpected error exporting blocks with '{regexName}' from '{softwarePath}' to {exportPath}: {ex.Message}", ex);
            }
        }

        #endregion

        // From the former McpServer.Source.cs:
        // Reading what an object contains, without going through the file system.
        //
        // Callers: the MCP host, through tool registration. Affected API: none existing - all three
        // tools are new. File I/O: none that the caller sees; the portal layer exports into a
        // scratch directory under the temp path and removes it again.
        //
        // Read-only: none of these modify the project, so none are gated behind '--allow-write'.
        // Unlike the Export* tools they are not marked destructive either - they leave nothing on
        // disk to overwrite.

        #region source

        [McpServerTool(Name = "GetBlockSource", Title = "Read a block's source", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Return the source text of one program block directly, instead of exporting a file and reading it back. 'document' gives readable SCL/LAD/STL (SIMATIC Source Document, V20+); objects TIA Portal cannot represent that way - STL and mixed-language blocks - fall back to XML automatically, and the response says which format was produced")]
        public static ResponseSourceText GetBlockSource(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: root-relative path of the block, e.g. '0_OBs/Main'. Use 'ResolveObjectPath' if you only know the name")] string blockPath,
            [Description("format: 'document' (default) for readable source text, or 'xml' for the SimaticML export")] string format = "document",
            [Description("maxChars: truncate the text at this many characters, on a line boundary (default 40000)")] int maxChars = 40000)
        {
            return Sourced(
                () => Portal.GetBlockSource(softwarePath, blockPath, format, maxChars),
                blockPath,
                "block");
        }

        [McpServerTool(Name = "GetBlockInterface", Title = "Read a data block's members", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the members of a data block with their data type and every attribute TIA Portal reports. Needs no export and works on inconsistent blocks. Data blocks only: Openness offers no interface accessor for FB, FC or OB, whose declarations come from 'GetBlockSource' instead")]
        public static ResponseBlockInterface GetBlockInterface(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: root-relative path of the data block, e.g. '1_Tests/DB_Block_1'")] string blockPath)
        {
            try
            {
                var members = Portal.GetBlockInterface(softwarePath, blockPath);

                return new ResponseBlockInterface
                {
                    Message = $"'{blockPath}' has {members.Count} member(s)",
                    Path = blockPath,
                    Items = members.Select(m => new ResponseInterfaceMember
                    {
                        Name = m.Name,
                        DataTypeName = m.DataTypeName,
                        Attributes = m.Attributes
                    }).ToList(),
                    Meta = Ok(new JsonObject { ["memberCount"] = members.Count })
                };
            }
            catch (PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error reading the interface of '{blockPath}': {ex.Message}", ex);
            }
        }

        /// <summary>Shared response shaping for the two source readers.</summary>
        private static ResponseSourceText Sourced(Func<SourceTextResult> read, string objectPath, string kind)
        {
            try
            {
                var result = read();

                var note = result.Truncated
                    ? $" (truncated to {result.Text.Length} of {result.TotalChars} characters)"
                    : string.Empty;

                return new ResponseSourceText
                {
                    Message = $"Source of {kind} '{objectPath}' as {result.Format}{note}",
                    Name = result.Name,
                    Path = result.Path,
                    Format = result.Format,
                    Text = result.Text,
                    TotalChars = result.TotalChars,
                    Truncated = result.Truncated,
                    FileNames = result.FileNames,
                    Meta = Ok(new JsonObject
                    {
                        ["format"] = result.Format,
                        ["totalChars"] = result.TotalChars,
                        ["truncated"] = result.Truncated
                    })
                };
            }
            catch (PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error reading the source of '{objectPath}': {ex.Message}", ex);
            }
        }

        #endregion

        // From the former McpServerWrite.Blocks.cs:
        // Write tools for program blocks, PLC data types and their groups.
        //
        // Callers: registered by Program.BuildTools() under '--allow-write'; also invoked
        // directly by the write test class. Affected API: additive - a partial of McpServer; the tools are marked [WriteTool].
        // No file I/O; changes are in memory until SaveProject / SaveSession.
        //
        // Openness offers no generic "create block" and no move/copy, so the creation surface here
        // is CreateFB and CreateInstanceDB only - anything else arrives through ImportBlock.

        // From the former McpServerWrite.MoveCopy.cs:
        // Write tools that copy or move program blocks and PLC data types between groups.
        //
        // Callers: registered by Program.BuildTools() under '--allow-write'; also invoked
        // directly by the write test class. Affected API: additive - a partial of McpServer; the tools are marked [WriteTool].
        // File I/O: none of its own; Portal writes and removes a temporary XML per call.
        //
        // Openness exposes no move or copy operation for these objects, so each tool is composed
        // from export, import and (for a move) deleting the source. The tool descriptions say so,
        // because the composition is observable: the object must be consistent, and its block
        // number travels with it, so importing into the same PLC can collide.

        #region groups (write)

        [WriteTool]
        [McpServerTool(Name = "CreateBlockGroup", Title = "Create a block group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a group below the Program blocks root of the plc software")]
        public static ResponseCreated CreateBlockGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("parentGroupPath: root-relative path of the parent group; empty creates directly below Program blocks")] string parentGroupPath,
            [Description("name: name of the new group, without a slash")] string name)
        {
            return Guarded(nameof(CreateBlockGroup), () =>
            {
                Portal.CreateBlockGroup(softwarePath, parentGroupPath, name);
                return Created("Block group", name, JoinPath(parentGroupPath, name));
            });
        }

        [WriteTool]
        [McpServerTool(Name = "DeleteBlockGroup", Title = "Delete a block group", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a block group and everything inside it. The Program blocks system group itself cannot be deleted")]
        public static ResponseDeleted DeleteBlockGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative path of the group to delete, e.g. Common/CarrierRegister")] string groupPath)
        {
            return Guarded(nameof(DeleteBlockGroup), () =>
            {
                Portal.DeleteBlockGroup(softwarePath, groupPath);
                return Deleted("Block group", groupPath);
            });
        }

        #endregion

        #region blocks and types (write)

        [WriteTool]
        [McpServerTool(Name = "DeleteBlock", Title = "Delete a block", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a program block. Know-how protected blocks are rejected: remove the protection in TIA Portal first")]
        public static ResponseDeleted DeleteBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: root-relative path of the block, e.g. 1_Tests/FC_Block_1")] string blockPath)
        {
            return Guarded(nameof(DeleteBlock), () =>
            {
                Portal.DeleteBlock(softwarePath, blockPath);
                return Deleted("Block", blockPath);
            });
        }

        [WriteTool]
        [McpServerTool(Name = "RenameBlock", Title = "Rename a block", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Rename a program block. Know-how protected blocks are rejected")]
        public static ResponseRenamed RenameBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: root-relative path of the block, e.g. 1_Tests/FC_Block_1")] string blockPath,
            [Description("newName: the new block name, without a slash")] string newName)
        {
            return Guarded(nameof(RenameBlock), () =>
            {
                Portal.RenameBlock(softwarePath, blockPath, newName);
                return Renamed("Block", blockPath, newName, ReplaceLeaf(blockPath, newName));
            });
        }

        [WriteTool]
        [McpServerTool(Name = "CreateFB", Title = "Create a function block", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create an empty function block. Openness has no generic create-block operation: FB and instance DB are the only kinds creatable without importing XML")]
        public static ResponseCreated CreateFB(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative block group that receives the FB; empty uses the Program blocks root")] string groupPath,
            [Description("name: name of the new function block, without a slash")] string name,
            [Description("language: programming language such as LAD (default), FBD, STL, SCL or GRAPH")] string language = "LAD",
            [Description("autoNumber: let TIA Portal assign the block number (default true)")] bool autoNumber = true,
            [Description("number: explicit block number, used only when autoNumber is false")] int number = 0)
        {
            return Guarded(nameof(CreateFB), () =>
            {
                var block = Portal.CreateFB(softwarePath, groupPath, name, autoNumber, number, language);
                return Created("FB", block.Name, JoinPath(groupPath, block.Name));
            });
        }

        [WriteTool]
        [McpServerTool(Name = "CreateInstanceDB", Title = "Create an instance data block", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create an instance data block for an existing function block")]
        public static ResponseCreated CreateInstanceDB(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative block group that receives the DB; empty uses the Program blocks root")] string groupPath,
            [Description("name: name of the new instance data block, without a slash")] string name,
            [Description("instanceOfName: name of the function block this instance DB belongs to")] string instanceOfName,
            [Description("autoNumber: let TIA Portal assign the block number (default true)")] bool autoNumber = true,
            [Description("number: explicit block number, used only when autoNumber is false")] int number = 0)
        {
            return Guarded(nameof(CreateInstanceDB), () =>
            {
                var block = Portal.CreateInstanceDB(softwarePath, groupPath, name, instanceOfName, autoNumber, number);
                return Created("InstanceDB", block.Name, JoinPath(groupPath, block.Name));
            });
        }

        #endregion

        #region move copy (write)

        [WriteTool]
        [McpServerTool(Name = "CopyBlock", Title = "Copy a block to another group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Copy a program block into another block group of the same plc software. Implemented as export plus import because Openness has no copy operation, so the block must be consistent and keeps its block number")]
        public static ResponseCreated CopyBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: root-relative path of the block to copy, e.g. 1_Tests/FC_Block_1")] string blockPath,
            [Description("targetGroupPath: root-relative block group that receives the copy; empty means the Program blocks root")] string targetGroupPath,
            [Description("overwrite: replace a block of the same name already in the target group (default false)")] bool overwrite = false)
        {
            return Guarded(nameof(CopyBlock), () =>
            {
                var block = Portal.CopyBlock(softwarePath, blockPath, targetGroupPath, overwrite);
                var newPath = JoinPath(targetGroupPath, block.Name);

                return new ResponseCreated
                {
                    Kind = "Block",
                    Name = block.Name,
                    Path = newPath,
                    Message = $"Block '{blockPath}' copied to '{newPath}'. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "MoveBlock", Title = "Move a block to another group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Move a program block into another block group of the same plc software. Implemented as export, import and deleting the original; the original is only removed after the import succeeds")]
        public static ResponseRenamed MoveBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: root-relative path of the block to move, e.g. 1_Tests/FC_Block_1")] string blockPath,
            [Description("targetGroupPath: root-relative block group that receives the block; empty means the Program blocks root")] string targetGroupPath,
            [Description("overwrite: replace a block of the same name already in the target group (default false)")] bool overwrite = false)
        {
            return Guarded(nameof(MoveBlock), () =>
            {
                var block = Portal.MoveBlock(softwarePath, blockPath, targetGroupPath, overwrite);
                var newPath = JoinPath(targetGroupPath, block.Name);

                return new ResponseRenamed
                {
                    Kind = "Block",
                    OldPath = blockPath,
                    NewName = block.Name,
                    NewPath = newPath,
                    Message = $"Block '{blockPath}' moved to '{newPath}'. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }

        #endregion
    }
}
