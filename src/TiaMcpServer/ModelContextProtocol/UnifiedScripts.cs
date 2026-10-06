using System.Collections.Generic;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One change to a script module of a WinCC Unified HMI.</summary>
    public class UnifiedScriptAction
    {
        [Description("'create' (fails if the module exists), 'update' (fails if it does not) or 'upsert'. Either way the module is written as a whole: what it held before is replaced. 'delete' is listed so that the error can explain: Openness offers no way to delete a script module")]
        public string? Action { get; set; }

        [Description("Name of the script module: letters, digits, underscore and spaces, not starting with a digit")]
        public string? ModuleName { get; set; }

        [Description("Global definitions: constants, variables and helper functions that sit above the functions of the module, as JavaScript source. Empty for none")]
        public string? GlobalDefinitions { get; set; }

        [Description("The functions of the module as JavaScript source, e.g. \"export function Add(a, b) {\\n return a + b;\\n}\". Only 'export function' definitions are callable from screens. Default parameter values (b = 5) are not supported by TIA Portal and are refused; comments between functions may be moved by TIA Portal")]
        public string? Functions { get; set; }
    }

    public class ResponseUnifiedScripts : ResponseMessage
    {
        public List<UnifiedScriptModuleInfo>? Items { get; set; }
    }

    public class UnifiedScriptModuleInfo
    {
        public string? Name { get; set; }

        /// <summary>Source above the functions: constants, variables, helpers.</summary>
        public string? GlobalDefinitions { get; set; }

        /// <summary>Source of the functions.</summary>
        public string? Functions { get; set; }

        /// <summary>The functions that can be called from outside, with their parameters, e.g. "Add(a, b)".</summary>
        public List<string> ExportedFunctions { get; set; } = new List<string>();
    }
}
