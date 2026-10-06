using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>One failed action of a batch: what it was, why, and the code of its own failure when it had one.</summary>
    public sealed class BatchFailure
    {
        public BatchFailure(string label, string reason, PortalErrorCode? code)
        {
            Label = label;
            Reason = reason;
            Code = code;
        }

        public string Label { get; }

        public string Reason { get; }

        public PortalErrorCode? Code { get; }
    }

    /// <summary>
    /// The error of a batch tool whose actions failed. The batch is thrown as one PortalException, which the client reads with
    /// a single "[code: ...; softwarePath: ...]" at the end - so the failed actions must not carry such a bracket themselves
    /// (they did: "... [code: NotFound] [code: InvalidParams; softwarePath: ...]", task 23, 2026-10-06).
    /// The code of the batch is the code its failed actions share; when they differ, each action names its own in square
    /// brackets and the batch is InvalidParams. Pure logic, tested without TIA Portal.
    /// </summary>
    public static class BatchErrorText
    {
        public static (string Message, PortalErrorCode Code) Compose(IReadOnlyList<BatchFailure> failed, int total, string outcome)
        {
            var codes = failed.Select(f => f.Code ?? PortalErrorCode.InvalidParams).Distinct().ToList();
            var shared = codes.Count == 1;
            var code = shared ? codes[0] : PortalErrorCode.InvalidParams;

            var details = string.Join(" | ", failed.Select(f =>
                shared ? $"{f.Label}: {f.Reason}" : $"{f.Label} [{f.Code ?? PortalErrorCode.InvalidParams}]: {f.Reason}"));

            return ($"{failed.Count} of {total} action(s) failed. {outcome} {details}", code);
        }
    }
}
