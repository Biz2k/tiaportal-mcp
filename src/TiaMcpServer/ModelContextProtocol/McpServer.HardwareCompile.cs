using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.Compiler;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        public class HardwareCompileMessage
        {
            /// <summary>Error or Warning.</summary>
            public string? State { get; set; }

            /// <summary>Where the message belongs: the objects above it in the result of TIA Portal, outermost first.</summary>
            public string? Path { get; set; }

            public string? Text { get; set; }

            /// <summary>How many times TIA Portal gave this message for this object, when more than once.</summary>
            public int? Count { get; set; }
        }

        public class ResponseHardwareCompile : ResponseMessage
        {
            public string? Device { get; set; }

            public string? State { get; set; }

            public int ErrorCount { get; set; }

            public int WarningCount { get; set; }

            public List<HardwareCompileMessage> Messages { get; set; } = new List<HardwareCompileMessage>();
        }

        // Callers: registered by Program.BuildTools() unless '--read-only'. A compile cannot run inside a transaction,
        // hence GuardedNoTransaction. A compile with errors is an answer, not a failed call: the errors are the result.
        [WriteTool]
        [McpServerTool(Name = "hw_compile", Title = "Compile a device (hardware and software)", Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Compile a device (station) the way 'Compile' on the device does in TIA Portal - its hardware configuration and the software on it - and return the errors and warnings with the object each belongs to; the same message for the same object is given once with a count. Use it after changing hardware, addresses, networks or connections, and when 'download_to_plc' answers that the hardware configuration does not compile - a download says only that. For the software alone 'plc_compile_software' is the tool. Errors are returned as the result; the call itself fails only when the device is not found or cannot be compiled")]
        public static ResponseHardwareCompile CompileHardware(
            [Description("devicePath: path of the device (station) as 'hw_get_devices' lists it, e.g. 'S7-1500%2FET200MP station_1'")] string devicePath)
        {
            return GuardedNoTransaction(nameof(CompileHardware), () =>
            {
                var result = Portal.CompileHardware(devicePath);
                var messages = new List<HardwareCompileMessage>();

                CollectHardwareMessages(result.Messages, new List<string>(), messages, 0);

                // A block with one mistake in many places repeats its message; say it once.
                messages = messages
                    .GroupBy(m => (m.State, m.Path, m.Text))
                    .Select(g => new HardwareCompileMessage { State = g.Key.State, Path = g.Key.Path, Text = g.Key.Text, Count = g.Count() > 1 ? g.Count() : (int?)null })
                    .OrderBy(m => m.State == "Error" ? 0 : 1)
                    .ToList();

                var shown = messages.Take(MaxHardwareCompileMessages).ToList();

                var state = result.State.ToString();

                return new ResponseHardwareCompile
                {
                    Device = devicePath,
                    State = state,
                    ErrorCount = result.ErrorCount,
                    WarningCount = result.WarningCount,
                    Messages = shown,
                    Message = $"Hardware compile of '{devicePath}' finished with state '{state}': {result.ErrorCount} error(s), {result.WarningCount} warning(s)." +
                              (result.ErrorCount > 0 ? " First error: " + (messages.FirstOrDefault(m => m.State == "Error") is { } first ? $"{first.Path}: {first.Text}" : "see 'messages'") : string.Empty) +
                              (messages.Count > shown.Count ? $" {messages.Count - shown.Count} more message(s) are not shown; fix the first ones and compile again." : string.Empty),
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = result.State != CompilerResultState.Error,
                        ["state"] = state
                    }
                };
            });
        }

        private const int MaxHardwareCompileMessages = 60;

        /// <summary>
        /// The leaves of the message tree that are errors or warnings. TIA Portal hangs the text on a leaf without a
        /// path and puts the objects it belongs to on the levels above, so the path is put together from those.
        /// </summary>
        private static void CollectHardwareMessages(CompilerResultMessageComposition? messages, List<string> above, List<HardwareCompileMessage> into, int depth)
        {
            // Depth guard: the tree is data of TIA Portal.
            if (messages == null || depth > 12)
            {
                return;
            }

            foreach (var message in messages)
            {
                if (message == null || (message.State != CompilerResultState.Error && message.State != CompilerResultState.Warning))
                {
                    continue;
                }

                var path = (message.Path ?? string.Empty).Trim();
                var text = (message.Description ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
                var here = path.Length > 0 ? above.Concat(new[] { path }).ToList() : above;

                // "Compiling finished (errors: 3; warnings: 2)" repeats the counts
                if (text.Length > 0 && !(depth == 0 && path.Length == 0))
                {
                    into.Add(new HardwareCompileMessage { State = message.State.ToString(), Path = here.Count > 0 ? string.Join(" / ", here) : null, Text = text });
                }

                CollectHardwareMessages(message.Messages, here, into, depth + 1);
            }
        }
    }
}
