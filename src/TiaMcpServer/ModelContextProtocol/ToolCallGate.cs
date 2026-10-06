using global::ModelContextProtocol.Protocol;
using global::ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// One tool call at a time. The MCP SDK runs concurrent requests concurrently, and the lock in
    /// <c>Operation</c> only covers the Portal methods: a tool goes on using the Openness objects it got (devices, blocks,
    /// sources) after the Portal method returned, so a 'close_project' or 'open_project' arriving meanwhile disposed them
    /// under it ("Access to a disposed object of type DeviceImpl", task 22, 2026-10-06). With the gate a call that started
    /// first finishes first, and one that starts after a close answers "no project is open".
    ///
    /// The tools that only look at the server or at the running processes are not held back by a long call (a download,
    /// a compile): they never touch an Openness object of the project.
    /// </summary>
    public static class ToolCallGate
    {
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);

        private static readonly HashSet<string> Free = new HashSet<string>(StringComparer.Ordinal)
        {
            "get_state", "get_tia_instances", "doctor"
        };

        /// <summary>Whether the tool takes its turn at the gate.</summary>
        internal static bool IsGated(string? toolName) => toolName == null || !Free.Contains(toolName);

        /// <summary>For the unit test: the class is internal, the test assembly sees this one.</summary>
        public static bool IsGatedForTest(string? toolName) => IsGated(toolName);

        /// <summary>The call-tool filter that puts the calls in a line.</summary>
        internal static McpRequestHandler<CallToolRequestParams, CallToolResult> Filter(McpRequestHandler<CallToolRequestParams, CallToolResult> next)
        {
            return async (request, cancellationToken) =>
            {
                if (!IsGated(request.Params?.Name))
                {
                    return await next(request, cancellationToken).ConfigureAwait(false);
                }

                await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    return await next(request, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    Gate.Release();
                }
            };
        }
    }
}
