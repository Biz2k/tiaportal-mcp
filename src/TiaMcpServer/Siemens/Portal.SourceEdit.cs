using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Siemens
{
    // Replacing the code of an existing block or PLC data type by its source text.
    //
    // Callers: the tool plc_replace_source in McpServer.Sources.cs. File I/O: temporary source files under the OS temp
    // directory; the copy of the previous code is kept there only when a restore failed.
    //
    // Found on TIA Portal V21 (2026-10-06, FB with an instance DB and a calling FC in a user group):
    //   - Generating from a source that declares an existing block replaces that block where it is: the group, the
    //     block number and its instance DBs stay, with or without a target group in the call.
    //   - The instance DBs and the callers become inconsistent and need a compile; compiling the block or a caller
    //     brings the instance DB up to date.
    //   - A wrong declaration ("Counter : Int" - a reserved word) makes the generation fail and changes nothing. A
    //     syntax error in the code ("#Out := #In + ;") does NOT: the block is replaced by the broken code and only the
    //     compile says "Expression expected". So the previous code would be lost without a copy of it.
    //   - A compile is not permitted inside a transaction. The steps are therefore: generate (in a transaction),
    //     compile, and on errors generate the previous source again (a second transaction).
    //   - The previous source can be taken only from a consistent object that is not know-how protected.
    public partial class Portal
    {
        public SourceEditResult ReplaceSource(string softwarePath, string objectPath, string source, string compile, string onCompileError)
        {
            return Operation.Run(_logger, nameof(ReplaceSource), PortalErrorCode.ImportFailed,
                () =>
                {
                    var compileMode = (compile ?? string.Empty).Trim().ToLowerInvariant();

                    if (compileMode != "object" && compileMode != "software" && compileMode != "none")
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, $"compile takes 'object', 'software' or 'none'; got '{compile}'.");
                    }

                    var onError = (onCompileError ?? string.Empty).Trim().ToLowerInvariant();

                    if (onError != "restore" && onError != "keep")
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, $"onCompileError takes 'restore' or 'keep'; got '{onCompileError}'.");
                    }

                    var restoreWanted = onError == "restore" && compileMode != "none";

                    var block = GetBlock(softwarePath, objectPath);
                    var type = block == null ? GetType(softwarePath, objectPath) : null;

                    if (block == null && type == null)
                    {
                        throw new PortalException(PortalErrorCode.NotFound,
                            $"No block or PLC data type at '{objectPath}'. This tool replaces the code of an existing object; 'plc_resolve_object_path' finds a path by name, " +
                            "'plc_create_scl_block' creates a new block.");
                    }

                    var name = block?.Name ?? type!.Name;
                    var kind = block != null ? SourceDeclarations.KindOfBlockClass(block.GetType().Name) : "TYPE";
                    string extension;

                    // A LAD block has no external source; its text form is the SIMATIC SD document (Portal.Lad.cs).
                    if (block != null && block.ProgrammingLanguage == ProgrammingLanguage.LAD)
                    {
                        if (block.IsKnowHowProtected)
                        {
                            throw new PortalException(PortalErrorCode.NotSupported, $"'{name}' is know-how protected; its code cannot be replaced.");
                        }

                        return ReplaceLadDocument(softwarePath, objectPath, block, source, compileMode, restoreWanted);
                    }

                    if (block != null)
                    {
                        var (blockExtension, reason) = BlockSourceExtension(block);

                        if (blockExtension == null || blockExtension == ".awl")
                        {
                            throw new PortalException(PortalErrorCode.NotSupported,
                                $"Block '{name}' is written in {block.ProgrammingLanguage}. Its code cannot be replaced by a source text here: " +
                                (reason ?? "STL sources were not tested") + ". SCL blocks, LAD blocks, data blocks and PLC data types can.");
                        }

                        extension = blockExtension;
                    }
                    else
                    {
                        extension = TypeSourceExtension;
                    }

                    if (block?.IsKnowHowProtected == true || type?.IsKnowHowProtected == true)
                    {
                        throw new PortalException(PortalErrorCode.NotSupported, $"'{name}' is know-how protected; its code cannot be replaced.");
                    }

                    CheckDeclares(source, kind, name);

                    var result = new SourceEditResult
                    {
                        Name = name,
                        Path = objectPath,
                        Kind = kind,
                        Number = block?.Number,
                        Compile = compileMode
                    };

                    // The previous code, in the form it can be generated from again.
                    string? previous = null;
                    var consistent = block?.IsConsistent ?? type!.IsConsistent;

                    if (consistent)
                    {
                        previous = ReadExternalSource(softwarePath, objectPath, block != null);
                    }
                    else if (restoreWanted)
                    {
                        throw new PortalException(PortalErrorCode.InvalidState,
                            $"'{name}' is inconsistent, so TIA Portal cannot give its present code as a source and it could not be restored if the new code does not compile. " +
                            "Compile it first, or pass onCompileError='keep' to replace the code without that safety.");
                    }
                    else
                    {
                        result.Notes.Add("The object was inconsistent: its previous code could not be read and is not kept anywhere.");
                    }

                    var consistentBefore = ConsistentObjects(softwarePath);

                    try
                    {
                        InTransaction($"Replace the code of {kind} '{name}' (plc_replace_source)", () => GenerateInPlace(softwarePath, objectPath, source, extension, block != null, result.Number));
                    }
                    catch (PortalException pex) when (pex.Code == PortalErrorCode.CreateFailed)
                    {
                        // A declaration TIA Portal cannot read (an unknown data type, a reserved word as a name) stops the
                        // generation; the transaction then leaves the object as it was.
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"TIA Portal refused the source of '{name}' and changed nothing. {GenerationReason(pex)}", null, pex);
                    }

                    result.Replaced = true;

                    if (compileMode == "none")
                    {
                        result.Notes.Add("Not compiled: a syntax error in the code shows only at the compile ('plc_compile_block', 'plc_compile_software').");
                        AddNowInconsistent(softwarePath, objectPath, consistentBefore, result);

                        return result;
                    }

                    var compiled = CompileObject(softwarePath, objectPath, block != null);

                    result.ObjectCompiles = compiled.ErrorCount == 0;
                    result.State = compiled.State.ToString();
                    result.ErrorCount = compiled.ErrorCount;
                    result.WarningCount = compiled.WarningCount;
                    result.Messages = FlattenCompile(compiled);

                    if (compiled.ErrorCount > 0 && restoreWanted)
                    {
                        try
                        {
                            InTransaction($"Put the previous code of {kind} '{name}' back: the new code does not compile (plc_replace_source)", () => GenerateInPlace(softwarePath, objectPath, previous!, extension, block != null, result.Number));
                            CompileObject(softwarePath, objectPath, block != null);
                        }
                        catch (Exception ex)
                        {
                            var kept = Path.Combine(Path.GetTempPath(), "TiaMcpServer", $"previous_{name}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");

                            Directory.CreateDirectory(Path.GetDirectoryName(kept)!);
                            File.WriteAllText(kept, previous!);

                            throw new PortalException(PortalErrorCode.ImportFailed,
                                $"The new code of '{name}' does not compile, and putting the previous code back FAILED: {ErrorText.Describe(ex)}. " +
                                $"The object now holds the new code. The previous code is in '{kept}'. Compile errors: {DescribeMessages(result.Messages)}", null, ex);
                        }

                        result.Restored = true;

                        return result;
                    }

                    if (compileMode == "software")
                    {
                        var all = CompileSoftware(softwarePath);

                        if (all != null)
                        {
                            result.State = all.State.ToString();
                            result.ErrorCount = all.ErrorCount;
                            result.WarningCount = all.WarningCount;
                            result.Messages = FlattenCompile(all);
                        }
                    }

                    AddNowInconsistent(softwarePath, objectPath, consistentBefore, result);

                    return result;
                },
                ("softwarePath", softwarePath), ("objectPath", objectPath));
        }

        /// <summary>What TIA Portal said about the source, without the name of the temporary external source around it.</summary>
        private static string GenerationReason(Exception ex)
        {
            var text = ErrorText.Describe(ex);
            var cut = text.IndexOf("Generating block", StringComparison.OrdinalIgnoreCase);

            if (cut >= 0)
            {
                text = text.Substring(cut);
            }

            return System.Text.RegularExpressions.Regex.Replace(text, @"\s*:?\s*Import of '?MCP_Edit_[0-9a-f]+'? (failed|completed with errors)\.?", ".").Trim();
        }

        /// <summary>The source must declare the one object it is meant for, and nothing else.</summary>
        private static void CheckDeclares(string source, string kind, string name)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The source text is empty.");
            }

            var declared = SourceDeclarations.Read(source);
            var example = kind == "TYPE" ? $"TYPE \"{name}\" ... END_TYPE" : kind == "DB" ? $"DATA_BLOCK \"{name}\" ... END_DATA_BLOCK" : $"the declaration of {kind} \"{name}\"";

            if (declared.Count == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"The source declares no object. It has to be the whole external source of the object: {example}. " +
                    "Read the present one with 'plc_get_block_source' / 'plc_get_type_source' and format 'source'.");
            }

            if (declared.Count > 1)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"The source declares {declared.Count} objects ({string.Join(", ", declared.Select(d => $"{d.Kind} \"{d.Name}\""))}). " +
                    $"TIA Portal would generate all of them; this tool replaces one. Pass only {example}.");
            }

            if (!string.Equals(declared[0].Name, name, StringComparison.OrdinalIgnoreCase) || declared[0].Kind != kind)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"The source declares {declared[0].Kind} \"{declared[0].Name}\", but the object to replace is {kind} \"{name}\". " +
                    "TIA Portal would create or replace that other object and leave this one as it is. A rename is 'plc_rename_block' / 'plc_rename_type'.");
            }
        }

        /// <summary>The present code as an external source text (.scl, .db, .udt).</summary>
        private string ReadExternalSource(string softwarePath, string objectPath, bool isBlock)
        {
            using (var scope = new SourceScope())
            {
                var written = isBlock
                    ? ExportSourceBlock(softwarePath, objectPath, scope.Directory)
                    : ExportSourceType(softwarePath, objectPath, scope.Directory);

                var file = Directory.GetFiles(scope.Directory, "*.*", SearchOption.AllDirectories).FirstOrDefault()
                    ?? throw new PortalException(PortalErrorCode.ExportFailed, $"TIA Portal wrote no source file for '{written.Name}'.");

                return File.ReadAllText(file);
            }
        }

        /// <summary>
        /// Generates the source over the object and checks that it is still where it was. Runs in a transaction, so a
        /// refusal here leaves the project as it was.
        /// </summary>
        private bool GenerateInPlace(string softwarePath, string objectPath, string source, string extension, bool isBlock, int? number)
        {
            var directory = CreateTempExportDirectory();
            var sourceName = "MCP_Edit_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            // A source does not carry the memory reserve and the user-defined attributes of a block either (Portal.Lad.cs).
            var present = isBlock ? GetBlock(softwarePath, objectPath) : null;
            var settingsBefore = present != null ? ReadBlockSettings(present) : null;

            try
            {
                var file = Path.Combine(directory, sourceName + extension);

                File.WriteAllText(file, source);
                CreateExternalSourceFromFile(softwarePath, string.Empty, sourceName, file);

                try
                {
                    ImportSourceBlocks(softwarePath, sourceName);
                }
                finally
                {
                    try
                    {
                        DeleteExternalSource(softwarePath, sourceName);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Could not remove temporary external source {Source}", sourceName);
                    }
                }
            }
            finally
            {
                DeleteTempExportDirectory(directory);
            }

            if (isBlock)
            {
                var block = GetBlock(softwarePath, objectPath)
                    ?? throw new PortalException(PortalErrorCode.ImportFailed,
                        $"After the generation there is no block at '{objectPath}': TIA Portal put the result elsewhere. The change was rolled back.");

                if (settingsBefore != null)
                {
                    RequireBlockSettingsKept(block, settingsBefore);
                }

                if (number.HasValue && block.Number != number.Value)
                {
                    throw new PortalException(PortalErrorCode.ImportFailed,
                        $"The generation changed the block number from {number} to {block.Number}. The change was rolled back; keep the number in the source or leave it out.");
                }
            }
            else if (GetType(softwarePath, objectPath) == null)
            {
                throw new PortalException(PortalErrorCode.ImportFailed,
                    $"After the generation there is no PLC data type at '{objectPath}': TIA Portal put the result elsewhere. The change was rolled back.");
            }

            return true;
        }

        private CompilerResult CompileObject(string softwarePath, string objectPath, bool isBlock)
        {
            ICompilable? compilable;

            if (isBlock)
            {
                var block = GetBlock(softwarePath, objectPath)!;

                compilable = block.GetService<ICompilable>();
            }
            else
            {
                var type = GetType(softwarePath, objectPath)!;

                compilable = type.GetService<ICompilable>();
            }

            return (compilable ?? throw new PortalException(PortalErrorCode.NotSupported,
                $"'{objectPath}' offers no compile service. Pass compile='software' or 'none'.")).Compile();
        }

        private HashSet<string> ConsistentObjects(string softwarePath)
        {
            var consistent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var block in GetBlocks(softwarePath))
            {
                if (block.IsConsistent)
                {
                    consistent.Add("block:" + GetBlockPath(block));
                }
            }

            foreach (var type in GetTypes(softwarePath))
            {
                if (type.IsConsistent)
                {
                    consistent.Add("type:" + GetTypePath(type));
                }
            }

            return consistent;
        }

        /// <summary>What used the object and now waits for a compile: callers, instance DBs, blocks with a tag of the type.</summary>
        private void AddNowInconsistent(string softwarePath, string objectPath, HashSet<string> before, SourceEditResult result)
        {
            var after = ConsistentObjects(softwarePath);

            result.NowInconsistent = before
                .Where(key => !after.Contains(key))
                .Select(key => key.Substring(key.IndexOf(':') + 1))
                .Where(path => !string.Equals(path, objectPath, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<CompileMessageLine> FlattenCompile(CompilerResult compiled)
        {
            var lines = new List<CompileMessageLine>();

            foreach (var message in compiled.Messages)
            {
                Flatten(message, new List<string>(), lines);
            }

            // The closing "Compiling finished (errors: 1; warnings: 0)" has no place and repeats the counts.
            lines.RemoveAll(l => string.IsNullOrEmpty(l.Path));

            foreach (var line in lines)
            {
                // Below a block the compiler names the line of the code as a bare number.
                var cut = line.Path!.LastIndexOf('/');

                if (cut >= 0 && int.TryParse(line.Path.Substring(cut + 1), out var number))
                {
                    line.Path = line.Path.Substring(0, cut) + ", line " + number;
                }
            }

            return lines;
        }

        internal static string DescribeMessages(IEnumerable<CompileMessageLine> messages)
        {
            var errors = messages.Where(m => m.Severity == "Error").Select(m => $"{m.Path}: {m.Text}").ToList();

            return errors.Count == 0 ? "none listed" : string.Join(" | ", errors);
        }
    }
}
