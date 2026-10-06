using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.Compiler;
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

        [McpServerTool(Name = "plc_get_block_info", Title = "Get block info", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get a block info, which is located in the plc software")]
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
                    throw ObjectNotFound("Block", blockPath, softwarePath, "blockPath", "plc_get_blocks");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"retrieving block info from '{blockPath}' in '{softwarePath}'", ex);
            }
        }

        [McpServerTool(Name = "plc_get_blocks", Title = "Get blocks", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get a list of blocks, which are located in plc software. A large PLC has thousands: the first 500 are returned, narrow with regexName or page with offset")]
        public static ResponseBlocks GetBlocks(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "",
            [Description("limit: the most items to return (default 500); 0 returns all")] int limit = 500,
            [Description("offset: items to skip, to read the next page of a long list (default 0)")] int offset = 0)
        {
            try
            {
                var page = ListPage<PlcBlock>.Of(Portal.GetBlocks(softwarePath, regexName), limit, offset);
                var list = page.Items;

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
                        Message = $"Blocks with regex '{regexName}' retrieved from '{softwarePath}': {page.Total}." + page.Note("regexName"),
                        Items = responseList,
                        Meta = page.Meta(new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        })
                    };
                }
                else
                {
                    throw new McpException($"Failed retrieving blocks with regex '{regexName}' in '{softwarePath}'");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"retrieving blocks with regex '{regexName}' in '{softwarePath}'", ex);
            }
        }

        [McpServerTool(Name = "plc_get_blocks_hierarchy", Title = "Get blocks with hierarchy", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get a list of all blocks with their group hierarchy from the plc software.")]
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
                throw Failure($"retrieving block hierarchy for '{softwarePath}'", ex);
            }
        }

        // [McpServerTool(Name = "export_xml_block", Title = "Export block as XML", Destructive = true, Idempotent = true, OpenWorld = false), Description("Export block as XML")]
        public static ResponseExportXmlBlock ExportXmlBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: full path to the block in the project structure, e.g. 'Group/Subgroup/Name' (single names are ambiguous)")] string blockPath,
            [Description("exportPath: defines the path where to export the block")] string exportPath,
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            try
            {
                var block = Portal.ExportXmlBlock(softwarePath, blockPath, exportPath, preservePath);
                if (block != null)
                {
                    return new ResponseExportXmlBlock
                    {
                        Message = $"Block exported from '{blockPath}' to '{exportPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                // Should not be reachable because Portal.ExportXmlBlock throws on failure
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

                            Logger?.LogError(pex, "MCP ExportXmlBlock failed for {SoftwarePath} {BlockPath} -> {ExportPath}",
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
                throw Failure($"exporting block from '{blockPath}' to '{exportPath}'", ex);
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

        // [McpServerTool(Name = "import_xml_block", Title = "Import block as XML", Destructive = true, Idempotent = true, OpenWorld = false), Description("Import block from XML")]
        public static ResponseImportXmlBlock ImportXmlBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: defines the path in the project structure to the group, where to import the block")] string groupPath,
            [Description("importPath: defines the path of the xml file from where to import the block")] string importPath)
        {
            try
            {
                if (Portal.ImportXmlBlock(softwarePath, groupPath, importPath))
                {
                    return new ResponseImportXmlBlock
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
                throw Failure($"importing block from '{importPath}' to '{groupPath}'", ex);
            }
        }

        // [McpServerTool(Name = "export_xml_blocks", Title = "Export blocks as XML", Destructive = true, Idempotent = true, OpenWorld = false), Description("Export blocks as XML")]
        public static async Task<ResponseExportXmlBlocks> ExportXmlBlocks(
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
                    
                    return new ResponseExportXmlBlocks
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
                var exportedBlocks = await Task.Run(() => Portal.ExportXmlBlocks(softwarePath, exportPath, regexName, preservePath));

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

                    return new ResponseExportXmlBlocks
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
                throw Failure($"exporting blocks with '{regexName}' from '{softwarePath}' to {exportPath}", ex);
            }
        }

        #endregion

        #region source

        [McpServerTool(Name = "get_block_interface", Title = "Get block interface", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the members of a data block with their data type and every attribute TIA Portal reports. Needs no export and works on inconsistent blocks. Data blocks only: Openness offers no interface accessor for FB, FC or OB, whose declarations come from 'plc_get_block_source' instead")]
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
                throw ToolError(pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"reading the interface of '{blockPath}'", ex);
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
        // is CreateFB and CreateInstanceDB only - anything else arrives through ImportXmlBlock.

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
        [McpServerTool(Name = "plc_create_block_group", Title = "Create block group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
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
        [McpServerTool(Name = "plc_delete_block_group", Title = "Delete block group", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
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
        [McpServerTool(Name = "plc_delete_block", Title = "Delete block", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
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
        [McpServerTool(Name = "plc_rename_block", Title = "Rename block", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
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
        [McpServerTool(Name = "plc_create_fb", Title = "Create function block", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create an empty function block in LAD, FBD, STL, SCL or ProDiag (LAD, FBD and STL are built from a template made for TIA Portal V21; on an older version they are untested). For a block with code use 'plc_create_scl_block' (SCL text) or 'import_objects' (exported XML). The response carries the block number TIA Portal assigned")]
        public static ResponseCreated CreateFB(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative block group that receives the FB; empty uses the Program blocks root")] string groupPath,
            [Description("name: name of the new function block, without a slash; must not exist in the PLC yet")] string name,
            [Description("language: LAD (default), FBD, STL, SCL or ProDiag")] string language = "LAD",
            [Description("autoNumber: let the server pick the first free FB number (default true)")] bool autoNumber = true,
            [Description("number: explicit block number from 1, used only when autoNumber is false; must be free")] int number = 0)
        {
            return Guarded(nameof(CreateFB), () =>
            {
                var block = Portal.CreateFB(softwarePath, groupPath, name, autoNumber, number, language);

                return CreatedBlock("FB", block);
            });
        }

        [WriteTool]
        [McpServerTool(Name = "plc_create_instance_db", Title = "Create instance data block", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create an instance data block for an existing function block. The response carries the block number TIA Portal assigned")]
        public static ResponseCreated CreateInstanceDB(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative block group that receives the DB; empty uses the Program blocks root")] string groupPath,
            [Description("name: name of the new instance data block, without a slash; must not exist in the PLC yet")] string name,
            [Description("instanceOfName: name of the function block this instance DB belongs to")] string instanceOfName,
            [Description("autoNumber: let the server pick the first free DB number (default true)")] bool autoNumber = true,
            [Description("number: explicit block number from 1, used only when autoNumber is false; must be free")] int number = 0)
        {
            return Guarded(nameof(CreateInstanceDB), () =>
            {
                var block = Portal.CreateInstanceDB(softwarePath, groupPath, name, instanceOfName, autoNumber, number);

                return CreatedBlock("InstanceDB", block);
            });
        }

        private static ResponseCreated CreatedBlock(string kind, PlcBlock block)
        {
            return new ResponseCreated
            {
                Kind = kind,
                Name = block.Name,
                Path = Portal.GetBlockPath(block),
                Number = block.Number,
                Message = $"{kind} '{block.Name}' created with number {block.Number}. {SaveHint}",
                Meta = OkMeta()
            };
        }

        #endregion

        #region move copy (write)

        [WriteTool]
        [McpServerTool(Name = "plc_copy_block", Title = "Copy block", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Copy a program block. Block names and numbers are unique within a PLC, so a copy inside the same PLC requires 'newName' and gets a free block number; to keep the name, copy into another PLC with 'targetSoftwarePath'. To change only the group use 'plc_move_block'. Implemented as export plus import, so the block must be consistent (compile first)")]
        public static ResponseCreated CopyBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software holding the block")] string softwarePath,
            [Description("blockPath: root-relative path of the block to copy, e.g. 1_Tests/FC_Block_1")] string blockPath,
            [Description("targetGroupPath: root-relative block group that receives the copy; empty means the Program blocks root")] string targetGroupPath,
            [Description("newName: name of the copy. Required when copying inside the same PLC; empty keeps the name when copying into another PLC")] string newName = "",
            [Description("targetSoftwarePath: plc software that receives the copy; empty (default) means the same PLC")] string targetSoftwarePath = "",
            [Description("overwrite: replace a block of the final name that already exists in the target PLC (default false)")] bool overwrite = false)
        {
            return Guarded(nameof(CopyBlock), () =>
            {
                var block = Portal.CopyBlock(softwarePath, blockPath, targetGroupPath, newName, targetSoftwarePath, overwrite);
                var created = CreatedBlock("Block", block);

                created.Message = $"Block '{blockPath}' copied to '{created.Path}' as number {block.Number}. {SaveHint}";

                return created;
            });
        }

        [WriteTool]
        [McpServerTool(Name = "plc_move_block", Title = "Move block", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Move a program block into another block group of the same plc software; name and number are kept. Implemented as export, deleting the original and import, so the block must be consistent (compile first). If the import fails the block is restored in its original group")]
        public static ResponseRenamed MoveBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: root-relative path of the block to move, e.g. 1_Tests/FC_Block_1")] string blockPath,
            [Description("targetGroupPath: root-relative block group that receives the block; empty means the Program blocks root")] string targetGroupPath)
        {
            return Guarded(nameof(MoveBlock), () =>
            {
                var block = Portal.MoveBlock(softwarePath, blockPath, targetGroupPath);
                var newPath = Portal.GetBlockPath(block);

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

        #region compile (write)

        [WriteTool]
        [McpServerTool(Name = "plc_compile_block", Title = "Compile block", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Compile a program block. Returns the compiler result with any errors or warnings")]
        public static ResponseCompilerResult CompileBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: root-relative path of the block to compile, e.g. 1_Tests/FC_Block_1")] string blockPath)
        {
            return GuardedNoTransaction(nameof(CompileBlock), () =>
            {
                var result = Portal.CompileBlock(softwarePath, blockPath);
                return MapCompilerResult(result, $"Block '{blockPath}' compilation completed.");
            });
        }

        [WriteTool]
        [McpServerTool(Name = "plc_compile_software", Title = "Compile software", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Compile the entire PLC software. Returns the compiler result with any errors or warnings")]
        public static ResponseCompilerResult CompileSoftware(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath)
        {
            return GuardedNoTransaction(nameof(CompileSoftware), () =>
            {
                var result = Portal.CompileSoftware(softwarePath);
                return MapCompilerResult(result, $"Software '{softwarePath}' compilation completed.");
            });
        }

        private static ResponseCompilerResult MapCompilerResult(CompilerResult? result, string message)
        {
            if (result == null)
            {
                return new ResponseCompilerResult
                {
                    Message = message + " No result returned.",
                    State = "Unknown",
                    Meta = OkMeta()
                };
            }

            return new ResponseCompilerResult
            {
                Message = message + $" State: {result.State}, Errors: {result.ErrorCount}, Warnings: {result.WarningCount}",
                State = result.State.ToString(),
                ErrorCount = result.ErrorCount,
                WarningCount = result.WarningCount,
                Messages = result.Messages.Select(MapCompilerMessage).ToList(),
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = result.State == CompilerResultState.Success || result.State == CompilerResultState.Warning
                }
            };
        }

        private static ResponseCompilerMessage MapCompilerMessage(CompilerResultMessage msg)
        {
            return new ResponseCompilerMessage
            {
                Description = msg.Description,
                Path = msg.Path,
                State = msg.State.ToString(),
                ErrorCount = msg.ErrorCount,
                WarningCount = msg.WarningCount,
                Messages = msg.Messages.Select(MapCompilerMessage).ToList()
            };
        }

        #endregion

        [McpServerTool(Name = "plc_get_block_data", Title = "Get Block Data", Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Get comprehensive block information including metadata, interface, and source code depending on requested parts.")]
        public static object GetBlockData(string softwarePath, string blockName, System.Text.Json.Nodes.JsonArray parts)
        {
            return Guarded(nameof(GetBlockData), () =>
            {
                var pList = new List<string>();
                if (parts != null) {
                    foreach(var p in parts) if (p != null) pList.Add(p.GetValue<string>().ToLowerInvariant());
                }

                var block = Portal.GetBlock(softwarePath, blockName);
                if (block == null) throw new Exception($"Block {blockName} not found.");

                string? xmlContent = null;
                if (pList.Contains("interface") || pList.Contains("source")) {
                    string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), blockName + "_" + Guid.NewGuid().ToString() + ".xml");
                    try {
                        Portal.ExportXmlBlock(softwarePath, blockName, tempFile, false);
                        xmlContent = System.IO.File.ReadAllText(tempFile);
                    } finally {
                        if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile);
                    }
                }

                var result = new Dictionary<string, object>();
                result["name"] = block.Name;
                
                if (pList.Contains("metadata") || pList.Count == 0) {
                    result["metadata"] = new {
                        Number = block.Number,
                        Language = block.ProgrammingLanguage.ToString(),
                        AutoNumber = block.AutoNumber,
                        IsConsistent = block.IsConsistent
                    };
                }

                if (xmlContent != null) {
                    if (pList.Contains("interface")) {
                        var match = System.Text.RegularExpressions.Regex.Match(xmlContent, @"<Interface>.*?</Interface>", System.Text.RegularExpressions.RegexOptions.Singleline);
                        if (match.Success) result["interface"] = match.Value;
                    }
                    if (pList.Contains("source")) {
                        var match = System.Text.RegularExpressions.Regex.Match(xmlContent, @"<ObjectList>.*?</ObjectList>", System.Text.RegularExpressions.RegexOptions.Singleline);
                        if (match.Success) result["source"] = match.Value;
                    }
                }

                return result;
            });
        }
    }
}

