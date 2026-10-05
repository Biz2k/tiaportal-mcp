using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    public partial class McpServer
    {
        [McpServerTool(Name = "export_objects", Title = "Export objects", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Universal tool to export PLC objects (blocks, types, tag tables) to files. format must be 'xml', 'document', or 'source'. Set object_paths to empty to export everything (supported for block/type folders).")]
        public static object ExportObjects(
            [Description("softwarePath: path to PLC software")] string softwarePath,
            [Description("object_paths: list of object paths or groups (e.g. 'Main', 'MyGroup')")] string[] object_paths,
            [Description("format: 'xml', 'document', or 'source'")] string format,
            [Description("exportPath: target directory")] string exportPath)
        {
            try
            {
                var results = new List<object>();
                int successCount = 0;
                
                // If object_paths is empty, export everything (using the bulk methods)
                if (object_paths == null || object_paths.Length == 0)
                {
                    if (format.ToLower() == "document")
                    {
                        var res = Portal.ExportPlcAsDocuments(softwarePath, exportPath);
                        return new { Message = "Exported entire PLC as documents", Result = res, Meta = OkMeta() };
                    }
                    else if (format.ToLower() == "xml")
                    {
                        var b = Portal.ExportXmlBlocks(softwarePath, exportPath, "", true);
                        var t = Portal.ExportXmlTypes(softwarePath, exportPath, "", true);
                        return new { Message = "Exported all blocks and types as XML", Blocks = b?.Count() ?? 0, Types = t?.Count() ?? 0, Meta = OkMeta() };
                    }
                    else if (format.ToLower() == "source")
                    {
                        var res = Portal.GenerateSources(softwarePath, exportPath, "", true);
                        return new { Message = "Exported all blocks and types as sources", Result = res, Meta = OkMeta() };
                    }
                }

                foreach (var path in object_paths)
                {
                    try
                    {
                        // 1. Determine kind
                        var matches = Portal.ResolveObjectPath(softwarePath, path, "any");
                        if (matches.Count == 0)
                        {
                            results.Add(new { status = "error", path = path, error = "Not found" });
                            continue;
                        }

                        var match = matches.First(); // take exact match or first match
                        string kind = match.Kind;
                        string resolvedPath = match.Path;

                        if (format.ToLower() == "xml")
                        {
                            if (kind == "block") Portal.ExportXmlBlock(softwarePath, resolvedPath, exportPath, true);
                            else if (kind == "type") Portal.ExportXmlType(softwarePath, resolvedPath, exportPath, true);
                            else if (kind == "tagTable") Portal.ExportXmlTagTable(softwarePath, resolvedPath, exportPath, true);
                            else if (kind == "watchTable") Portal.ExportXmlWatchTable(softwarePath, resolvedPath, exportPath, true);
                            else throw new Exception($"XML export not supported for {kind}");
                        }
                        else if (format.ToLower() == "document")
                        {
                            if (kind == "block") Portal.ExportAsDocuments(softwarePath, resolvedPath, exportPath, true);
                            else if (kind == "type") Portal.ExportTypeAsDocuments(softwarePath, resolvedPath, exportPath, true);
                            else throw new Exception($"Document export not supported for {kind}");
                        }
                        else if (format.ToLower() == "source")
                        {
                            if (kind == "block") Portal.ExportSourceBlock(softwarePath, resolvedPath, exportPath, false, true);
                            else if (kind == "type") Portal.ExportSourceType(softwarePath, resolvedPath, exportPath, false, true);
                            else throw new Exception($"Source export not supported for {kind}");
                        }
                        else
                        {
                            throw new Exception($"Unknown format '{format}'");
                        }

                        results.Add(new { status = "success", path = resolvedPath, kind = kind });
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        results.Add(new { status = "error", path = path, error = Why(ex) });
                    }
                }

                return new { 
                    Message = $"Exported {successCount} out of {object_paths.Length} objects.",
                    SuccessCount = successCount, 
                    Results = results, 
                    Meta = OkMeta() 
                };
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
            }
        }

        [McpServerTool(Name = "import_objects", Title = "Import objects", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Universal tool to import files into the PLC. format must be 'xml', 'document', or 'source'. For document/xml imports conflict_resolution must be 'overwrite', 'skip', or 'rename'.")]
        public static object ImportObjects(
            [Description("softwarePath: path to PLC software")] string softwarePath,
            [Description("file_paths: list of file paths to import")] string[] file_paths,
            [Description("format: 'xml', 'document', or 'source'")] string format,
            [Description("target_group: optional group path to import into (e.g. 'MyGroup'). Leave empty for root.")] string target_group = "",
            [Description("conflict_resolution: 'overwrite', 'skip', or 'rename'")] string conflict_resolution = "overwrite")
        {
            try
            {
                var results = new List<object>();
                int successCount = 0;

                foreach (var file in file_paths)
                {
                    try
                    {
                        if (format.ToLower() == "xml")
                        {
                            string xmlContent = File.ReadAllText(file);
                            if (xmlContent.Contains("<SW.Blocks."))
                                Portal.ImportXmlBlock(softwarePath, target_group, file);
                            else if (xmlContent.Contains("<SW.Types."))
                                Portal.ImportXmlType(softwarePath, target_group, file);
                            else if (xmlContent.Contains("<SW.Tags."))
                                Portal.ImportXmlTagTable(softwarePath, target_group, file);
                            else if (xmlContent.Contains("<SW.WatchAndForceTables."))
                                Portal.ImportWatchTable(softwarePath, target_group, file);
                            else
                                throw new Exception("Unknown XML content type. Could not determine block/type/tag/watch from file.");
                        }
                        else if (format.ToLower() == "document")
                        {
                            var importPath = Path.GetDirectoryName(file);
                            var fileName = Path.GetFileNameWithoutExtension(file);
                            var option = ParseImportDocumentOption(conflict_resolution == "skip" ? "None" : "Override");
                            Portal.ImportFromDocuments(softwarePath, target_group, importPath, fileName, option);
                        }
                        else if (format.ToLower() == "source")
                        {
                            Portal.ImportSources(softwarePath, file, "", false); 
                        }
                        else
                        {
                            throw new Exception($"Unknown format '{format}'");
                        }

                        results.Add(new { status = "success", file = file });
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        results.Add(new { status = "error", file = file, error = Why(ex) });
                    }
                }

                return new { 
                    Message = $"Imported {successCount} out of {file_paths.Length} files.",
                    SuccessCount = successCount, 
                    Results = results, 
                    Meta = OkMeta() 
                };
            }
            catch (Exception ex)
            {
                throw ToolError(ex);
            }
        }
    }
}
