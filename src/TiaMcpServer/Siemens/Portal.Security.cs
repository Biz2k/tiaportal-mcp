using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;

namespace TiaMcpServer.Siemens
{
    /// <summary>The protection settings of a PLC, without any secret: Openness gives no password back.</summary>
    public class PlcSecurityInfo
    {
        public string? Cpu { get; set; }

        /// <summary>Protection of confidential PLC configuration data: None, WithoutPassword, WithPassword, WithPasswordAllDataProtection.</summary>
        public string? ConfigurationProtection { get; set; }

        /// <summary>The access level of a CPU with access levels: FullAccess, ReadAccess, HMIAccess, NoAccess ...; null on a CPU with access control by users.</summary>
        public string? AccessLevel { get; set; }

        /// <summary>The attribute PlcAccessControlConfiguration of a CPU with access control by users and roles.</summary>
        public string? AccessControl { get; set; }

        public bool? WebserverActive { get; set; }

        public List<string>? WebserverUsers { get; set; }

        public List<string>? OpcUaUsers { get; set; }

        public bool DisplayPasswordSupported { get; set; }

        public List<string> Notes { get; set; } = new List<string>();
    }

    /// <summary>One change to the users of the web server or of the OPC UA server of a PLC.</summary>
    public class PlcUserAction
    {
        [System.ComponentModel.Description("'create', 'update' (permissions and/or password) or 'delete'")]
        public string? Action { get; set; }

        [System.ComponentModel.Description("Name of the user")]
        public string? UserName { get; set; }

        [System.ComponentModel.Description("Password, as the user of this conversation gave it. Needed for create")]
        public string? Password { get; set; }

        [System.ComponentModel.Description("Web server only: the permissions, e.g. [\"DoDiagnosis\", \"ReadTag\"]. Names: DoDiagnosis, ReadTag, ModifyTag, ReadTagStatus, ModifyTagStatus, AcknowledgeMessages, OpenUserDefinedWebPages, WriteUserDefinedWebPages, ReadFiles, ModifyFiles, ChangeOperatingMode, FlashLed, WriteFirmware, ChangeSystemParameter, ChangeApplicationParameter, Backup, Restore, FAdmin, ManageUserDefinedWebPages")]
        public List<string>? Permissions { get; set; }

        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement>? Unknown { get; set; }
    }

    // Protection and users of a PLC. Callers: the sec_* tools in McpServer.Security.cs.
    //
    // Found on TIA Portal V21 (probe of 2026-10-07 on temporary devices, rolled back):
    //   - PlcMasterSecretConfigurator (on the CPU item of S7-1500 and S7-1200): a new CPU is 'WithoutPassword';
    //     Protect(password) makes it 'WithPassword'; a second Protect is refused ("already configured");
    //     Unprotect() without the password is refused once one is set ("PLC Master Secret is not provided").
    //   - PlcAccessLevelProvider exists on CPUs with access levels (S7-1500 V2.9, S7-1200 V4.5): the level is a
    //     property, SetPassword(level, password) and ResetPassword(level) belong to it. A CPU with firmware V4.0 has no
    //     such service: its access is decided by users and roles (attribute PlcAccessControlConfiguration, project users).
    //   - WebserverUserManagement (CPUs with access levels): users with a flags enum of permissions; 'Everybody' is
    //     always there. OpcUaUserManagement sits on the item 'OPC UA_1'; creating a user is refused while user
    //     authentication of the OPC UA server is off. DisplayProtection sits on 'CPU display_1'.
    //   - Tried through the tools (2026-10-07, CPU 1511-1 PN V2.9): protect, change_password and unprotect with the
    //     password work; unprotect without it is refused; protect_all / unprotect_all answer "Additional protection of
    //     downloadable configuration data is not supported" on this CPU; reset on an unprotected CPU "is not
    //     configured"; a short password is refused with the rule of TIA Portal (8 to 120 characters). Access level and
    //     its passwords work. Web server users need WebserverActivate first.
    //   - Openness never returns a password. The server takes passwords as arguments, hands them to Openness as
    //     SecureString and keeps them nowhere: not in a log, not in an answer.
    public partial class Portal
    {
        private static SecureString Secret(string? text, string what)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} is missing. Ask the user for it; do not make one up.");
            }

            var secret = new SecureString();

            foreach (var c in text!)
            {
                secret.AppendChar(c);
            }

            secret.MakeReadOnly();

            return secret;
        }

        /// <summary>The permissions of a web server user by name; the enum is a set of flags that prints as a number.</summary>
        private static string PermissionNames(WebserverUserPermissions permissions)
        {
            var names = Enum.GetValues(typeof(WebserverUserPermissions)).Cast<WebserverUserPermissions>()
                .Where(p => p != WebserverUserPermissions.None && (Convert.ToInt64(p) & (Convert.ToInt64(p) - 1)) == 0 && permissions.HasFlag(p))
                .Select(p => p.ToString())
                .ToList();

            return names.Count == 0 ? "None" : string.Join(", ", names);
        }

        /// <summary>
        /// Runs a change and, where TIA Portal refuses it because something has to be switched on first, says what.
        /// Seen on V21: users of the web server need the web server on; OPC UA users and the display password need
        /// their authentication on.
        /// </summary>
        private static T WithSecurityHints<T>(string cpu, Func<T> change)
        {
            try
            {
                return change();
            }
            catch (Exception ex) when (ex is not PortalException)
            {
                var text = ex.ToString();

                if (text.IndexOf("web server is disabled", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    throw new PortalException(PortalErrorCode.InvalidState,
                        $"The web server of '{cpu}' is off, and TIA Portal takes web server users only while it is on. Switch it on first: 'hw_set_device_item_attributes' with {{\"WebserverActivate\": true}} on the CPU. Nothing was changed.", null, ex);
                }

                if (text.IndexOf("authentication is disabled", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    throw new PortalException(PortalErrorCode.InvalidState,
                        $"TIA Portal refuses this on '{cpu}' while the protection it belongs to is off (\"authentication is disabled\"). Switch it on first: for OPC UA users the server and its " +
                        "authentication by user name on the item 'OPC UA_1', for the display its password protection on the item 'CPU display_1' - 'hw_get_device_item_info' shows the " +
                        "attributes of the item, 'hw_set_device_item_attributes' sets them. Nothing was changed.", null, ex);
                }

                throw;
            }
        }

        /// <summary>The CPU a path means: the item itself, or the CPU below it.</summary>
        private DeviceItem RequireCpu(string deviceItemPath)
        {
            RequireProject();

            var item = GetDeviceItemUnlocked(deviceItemPath)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"No device item at '{deviceItemPath}'. Pass the path of the CPU, e.g. 'Station_1/PLC_1'; 'get_project_tree' with structured = true lists the paths.");

            if (item.Classification == DeviceItemClassifications.CPU)
            {
                return item;
            }

            return item.DeviceItems.FirstOrDefault(i => i.Classification == DeviceItemClassifications.CPU)
                ?? throw new PortalException(PortalErrorCode.InvalidParams, $"'{deviceItemPath}' is not a CPU. Pass the path of the CPU item, e.g. 'Station_1/PLC_1'.");
        }

        /// <summary>A feature of the CPU or of one of its items (OPC UA_1, CPU display_1).</summary>
        // No constraint on a Siemens type: the runtime would load that assembly with the class, also where TIA Portal is not installed (unit tests).
        private static T? CpuFeature<T>(DeviceItem cpu, Func<DeviceItem, T?> get) where T : class
        {
            foreach (var item in new[] { cpu }.Concat(cpu.DeviceItems))
            {
                T? feature = null;

                try
                {
                    feature = get(item);
                }
                catch (Exception)
                {
                    // This item does not offer it.
                }

                if (feature != null)
                {
                    return feature;
                }
            }

            return null;
        }

        private static string? ReadCpuAttribute(DeviceItem cpu, string name)
        {
            try
            {
                return cpu.GetAttributeInfos().Any(a => a.Name == name) ? Convert.ToString(cpu.GetAttribute(name), System.Globalization.CultureInfo.InvariantCulture) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public PlcSecurityInfo GetPlcSecurity(string deviceItemPath)
        {
            return Operation.Run(_logger, nameof(GetPlcSecurity), PortalErrorCode.InvalidState,
                () =>
                {
                    var cpu = RequireCpu(deviceItemPath);
                    var info = new PlcSecurityInfo { Cpu = cpu.Name };
                    var secret = CpuFeature(cpu, i => i.GetService<PlcMasterSecretConfigurator>());
                    var level = CpuFeature(cpu, i => i.GetService<PlcAccessLevelProvider>());
                    var web = CpuFeature(cpu, i => i.GetService<WebserverUserManagement>());
                    var opc = CpuFeature(cpu, i => i.GetService<OpcUaUserManagement>());

                    info.ConfigurationProtection = secret?.MasterSecretConfiguration.ToString();
                    info.AccessLevel = level?.PlcProtectionAccessLevel.ToString();
                    info.AccessControl = ReadCpuAttribute(cpu, "PlcAccessControlConfiguration");
                    info.WebserverActive = ReadCpuAttribute(cpu, "WebserverActivate") is string active ? active.Equals("True", StringComparison.OrdinalIgnoreCase) : (bool?)null;
                    info.WebserverUsers = web?.WebserverUsers.Select(u => $"{u.UserName}: {PermissionNames(u.Permissions)}").ToList();
                    info.OpcUaUsers = opc?.OpcUaUsers.Select(u => u.UserName).ToList();
                    info.DisplayPasswordSupported = CpuFeature(cpu, i => i.GetService<DisplayProtection>()) != null;

                    if (level == null)
                    {
                        info.Notes.Add("This CPU has no access levels: its access is decided by users and roles (attribute PlcAccessControlConfiguration, set with 'hw_set_device_item_attributes', and the users of the project).");
                    }

                    if (web == null)
                    {
                        info.Notes.Add("This CPU has no web server users of its own: its web server takes the users of the project.");
                    }

                    return info;
                },
                ("deviceItemPath", deviceItemPath));
        }

        /// <param name="action">protect, protect_all, unprotect, unprotect_all, change_password, reset.</param>
        public (string Before, string After) SetPlcConfigurationProtection(string deviceItemPath, string action, string? password, string? newPassword)
        {
            return Operation.Run(_logger, nameof(SetPlcConfigurationProtection), PortalErrorCode.InvalidState,
                () =>
                {
                    var cpu = RequireCpu(deviceItemPath);
                    var secret = CpuFeature(cpu, i => i.GetService<PlcMasterSecretConfigurator>())
                        ?? throw new PortalException(PortalErrorCode.NotSupported, $"CPU '{cpu.Name}' has no protection of confidential PLC configuration data.");
                    var before = secret.MasterSecretConfiguration.ToString();
                    var verb = (action ?? string.Empty).Trim().ToLowerInvariant();

                    switch (verb)
                    {
                        case "protect":
                            secret.Protect(Secret(password, "'password'"));
                            break;

                        case "protect_all":
                            if (string.IsNullOrEmpty(password))
                            {
                                secret.ProtectAllPlcConfiguration();
                            }
                            else
                            {
                                secret.ProtectAllPlcConfigurationWithPassword(Secret(password, "'password'"));
                            }

                            break;

                        case "unprotect":
                            if (string.IsNullOrEmpty(password))
                            {
                                secret.Unprotect();
                            }
                            else
                            {
                                secret.Unprotect(Secret(password, "'password'"));
                            }

                            break;

                        case "unprotect_all":
                            secret.UnprotectAllPlcConfiguration();
                            break;

                        case "change_password":
                            secret.ChangePassword(Secret(password, "'password' (the present one)"), Secret(newPassword, "'newPassword'"));
                            break;

                        case "reset":
                            secret.Reset();
                            break;

                        default:
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                $"action '{action}' is not known. Use protect (with password), protect_all, unprotect (with the password when one is set), unprotect_all, change_password (password, newPassword) or reset.");
                    }

                    return (before, secret.MasterSecretConfiguration.ToString());
                },
                ("deviceItemPath", deviceItemPath), ("action", action));
        }

        public (string Before, string After, List<string> Notes) SetPlcAccessLevel(string deviceItemPath, string? accessLevel, string? passwordFor, string? password, string? resetPasswordFor)
        {
            return Operation.Run(_logger, nameof(SetPlcAccessLevel), PortalErrorCode.InvalidState,
                () =>
                {
                    var cpu = RequireCpu(deviceItemPath);
                    var provider = CpuFeature(cpu, i => i.GetService<PlcAccessLevelProvider>())
                        ?? throw new PortalException(PortalErrorCode.NotSupported,
                            $"CPU '{cpu.Name}' has no access levels (a CPU with firmware V4 or newer decides access by users and roles). Its attribute PlcAccessControlConfiguration is set with " +
                            "'hw_set_device_item_attributes'; the users are those of the project.");
                    var before = provider.PlcProtectionAccessLevel.ToString();
                    var notes = new List<string>();

                    PlcProtectionAccessLevel Level(string? text, string what)
                    {
                        return Enum.TryParse<PlcProtectionAccessLevel>((text ?? string.Empty).Trim(), true, out var parsed)
                            ? parsed
                            : throw new PortalException(PortalErrorCode.InvalidParams, $"{what} '{text}' is not an access level. Levels: {string.Join(", ", Enum.GetNames(typeof(PlcProtectionAccessLevel)))}.");
                    }

                    if (string.IsNullOrWhiteSpace(accessLevel) && string.IsNullOrWhiteSpace(passwordFor) && string.IsNullOrWhiteSpace(resetPasswordFor))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, "Nothing to do: give accessLevel, passwordFor with password, or resetPasswordFor.");
                    }

                    if (!string.IsNullOrWhiteSpace(accessLevel))
                    {
                        provider.PlcProtectionAccessLevel = Level(accessLevel, "accessLevel");
                    }

                    if (!string.IsNullOrWhiteSpace(passwordFor))
                    {
                        provider.SetPassword(Level(passwordFor, "passwordFor"), Secret(password, "'password'"));
                        notes.Add($"Password set for the level {Level(passwordFor, "passwordFor")}.");
                    }

                    if (!string.IsNullOrWhiteSpace(resetPasswordFor))
                    {
                        provider.ResetPassword(Level(resetPasswordFor, "resetPasswordFor"));
                        notes.Add($"Password of the level {Level(resetPasswordFor, "resetPasswordFor")} removed.");
                    }

                    return (before, provider.PlcProtectionAccessLevel.ToString(), notes);
                },
                ("deviceItemPath", deviceItemPath), ("accessLevel", accessLevel ?? string.Empty));
        }

        private static string UserVerb(PlcUserAction? action, int index, bool withPermissions)
        {
            if (action == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Action {index}: is empty.");
            }

            if (action.Unknown != null && action.Unknown.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"Action {index}: unknown field(s) {string.Join(", ", action.Unknown.Keys)}. The fields are: action, userName, password{(withPermissions ? ", permissions" : string.Empty)}.");
            }

            var verb = (action.Action ?? string.Empty).Trim().ToLowerInvariant();

            if (verb != "create" && verb != "update" && verb != "delete")
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Action {index}: action '{action.Action}' is not known. Use create, update or delete.");
            }

            if (string.IsNullOrWhiteSpace(action.UserName))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Action {index} ({verb}): 'userName' is missing.");
            }

            return verb;
        }

        public List<string> ManageWebserverUsers(string deviceItemPath, List<PlcUserAction>? actions)
        {
            return Operation.Run(_logger, nameof(ManageWebserverUsers), PortalErrorCode.InvalidState,
                () =>
                {
                    var cpu = RequireCpu(deviceItemPath);
                    var users = CpuFeature(cpu, i => i.GetService<WebserverUserManagement>())?.WebserverUsers
                        ?? throw new PortalException(PortalErrorCode.NotSupported, $"CPU '{cpu.Name}' has no web server users of its own: its web server takes the users of the project.");

                    if (actions == null || actions.Count == 0)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, "No actions given. Example: [{\"action\": \"create\", \"userName\": \"operator\", \"permissions\": [\"DoDiagnosis\", \"ReadTag\"], \"password\": \"...\"}].");
                    }

                    WebserverUserPermissions Permissions(PlcUserAction action, int index)
                    {
                        var all = WebserverUserPermissions.None;

                        foreach (var name in action.Permissions ?? new List<string>())
                        {
                            all |= Enum.TryParse<WebserverUserPermissions>(name.Trim(), true, out var one)
                                ? one
                                : throw new PortalException(PortalErrorCode.InvalidParams,
                                    $"Action {index}: '{name}' is not a permission. Permissions: {string.Join(", ", Enum.GetNames(typeof(WebserverUserPermissions)).Where(n => n != "None"))}.");
                        }

                        return all;
                    }

                    var done = new List<string>();

                    for (var i = 0; i < actions.Count; i++)
                    {
                        var action = actions[i];
                        var verb = UserVerb(action, i + 1, true);
                        var name = action.UserName!.Trim();
                        var user = users.Find(name);

                        Progress(i + 1, actions.Count, $"{cpu.Name}: {verb} web server user '{name}'");

                        if (verb == "create")
                        {
                            if (user != null)
                            {
                                throw new PortalException(PortalErrorCode.InvalidParams, $"Action {i + 1}: web server user '{name}' exists already; use update.");
                            }

                            var password = Secret(action.Password, $"Action {i + 1}: 'password'");
                            var permissions = Permissions(action, i + 1);

                            WithSecurityHints(cpu.Name, () => users.Create(name, permissions, password));
                            done.Add($"created '{name}': {PermissionNames(permissions)}");
                            continue;
                        }

                        if (user == null)
                        {
                            throw new PortalException(PortalErrorCode.NotFound, $"Action {i + 1}: no web server user '{name}'. Users: {string.Join(", ", users.Select(u => u.UserName))}.");
                        }

                        if (verb == "delete")
                        {
                            user.Delete();
                            done.Add($"deleted '{name}'");
                            continue;
                        }

                        if (action.Permissions == null && string.IsNullOrEmpty(action.Password))
                        {
                            throw new PortalException(PortalErrorCode.InvalidParams, $"Action {i + 1}: an update needs 'permissions' or 'password'.");
                        }

                        if (action.Permissions != null)
                        {
                            user.Permissions = Permissions(action, i + 1);
                        }

                        if (!string.IsNullOrEmpty(action.Password))
                        {
                            user.SetPassword(Secret(action.Password, "'password'"));
                        }

                        done.Add($"updated '{name}'" + (action.Permissions != null ? $": {PermissionNames(user.Permissions)}" : string.Empty) + (string.IsNullOrEmpty(action.Password) ? string.Empty : " (password set)"));
                    }

                    return done;
                },
                ("deviceItemPath", deviceItemPath));
        }

        public List<string> ManageOpcUaUsers(string deviceItemPath, List<PlcUserAction>? actions)
        {
            return Operation.Run(_logger, nameof(ManageOpcUaUsers), PortalErrorCode.InvalidState,
                () =>
                {
                    var cpu = RequireCpu(deviceItemPath);
                    var users = CpuFeature(cpu, i => i.GetService<OpcUaUserManagement>())?.OpcUaUsers
                        ?? throw new PortalException(PortalErrorCode.NotSupported, $"CPU '{cpu.Name}' has no OPC UA users.");

                    if (actions == null || actions.Count == 0)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, "No actions given. Example: [{\"action\": \"create\", \"userName\": \"scada\", \"password\": \"...\"}].");
                    }

                    var done = new List<string>();

                    for (var i = 0; i < actions.Count; i++)
                    {
                        var action = actions[i];
                        var verb = UserVerb(action, i + 1, false);
                        var name = action.UserName!.Trim();
                        var user = users.Find(name);

                        Progress(i + 1, actions.Count, $"{cpu.Name}: {verb} OPC UA user '{name}'");

                        if (verb == "create")
                        {
                            if (user != null)
                            {
                                throw new PortalException(PortalErrorCode.InvalidParams, $"Action {i + 1}: OPC UA user '{name}' exists already; use update to set its password.");
                            }

                            var password = Secret(action.Password, $"Action {i + 1}: 'password'");

                            WithSecurityHints(cpu.Name, () => users.Create(name, password));

                            done.Add($"created '{name}'");
                            continue;
                        }

                        if (user == null)
                        {
                            throw new PortalException(PortalErrorCode.NotFound, $"Action {i + 1}: no OPC UA user '{name}'. Users: {string.Join(", ", users.Select(u => u.UserName))}.");
                        }

                        if (verb == "delete")
                        {
                            user.Delete();
                            done.Add($"deleted '{name}'");
                            continue;
                        }

                        user.SetPassword(Secret(action.Password, $"Action {i + 1}: 'password'"));
                        done.Add($"password of '{name}' set");
                    }

                    return done;
                },
                ("deviceItemPath", deviceItemPath));
        }

        public bool SetDisplayPassword(string deviceItemPath, string? password)
        {
            return Operation.Run(_logger, nameof(SetDisplayPassword), PortalErrorCode.InvalidState,
                () =>
                {
                    var cpu = RequireCpu(deviceItemPath);
                    var display = CpuFeature(cpu, i => i.GetService<DisplayProtection>())
                        ?? throw new PortalException(PortalErrorCode.NotSupported, $"CPU '{cpu.Name}' has no display with a password.");

                    var secret = Secret(password, "'password'");

                    return WithSecurityHints(cpu.Name, () =>
                    {
                        display.SetPassword(secret);

                        return true;
                    });
                },
                ("deviceItemPath", deviceItemPath));
        }
    }
}
