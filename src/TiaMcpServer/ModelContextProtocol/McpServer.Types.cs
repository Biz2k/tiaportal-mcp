using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        #region types

        [McpServerTool(Name = "plc_get_type_info", Title = "Get type info", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get a type info from the plc software")]
        public static ResponseTypeInfo GetTypeInfo(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("typePath: defines the path in the project structure to the type")] string typePath)
        {
            try
            {
                var type = Portal.GetType(softwarePath, typePath);
                if (type != null)
                {
                    var attributes = Helper.GetAttributeList(type);

                    return new ResponseTypeInfo
                    {
                        Path = Portal.GetTypePath(type),
                        Message = $"Type info retrieved from '{typePath}' in '{softwarePath}'",
                        Name = type.Name,
                        TypeName = type.GetType().Name,
                        Namespace = type.Namespace,
                        IsConsistent = type.IsConsistent,
                        ModifiedDate = type.ModifiedDate,
                        IsKnowHowProtected = type.IsKnowHowProtected,
                        Attributes = attributes,
                        Description = type.ToString(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw ObjectNotFound("Type", typePath, softwarePath, "typePath", "plc_get_types");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"retrieving type info from '{typePath}' in '{softwarePath}'", ex);
            }
        }

        [McpServerTool(Name = "plc_get_types", Title = "Get types", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get a list of types from the plc software")]
        public static ResponseTypes GetTypes(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("regexName: defines the name or regular expression to find the type. Use empty string (default) to find all")] string regexName = "",
            [Description(Paging.LimitText)] int limit = 500,
            [Description(Paging.OffsetText)] int offset = 0)
        {
            try
            {
                var page = ListPage<global::Siemens.Engineering.SW.Types.PlcType>.Of(Portal.GetTypes(softwarePath, regexName), limit, offset);
                var list = page.Items;

                var responseList = new List<ResponseTypeInfo>();
                foreach (var type in list)
                {
                    if (type != null)
                    {
                        var attributes = Helper.GetAttributeList(type);

                        responseList.Add(new ResponseTypeInfo
                        {
                            Path = Portal.GetTypePath(type),
                            Name = type.Name,
                            TypeName = type.GetType().Name,
                            Namespace = type.Namespace,
                            IsConsistent = type.IsConsistent,
                            ModifiedDate = type.ModifiedDate,
                            IsKnowHowProtected = type.IsKnowHowProtected,
                            Attributes = attributes,
                            Description = type.ToString()
                        });
                    }
                }

                if (list != null)
                {
                    return new ResponseTypes
                    {
                        Message = $"Types with regex '{regexName}' retrieved from '{softwarePath}': {page.Total}." + page.Note("regexName"),
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
                    throw new McpException($"Failed retrieving user defined types with regex '{regexName}' in '{softwarePath}'");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"retrieving user defined types with regex '{regexName}' in '{softwarePath}'", ex);
            }
        }

        // [McpServerTool(Name = "export_xml_type", Title = "Export type as XML", Destructive = true, Idempotent = true, OpenWorld = false), Description("Export type as XML")]
        public static ResponseExportXmlType ExportXmlType(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: defines the path where export the type")] string exportPath,
            [Description("typePath: defines the path in the project structure to the type")] string typePath,
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            try
            {
                var type = Portal.ExportXmlType(softwarePath, typePath, exportPath, preservePath);
                if (type != null)
                {
                    return new ResponseExportXmlType
                    {
                        Message = $"Type exported from '{typePath}' to '{exportPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting type from '{typePath}' to '{exportPath}'");
                }
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException("Type not found.");
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                        throw new McpException(pex.Message);
                    case TiaMcpServer.Siemens.PortalErrorCode.ExportFailed:
                        {
                            var reason = pex.InnerException?.Message?.Trim();
                            var msg = "Failed to export type.";
                            if (!string.IsNullOrEmpty(reason)) msg += $" Reason: {reason}";
                            Logger?.LogError(pex, "MCP ExportXmlType failed for {SoftwarePath} {TypePath} -> {ExportPath}",
                                pex.Data?["softwarePath"], pex.Data?["typePath"], pex.Data?["exportPath"]);
                            throw new McpException(msg);
                        }
                }
                throw new McpException(pex.Message);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"exporting type from '{typePath}' to '{exportPath}'", ex);
            }
        }

        // [McpServerTool(Name = "import_xml_type", Title = "Import type as XML", Destructive = true, Idempotent = true, OpenWorld = false), Description("Import type from XML")]
        public static ResponseImportXmlType ImportXmlType(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: defines the path in the project structure to the group, where to import the type")] string groupPath,
            [Description("importPath: defines the path of the xml file from where to import the type")] string importPath)
        {
            try
            {
                if (Portal.ImportXmlType(softwarePath, groupPath, importPath))
                {
                    return new ResponseImportXmlType
                    {
                        Message = $"Type imported from '{importPath}' to '{groupPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed importing type from '{importPath}' to '{groupPath}'");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"importing type from '{importPath}' to '{groupPath}'", ex);
            }
        }

        // [McpServerTool(Name = "export_xml_types", Title = "Export types as XML", Destructive = true, Idempotent = true, OpenWorld = false), Description("Export types as XML")]
        public static async Task<ResponseExportXmlTypes> ExportXmlTypes(
            IProgress<ProgressNotificationValue> progress,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: defines the path where to export the types")] string exportPath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "",
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            var startTime = DateTime.Now;
            
            try
            {
                // First, get the list of types to determine total count
                Logger?.LogInformation($"Starting export of types from '{softwarePath}' to '{exportPath}'");
                
                var allTypes = await Task.Run(() => Portal.GetTypes(softwarePath, regexName));
                var totalTypes = allTypes?.Count ?? 0;

                if (totalTypes == 0)
                {
                    progress.Report(new ProgressNotificationValue { Progress = 0, Total = 0, Message = "No types found to export" });
                    
                    return new ResponseExportXmlTypes
                    {
                        Message = $"No types found with regex '{regexName}' in '{softwarePath}'",
                        Items = new List<ResponseTypeInfo>(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalTypes"] = 0,
                            ["exportedTypes"] = 0,
                            ["duration"] = (DateTime.Now - startTime).TotalSeconds
                        }
                    };
                }

                // Send initial progress notification
                progress.Report(new ProgressNotificationValue { Progress = 0, Total = totalTypes, Message = $"Starting export of {totalTypes} types..." });

                // Export types asynchronously
                var exportedTypes = await Task.Run(() => Portal.ExportXmlTypes(softwarePath, exportPath, regexName, preservePath));

                // Build list of inconsistent (skipped) types for reporting
                var inconsistentTypeInfos = new List<ResponseTypeInfo>();
                if (allTypes != null)
                {
                    foreach (var t in allTypes)
                    {
                        if (t != null && t.IsConsistent == false)
                        {
                            var attrs = Helper.GetAttributeList(t);
                            inconsistentTypeInfos.Add(new ResponseTypeInfo
                            {
                                Path = Portal.GetTypePath(t),
                                Name = t.Name,
                                TypeName = t.GetType().Name,
                                Namespace = t.Namespace,
                                IsConsistent = t.IsConsistent,
                                ModifiedDate = t.ModifiedDate,
                                IsKnowHowProtected = t.IsKnowHowProtected,
                                Attributes = attrs,
                                Description = t.ToString()
                            });
                        }
                    }
                }
                
                // Send progress update after export completion
                if (exportedTypes != null)
                {
                    var exportedCount = exportedTypes.Count();
                    progress.Report(new ProgressNotificationValue { Progress = exportedCount, Total = totalTypes, Message = $"Exported {exportedCount} of {totalTypes} types" });
                }

                if (exportedTypes != null)
                {
                    var responseList = new List<ResponseTypeInfo>();
                    var processedCount = 0;
                    
                    foreach (var type in exportedTypes)
                    {
                        if (type != null)
                        {
                            var attributes = Helper.GetAttributeList(type);

                            responseList.Add(new ResponseTypeInfo
                            {
                                Path = Portal.GetTypePath(type),
                                Name = type.Name,
                                TypeName = type.GetType().Name,
                                Namespace = type.Namespace,
                                IsConsistent = type.IsConsistent,
                                ModifiedDate = type.ModifiedDate,
                                IsKnowHowProtected = type.IsKnowHowProtected,
                                Attributes = attributes,
                                Description = type.ToString()
                            });
                        }
                        processedCount++;
                    }

                    // Send final progress notification
                    progress.Report(new ProgressNotificationValue { Progress = processedCount, Total = totalTypes, Message = $"Export completed: {processedCount} types exported successfully" });

                    var duration = (DateTime.Now - startTime).TotalSeconds;
                    Logger?.LogInformation($"Type export completed: {processedCount} types exported in {duration:F2} seconds");

                    return new ResponseExportXmlTypes
                    {
                        Message = $"Export completed: {processedCount} types with regex '{regexName}' exported from '{softwarePath}' to '{exportPath}'",
                        Items = responseList,
                        Inconsistent = inconsistentTypeInfos,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalTypes"] = totalTypes,
                            ["exportedTypes"] = processedCount,
                            ["inconsistentTypes"] = inconsistentTypeInfos.Count,
                            ["duration"] = duration
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting types '{regexName}' from '{softwarePath}' to {exportPath}");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                // Send error progress notification if we have a progress token
                progress.Report(new ProgressNotificationValue { Progress = 0, Total = 0, Message = $"Type export failed: {ex.Message}" });
                
                Logger?.LogError(ex, $"Failed exporting types '{regexName}' from '{softwarePath}' to {exportPath}");
                throw Failure($"exporting types '{regexName}' from '{softwarePath}' to {exportPath}", ex);
            }
        }

        #endregion

        // From the former McpServerWrite.Types.cs:

        #region groups (write)

        [WriteTool]
        [McpServerTool(Name = "plc_create_type_group", Title = "Create type group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a group below the PLC data types root of the plc software")]
        public static ResponseCreated CreateTypeGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("parentGroupPath: root-relative path of the parent group; empty creates directly below PLC data types")] string parentGroupPath,
            [Description("name: name of the new group, without a slash")] string name)
        {
            return Guarded(nameof(CreateTypeGroup), () =>
            {
                Portal.CreateTypeGroup(softwarePath, parentGroupPath, name);
                return Created("Type group", name, JoinPath(parentGroupPath, name));
            });
        }

        [WriteTool]
        [McpServerTool(Name = "plc_delete_type_group", Title = "Delete type group", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a PLC data type group and everything inside it. The PLC data types system group itself cannot be deleted")]
        public static ResponseDeleted DeleteTypeGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: root-relative path of the group to delete")] string groupPath)
        {
            return Guarded(nameof(DeleteTypeGroup), () =>
            {
                Portal.DeleteTypeGroup(softwarePath, groupPath);
                return Deleted("Type group", groupPath);
            });
        }

        #endregion

        #region blocks and types (write)

        [WriteTool]
        [McpServerTool(Name = "plc_delete_type", Title = "Delete type", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a PLC data type (UDT). Know-how protected types are rejected")]
        public static ResponseDeleted DeleteType(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("typePath: root-relative path of the type, e.g. Common/CarrierRegister/ML_SubstratState")] string typePath)
        {
            return Guarded(nameof(DeleteType), () =>
            {
                Portal.DeleteType(softwarePath, typePath);
                return Deleted("Type", typePath);
            });
        }

        [WriteTool]
        [McpServerTool(Name = "plc_rename_type", Title = "Rename type", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Rename a PLC data type (UDT). Know-how protected types are rejected")]
        public static ResponseRenamed RenameType(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("typePath: root-relative path of the type")] string typePath,
            [Description("newName: the new type name, without a slash")] string newName)
        {
            return Guarded(nameof(RenameType), () =>
            {
                Portal.RenameType(softwarePath, typePath, newName);
                return Renamed("Type", typePath, newName, ReplaceLeaf(typePath, newName));
            });
        }

        #endregion

        #region move copy (write)

        [WriteTool]
        [McpServerTool(Name = "plc_copy_type", Title = "Copy type", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Copy a PLC data type (UDT). A type name is unique across the whole PLC, so a copy inside the same PLC requires 'newName'; to keep the name, copy into another PLC with 'targetSoftwarePath'. To change only the group use 'plc_move_type'. Implemented as export plus import, so the type must be consistent (compile first)")]
        public static ResponseCreated CopyType(
            [Description("softwarePath: defines the path in the project structure to the plc software holding the type")] string softwarePath,
            [Description("typePath: root-relative path of the type to copy, e.g. Common/CarrierRegister/ML_SubstratState")] string typePath,
            [Description("targetGroupPath: root-relative type group that receives the copy; empty means the PLC data types root")] string targetGroupPath,
            [Description("newName: name of the copy. Required when copying inside the same PLC; empty keeps the name when copying into another PLC")] string newName = "",
            [Description("targetSoftwarePath: plc software that receives the copy; empty (default) means the same PLC")] string targetSoftwarePath = "",
            [Description("overwrite: replace a type of the final name that already exists in the target PLC (default false)")] bool overwrite = false)
        {
            return Guarded(nameof(CopyType), () =>
            {
                var type = Portal.CopyType(softwarePath, typePath, targetGroupPath, newName, targetSoftwarePath, overwrite);
                var newPath = Portal.GetTypePath(type);

                return new ResponseCreated
                {
                    Kind = "Type",
                    Name = type.Name,
                    Path = newPath,
                    Message = $"Type '{typePath}' copied to '{newPath}'. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "plc_move_type", Title = "Move type", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Move a PLC data type (UDT) into another type group of the same plc software; the name is kept. Implemented as export, deleting the original and import, so the type must be consistent (compile first). If the import fails the type is restored in its original group")]
        public static ResponseRenamed MoveType(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("typePath: root-relative path of the type to move")] string typePath,
            [Description("targetGroupPath: root-relative type group that receives the type; empty means the PLC data types root")] string targetGroupPath)
        {
            return Guarded(nameof(MoveType), () =>
            {
                var type = Portal.MoveType(softwarePath, typePath, targetGroupPath);
                var newPath = Portal.GetTypePath(type);

                return new ResponseRenamed
                {
                    Kind = "Type",
                    OldPath = typePath,
                    NewName = type.Name,
                    NewPath = newPath,
                    Message = $"Type '{typePath}' moved to '{newPath}'. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }

        #endregion
    }
}

