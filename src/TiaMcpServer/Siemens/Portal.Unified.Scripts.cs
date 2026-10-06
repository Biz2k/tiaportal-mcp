using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.HmiUnified.Scripts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Siemens
{
    // WinCC Unified: script modules (HmiSoftware.Scripts) - read and write.
    //
    // Callers: the tools unified_get_scripts and unified_manage_scripts in McpServer.Unified.cs.
    // Writes its files into a temporary folder of its own per operation, removed afterwards.
    //
    // Openness has no object model for the content of a module: HmiScriptModuleComposition only
    // exports and imports, HmiScriptModule only exports. A module is two files, as found on
    // TIA Portal V21 (2026-10-06) on modules made by hand:
    //   <name>.hmi.yml  "#Version: 2.0", "ScriptModules:", "  <name>:", "    ScriptFile: <name>.hmi.js"
    //   <name>.hmi.js   UTF-8 without BOM, CRLF. The global definitions come first, then a comment
    //                   block with the line "***End of Global definition area***", then the functions.
    //
    // What Import does (V21, 2026-10-06):
    //   - Import(folder, name) takes the file <name>.hmi.yml; it returns false when there is none.
    //   - A module of that name is replaced as a whole, a new one is created. Nothing is merged.
    //   - There is no way to delete or rename a module: HmiScriptModule has no Delete.
    //   - The code is parsed and written out again, so spacing changes. What it cannot parse it
    //     does not reject: a default parameter 'f(a, b = 5)' comes back as 'f(a, b___5)', and a
    //     syntax error returns true with the function body gone ('H(__return____) {}'). A comment
    //     between functions may be moved above the marker. Comments inside functions, arrow
    //     functions, let/const, loops, quotes and backslashes survive.
    //   - Without a marker in the file the whole code counts as functions; TIA adds the marker.
    // That is why every write is read back and compared with comments and spacing left out.
    public partial class Portal
    {
        #region read

        public List<UnifiedScriptModuleInfo> GetUnifiedScripts(string softwarePath, string moduleName = "")
        {
            return Operation.Run(_logger, nameof(GetUnifiedScripts), PortalErrorCode.InvalidState,
                () =>
                {
                    var software = RequireUnifiedSoftware(softwarePath);
                    var wanted = moduleName?.Trim() ?? string.Empty;
                    var modules = new List<UnifiedScriptModuleInfo>();

                    foreach (var module in software.Scripts)
                    {
                        if (wanted.Length > 0 && !string.Equals(module.Name, wanted, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        modules.Add(UnifiedScriptCode.Describe(module.Name, ExportScriptModule(module)));
                    }

                    if (wanted.Length > 0 && modules.Count == 0)
                    {
                        throw new PortalException(PortalErrorCode.NotFound,
                            $"Script module '{wanted}' not found. Existing: {DescribeScriptModules(software)}.");
                    }

                    return modules;
                },
                ("softwarePath", softwarePath), ("moduleName", moduleName ?? string.Empty));
        }

        private static string DescribeScriptModules(HmiSoftware software)
        {
            var names = software.Scripts.Select(m => m.Name).ToList();

            return names.Count == 0 ? "none" : string.Join(", ", names);
        }

        private static string ExportScriptModule(HmiScriptModule module)
        {
            string? code = null;

            WithTemporaryFolder("scripts", folder =>
            {
                module.Export(folder);

                var file = folder.GetFiles("*.hmi.js").FirstOrDefault()
                    ?? throw new PortalException(PortalErrorCode.ExportFailed,
                        $"TIA Portal exported script module '{module.Name}' without a .hmi.js file. It wrote: {string.Join(", ", folder.GetFiles().Select(f => f.Name))}.");

                code = File.ReadAllText(file.FullName, Encoding.UTF8);
            });

            return code ?? string.Empty;
        }

        #endregion

        #region write

        public List<UnifiedActionResult> ManageUnifiedScripts(string softwarePath, IList<UnifiedScriptAction>? actions)
        {
            return RunUnifiedBatch(nameof(ManageUnifiedScripts), softwarePath, actions,
                "{ \"action\": \"upsert\", \"moduleName\": \"Helpers\", \"globalDefinitions\": \"const Limit = 10;\", \"functions\": \"export function Clamp(v) { return v > Limit ? Limit : v; }\" }",
                a => a.ModuleName,
                (software, action, verb, result) =>
                {
                    var name = RequireName(action.ModuleName, "moduleName");

                    if (verb == "delete")
                    {
                        throw new PortalException(PortalErrorCode.NotSupported,
                            "Openness offers no way to delete a script module (HmiScriptModule has no Delete). Delete it in the TIA Portal editor, or empty it with 'update' and an empty 'functions'.");
                    }

                    if (verb != "create" && verb != "update" && verb != "upsert")
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Unknown action '{action.Action}'. Use 'create', 'update' or 'upsert'.");
                    }

                    if (!UnifiedScriptCode.ModuleNamePattern.IsMatch(name))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"'{name}' is not a usable module name: use letters, digits, underscore and spaces, and do not start with a digit.");
                    }

                    var existing = software.Scripts.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

                    if (verb == "create" && existing != null)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, $"Script module '{name}' already exists. Use 'update' or 'upsert'.");
                    }

                    if (verb == "update" && existing == null)
                    {
                        throw new PortalException(PortalErrorCode.NotFound,
                            $"Script module '{name}' not found. Existing: {DescribeScriptModules(software)}. Use 'upsert' to create it.");
                    }

                    // The name TIA Portal knows the module by, in case only the case differs.
                    name = existing?.Name ?? name;

                    CheckScriptSource(action.GlobalDefinitions, "globalDefinitions");
                    CheckScriptSource(action.Functions, "functions");

                    var code = UnifiedScriptCode.Build(action.GlobalDefinitions, action.Functions);

                    WithTemporaryFolder("scripts", folder =>
                    {
                        File.WriteAllText(Path.Combine(folder.FullName, name + ".hmi.yml"),
                            $"#Version: 2.0\r\n\r\nScriptModules:\r\n  {name}:\r\n    ScriptFile: {name}.hmi.js\r\n", new UTF8Encoding(false));
                        File.WriteAllText(Path.Combine(folder.FullName, name + ".hmi.js"), code, new UTF8Encoding(false));

                        if (!software.Scripts.Import(folder, name))
                        {
                            throw new PortalException(PortalErrorCode.ImportFailed, $"TIA Portal did not import script module '{name}' and gave no reason.");
                        }
                    });

                    // Import accepts what it cannot parse and mangles it, so the module is read back.
                    var written = software.Scripts.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
                        ?? throw new PortalException(PortalErrorCode.ImportFailed, $"Script module '{name}' is not there after the import.");

                    var (wantedGlobal, wantedFunctions) = UnifiedScriptCode.Split(code);
                    var (storedGlobal, storedFunctions) = UnifiedScriptCode.Split(ExportScriptModule(written));

                    var difference = UnifiedScriptCode.DescribeDifference(wantedGlobal, wantedFunctions, storedGlobal, storedFunctions);

                    if (difference != null)
                    {
                        throw new PortalException(PortalErrorCode.ImportFailed,
                            $"TIA Portal imported script module '{name}' differently from what was asked: {difference}");
                    }

                    result.Applied.Add(existing == null ? "created" : "replaced");

                    var exported = UnifiedScriptCode.ExportedFunctionPattern.Matches(UnifiedScriptCode.StripComments(storedFunctions)).Count;

                    result.Notes.Add(existing == null
                        ? $"Created with {exported} exported function(s)."
                        : $"Replaced as a whole: what the module held before is gone. {exported} exported function(s) now.");

                    // TIA Portal stores code it cannot run exactly as written (checked 2026-10-06: "return a *;" came back
                    // unchanged), and a server cannot parse JavaScript. The compile of the HMI is the check.
                    result.Notes.Add($"The syntax of the code is not checked: it was stored as written. Run 'unified_compile' (pathFilter \"Scripts/{name}\") to have TIA Portal report syntax errors.");
                });
        }

        private static void CheckScriptSource(string? source, string parameter)
        {
            if (source != null && source.IndexOf(UnifiedScriptCode.MarkerText, StringComparison.Ordinal) >= 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"{parameter} contains the marker comment TIA Portal uses to separate global definitions from functions; leave it out, it is added.");
            }
        }

        #endregion

        /// <summary>Runs an action with a temporary folder of its own and removes the folder afterwards.</summary>
        private static void WithTemporaryFolder(string prefix, Action<DirectoryInfo> action)
        {
            var folder = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "TiaMcpServer", prefix + "-" + Guid.NewGuid().ToString("N")));

            folder.Create();

            try
            {
                action(folder);
            }
            finally
            {
                try
                {
                    folder.Delete(true);
                }
                catch (IOException)
                {
                    // Left for the system to clean up; the next call uses a folder of its own.
                }
            }
        }
    }
}
