using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Siemens
{
    // Reading and changing the networks of a LAD block.
    //
    // Callers: the tools plc_get_lad_networks and plc_manage_lad_networks in McpServer.Lad.cs, and plc_replace_source
    // for a LAD block. File I/O: temporary document files under the OS temp directory; the previous document is kept
    // there only when putting it back failed.
    //
    // Openness V21 has no objects for networks or LAD elements; a block is reached as a whole, as a SIMATIC SD document
    // (ExportAsDocuments / ImportFromDocuments) or as SimaticML. The document is text - RUNG, wire#, Contact( ... ) - and
    // LadDocument takes it apart. Found on V21 (2026-10-07, probes on temporary blocks, docs/handoff/lad.md):
    //   - The import over an existing block works only through the block composition of the group the block is in;
    //     through the root it is refused ("an object with the name ... already exists in the plc").
    //   - Without S7_BlockNumber in the document the block gets a new number (900 became 1). An external SCL source
    //     keeps the number; a document does not. The number is set back here.
    //   - A document TIA Portal cannot read is refused as a whole and the block stays as it was; the messages name the
    //     instruction and the line. A tag that does not exist is NOT refused - only the compile reports it. So, as for
    //     SCL: import (in a transaction), compile, and on errors import the previous document again.
    //   - The instance DBs stay and wait for a compile.
    //   - The import drops what the document cannot carry: the start values of an embedded technology object
    //     (PID_Compact: PhysicalUnit, "Retain".CtrlParams). The declaration is therefore read back and compared, and a
    //     loss rolls the change back.
    //   - The import runs inside a transaction and is rolled back with it; a compile is not permitted there.
    public partial class Portal
    {
        private const string LadWriteHelp =
            "Take a network of 'plc_get_lad_networks' as the pattern: instruction names cannot be guessed (Contact, I_Contact, Coil, S_Coil, R_Coil, P_Trig, Move, " +
            "EQ_Contact ...), a local tag is #Name, a global one \"Name\"; a name with other characters than ASCII letters, digits and '_' goes in quotes, also after #.";

        public LadNetworksResult GetLadNetworks(string softwarePath, string blockPath, int network, bool withCode, bool withDeclaration, int maxChars)
        {
            return Operation.Run(_logger, nameof(GetLadNetworks), PortalErrorCode.ExportFailed,
                () =>
                {
                    var block = RequireLadBlock(softwarePath, blockPath);
                    var (document, _) = ReadLadDocument(block);

                    if (network < 0 || network > document.Networks.Count)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Block '{block.Name}' has {document.Networks.Count} network(s); network {network} does not exist. 0 returns all.");
                    }

                    var result = new LadNetworksResult
                    {
                        Name = block.Name,
                        Path = blockPath,
                        Kind = SourceDeclarations.KindOfBlockClass(block.GetType().Name),
                        Number = block.Number,
                        Count = document.Networks.Count,
                        Declaration = withDeclaration ? document.Head.Replace("\n", "\r\n") : null
                    };

                    var cultures = ProjectCultures();
                    var budget = maxChars <= 0 ? int.MaxValue : maxChars;

                    foreach (var item in document.Networks.Where(n => network == 0 || n.Original == network))
                    {
                        var info = DescribeNetwork(document, item, item.Original, cultures);

                        if (withCode)
                        {
                            if (item.Code.Length <= budget)
                            {
                                info.Code = item.Code.Replace("\n", "\r\n");
                                budget -= item.Code.Length;
                            }
                            else
                            {
                                result.Truncated = true;
                            }
                        }

                        result.Networks.Add(info);
                    }

                    if (result.Truncated)
                    {
                        result.Notes.Add($"The code of some networks is left out: together it is longer than maxChars ({maxChars}). Ask for them one at a time with 'network'.");
                    }

                    if (!block.IsConsistent)
                    {
                        result.Notes.Add("The block is inconsistent: it waits for a compile.");
                    }

                    return result;
                },
                ("softwarePath", softwarePath), ("blockPath", blockPath), ("network", network));
        }

        public LadEditResult ManageLadNetworks(string softwarePath, string blockPath, List<LadNetworkAction> actions, string compile, string onCompileError)
        {
            return Operation.Run(_logger, nameof(ManageLadNetworks), PortalErrorCode.ImportFailed,
                () =>
                {
                    var (compileMode, restoreWanted) = ReadCompileOptions(compile, onCompileError);
                    var block = RequireLadBlock(softwarePath, blockPath);
                    var (previous, baseName) = ReadLadDocument(block);
                    var (next, _) = ReadLadDocument(block);
                    var problems = LadNetworkActions.Check(next, actions);

                    if (problems.Count > 0)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Nothing was changed: {problems.Count} of the {actions?.Count ?? 0} action(s) cannot be applied. {string.Join(" ", problems)}");
                    }

                    LadNetworkActions.Apply(next, actions, ProjectCultures());

                    return WriteLadDocument(softwarePath, blockPath, block, previous, next, baseName, compileMode, restoreWanted, "plc_manage_lad_networks",
                        $"Change {actions.Count} network(s) of {block.Name}");
                },
                ("softwarePath", softwarePath), ("blockPath", blockPath));
        }

        /// <summary>plc_replace_source for a LAD block: the whole document, with the texts of its titles when it refers to any.</summary>
        private LadEditResult ReplaceLadDocument(string softwarePath, string blockPath, PlcBlock block, string source, string compileMode, bool restoreWanted)
        {
            var (declaration, resources) = SplitDocumentText(source);
            var kind = SourceDeclarations.KindOfBlockClass(block.GetType().Name);

            CheckDeclares(declaration, kind, block.Name);

            LadDocument next;

            try
            {
                next = LadDocument.Parse(declaration, resources);
            }
            catch (FormatException ex)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"The text is not the document of a LAD block: {ex.Message} Read the present one with 'plc_get_block_source' and format 'document'; " +
                    "to change single networks use 'plc_manage_lad_networks'.");
            }

            if (next.Head.IndexOf("S7_PreferredLanguage", StringComparison.Ordinal) < 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"The document does not start with the attributes of the block. TIA Portal needs them to know the language: begin with {{ S7_Optimized := \"TRUE\"; S7_PreferredLanguage := \"LAD\"; S7_Version := \"0.1\" }} " +
                    $"before the line that declares {kind} \"{block.Name}\", as 'plc_get_block_source' (format 'document') returns it.");
            }

            var known = new HashSet<string>(next.Texts.Select(t => t.Id), StringComparer.Ordinal);
            var unknown = Regex.Matches(declaration, @":=\s*""(MLC_\w+)""").Cast<Match>().Select(m => m.Groups[1].Value).Where(id => !known.Contains(id)).Distinct().ToList();

            if (unknown.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"The document refers to the text(s) {string.Join(", ", unknown)}, but the call carries no such text. A title or comment is an id in the document and a text in its " +
                    "resource part: pass both parts as 'plc_get_block_source' (format 'document') returns them, '===== Name.s7dcl =====' and '===== Name.s7res =====', " +
                    "or set titles with 'plc_manage_lad_networks' (action 'set_title'), which takes plain text.");
            }

            var (previous, baseName) = ReadLadDocument(block);

            return WriteLadDocument(softwarePath, blockPath, block, previous, next, baseName, compileMode, restoreWanted, "plc_replace_source", $"Replace the code of {kind} '{block.Name}'");
        }

        private LadEditResult WriteLadDocument(string softwarePath, string blockPath, PlcBlock block, LadDocument previous, LadDocument next, string baseName,
            string compileMode, bool restoreWanted, string tool, string activity)
        {
            var name = block.Name;
            var number = block.Number;
            var result = new LadEditResult
            {
                Name = name,
                Path = blockPath,
                Kind = SourceDeclarations.KindOfBlockClass(block.GetType().Name),
                Number = number,
                Compile = compileMode
            };

            var consistentBefore = ConsistentObjects(softwarePath);

            InTransaction($"{activity} ({tool})", () => ImportLadDocument(softwarePath, blockPath, baseName, next, number, result.Notes));

            result.Replaced = true;

            var cultures = ProjectCultures();

            result.Networks = next.Networks.Select((n, i) => DescribeNetwork(next, n, i + 1, cultures)).ToList();

            if (compileMode == "none")
            {
                result.Notes.Add("Not compiled: a tag that does not exist or a wrong operand type shows only at the compile ('plc_compile_block', 'plc_compile_software').");
                AddNowInconsistent(softwarePath, blockPath, consistentBefore, result);

                return result;
            }

            var compiled = CompileObject(softwarePath, blockPath, true);

            result.ObjectCompiles = compiled.ErrorCount == 0;
            result.State = compiled.State.ToString();
            result.ErrorCount = compiled.ErrorCount;
            result.WarningCount = compiled.WarningCount;
            result.Messages = FlattenCompile(compiled);

            if (compiled.ErrorCount > 0 && restoreWanted)
            {
                try
                {
                    InTransaction($"Put the previous code of '{name}' back: the new code does not compile ({tool})",
                        () => ImportLadDocument(softwarePath, blockPath, baseName, previous, number, null));
                    CompileObject(softwarePath, blockPath, true);
                }
                catch (Exception ex)
                {
                    var kept = Path.Combine(Path.GetTempPath(), "TiaMcpServer", $"previous_{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}");

                    WriteLadFiles(kept, baseName, previous);

                    throw new PortalException(PortalErrorCode.ImportFailed,
                        $"The new code of '{name}' does not compile, and putting the previous code back FAILED: {ErrorText.Describe(ex)}. " +
                        $"The block now holds the new code. The previous document is in '{kept}'. Compile errors: {DescribeMessages(result.Messages)}", null, ex);
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

            AddNowInconsistent(softwarePath, blockPath, consistentBefore, result);

            return result;
        }

        /// <summary>
        /// Imports the document over the block and checks what came of it. Runs in a transaction: a refusal of TIA Portal
        /// or a loss found here leaves the block as it was.
        /// </summary>
        /// <param name="notes">Where remarks go; null when the previous document is put back and nothing is compared.</param>
        private bool ImportLadDocument(string softwarePath, string blockPath, string baseName, LadDocument document, int number, List<string>? notes)
        {
            var block = GetBlock(softwarePath, blockPath)
                ?? throw new PortalException(PortalErrorCode.NotFound, $"No block at '{blockPath}'.");

            // Only the composition of the block's own group replaces the block.
            var group = block.Parent as PlcBlockGroup
                ?? throw new PortalException(PortalErrorCode.NotSupported, $"Block '{block.Name}' is not in a block group of the PLC; its networks cannot be written.");

            using (var scope = new SourceScope())
            {
                WriteLadFiles(scope.Directory, baseName, document);

                var imported = group.Blocks.ImportFromDocuments(new DirectoryInfo(scope.Directory), baseName, ImportDocumentOptions.Override);

                if (!string.Equals(imported.State.ToString(), "Success", StringComparison.OrdinalIgnoreCase))
                {
                    var said = imported.Messages
                        .Select(m => Regex.Replace(m.Message ?? string.Empty, @"\s+", " ").Trim())
                        .Where(m => m.Length > 0 && !m.StartsWith("Importing from file", StringComparison.OrdinalIgnoreCase) && m.IndexOf("please check the s7dcl file", StringComparison.OrdinalIgnoreCase) < 0)
                        .ToList();

                    throw new PortalException(PortalErrorCode.InvalidParams,
                        $"TIA Portal refused the document and changed nothing ({imported.State}). {string.Join(" ", said)} {LadWriteHelp}");
                }
            }

            block = GetBlock(softwarePath, blockPath)
                ?? throw new PortalException(PortalErrorCode.ImportFailed,
                    $"After the import there is no block at '{blockPath}': TIA Portal put the result elsewhere. The change was rolled back.");

            if (block.Number != number)
            {
                block.AutoNumber = false;
                block.Number = number;
            }

            if (notes == null)
            {
                return true;
            }

            var (after, _) = ReadLadDocument(block);
            var missing = LadDocument.LinesMissing(document.Head, after.Head);

            if (missing.Count == 0)
            {
                return true;
            }

            if (LadDocument.MeaningfulLines(after.Head).Count < LadDocument.MeaningfulLines(document.Head).Count)
            {
                throw new PortalException(PortalErrorCode.NotSupported,
                    $"TIA Portal dropped {missing.Count} line(s) of the declaration of '{block.Name}' on the import: {string.Join(" | ", missing.Take(8))}. " +
                    "The document does not carry everything - the start values of an embedded technology object (PID_Compact and the like) are lost this way. " +
                    "Nothing was changed. Change this block in TIA Portal itself.");
            }

            notes.Add($"TIA Portal wrote {missing.Count} line(s) of the declaration in its own way (the same value in another notation, e.g. T#5000ms as T#5S): {string.Join(" | ", missing.Take(5))}.");

            return true;
        }

        private static void WriteLadFiles(string directory, string baseName, LadDocument document)
        {
            Directory.CreateDirectory(directory);
            SourceFileEncoding.Write(Path.Combine(directory, baseName + ".s7dcl"), document.RenderDeclaration());

            var resources = document.RenderResources();

            if (resources != null)
            {
                SourceFileEncoding.Write(Path.Combine(directory, baseName + ".s7res"), resources);
            }
        }

        /// <summary>The document of the block as TIA Portal writes it now, and the file name TIA Portal chose for it.</summary>
        private (LadDocument Document, string BaseName) ReadLadDocument(PlcBlock block)
        {
            using (var scope = new SourceScope())
            {
                block.ExportAsDocuments(new DirectoryInfo(scope.Directory), block.Name);

                var declaration = Directory.GetFiles(scope.Directory, "*.s7dcl", SearchOption.AllDirectories).FirstOrDefault()
                    ?? throw new PortalException(PortalErrorCode.ExportFailed, $"TIA Portal wrote no document for block '{block.Name}'.");
                var resources = Path.ChangeExtension(declaration, ".s7res");

                try
                {
                    return (LadDocument.Parse(
                        SourceFileEncoding.Decode(File.ReadAllBytes(declaration)),
                        File.Exists(resources) ? SourceFileEncoding.Decode(File.ReadAllBytes(resources)) : null),
                        Path.GetFileNameWithoutExtension(declaration));
                }
                catch (FormatException ex)
                {
                    throw new PortalException(PortalErrorCode.NotSupported, $"The document of block '{block.Name}' could not be read: {ex.Message}");
                }
            }
        }

        private PlcBlock RequireLadBlock(string softwarePath, string blockPath)
        {
            var block = GetBlock(softwarePath, blockPath)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"No block at '{blockPath}'. 'plc_resolve_object_path' finds a path by name, 'plc_get_blocks' lists the blocks with their language.");

            if (block.ProgrammingLanguage != ProgrammingLanguage.LAD)
            {
                throw new PortalException(PortalErrorCode.NotSupported,
                    $"Block '{block.Name}' is written in {block.ProgrammingLanguage}, not in LAD. The LAD tools work on LAD blocks only" +
                    (block.ProgrammingLanguage == ProgrammingLanguage.SCL || block.ProgrammingLanguage == ProgrammingLanguage.DB
                        ? "; for this one use 'plc_get_block_source' and 'plc_replace_source'."
                        : "; 'plc_get_block_source' reads this one, the server cannot change its code."));
            }

            if (block.IsKnowHowProtected)
            {
                throw new PortalException(PortalErrorCode.NotSupported, $"Block '{block.Name}' is know-how protected; its networks cannot be read or changed.");
            }

            return block;
        }

        private static LadNetworkInfo DescribeNetwork(LadDocument document, LadNetwork network, int number, IList<string> cultures)
        {
            return new LadNetworkInfo
            {
                Number = number,
                Language = network.Language,
                Title = document.TextOf(network.TitleId, cultures),
                Comment = document.TextOf(network.CommentId, cultures)?.Replace("\n", "\r\n"),
                Titles = document.TextsOf(network.TitleId)
            };
        }

        /// <summary>The languages of the project, the editing language first: a text is shown in the first that has one and written in all.</summary>
        private List<string> ProjectCultures()
        {
            var cultures = new List<string>();

            try
            {
                var settings = _project!.LanguageSettings;

                foreach (var language in new[] { settings.EditingLanguage, settings.ReferenceLanguage }.Concat(settings.ActiveLanguages))
                {
                    var name = language?.Culture?.Name;

                    if (!string.IsNullOrEmpty(name) && !cultures.Contains(name!, StringComparer.OrdinalIgnoreCase))
                    {
                        cultures.Add(name!);
                    }
                }
            }
            catch (Exception)
            {
                // A session without language settings: the fallback below.
            }

            if (cultures.Count == 0)
            {
                cultures.Add("en-US");
            }

            return cultures;
        }

        /// <summary>The two parts of a document as 'plc_get_block_source' joins them: "===== Name.s7dcl =====" and "===== Name.s7res =====".</summary>
        private static (string Declaration, string? Resources) SplitDocumentText(string source)
        {
            var text = (source ?? string.Empty).Replace("\r\n", "\n");
            var marks = Regex.Matches(text, @"^=====\s*(.+?)\s*=====[ \t]*$", RegexOptions.Multiline);

            if (marks.Count == 0)
            {
                return (text, null);
            }

            string? declaration = null;
            string? resources = null;

            for (var i = 0; i < marks.Count; i++)
            {
                var from = marks[i].Index + marks[i].Length;
                var to = i + 1 < marks.Count ? marks[i + 1].Index : text.Length;
                var part = text.Substring(from, to - from).Trim('\n');

                if (marks[i].Groups[1].Value.EndsWith(".s7res", StringComparison.OrdinalIgnoreCase))
                {
                    resources = part;
                }
                else
                {
                    declaration = part;
                }
            }

            return (declaration ?? string.Empty, resources);
        }

        private static (string CompileMode, bool RestoreWanted) ReadCompileOptions(string compile, string onCompileError)
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

            return (compileMode, onError == "restore" && compileMode != "none");
        }
    }
}
