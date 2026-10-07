using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    // The tools of the area 'security': protection, passwords and users. Each sensitive kind of change is a tool of
    // its own, so that a client which lets the user decide per tool (always allow / ask / block) can treat them apart.
    // The work is in Siemens/Portal.Security.cs.
    public static partial class McpServer
    {
        /// <summary>What every tool of this area tells the agent about asking first and about passwords.</summary>
        private const string SecurityRule =
            " SENSITIVE: before calling, tell the user in plain words what will change on which PLC and get their consent. A password is the one the user gave in this conversation - never make one up, " +
            "never repeat it in your answer. The server passes it to TIA Portal and keeps it nowhere.";

        public class ResponsePlcSecurity : ResponseMessage
        {
            public PlcSecurityInfo? Security { get; set; }
        }

        public class ResponseSecurityChange : ResponseMessage
        {
            public string? Cpu { get; set; }

            public string? Before { get; set; }

            public string? After { get; set; }

            public IEnumerable<string>? Done { get; set; }
        }

        private static JsonObject SecurityMeta() => new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true, ["pendingSave"] = true };

        [McpServerTool(Name = "sec_get_plc_security", Title = "Get the protection settings of a PLC", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Read how a PLC is protected: the protection of confidential PLC configuration data, the access level (CPUs with access levels) or the access control by users (CPUs with firmware V4 and newer), whether the web server is on, the users of the web server and of the OPC UA server, and whether the display takes a password. No password is ever returned: TIA Portal does not give them out")]
        public static ResponsePlcSecurity GetPlcSecurity(
            [Description("deviceItemPath: path of the CPU, e.g. 'Station_1/PLC_1'")] string deviceItemPath)
        {
            try
            {
                var info = Portal.GetPlcSecurity(deviceItemPath);

                return new ResponsePlcSecurity
                {
                    Security = info,
                    Message = $"Protection of '{info.Cpu}': configuration data {info.ConfigurationProtection ?? "n/a"}, " + (info.AccessLevel != null ? $"access level {info.AccessLevel}" : $"access control {info.AccessControl ?? "n/a"}"),
                    Meta = Ok(new JsonObject())
                };
            }
            catch (PortalException pex)
            {
                throw ToolError(pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure($"reading the protection of '{deviceItemPath}'", ex);
            }
        }

        [WriteTool]
        [McpServerTool(Name = "sec_set_plc_configuration_protection", Title = "Protect the PLC configuration data (password)", Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true),
         Description("Change the protection of confidential PLC configuration data of a CPU: 'protect' sets a password, 'protect_all' protects all configuration data (with a password when one is given), 'unprotect' removes the protection (the present password is needed when one is set), 'unprotect_all', 'change_password' (password = the present one, newPassword), 'reset'. A password that is lost cannot be recovered from the project. The CPU has to be loaded again afterwards." + SecurityRule)]
        public static ResponseSecurityChange SetPlcConfigurationProtection(
            [Description("deviceItemPath: path of the CPU, e.g. 'Station_1/PLC_1'")] string deviceItemPath,
            [Description("action: protect, protect_all, unprotect, unprotect_all, change_password or reset")] string action,
            [Description("password: the password to set, or the present one for unprotect and change_password")] string password = "",
            [Description("newPassword: the new password, for change_password")] string newPassword = "")
        {
            return Guarded(nameof(SetPlcConfigurationProtection), () =>
            {
                var (before, after) = Portal.SetPlcConfigurationProtection(deviceItemPath, action, password, newPassword);

                return new ResponseSecurityChange
                {
                    Cpu = deviceItemPath,
                    Before = before,
                    After = after,
                    Message = $"Protection of the PLC configuration data of '{deviceItemPath}': {before} -> {after} ({action}). {SaveHint}",
                    Meta = SecurityMeta()
                };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "sec_set_plc_access_level", Title = "Set the access level of a PLC and its passwords", Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true),
         Description("Set the access level of a CPU with access levels (S7-1200, S7-1500 before firmware V4) and the passwords of its levels: accessLevel is what is allowed WITHOUT a password - FullAccess, ReadAccess, HMIAccess or NoAccess (FullAccessIncludingFailsafe on F-CPUs); passwordFor with password sets the password that opens a level; resetPasswordFor removes one. A wrong level locks people out of the PLC until the project is loaded again. A CPU with firmware V4 or newer has no access levels and is refused." + SecurityRule)]
        public static ResponseSecurityChange SetPlcAccessLevel(
            [Description("deviceItemPath: path of the CPU, e.g. 'Station_1/PLC_1'")] string deviceItemPath,
            [Description("accessLevel: the level granted without a password; empty leaves it as it is")] string accessLevel = "",
            [Description("passwordFor: the level the password opens, e.g. FullAccess; empty sets no password")] string passwordFor = "",
            [Description("password: the password for passwordFor")] string password = "",
            [Description("resetPasswordFor: the level whose password is removed; empty removes none")] string resetPasswordFor = "")
        {
            return Guarded(nameof(SetPlcAccessLevel), () =>
            {
                var (before, after, notes) = Portal.SetPlcAccessLevel(deviceItemPath, accessLevel, passwordFor, password, resetPasswordFor);

                return new ResponseSecurityChange
                {
                    Cpu = deviceItemPath,
                    Before = before,
                    After = after,
                    Done = notes,
                    Message = $"Access level of '{deviceItemPath}': {before} -> {after}. {string.Join(" ", notes)} {SaveHint}".Replace("  ", " "),
                    Meta = SecurityMeta()
                };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "sec_manage_webserver_users", Title = "Manage the web server users of a PLC", Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true),
         Description("Create, update or delete users of the web server of a CPU that has its own web server users (S7-1200, S7-1500 before firmware V4), several at once, all or nothing. A user has a name, a password and permissions; the user 'Everybody' is what anyone may do without logging in. The web server itself is switched on with 'hw_set_device_item_attributes' (WebserverActivate)." + SecurityRule)]
        public static ResponseSecurityChange ManageWebserverUsers(
            [Description("deviceItemPath: path of the CPU, e.g. 'Station_1/PLC_1'")] string deviceItemPath,
            [Description("actions: the changes, applied in order; fields: action (create, update, delete), userName, password, permissions")] List<PlcUserAction> actions)
        {
            return Guarded(nameof(ManageWebserverUsers), () =>
            {
                var done = Portal.ManageWebserverUsers(deviceItemPath, actions);

                return new ResponseSecurityChange { Cpu = deviceItemPath, Done = done, Message = $"Web server users of '{deviceItemPath}': {string.Join("; ", done)}. {SaveHint}", Meta = SecurityMeta() };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "sec_manage_opcua_users", Title = "Manage the OPC UA users of a PLC", Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true),
         Description("Create or delete users of the OPC UA server of a CPU, or set their password ('update'), several at once, all or nothing. The OPC UA server and its authentication by user name have to be on first ('hw_set_device_item_attributes' on the item 'OPC UA_1'); otherwise TIA Portal refuses a new user." + SecurityRule)]
        public static ResponseSecurityChange ManageOpcUaUsers(
            [Description("deviceItemPath: path of the CPU, e.g. 'Station_1/PLC_1'")] string deviceItemPath,
            [Description("actions: the changes, applied in order; fields: action (create, update, delete), userName, password")] List<PlcUserAction> actions)
        {
            return Guarded(nameof(ManageOpcUaUsers), () =>
            {
                var done = Portal.ManageOpcUaUsers(deviceItemPath, actions);

                return new ResponseSecurityChange { Cpu = deviceItemPath, Done = done, Message = $"OPC UA users of '{deviceItemPath}': {string.Join("; ", done)}. {SaveHint}", Meta = SecurityMeta() };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "sec_set_display_password", Title = "Set the display password of a PLC", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Set the password that protects the display of an S7-1500 CPU." + SecurityRule)]
        public static ResponseSecurityChange SetDisplayPassword(
            [Description("deviceItemPath: path of the CPU, e.g. 'Station_1/PLC_1'")] string deviceItemPath,
            [Description("password: the password of the display")] string password)
        {
            return Guarded(nameof(SetDisplayPassword), () =>
            {
                Portal.SetDisplayPassword(deviceItemPath, password);

                return new ResponseSecurityChange { Cpu = deviceItemPath, Message = $"Display password of '{deviceItemPath}' set. {SaveHint}", Meta = SecurityMeta() };
            });
        }

        public class ResponseProjectUsers : ResponseMessage
        {
            public ProjectUsersInfo? Security { get; set; }
        }

        public class ResponsePasswordPolicy : ResponseMessage
        {
            public PasswordPolicyInfo? Before { get; set; }

            public PasswordPolicyInfo? After { get; set; }
        }

        public class ResponseBlockProtection : ResponseMessage
        {
            public string? Block { get; set; }

            public string? Before { get; set; }

            public string? After { get; set; }
        }

        [McpServerTool(Name = "sec_get_project_users", Title = "Get the users, groups and roles of the project", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Read the users and roles of the project ('Security settings > Users and roles' in TIA Portal): the users with their roles and whether they are active (the user 'Anonymous' is access without login), the user groups, the roles - those of TIA Portal (system: true) and those of the project - with their function rights per device, the password policy, and which devices have function rights. These users decide the access to CPUs with firmware V4 and newer, to Unified panels and to network devices. rightsOfDevice lists the function rights a device offers. No password is ever returned")]
        public static ResponseProjectUsers GetProjectUsers(
            [Description("rightsOfDevice: name of a device (or of its CPU / panel) whose available function rights are listed too; empty lists none")] string rightsOfDevice = "")
        {
            try
            {
                var info = Portal.GetProjectUsers(rightsOfDevice);

                return new ResponseProjectUsers
                {
                    Security = info,
                    Message = $"{info.Users.Count} user(s), {info.Groups.Count} group(s), {info.Roles.Count(r => !r.System)} role(s) of the project and {info.Roles.Count(r => r.System)} of TIA Portal",
                    Meta = Ok(new JsonObject())
                };
            }
            catch (PortalException pex)
            {
                throw ToolError(pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw Failure("reading the users and roles of the project", ex);
            }
        }

        [WriteTool]
        [McpServerTool(Name = "sec_manage_project_users", Title = "Manage the users and user groups of the project", Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true),
         Description("Create, update or delete users and user groups of the project, several at once, all or nothing: name, password, roles (roles = exactly these, addRoles / removeRoles = change the present ones), active, comment, session timeout, alias, authentication. A user gets rights only through roles; 'sec_get_project_users' lists the roles. The user 'Anonymous' with active: true lets everybody in without login, with the roles it has. The role 'Engineering administrator' decides who administers a protected project: give or take it only on an explicit request. The devices have to be loaded again for a change to reach them." + SecurityRule)]
        public static ResponseSecurityChange ManageProjectUsers(
            [Description("actions: the changes, applied in order; fields: action (create, update, delete), kind (user, group), name, password, newName, comment, active, sessionTimeout, runtimeSessionTimeout, alias, authentication, roles, addRoles, removeRoles")] List<ProjectUserAction> actions)
        {
            return Guarded(nameof(ManageProjectUsers), () =>
            {
                var done = Portal.ManageProjectUsers(actions);

                return new ResponseSecurityChange { Done = done, Message = $"Users of the project: {string.Join("; ", done)}. {SaveHint}", Meta = SecurityMeta() };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "sec_manage_project_roles", Title = "Manage the roles of the project and their rights", Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true),
         Description("Create, update or delete roles of the project and give them function rights of a device (addRights / removeRights with device; one device per action), several at once, all or nothing. A function right is e.g. full access to a PLC, reading tags over its web server, operating a Unified panel; 'sec_get_project_users' with rightsOfDevice lists what a device offers. The roles TIA Portal brings along cannot be changed. Deleting a role takes it away from every user that has it." + SecurityRule)]
        public static ResponseSecurityChange ManageProjectRoles(
            [Description("actions: the changes, applied in order; fields: action (create, update, delete), name, newName, comment, sessionTimeout, device, addRights, removeRights")] List<ProjectRoleAction> actions)
        {
            return Guarded(nameof(ManageProjectRoles), () =>
            {
                var done = Portal.ManageProjectRoles(actions);

                return new ResponseSecurityChange { Done = done, Message = $"Roles of the project: {string.Join("; ", done)}. {SaveHint}", Meta = SecurityMeta() };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "sec_protect_project", Title = "Protect the project (cannot be undone)", Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true),
         Description("Protect the open project: a user with the role 'Engineering administrator' is made, and from then on the project opens - in TIA Portal and through 'open_project' - only with the name and password of one of its users. THIS CANNOT BE UNDONE: TIA Portal has no way to remove the protection of a project, and with the password lost the project cannot be opened any more. Call it only when the user asked for exactly this, after telling them it is final and getting a clear yes; suggest a copy of the project first ('save_as_project')." + SecurityRule)]
        public static ResponseSecurityChange ProtectProject(
            [Description("administratorName: name of the user that becomes the administrator of the project")] string administratorName,
            [Description("password: the password of that user; TIA Portal asks for at least 10 characters here")] string password)
        {
            return Guarded(nameof(ProtectProject), () =>
            {
                var users = Portal.ProtectProject(administratorName, password);

                return new ResponseSecurityChange
                {
                    Done = users,
                    Message = $"The project is protected; its administrator is '{administratorName}'. Users: {string.Join("; ", users)}. This cannot be undone. {SaveHint}",
                    Meta = SecurityMeta()
                };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "sec_set_password_policy", Title = "Set the password policy of the project", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Set the password policy for the users of the project; only the settings given are changed. TIA Portal checks the limits (minimum length 8 to 32). The policy applies to passwords set from now on." + SecurityRule)]
        public static ResponsePasswordPolicy SetPasswordPolicy(
            [Description("minimumLength: least number of characters, 8 to 32")] int? minimumLength = null,
            [Description("minimumNumericCharacters: least number of digits")] int? minimumNumericCharacters = null,
            [Description("minimumSpecialCharacters: least number of special characters")] int? minimumSpecialCharacters = null,
            [Description("upperAndLowerCase: whether upper and lower case letters are both required")] bool? upperAndLowerCase = null,
            [Description("passwordAging: whether passwords expire")] bool? passwordAging = null,
            [Description("passwordValidity: days a password is valid")] int? passwordValidity = null,
            [Description("prewarningTime: days of warning before a password expires")] int? prewarningTime = null,
            [Description("passwordsBlockedForReuse: how many former passwords cannot be used again")] int? passwordsBlockedForReuse = null)
        {
            return Guarded(nameof(SetPasswordPolicy), () =>
            {
                var (before, after) = Portal.SetPasswordPolicy(minimumLength, minimumNumericCharacters, minimumSpecialCharacters, upperAndLowerCase, passwordAging, passwordValidity, prewarningTime, passwordsBlockedForReuse);

                return new ResponsePasswordPolicy { Before = before, After = after, Message = $"Password policy of the project set. {SaveHint}", Meta = SecurityMeta() };
            });
        }

        [WriteTool]
        [McpServerTool(Name = "sec_set_block_protection", Title = "Protect a block (know-how or write protection)", Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true),
         Description("Change the protection of a block with a password: 'protect' / 'unprotect' is the know-how protection (the code cannot be read or changed without the password), 'write_protect' / 'write_unprotect' / 'write_change_password' is the write protection (readable, not changeable). A know-how password has to have 8 to 120 characters with a digit, a special character, upper and lower case. A know-how password that is lost cannot be recovered: the code of the block is then gone for good. While a block is know-how protected its code can be neither read nor changed by the plc_* tools." + SecurityRule)]
        public static ResponseBlockProtection SetBlockProtection(
            [Description("softwarePath: path of the PLC software, e.g. 'Station_1/PLC_1'")] string softwarePath,
            [Description("blockPath: path of the block, e.g. 'Group/Block_1'")] string blockPath,
            [Description("action: protect, unprotect, write_protect, write_unprotect or write_change_password")] string action,
            [Description("password: the password to set, or the present one for unprotect, write_unprotect and write_change_password")] string password = "",
            [Description("newPassword: the new password, for write_change_password")] string newPassword = "")
        {
            return Guarded(nameof(SetBlockProtection), () =>
            {
                var (before, after) = Portal.SetBlockProtection(softwarePath, blockPath, action, password, newPassword);

                return new ResponseBlockProtection
                {
                    Block = blockPath,
                    Before = before,
                    After = after,
                    Message = $"Protection of block '{blockPath}' ({action}): {before} -> {after}. {SaveHint}",
                    Meta = SecurityMeta()
                };
            });
        }
    }
}
