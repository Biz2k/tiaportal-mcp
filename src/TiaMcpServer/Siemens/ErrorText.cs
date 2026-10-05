using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Turns an exception into the text an MCP client gets to read.
    ///
    /// Callers: Operation.Decorate (portal layer) and McpServer.ToolError (MCP layer). Affected
    /// API: none existing - this is a new internal helper. Reads/writes no data files.
    ///
    /// Why: a failed Openness call used to reach the client as "CreateFB failed" - the reason
    /// stayed in InnerException, which the MCP SDK never sends. The model then had nothing to
    /// correct itself with.
    /// </summary>
    internal static class ErrorText
    {
        /// <summary>Keeps a runaway message from flooding the client's context.</summary>
        private const int MaxLength = 2000;

        private const string Separator = " -> ";

        /// <summary>
        /// Keys Operation.Run writes into Exception.Data for its own bookkeeping; they are not
        /// context for the caller.
        /// </summary>
        private static readonly HashSet<string> InternalKeys =
            new HashSet<string>(StringComparer.Ordinal) { "__logged", "operation" };

        /// <summary>
        /// The messages of <paramref name="ex"/> and everything nested in it, outermost first,
        /// without repeats. Reflection wrappers are skipped, and the detail messages TIA Portal
        /// attaches to its own exceptions are included.
        /// </summary>
        internal static string Describe(Exception? ex)
        {
            var parts = new List<string>();

            Collect(ex, parts, 0);

            if (parts.Count == 0)
            {
                return ex?.GetType().Name ?? "Unknown error";
            }

            return Truncate(string.Join(Separator, parts));
        }

        /// <summary>
        /// Message for the client: the description, followed by the error code and the context
        /// Operation.Run recorded (paths, names), so a failure names the object it was about.
        /// </summary>
        internal static string ForClient(Exception ex)
        {
            var text = ex is PortalException ? ex.Message : Describe(ex);
            var details = new List<string>();

            if (ex is PortalException pex)
            {
                details.Add($"code: {pex.Code}");

                foreach (DictionaryEntry entry in pex.Data)
                {
                    if (entry.Key is string key && !InternalKeys.Contains(key) && entry.Value != null)
                    {
                        details.Add($"{key}: '{entry.Value}'");
                    }
                }
            }

            var cause = Unwrap(ex is PortalException ? ex.InnerException : ex);

            if (cause != null && cause is not PortalException)
            {
                details.Add($"cause: {cause.GetType().Name}");
            }

            return details.Count == 0 ? text : Truncate($"{text} [{string.Join("; ", details)}]");
        }

        private static void Collect(Exception? ex, List<string> parts, int depth)
        {
            // Depth guard: an exception chain is caller-controlled data.
            if (ex == null || depth > 8)
            {
                return;
            }

            if (ex is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    Collect(inner, parts, depth + 1);
                }

                return;
            }

            // "Exception has been thrown by the target of an invocation" says nothing.
            if (ex is not TargetInvocationException)
            {
                Add(parts, ex.Message);

                foreach (var detail in EngineeringDetails(ex))
                {
                    Add(parts, detail);
                }
            }

            Collect(ex.InnerException, parts, depth + 1);
        }

        private static void Add(List<string> parts, string? message)
        {
            var text = (message ?? string.Empty).Trim();

            // A wrapper often repeats its inner message; say it once.
            if (text.Length > 0 && !parts.Any(p => p.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                parts.Add(text);
            }
        }

        /// <summary>
        /// Siemens.Engineering exceptions carry their real explanation in MessageData and
        /// DetailMessageData. Read by reflection: the exception types differ between TIA Portal
        /// versions and not all of them expose both properties.
        /// </summary>
        private static IEnumerable<string> EngineeringDetails(Exception ex)
        {
            var result = new List<string>();

            try
            {
                var type = ex.GetType();

                if (type.Namespace == null || !type.Namespace.StartsWith("Siemens.Engineering", StringComparison.Ordinal))
                {
                    return result;
                }

                AddText(result, type.GetProperty("MessageData")?.GetValue(ex));

                if (type.GetProperty("DetailMessageData")?.GetValue(ex) is IEnumerable details)
                {
                    foreach (var detail in details)
                    {
                        AddText(result, detail);
                    }
                }
            }
            catch (Exception)
            {
                // Describing an error must never raise another one.
            }

            return result;
        }

        private static void AddText(List<string> sink, object? messageData)
        {
            if (messageData?.GetType().GetProperty("Text")?.GetValue(messageData) is string text
                && !string.IsNullOrWhiteSpace(text))
            {
                sink.Add(text);
            }
        }

        private static Exception? Unwrap(Exception? ex)
        {
            while (ex is TargetInvocationException && ex.InnerException != null)
            {
                ex = ex.InnerException;
            }

            return ex;
        }

        private static string Truncate(string text)
        {
            return text.Length <= MaxLength ? text : text.Substring(0, MaxLength) + "...";
        }
    }
}
