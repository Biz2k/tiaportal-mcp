using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    // Tools for the networks of LAD blocks. The work is in Siemens/Portal.Lad.cs, the text handling in LadDocument.cs.
    public static partial class McpServer
    {
        [McpServerTool(Name = "plc_get_lad_networks", Title = "Get the networks of a LAD block", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Read the networks of a LAD block (FB, FC, OB written in ladder logic): for each its number, title and comment as plain text, and its code as text - 'RUNG wire#powerrail ... END_RUNG' with one instruction per line, e.g. Contact( #Start ), Coil( \"Motor_On\" ). A LAD block may hold SCL networks; they are listed with language SCL. This is what 'plc_manage_lad_networks' takes back. Blocks in other languages are refused: use 'plc_get_block_source'")]
        public static ResponseLadNetworks GetLadNetworks(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: root-relative path of the block, e.g. 'Valves/Valve_Control'. Use 'plc_resolve_object_path' if you only know the name")] string blockPath,
            [Description("network: number of the one network to return, 1-based; 0 (default) returns all")] int network = 0,
            [Description("withCode: false returns numbers, titles and comments only - the table of contents of a long block (default true)")] bool withCode = true,
            [Description("withDeclaration: true adds the declaration of the block - its attributes and interface with the local tags the code uses (default false)")] bool withDeclaration = false,
            [Description("maxChars: the most code to return; the networks beyond it come without code and the answer says so (default 40000)")] int maxChars = 40000)
        {
            try
            {
                var result = Portal.GetLadNetworks(softwarePath, blockPath, network, withCode, withDeclaration, maxChars);

                return new ResponseLadNetworks
                {
                    Name = result.Name,
                    Path = result.Path,
                    Kind = result.Kind,
                    Number = result.Number,
                    Count = result.Count,
                    Declaration = result.Declaration,
                    Networks = result.Networks,
                    Truncated = result.Truncated,
                    Notes = result.Notes,
                    Message = $"{result.Kind} '{result.Name}': {result.Count} network(s)" + (network > 0 ? $", network {network} returned" : string.Empty) +
                              (result.Truncated ? "; the code of some is left out (maxChars)" : string.Empty),
                    Meta = Ok(new JsonObject { ["count"] = result.Count, ["truncated"] = result.Truncated })
                };
            }
            catch (PortalException pex)
            {
                throw ToolError(pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"reading the networks of '{blockPath}'", ex);
            }
        }

        [McpServerTool(Name = "plc_get_lad_instructions", Title = "Get the LAD instructions and how they are written", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("The reference for writing LAD networks as text ('plc_create_lad_block', 'plc_manage_lad_networks'): how a network is written, and the instructions with their exact names and pins - contacts, coils, timers, counters, comparisons, math, moves, conversions, jumps, extended instructions (about 300, TIA Portal V21, S7-1500). Without a filter it returns the syntax and the names by category; with a filter the matching instructions in full: name, pins, the data type line it needs, notes. Instruction names cannot be guessed and differ from the help (SR is S_SR, SCALE_X is Scale), so look an instruction up before using it. Needs no connection to TIA Portal")]
        public static ResponseLadInstructions GetLadInstructions(
            [Description("filter: regular expression or text matched against name, other spellings and description, e.g. 'TON', 'timer', '^S_', 'compare'; empty returns the syntax and all names")] string filter = "",
            [Description("limit: the most instructions to return in full (default 40)")] int limit = 40)
        {
            try
            {
                var all = LadInstructions.All;
                var found = LadInstructions.Find(all, filter);

                if (string.IsNullOrWhiteSpace(filter))
                {
                    return new ResponseLadInstructions
                    {
                        Syntax = LadInstructions.Syntax,
                        Names = all.GroupBy(i => i.Category ?? "Basic").ToDictionary(g => g.Key, g => g.Select(i => i.Name).ToList()),
                        Count = 0,
                        Total = all.Count,
                        Message = $"{all.Count} LAD instruction(s) known. Pass a filter to get an instruction with its pins.",
                        Meta = Ok(new JsonObject { ["total"] = all.Count })
                    };
                }

                var items = found.Take(limit <= 0 ? int.MaxValue : limit).ToList();

                return new ResponseLadInstructions
                {
                    Items = items,
                    Count = items.Count,
                    Total = found.Count,
                    Message = found.Count == 0
                        ? $"No LAD instruction matches '{filter}'. Without a filter the answer lists all names."
                        : $"{items.Count} of {found.Count} LAD instruction(s) matching '{filter}'" + (items.Count < found.Count ? "; narrow the filter or raise 'limit' for the rest" : string.Empty),
                    Meta = Ok(new JsonObject { ["total"] = found.Count })
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure("reading the LAD instructions", ex);
            }
        }

        [WriteTool]
        [McpServerTool(Name = "plc_create_lad_block", Title = "Create a LAD block", Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true),
         Description("Create a NEW function block (FB) or function (FC) in LAD with its interface and its networks in one call, then compile it. A network is the text 'plc_get_lad_networks' shows: 'RUNG wire#powerrail ... END_RUNG' with one instruction per line, e.g. Contact( #Start ), I_Contact( #Stop ), Coil( #Run ); a parallel branch is a further RUNG that ends with 'END_RUNG wire#w1', where 'wire#w1' stands in the first rung at the place the branches join. Instruction names and pins cannot be guessed: 'plc_get_lad_instructions' has them and the syntax. If TIA Portal refuses the text nothing is created, and by default a block that does not compile is removed again. Existing blocks are changed with 'plc_manage_lad_networks'")]
        public static ResponseLadEdit CreateLadBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("name: name of the new block; must not exist in the PLC yet")] string name,
            [Description("networks: the networks of the block, in order")] List<LadNewNetwork> networks,
            [Description("kind: 'FC' (default) or 'FB'")] string kind = "FC",
            [Description("declaration: the interface of the block as text - VAR_INPUT ... END_VAR, VAR_OUTPUT, VAR_IN_OUT, VAR (FB only), VAR_TEMP - one tag per line, e.g. 'Start : Bool;   // comment of the tag'. Empty for a block without interface")] string declaration = "",
            [Description("groupPath: root-relative block group that receives the block; empty uses the Program blocks root")] string groupPath = "",
            [Description("title: title of the block as plain text (optional)")] string title = "",
            [Description("returnType: data type an FC returns (default Void)")] string returnType = "Void",
            [Description("number: block number; 0 (default) lets TIA Portal choose")] int number = 0,
            [Description("compile: 'object' (default) compiles the block, 'software' then the whole PLC as well, 'none' compiles nothing")] string compile = "object",
            [Description("onCompileError: 'delete' (default) removes the block again when it does not compile, 'keep' leaves it for corrections")] string onCompileError = "delete")
        {
            return GuardedNoTransaction(nameof(CreateLadBlock), () =>
            {
                var result = Portal.CreateLadBlock(softwarePath, groupPath, name, kind, returnType, number, declaration, title, networks, compile, onCompileError);

                if (result.Restored)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams,
                        $"{result.Kind} '{result.Name}' does not compile ({result.ErrorCount} error(s)); it was removed again and nothing is left of it. " +
                        $"Errors: {Portal.DescribeMessages(result.Messages)}");
                }

                var errors = result.ErrorCount ?? 0;

                return new ResponseLadEdit
                {
                    Name = result.Name,
                    Path = result.Path,
                    Kind = result.Kind,
                    Number = result.Number,
                    State = result.State,
                    ErrorCount = result.ErrorCount,
                    WarningCount = result.WarningCount,
                    Messages = result.Messages.Where(m => m.Severity == "Error" || m.Severity == "Warning").ToList(),
                    NowInconsistent = result.NowInconsistent,
                    Notes = result.Notes,
                    Networks = result.Networks.Select(n => new LadNetworkInfo { Number = n.Number, Language = n.Language, Title = n.Title }).ToList(),
                    Message = $"{result.Kind} '{result.Name}' created at '{result.Path}' as number {result.Number} with {result.Networks.Count} network(s)" +
                              (result.State == null ? "; not compiled." : $"; compile ({result.Compile}): {result.State}, {errors} error(s), {result.WarningCount} warning(s).") +
                              (errors > 0 ? " The block is in place with its errors (onCompileError='keep')." : string.Empty) +
                              " " + SaveHint,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = errors == 0,
                        ["pendingSave"] = true
                    }
                };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "plc_manage_lad_networks", Title = "Change the networks of a LAD block", Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true),
         Description("Replace, insert, delete or move networks of an EXISTING LAD block, or set their titles and comments, several at once; then compile the block. Every number in a call means the block as it is BEFORE the call, so one action never shifts the target of another. All or nothing: a call that TIA Portal refuses changes nothing and the error names the instruction and the line; if the new code does not compile (a tag that does not exist, a wrong operand type), the previous block is put back by default. The block keeps its number, its place and its instance DBs; the interface is not changed here - for that pass the whole document to 'plc_replace_source'. Instruction names and pins cannot be guessed: look them up with 'plc_get_lad_instructions', or follow an existing network from 'plc_get_lad_networks'. Fields of an action: action, network, after, code, language, title, comment")]
        public static ResponseLadEdit ManageLadNetworks(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: root-relative path of the LAD block, e.g. 'Valves/Valve_Control'")] string blockPath,
            [Description("actions: the changes to make; numbers mean the block as it is before the call")] List<LadNetworkAction> actions,
            [Description("compile: 'object' (default) compiles the block itself, 'software' then compiles the whole PLC as well, 'none' compiles nothing")] string compile = "object",
            [Description("onCompileError: 'restore' (default) puts the previous block back when the new code does not compile, 'keep' leaves the new code in place")] string onCompileError = "restore")
        {
            return GuardedNoTransaction(nameof(ManageLadNetworks), () =>
            {
                var result = Portal.ManageLadNetworks(softwarePath, blockPath, actions, compile, onCompileError);

                if (result.Restored)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams,
                        $"The new networks of {result.Kind} '{result.Name}' do not compile ({result.ErrorCount} error(s)); the previous block was put back and compiled. " +
                        $"Nothing else changed. Errors: {Portal.DescribeMessages(result.Messages)}");
                }

                var errors = result.ErrorCount ?? 0;

                return new ResponseLadEdit
                {
                    Name = result.Name,
                    Path = result.Path,
                    Kind = result.Kind,
                    Number = result.Number,
                    State = result.State,
                    ErrorCount = result.ErrorCount,
                    WarningCount = result.WarningCount,
                    Messages = result.Messages.Where(m => m.Severity == "Error" || m.Severity == "Warning").ToList(),
                    NowInconsistent = result.NowInconsistent,
                    Notes = result.Notes,
                    Networks = result.Networks.Select(n => new LadNetworkInfo { Number = n.Number, Language = n.Language, Title = n.Title }).ToList(),
                    Message = $"{actions.Count} action(s) applied to {result.Kind} '{result.Name}', which now has {result.Networks.Count} network(s)" +
                              (result.State == null ? "; not compiled." : $"; compile ({result.Compile}): {result.State}, {errors} error(s), {result.WarningCount} warning(s).") +
                              (errors > 0 && !result.ObjectCompiles ? " The new code is in place with its errors (onCompileError='keep')." : string.Empty) +
                              (errors > 0 && result.ObjectCompiles ? " The block itself compiles; the errors are in objects that use it." : string.Empty) +
                              (result.NowInconsistent.Count > 0 ? $" {result.NowInconsistent.Count} object(s) that use it now wait for a compile." : string.Empty) +
                              " " + SaveHint,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = errors == 0,
                        ["pendingSave"] = true
                    }
                };
            });
        }
    }
}
