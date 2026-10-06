using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>The 'conflict_resolution' argument of 'import_objects': what is offered, checked before TIA Portal is touched.</summary>
    internal static class ImportConflictResolution
    {
        /// <summary>True for 'overwrite', false for 'skip'; anything else is refused with the way out.</summary>
        internal static bool ParseOverwrite(string? value)
        {
            var resolution = (value ?? string.Empty).Trim().ToLowerInvariant();

            if (resolution == "rename")
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    "conflict_resolution 'rename' is not offered: TIA Portal cannot rename an object while it is imported. " +
                    "Use 'plc_copy_block' / 'plc_copy_type' with 'newName', or give the object another name in the file.");
            }

            if (resolution != "overwrite" && resolution != "skip")
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"Unknown conflict_resolution '{value}'. Use 'overwrite' or 'skip'.");
            }

            return resolution == "overwrite";
        }
    }
}
