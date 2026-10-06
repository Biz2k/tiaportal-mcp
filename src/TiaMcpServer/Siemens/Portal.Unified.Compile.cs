using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.HW;
using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Siemens
{
    // WinCC Unified: compiling the HMI as the check of what Openness and Validate() do not look into.
    //
    // Callers: the tool unified_compile in McpServer.Unified.cs.
    //
    // Found on TIA Portal V21 (2026-10-06):
    //   - Only the DEVICE of an HMI offers ICompilable. HmiSoftware, a screen, a screen item, a script module, a tag
    //     have no compile service (most have no GetService at all), so one script or one screen cannot be compiled
    //     on its own, as the editor of TIA Portal can.
    //   - The compile is incremental: after a full one, a compile that follows a change of one screen took about a
    //     second on a panel with 14 screens and 2500 tags.
    //   - It is refused inside a transaction ("The operation is not permitted within a transaction"). So it cannot
    //     judge a write before the commit: it reports on what is already in the project.
    //   - It does not save the project; it marks it as modified.
    //   - The result is a tree: device > "Screens" > screen > item > message. It names a syntax error of a script
    //     dynamization and of an event script ("SyntaxError: Unexpected token ';' in line 1, in column 55") and an
    //     invalid formula of a tag dynamization ("Invalid formula: ERR208 - Premature end of expression"). Seen NOT to
    //     be reported: a broken formula of an expression dynamization, a call of a function that does not exist
    //     (that is a runtime error of JavaScript), a senseless output format of an I/O field.
    public partial class Portal
    {
        public UnifiedCompileResult CompileUnified(string softwarePath, string? pathFilter, bool errorsOnly)
        {
            return Operation.Run(_logger, nameof(CompileUnified), PortalErrorCode.InvalidState,
                () =>
                {
                    RequireUnifiedSoftware(softwarePath);

                    IEngineeringObject? current = RequireHmiContainer(softwarePath).Parent;

                    while (current != null && !(current is Device))
                    {
                        current = current.Parent;
                    }

                    var device = current as Device
                        ?? throw new PortalException(PortalErrorCode.InvalidState, $"The device of '{softwarePath}' was not found above its software.");

                    var compilable = device.GetService<ICompilable>()
                        ?? throw new PortalException(PortalErrorCode.NotSupported, $"The device '{device.Name}' offers no compile service.");

                    var compiled = compilable.Compile();

                    var result = new UnifiedCompileResult
                    {
                        Device = device.Name,
                        State = compiled.State.ToString(),
                        ErrorCount = compiled.ErrorCount,
                        WarningCount = compiled.WarningCount
                    };

                    foreach (var message in compiled.Messages)
                    {
                        Flatten(message, new List<string>(), result.Items);
                    }

                    result.Items = result.Items
                        .Where(i => !errorsOnly || i.Severity == "Error")
                        .Where(i => string.IsNullOrWhiteSpace(pathFilter) || (i.Path ?? string.Empty).IndexOf(pathFilter!.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();

                    return result;
                },
                ("softwarePath", softwarePath));
        }

        /// <summary>
        /// The tree of the compiler turned into lines: a node with a path only names the place, a node with a
        /// description is the message for the place its parents name.
        /// </summary>
        private static void Flatten(CompilerResultMessage message, List<string> path, List<UnifiedCompileMessage> items)
        {
            var here = new List<string>(path);

            if (!string.IsNullOrWhiteSpace(message.Path))
            {
                here.Add(message.Path.Trim());
            }

            var text = (message.Description ?? string.Empty).Trim();
            var severity = message.State.ToString();

            // "Information" lines are the compiler's progress ("Software compilation started."), not findings.
            if (text.Length > 0 && (severity == "Error" || severity == "Warning"))
            {
                items.Add(new UnifiedCompileMessage { Severity = severity, Path = string.Join("/", here), Text = text });
            }

            foreach (var child in message.Messages)
            {
                Flatten(child, here, items);
            }
        }
    }
}
