using System;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Recognises the exceptions that mean TIA Portal is gone, and words what the client should
    /// do about it. No Siemens types in here, so the tests can use it without TIA Portal.
    ///
    /// Callers: Operation.Decorate, Portal.InTransaction, Portal.GetState. Reads and writes no
    /// files.
    ///
    /// Why: a few Openness calls end in a NonRecoverableException, after which TIA Portal closes
    /// itself together with the unsaved project (the write of HmiTag.DisplayName, a handler that
    /// throws inside DownloadProvider, GetAttributeInfos on a WinCC Unified alarm - see the table
    /// in docs/handoff/context.md). The server then believed it was still connected and every
    /// later call failed with "Access to a disposed object of type 'Siemens.Engineering.Project'
    /// is not possible", which tells the client nothing. Found 2026-10-05 and -06, four times.
    /// </summary>
    internal static class TiaLoss
    {
        internal enum Kind
        {
            /// <summary>An ordinary failure.</summary>
            None,

            /// <summary>Openness said the call cannot be recovered from: TIA Portal is closing.</summary>
            Fatal,

            /// <summary>An Openness object that belongs to a closed project or a closed TIA Portal was used.</summary>
            Disposed
        }

        internal const string FatalTypeName = "NonRecoverableException";

        internal static Kind Classify(Exception? ex)
        {
            var kind = Kind.None;

            Walk(ex, 0, ref kind);

            return kind;
        }

        private static void Walk(Exception? ex, int depth, ref Kind kind)
        {
            // An exception chain is caller-controlled data: the depth is limited.
            if (ex == null || depth > 8 || kind == Kind.Fatal)
            {
                return;
            }

            for (var type = ex.GetType(); type != null; type = type.BaseType)
            {
                if (type.Name == FatalTypeName)
                {
                    kind = Kind.Fatal;

                    return;
                }
            }

            if (ex.Message != null && ex.Message.IndexOf("disposed object", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = Kind.Disposed;
            }

            if (ex is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    Walk(inner, depth + 1, ref kind);
                }

                return;
            }

            Walk(ex.InnerException, depth + 1, ref kind);
        }

        /// <summary>TIA Portal is gone and the connection has been dropped.</summary>
        internal static string GoneMessage(Kind kind)
        {
            return (kind == Kind.Fatal
                       ? "TIA Portal was closed by the call that was just made: Openness reported a non-recoverable error. "
                       : "TIA Portal is no longer running. ") +
                   "Unsaved changes of the project are lost. Start TIA Portal (the project opens with it if it was the last one used) and call 'connect', then 'open_project' if no project is open. " +
                   "The call that failed was not completed.";
        }

        /// <summary>TIA Portal still runs, but the project the server held was closed in it.</summary>
        internal static string ClosedProjectMessage()
        {
            return "The project or object this call used was closed in TIA Portal (a disposed Openness object). TIA Portal itself is still running: " +
                   "call 'get_state' to pick up the open project, or 'open_project' to open one.";
        }
    }
}
