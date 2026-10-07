using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    }
}
