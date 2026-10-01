using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.Multiuser;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Blocks.Interface;
using Siemens.Engineering.SW.ExternalSources;
// Imported rather than written inline: TiaMcpServer.Siemens.Engineering shadows the
// Siemens.Engineering namespace, so a fully qualified Siemens.Engineering.SW.Types.PlcType
// does not resolve from inside this namespace.
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
        // From the former Portal.Source.cs:
        // Reading what an object actually contains, rather than only its metadata.
        //
        // Callers: the GetBlockSource / GetTypeSource / GetBlockInterface tools in
        // McpServer.Source.cs. Affected API: none existing - every member here is new.
        // GetBlockSource, GetTypeSource and GetBlockInterface themselves now live in Portal.Blocks.cs and
        // Portal.Types.cs; the shared reader, the scratch directory and the whole-PLC export are here.
        //
        // File I/O: Openness exposes no in-memory object for a block body - the only way to obtain
        // the code is to export it - so the source readers export into a scratch directory under
        // the system temp path and remove it again before returning. Nothing is written to a
        // caller-visible location, and the project is never modified.
        //
        // GetBlockInterface needs no export at all: PlcBlockInterface.Members is available in
        // memory, and it also works on inconsistent blocks that cannot be exported.

        #region scratch directory

        /// <summary>
        /// A throwaway directory for one export. Cleanup is best-effort: leaving a few files in
        /// the temp path is preferable to failing a read that already succeeded.
        /// </summary>
        private sealed class SourceScope : IDisposable
        {
            public SourceScope()
            {
                Directory = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), "TiaMcpServer", Guid.NewGuid().ToString("N"));

                System.IO.Directory.CreateDirectory(Directory);
            }

            public string Directory { get; }

            public void Dispose()
            {
                try
                {
                    if (System.IO.Directory.Exists(Directory))
                    {
                        System.IO.Directory.Delete(Directory, recursive: true);
                    }
                }
                catch (Exception)
                {
                    // Best effort - the OS cleans the temp path eventually.
                }
            }
        }

        #endregion

        #region source text

        /// <summary>
        /// Shared body of the two readers: export into a scratch directory with the requested
        /// exporter, concatenate what landed there, and truncate on a line boundary.
        /// </summary>
        private SourceTextResult ReadSource(
            string format,
            string objectPath,
            string name,
            int maxChars,
            Func<string, bool> exportDocuments,
            Func<string, bool> exportXml)
        {
            var wantsDocument = format.Equals("document", StringComparison.OrdinalIgnoreCase);

            if (!wantsDocument && !format.Equals("xml", StringComparison.OrdinalIgnoreCase))
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"Unknown format '{format}'. Use 'document' for readable source text or 'xml' for SimaticML.");
            }

            if (maxChars <= 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "'maxChars' must be greater than zero.");
            }

            using (var scope = new SourceScope())
            {
                var used = wantsDocument ? "document" : "xml";

                if (wantsDocument)
                {
                    try
                    {
                        exportDocuments(scope.Directory);
                    }
                    catch (PortalException pex) when (IsUnsupportedForDocuments(pex))
                    {
                        // TIA Portal refuses a source document for whole categories of object -
                        // STL blocks and mixed-language blocks among them. XML always exists, so
                        // fall back and say so in Format rather than returning nothing.
                        _logger?.LogWarning("No source document for '{Name}', falling back to XML: {Reason}", name, pex.Message);

                        exportXml(scope.Directory);
                        used = "xml";
                    }
                }
                else
                {
                    exportXml(scope.Directory);
                }

                var files = System.IO.Directory
                    .GetFiles(scope.Directory, "*.*", SearchOption.AllDirectories)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (files.Count == 0)
                {
                    throw new PortalException(PortalErrorCode.ExportFailed,
                        $"TIA Portal produced no file for '{objectPath}', so its source cannot be read.");
                }

                var text = string.Join(
                    Environment.NewLine,
                    files.Select(f => files.Count > 1
                        ? $"===== {System.IO.Path.GetFileName(f)} ====={Environment.NewLine}{File.ReadAllText(f)}"
                        : File.ReadAllText(f)));

                var full = text.Length;
                var truncated = full > maxChars;

                if (truncated)
                {
                    // Cut on a line boundary so the caller never sees half a statement.
                    var cut = text.LastIndexOf('\n', Math.Min(maxChars, text.Length - 1));

                    text = text.Substring(0, cut > 0 ? cut : maxChars);
                }

                return new SourceTextResult
                {
                    Name = name,
                    Path = objectPath,
                    Format = used,
                    Text = text,
                    TotalChars = full,
                    Truncated = truncated,
                    FileNames = files.Select(f => System.IO.Path.GetFileName(f)).ToList()
                };
            }
        }

        /// <summary>
        /// Whether a failed document export means "this object can never have one", in which
        /// case falling back to XML is right, as opposed to a real failure worth reporting.
        ///
        /// The check walks the inner exception chain rather than trusting the PortalErrorCode:
        /// the type document exporter maps EngineeringNotSupportedException to NotSupported, but
        /// the older block exporter reports the same condition as ExportFailed, and changing
        /// that would alter what the shipped ExportAsDocuments tool returns.
        /// </summary>
        private static bool IsUnsupportedForDocuments(PortalException exception)
        {
            if (exception.Code == PortalErrorCode.NotSupported)
            {
                return true;
            }

            for (Exception? inner = exception; inner != null; inner = inner.InnerException)
            {
                if (inner is EngineeringNotSupportedException)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region whole-plc snapshot

        /// <summary>
        /// Writes one PLC software to a folder tree that mirrors the project: program blocks and
        /// PLC data types as SIMATIC Source Documents where TIA Portal supports them, tag tables
        /// and watch tables as XML, each below its localised system folder. The result is a
        /// directory a version control system can diff.
        ///
        /// Composes the existing bulk exporters rather than calling Openness directly, so it
        /// inherits their skip-and-continue behaviour: one object that cannot be exported does
        /// not lose the rest of the snapshot.
        /// </summary>
        public SourceTreeResult ExportPlcAsSourceTree(string softwarePath, string exportPath)
        {
            return Operation.Run(_logger, nameof(ExportPlcAsSourceTree), PortalErrorCode.ExportFailed,
                () =>
                {
                    // Fail before writing anything when the path is wrong.
                    GetPlcSoftwareOrThrow(softwarePath);

                    var result = new SourceTreeResult { Directory = exportPath };

                    // Blocks: documents when the Portal is new enough, XML otherwise, since a
                    // snapshot missing every block would be worse than a less readable one.
                    if (Engineering.TiaMajorVersion >= 20)
                    {
                        var blocks = ExportBlocksAsDocuments(softwarePath, exportPath, string.Empty, preservePath: true);

                        Record(result, "blocks", "document", blocks?.Count() ?? 0);
                    }
                    else
                    {
                        var blocks = ExportBlocks(softwarePath, exportPath, string.Empty, preservePath: true);

                        Record(result, "blocks", "xml", blocks?.Count() ?? 0);
                    }

                    if (Engineering.TiaMajorVersion >= MinVersionForTypeDocuments)
                    {
                        var types = ExportTypesAsDocuments(softwarePath, exportPath, string.Empty, preservePath: true);

                        Record(result, "types", "document", types.Exported.Count);
                        result.Skipped.AddRange(types.Inconsistent.Select(t => $"type {GetTypePath(t)}: inconsistent"));
                        result.Failures.AddRange(types.Failures.Select(f => $"type {f}"));
                    }
                    else
                    {
                        var types = ExportTypes(softwarePath, exportPath, string.Empty, preservePath: true);

                        Record(result, "types", "xml", types?.Count() ?? 0);
                    }

                    // Tag and watch tables have no document format in V21; XML is all there is.
                    Record(result, "tagTables", "xml", ExportEach(
                        GetTagTables(softwarePath).Select(t => (GetTagTablePath(t), (Action)(() => ExportTagTable(softwarePath, GetTagTablePath(t), exportPath, preservePath: true)))),
                        "tag table",
                        result));

                    Record(result, "watchTables", "xml", ExportEach(
                        GetWatchTables(softwarePath).Select(t => (GetWatchTablePath(t), (Action)(() => ExportWatchTable(softwarePath, GetWatchTablePath(t), exportPath, preservePath: true)))),
                        "watch table",
                        result));

                    _logger?.LogInformation(
                        "Source tree for '{Software}' written to '{Directory}': {Written} object(s), {Skipped} skipped, {Failed} failed",
                        softwarePath, exportPath, result.TotalWritten, result.Skipped.Count, result.Failures.Count);

                    return result;
                },
                ("softwarePath", softwarePath), ("exportPath", exportPath));
        }

        private static void Record(SourceTreeResult result, string area, string format, int count)
        {
            result.Written[area] = count;
            result.Formats[area] = format;
        }

        /// <summary>
        /// Runs one export per object, collecting failures instead of aborting - the table
        /// exporters throw per object, unlike the bulk block and type exporters.
        /// </summary>
        private int ExportEach(IEnumerable<(string Path, Action Export)> items, string kind, SourceTreeResult result)
        {
            var written = 0;

            foreach (var item in items)
            {
                try
                {
                    item.Export();
                    written++;
                }
                catch (Exception ex)
                {
                    result.Failures.Add($"{kind} {item.Path}: {ex.Message}");
                    _logger?.LogWarning(ex, "Snapshot could not export {Kind} '{Path}'", kind, item.Path);
                }
            }

            return written;
        }

        #endregion
    }

    /// <summary>Source text of one object plus what had to be done to obtain it.</summary>
    public class SourceTextResult
    {
        public string Name { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        /// <summary>'document' or 'xml' - the format actually produced, which may differ from the request.</summary>
        public string Format { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        /// <summary>Length before truncation, so the caller can tell how much was withheld.</summary>
        public int TotalChars { get; set; }

        public bool Truncated { get; set; }

        /// <summary>File names TIA Portal produced, without the scratch directory.</summary>
        public List<string> FileNames { get; set; } = new List<string>();
    }

    /// <summary>What a whole-PLC snapshot produced.</summary>
    public class SourceTreeResult
    {
        public string Directory { get; set; } = string.Empty;

        /// <summary>Objects written per area: blocks, types, tagTables, watchTables.</summary>
        public Dictionary<string, int> Written { get; set; } = new Dictionary<string, int>();

        /// <summary>Format used per area: 'document' where TIA Portal supports it, else 'xml'.</summary>
        public Dictionary<string, string> Formats { get; set; } = new Dictionary<string, string>();

        /// <summary>Objects deliberately left out, with the reason.</summary>
        public List<string> Skipped { get; set; } = new List<string>();

        /// <summary>Objects that failed to export, with the reason.</summary>
        public List<string> Failures { get; set; } = new List<string>();

        public int TotalWritten => Written.Values.Sum();
    }
}
