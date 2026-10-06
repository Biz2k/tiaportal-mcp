using Microsoft.Extensions.Logging;
using System;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// The way back of 'plc_move_block' / 'plc_move_type'. A move exports the object, deletes it and imports it into the
    /// target group (the original has to go first, or the import fails on the duplicate name). When the import fails, the
    /// object is imported into its old group again.
    ///
    /// Is this needed? The tools run in a TIA Portal transaction (<c>Guarded</c> / <c>Portal.InTransaction</c>), and a
    /// failure rolls the delete back by itself. The transaction is best-effort, though: when TIA Portal grants no exclusive
    /// access, or the project cannot host a transaction, the write runs unwrapped - and then nothing but this puts the
    /// object back. So the branch stays. Not run against a real failure (task 23, 2026-10-06): an import that fails after
    /// a successful export of the same object cannot be provoked in the test project, and a call without a granted
    /// transaction cannot be forced either; both outcomes are covered by tests with the restore replaced by a stand-in.
    /// </summary>
    public static class MoveRecovery
    {
        /// <summary>
        /// Runs <paramref name="restore"/>. Returns normally when the object is back, so the caller rethrows the original
        /// import error; throws when it is not, because "the object is gone" outranks the original error.
        /// </summary>
        public static void Restore(ILogger? logger, Action restore, string kind, string name, Exception importError)
        {
            try
            {
                restore();
            }
            catch (Exception restoreError)
            {
                logger?.LogError(restoreError, "{Kind} {Name} could not be restored after a failed move", kind, name);

                throw new PortalException(PortalErrorCode.ImportFailed,
                    $"{kind} '{name}' was removed from its group, the import into the target failed ({ErrorText.Describe(importError)}), " +
                    $"and restoring it failed as well ({ErrorText.Describe(restoreError)}). Undo the change in TIA Portal or close the project without saving.",
                    null, restoreError);
            }
        }
    }
}
