using System;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// The answer of the download step 'UserManagementDownload' (Siemens.Engineering.Download.Configurations,
    /// enum UserManagementPreDownloadSelections), by the short word 'download_to_plc' takes. TIA Portal raises the step when
    /// the CPU holds user management data (users, roles) that differs from the project. Pure mapping, tested without TIA Portal.
    /// </summary>
    public static class DownloadUserManagement
    {
        /// <summary>The words 'download_to_plc' accepts for 'downloadUserManagement'.</summary>
        public const string Words = "keep, update, overwrite";

        /// <summary>The option name for a word; PortalException (InvalidParams) for anything else.</summary>
        public static string OptionFor(string? word)
        {
            switch ((word ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "":
                case "keep":
                    return "KeepOnlineUserManagementData";
                case "update":
                    return "UpdateUserManagementDataButKeepOnlinePassword";
                case "overwrite":
                    return "DownloadAllUserManagementDataResetToProject";
                default:
                    throw new PortalException(PortalErrorCode.InvalidParams,
                        $"downloadUserManagement '{word}' is not one of: {Words}. " +
                        "keep leaves the user management data of the CPU as it is (default), update takes the users of the project but keeps the passwords of the CPU, " +
                        "overwrite replaces all of it by the project's data and resets the passwords.");
            }
        }
    }
}
