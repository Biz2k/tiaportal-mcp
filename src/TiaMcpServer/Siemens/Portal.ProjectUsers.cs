using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.Umac;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>A user or a user group of the project, without any secret.</summary>
    public class ProjectUserInfo
    {
        public string? Name { get; set; }

        /// <summary>user, group (a user group) or umc_user (a user of a UMC server).</summary>
        public string? Kind { get; set; }

        public bool Active { get; set; }

        public string? Authentication { get; set; }

        public string? Alias { get; set; }

        public string? Comment { get; set; }

        /// <summary>Minutes.</summary>
        public int? SessionTimeout { get; set; }

        public bool? RuntimeSessionTimeout { get; set; }

        public List<string> Roles { get; set; } = new List<string>();
    }

    public class ProjectRoleInfo
    {
        public string? Name { get; set; }

        public string? Identifier { get; set; }

        /// <summary>True for a role TIA Portal brings along; such a role cannot be changed.</summary>
        public bool System { get; set; }

        public string? Comment { get; set; }

        public int? SessionTimeout { get; set; }

        /// <summary>The identifiers of the function rights of the role, by device name.</summary>
        public Dictionary<string, List<string>>? Rights { get; set; }
    }

    public class DeviceRightInfo
    {
        public string? Identifier { get; set; }

        public string? Name { get; set; }

        public string? Group { get; set; }
    }

    public class PasswordPolicyInfo
    {
        public int MinimumLength { get; set; }

        public int MinimumNumericCharacters { get; set; }

        public int MinimumSpecialCharacters { get; set; }

        public bool UpperAndLowerCase { get; set; }

        public bool PasswordAging { get; set; }

        /// <summary>Days.</summary>
        public int PasswordValidity { get; set; }

        /// <summary>Days.</summary>
        public int PrewarningTime { get; set; }

        public int PasswordsBlockedForReuse { get; set; }
    }

    public class ProjectUsersInfo
    {
        public List<ProjectUserInfo> Users { get; set; } = new List<ProjectUserInfo>();

        public List<ProjectUserInfo> Groups { get; set; } = new List<ProjectUserInfo>();

        public List<ProjectRoleInfo> Roles { get; set; } = new List<ProjectRoleInfo>();

        public PasswordPolicyInfo? PasswordPolicy { get; set; }

        /// <summary>The devices that have function rights, with the number of them.</summary>
        public Dictionary<string, int> Devices { get; set; } = new Dictionary<string, int>();

        /// <summary>The function rights of the device asked for with 'rightsOfDevice'.</summary>
        public List<DeviceRightInfo>? AvailableRights { get; set; }
    }

    /// <summary>One change to the users or user groups of the project.</summary>
    public class ProjectUserAction
    {
        [System.ComponentModel.Description("'create', 'update' or 'delete'")]
        public string? Action { get; set; }

        [System.ComponentModel.Description("'user' (default) or 'group' (a user group; it has a name, roles and 'active' only)")]
        public string? Kind { get; set; }

        [System.ComponentModel.Description("Name of the user or group")]
        public string? Name { get; set; }

        [System.ComponentModel.Description("Password, as the user of this conversation gave it. Needed to create a user; on update it replaces the password")]
        public string? Password { get; set; }

        [System.ComponentModel.Description("New name")]
        public string? NewName { get; set; }

        public string? Comment { get; set; }

        [System.ComponentModel.Description("false deactivates the user or group, true activates. For the user 'Anonymous' this switches access without login on or off")]
        public bool? Active { get; set; }

        [System.ComponentModel.Description("Session timeout in minutes")]
        public int? SessionTimeout { get; set; }

        [System.ComponentModel.Description("Whether the session timeout applies in runtime")]
        public bool? RuntimeSessionTimeout { get; set; }

        public string? Alias { get; set; }

        [System.ComponentModel.Description("'Password' or 'Radius'")]
        public string? Authentication { get; set; }

        [System.ComponentModel.Description("The roles the user or group has afterwards - exactly these; names from 'sec_get_project_users'")]
        public List<string>? Roles { get; set; }

        [System.ComponentModel.Description("Roles to add to the present ones")]
        public List<string>? AddRoles { get; set; }

        [System.ComponentModel.Description("Roles to take away")]
        public List<string>? RemoveRoles { get; set; }

        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement>? Unknown { get; set; }
    }

    /// <summary>One change to the roles of the project.</summary>
    public class ProjectRoleAction
    {
        [System.ComponentModel.Description("'create', 'update' or 'delete'")]
        public string? Action { get; set; }

        [System.ComponentModel.Description("Name of the role")]
        public string? Name { get; set; }

        public string? NewName { get; set; }

        public string? Comment { get; set; }

        [System.ComponentModel.Description("Session timeout in minutes")]
        public int? SessionTimeout { get; set; }

        [System.ComponentModel.Description("Name of the device whose function rights are changed, e.g. 'S7-1500/ET200MP station_1'. Needed with addRights / removeRights; one device per action")]
        public string? Device { get; set; }

        [System.ComponentModel.Description("Identifiers of function rights of that device to give to the role, e.g. [\"WebReadTags\"]; 'sec_get_project_users' with rightsOfDevice lists them")]
        public List<string>? AddRights { get; set; }

        [System.ComponentModel.Description("Identifiers of function rights to take away")]
        public List<string>? RemoveRights { get; set; }

        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement>? Unknown { get; set; }
    }

    // Users, user groups, roles and the password policy of the project (Siemens.Engineering.Umac), and the protection
    // of blocks. Callers: the sec_* tools in McpServer.Security.cs.
    //
    // Found on TIA Portal V21 (probes of 2026-10-07 in rolled-back transactions):
    //   - Project.GetService<UmacConfigurator>() gives ProjectUsers (the user 'Anonymous' is one of them), CustomRoles,
    //     SystemRoles, UmcUserGroups (the "user groups" of the project tree) and UmcUsers.
    //     Project.GetService<PasswordPolicyConfigurator>() is the password policy; its limits are checked by TIA Portal
    //     ("Minimum length: [2] should be between 8 and 32").
    //   - SystemRoles.Find takes the IDENTIFIER ('PLCOperator'), not the name ('PLC user'); CustomRoles have name =
    //     identifier. RoleAssociation.Add(null) answers "argument 'item' ... is not supported", which reads like a
    //     refusal of the role - so roles are looked up by enumeration here and never passed as null.
    //   - Roles.Add of a role the user has already is refused ("has already been assigned"). Deleting a role that
    //     users have takes it away from them without a question.
    //   - Function rights belong to a device: Device.GetService<UmacDevice>().AvailableDeviceFunctionRights (a PLC with
    //     firmware V4: 28 rights - access level, web server, OPC UA; a Unified panel: 13; a SCALANCE: 2).
    //     CustomRole.AssignDeviceFunctionRight(device, right) / UnAssignDeviceFunctionRight; a system role is read-only.
    //   - A user with the role 'Engineering administrator' alone does not protect a project: it still opens without a
    //     login. Project.ProtectProject(administratorName, password) does - it makes that user with the role, and from
    //     then on the project opens only with credentials (Projects.Open(file, UmacDelegate)). It cannot be undone and
    //     not be done twice ("already know-how protected"). In a protected project the last holder of the role cannot
    //     lose it, and the logged-in user cannot delete or deactivate itself (tried on a throwaway project).
    //   - ProjectUsers.Create checks the password policy ("The password must contain at least 8 characters").
    //   - Know-how protection: PlcBlock.GetService<PlcBlockProtectionProvider>() - Protect(password),
    //     Unprotect(password); the password has to have 8 to 120 characters, a digit, a special character, lower and
    //     upper case. After a rejected password every further Unprotect in the same transaction is rejected too, even
    //     with the right one. Write protection: PlcBlockWriteProtectionProvider - Define(password) first, then
    //     Protect / Unprotect / Change(old, new); IsDefined, IsProtected.
    public partial class Portal
    {
        private const string AnonymousUserName = "Anonymous";

        private UmacConfigurator RequireUmac()
        {
            return RequireProject().GetService<UmacConfigurator>()
                   ?? throw new PortalException(PortalErrorCode.NotSupported, "This project gives no access to its users and roles.");
        }

        private static bool SameName(string? a, string? b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        private static List<Role> AllRoles(UmacConfigurator umac)
        {
            return umac.SystemRoles.Cast<Role>().Concat(umac.CustomRoles.Cast<Role>()).ToList();
        }

        private static Role RequireRole(UmacConfigurator umac, string? name, string where)
        {
            var roles = AllRoles(umac);

            return roles.FirstOrDefault(r => SameName(r.Name, name)) ?? roles.FirstOrDefault(r => SameName(r.Identifier, name))
                   ?? throw new PortalException(PortalErrorCode.NotFound, $"{where}: no role '{name}'. Roles: {string.Join(", ", roles.Select(r => r.Name))}.");
        }

        /// <summary>The devices of the project that take function rights, by name.</summary>
        private List<(Device Device, UmacDevice Umac)> UmacDevices()
        {
            var result = new List<(Device, UmacDevice)>();

            foreach (var device in GetDevicesUnlocked())
            {
                UmacDevice? umac = null;

                try
                {
                    umac = device.GetService<UmacDevice>();
                }
                catch (Exception)
                {
                    // a device without user management
                }

                if (umac != null)
                {
                    result.Add((device, umac));
                }
            }

            return result;
        }

        private static List<DeviceFunctionRight> RightsOfRole(Role role, UmacDevice device)
        {
            try
            {
                return role is CustomRole custom
                    ? custom.GetAssignedDeviceFunctionRights(device).ToList()
                    : ((SystemRole)role).GetAssignedSystemDeviceFunctionRights(device).Cast<DeviceFunctionRight>().ToList();
            }
            catch (Exception)
            {
                return new List<DeviceFunctionRight>();
            }
        }

        public ProjectUsersInfo GetProjectUsers(string? rightsOfDevice)
        {
            return Operation.Run(_logger, nameof(GetProjectUsers), PortalErrorCode.InvalidState,
                () =>
                {
                    var umac = RequireUmac();
                    var devices = UmacDevices();
                    var info = new ProjectUsersInfo();

                    foreach (var user in umac.ProjectUsers)
                    {
                        info.Users.Add(new ProjectUserInfo
                        {
                            Name = user.Name,
                            Kind = "user",
                            Active = user.IsActive,
                            Authentication = user.ProjectUserAuthenticationType.ToString(),
                            Alias = string.IsNullOrEmpty(user.AliasName) ? null : user.AliasName,
                            Comment = string.IsNullOrEmpty(user.Comment) ? null : user.Comment,
                            SessionTimeout = user.SessionTimeOut,
                            RuntimeSessionTimeout = user.IsRuntimeSessionTimeoutActive,
                            Roles = user.Roles.Select(r => r.Name).ToList()
                        });
                    }

                    foreach (var user in umac.UmcUsers)
                    {
                        info.Users.Add(new ProjectUserInfo { Name = user.Name, Kind = "umc_user", Active = user.IsActive, Roles = user.Roles.Select(r => r.Name).ToList() });
                    }

                    foreach (var group in umac.UmcUserGroups)
                    {
                        info.Groups.Add(new ProjectUserInfo { Name = group.Name, Kind = "group", Active = group.IsActive, Roles = group.Roles.Select(r => r.Name).ToList() });
                    }

                    foreach (var role in AllRoles(umac))
                    {
                        var custom = role as CustomRole;
                        var one = new ProjectRoleInfo
                        {
                            Name = role.Name,
                            Identifier = role.Identifier,
                            System = custom == null,
                            Comment = string.IsNullOrEmpty(custom?.Comment) ? null : custom!.Comment,
                            SessionTimeout = custom?.SessionTimeOut
                        };

                        foreach (var (device, umacDevice) in devices)
                        {
                            var rights = RightsOfRole(role, umacDevice);

                            if (rights.Count > 0)
                            {
                                one.Rights ??= new Dictionary<string, List<string>>();
                                one.Rights[device.Name] = rights.Select(r => r.Identifier).ToList();
                            }
                        }

                        info.Roles.Add(one);
                    }

                    foreach (var (device, umacDevice) in devices)
                    {
                        var count = umacDevice.AvailableDeviceFunctionRights.Count;

                        if (count > 0)
                        {
                            info.Devices[device.Name] = count;
                        }
                    }

                    var policy = RequireProject().GetService<PasswordPolicyConfigurator>();

                    if (policy != null)
                    {
                        info.PasswordPolicy = new PasswordPolicyInfo
                        {
                            MinimumLength = policy.MinimumLength,
                            MinimumNumericCharacters = policy.MinimumNumericCharacterLength,
                            MinimumSpecialCharacters = policy.MinimumSpecialCharacterLength,
                            UpperAndLowerCase = policy.IncludesLowerCaseAndUpperCaseCharacters,
                            PasswordAging = policy.EnablePasswordAging,
                            PasswordValidity = policy.PasswordValidity,
                            PrewarningTime = policy.PasswordValidityPrewarningTime,
                            PasswordsBlockedForReuse = policy.MinimumUserPasswordsBlockedForReuse
                        };
                    }

                    if (!string.IsNullOrWhiteSpace(rightsOfDevice))
                    {
                        var (_, umacDevice) = RequireUmacDevice(devices, rightsOfDevice, "rightsOfDevice");

                        info.AvailableRights = umacDevice.AvailableDeviceFunctionRights
                            .Select(r => new DeviceRightInfo { Identifier = r.Identifier, Name = r.Name, Group = string.IsNullOrEmpty(r.Group) ? null : r.Group })
                            .ToList();
                    }

                    return info;
                },
                ("rightsOfDevice", rightsOfDevice));
        }

        private static (Device Device, UmacDevice Umac) RequireUmacDevice(List<(Device Device, UmacDevice Umac)> devices, string? name, string where)
        {
            var found = devices.Where(d => SameName(d.Device.Name, name)).ToList();

            if (found.Count == 0)
            {
                // the name of the CPU or panel is what people know; the device around it has another one
                found = devices.Where(d => d.Device.DeviceItems.Any(i => SameName(i.Name, name))).ToList();
            }

            return found.Count == 1
                ? found[0]
                : throw new PortalException(PortalErrorCode.NotFound,
                    $"{where}: no device '{name}' with function rights. Devices: {string.Join(", ", devices.Where(d => d.Umac.AvailableDeviceFunctionRights.Count > 0).Select(d => d.Device.Name))}.");
        }

        private static string Verb(string? action, int index)
        {
            var verb = (action ?? string.Empty).Trim().ToLowerInvariant();

            return verb == "create" || verb == "update" || verb == "delete"
                ? verb
                : throw new PortalException(PortalErrorCode.InvalidParams, $"Action {index}: action '{action}' is not known. Use create, update or delete.");
        }

        private static void RefuseUnknown(Dictionary<string, System.Text.Json.JsonElement>? unknown, int index, string fields)
        {
            if (unknown != null && unknown.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Action {index}: unknown field(s) {string.Join(", ", unknown.Keys)}. The fields are: {fields}.");
            }
        }

        /// <summary>Brings the roles of a user or group to what the action asks for; returns the names afterwards.</summary>
        private static void ApplyRoles(UmacConfigurator umac, RoleAssociation roles, ProjectUserAction action, int index)
        {
            bool Has(Role role) => roles.Any(r => r.Identifier == role.Identifier);

            if (action.Roles != null)
            {
                var wanted = action.Roles.Select(n => RequireRole(umac, n, $"Action {index}")).ToList();

                foreach (var present in roles.ToList().Where(p => wanted.All(w => w.Identifier != p.Identifier)))
                {
                    roles.Remove(present);
                }

                foreach (var role in wanted.Where(w => !Has(w)))
                {
                    roles.Add(role);
                }
            }

            foreach (var name in action.AddRoles ?? new List<string>())
            {
                var role = RequireRole(umac, name, $"Action {index}");

                if (!Has(role))
                {
                    roles.Add(role);
                }
            }

            foreach (var name in action.RemoveRoles ?? new List<string>())
            {
                var role = RequireRole(umac, name, $"Action {index}");
                var present = roles.FirstOrDefault(r => r.Identifier == role.Identifier);

                if (present != null)
                {
                    roles.Remove(present);
                }
            }
        }

        public List<string> ManageProjectUsers(List<ProjectUserAction>? actions)
        {
            return Operation.Run(_logger, nameof(ManageProjectUsers), PortalErrorCode.InvalidState,
                () =>
                {
                    var umac = RequireUmac();

                    if (actions == null || actions.Count == 0)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            "No actions given. Example: [{\"action\": \"create\", \"name\": \"operator\", \"password\": \"...\", \"roles\": [\"HMI Operator\"]}].");
                    }

                    var done = new List<string>();

                    for (var i = 0; i < actions.Count; i++)
                    {
                        var action = actions[i] ?? throw new PortalException(PortalErrorCode.InvalidParams, $"Action {i + 1}: is empty.");

                        RefuseUnknown(action.Unknown, i + 1, "action, kind, name, password, newName, comment, active, sessionTimeout, runtimeSessionTimeout, alias, authentication, roles, addRoles, removeRoles");

                        var verb = Verb(action.Action, i + 1);
                        var kind = string.IsNullOrWhiteSpace(action.Kind) ? "user" : action.Kind!.Trim().ToLowerInvariant();
                        var name = string.IsNullOrWhiteSpace(action.Name)
                            ? throw new PortalException(PortalErrorCode.InvalidParams, $"Action {i + 1} ({verb}): 'name' is missing.")
                            : action.Name!.Trim();

                        Progress(i + 1, actions.Count, $"{verb} {kind} '{name}'");

                        if (kind == "group")
                        {
                            done.Add(ManageUserGroup(umac, action, verb, name, i + 1));
                            continue;
                        }

                        if (kind != "user")
                        {
                            throw new PortalException(PortalErrorCode.InvalidParams, $"Action {i + 1}: kind '{action.Kind}' is not known. Use user or group.");
                        }

                        var user = umac.ProjectUsers.FirstOrDefault(u => SameName(u.Name, name));

                        if (verb == "create")
                        {
                            if (user != null)
                            {
                                throw new PortalException(PortalErrorCode.InvalidParams, $"Action {i + 1}: user '{name}' exists already; use update.");
                            }

                            user = umac.ProjectUsers.Create(name, Secret(action.Password, $"Action {i + 1}: 'password'"));
                        }
                        else if (user == null)
                        {
                            throw new PortalException(PortalErrorCode.NotFound, $"Action {i + 1}: no user '{name}'. Users: {string.Join(", ", umac.ProjectUsers.Select(u => u.Name))}.");
                        }
                        else if (verb == "delete")
                        {
                            user.Delete();
                            done.Add($"deleted user '{name}'");
                            continue;
                        }
                        else if (!string.IsNullOrEmpty(action.Password))
                        {
                            user.SetPassword(Secret(action.Password, "'password'"));
                        }

                        if (action.Comment != null)
                        {
                            user.Comment = action.Comment;
                        }

                        if (action.SessionTimeout != null)
                        {
                            user.SessionTimeOut = action.SessionTimeout.Value;
                        }

                        if (action.RuntimeSessionTimeout == true)
                        {
                            user.ActivateRuntimeSessionTimeout();
                        }
                        else if (action.RuntimeSessionTimeout == false)
                        {
                            user.DeactivateRuntimeSessionTimeout();
                        }

                        if (action.Alias != null)
                        {
                            user.SetAliasName(action.Alias);
                        }

                        if (!string.IsNullOrWhiteSpace(action.Authentication))
                        {
                            user.ProjectUserAuthenticationType = Enum.TryParse<AuthenticationType>(action.Authentication!.Trim(), true, out var type)
                                ? type
                                : throw new PortalException(PortalErrorCode.InvalidParams, $"Action {i + 1}: authentication '{action.Authentication}' is not known. Use Password or Radius.");
                        }

                        ApplyRoles(umac, user.Roles, action, i + 1);

                        if (action.Active != null && action.Active.Value != user.IsActive)
                        {
                            var anonymous = SameName(user.Name, AnonymousUserName);

                            if (action.Active.Value)
                            {
                                if (anonymous) { umac.ActivateAnonymousUser(); } else { user.Activate(); }
                            }
                            else
                            {
                                if (anonymous) { umac.DeactivateAnonymousUser(); } else { user.Deactivate(); }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(action.NewName) && !string.Equals(action.NewName!.Trim(), user.Name, StringComparison.Ordinal))
                        {
                            user.SetName(action.NewName.Trim());
                        }

                        done.Add($"{(verb == "create" ? "created" : "updated")} user '{user.Name}' ({(user.IsActive ? "active" : "not active")}; roles: {(user.Roles.Count == 0 ? "none" : string.Join(", ", user.Roles.Select(r => r.Name)))})" +
                                 (verb == "update" && !string.IsNullOrEmpty(action.Password) ? " (password set)" : string.Empty));
                    }

                    return done;
                });
        }

        private static string ManageUserGroup(UmacConfigurator umac, ProjectUserAction action, string verb, string name, int index)
        {
            if (action.Password != null || action.Comment != null || action.SessionTimeout != null || action.RuntimeSessionTimeout != null || action.Alias != null || action.Authentication != null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Action {index}: a user group has a name, roles and 'active' only.");
            }

            var group = umac.UmcUserGroups.FirstOrDefault(g => SameName(g.Name, name));

            if (verb == "create")
            {
                if (group != null)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, $"Action {index}: user group '{name}' exists already; use update.");
                }

                group = umac.UmcUserGroups.CreateOfflineUmcUserGroup();
                group.SetName(name);
            }
            else if (group == null)
            {
                throw new PortalException(PortalErrorCode.NotFound, $"Action {index}: no user group '{name}'. Groups: {string.Join(", ", umac.UmcUserGroups.Select(g => g.Name))}.");
            }
            else if (verb == "delete")
            {
                group.Delete();

                return $"deleted group '{name}'";
            }

            ApplyRoles(umac, group.Roles, action, index);

            if (action.Active == true && !group.IsActive)
            {
                group.Activate();
            }
            else if (action.Active == false && group.IsActive)
            {
                group.Deactivate();
            }

            if (!string.IsNullOrWhiteSpace(action.NewName) && !string.Equals(action.NewName!.Trim(), group.Name, StringComparison.Ordinal))
            {
                group.SetName(action.NewName.Trim());
            }

            return $"{(verb == "create" ? "created" : "updated")} group '{group.Name}' ({(group.IsActive ? "active" : "not active")}; roles: {(group.Roles.Count == 0 ? "none" : string.Join(", ", group.Roles.Select(r => r.Name)))})";
        }

        public List<string> ManageProjectRoles(List<ProjectRoleAction>? actions)
        {
            return Operation.Run(_logger, nameof(ManageProjectRoles), PortalErrorCode.InvalidState,
                () =>
                {
                    var umac = RequireUmac();

                    if (actions == null || actions.Count == 0)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            "No actions given. Example: [{\"action\": \"create\", \"name\": \"Maintenance\", \"device\": \"S7-1500/ET200MP station_1\", \"addRights\": [\"WebAllowDiagnostics\"]}].");
                    }

                    var devices = UmacDevices();
                    var done = new List<string>();

                    for (var i = 0; i < actions.Count; i++)
                    {
                        var action = actions[i] ?? throw new PortalException(PortalErrorCode.InvalidParams, $"Action {i + 1}: is empty.");

                        RefuseUnknown(action.Unknown, i + 1, "action, name, newName, comment, sessionTimeout, device, addRights, removeRights");

                        var verb = Verb(action.Action, i + 1);
                        var name = string.IsNullOrWhiteSpace(action.Name)
                            ? throw new PortalException(PortalErrorCode.InvalidParams, $"Action {i + 1} ({verb}): 'name' is missing.")
                            : action.Name!.Trim();
                        var role = umac.CustomRoles.FirstOrDefault(r => SameName(r.Name, name));

                        Progress(i + 1, actions.Count, $"{verb} role '{name}'");

                        if (role == null && umac.SystemRoles.Any(r => SameName(r.Name, name) || SameName(r.Identifier, name)))
                        {
                            throw new PortalException(PortalErrorCode.NotSupported,
                                $"Action {i + 1}: '{name}' is a role TIA Portal brings along; it cannot be {(verb == "create" ? "created again" : "changed or deleted")}. Make a role of your own and give it the rights.");
                        }

                        if (verb == "create")
                        {
                            if (role != null)
                            {
                                throw new PortalException(PortalErrorCode.InvalidParams, $"Action {i + 1}: role '{name}' exists already; use update.");
                            }

                            role = umac.CustomRoles.Create(name, action.Comment ?? string.Empty);
                        }
                        else if (role == null)
                        {
                            throw new PortalException(PortalErrorCode.NotFound, $"Action {i + 1}: no role '{name}'. Roles of the project: {string.Join(", ", umac.CustomRoles.Select(r => r.Name))}.");
                        }
                        else if (verb == "delete")
                        {
                            var holders = umac.ProjectUsers.Where(u => u.Roles.Any(r => r.Identifier == role.Identifier)).Select(u => u.Name)
                                .Concat(umac.UmcUserGroups.Where(g => g.Roles.Any(r => r.Identifier == role.Identifier)).Select(g => g.Name)).ToList();

                            role.Delete();
                            done.Add($"deleted role '{name}'" + (holders.Count > 0 ? $" (taken away from: {string.Join(", ", holders)})" : string.Empty));
                            continue;
                        }
                        else if (action.Comment != null)
                        {
                            role.Comment = action.Comment;
                        }

                        if (action.SessionTimeout != null)
                        {
                            role.SessionTimeOut = action.SessionTimeout.Value;
                        }

                        var changes = string.Empty;
                        var add = action.AddRights ?? new List<string>();
                        var remove = action.RemoveRights ?? new List<string>();

                        if (add.Count + remove.Count > 0)
                        {
                            if (string.IsNullOrWhiteSpace(action.Device))
                            {
                                throw new PortalException(PortalErrorCode.InvalidParams, $"Action {i + 1}: 'device' is missing - a function right belongs to a device.");
                            }

                            var (device, umacDevice) = RequireUmacDevice(devices, action.Device, $"Action {i + 1}");
                            var available = umacDevice.AvailableDeviceFunctionRights.ToList();

                            DeviceFunctionRight Right(string id)
                            {
                                return available.FirstOrDefault(r => SameName(r.Identifier, id)) ?? available.FirstOrDefault(r => SameName(r.Name, id))
                                       ?? throw new PortalException(PortalErrorCode.NotFound,
                                           $"Action {i + 1}: device '{device.Name}' has no function right '{id}'. Its rights: {string.Join(", ", available.Select(r => r.Identifier))}.");
                            }

                            var present = role.GetAssignedDeviceFunctionRights(umacDevice).Select(r => r.Identifier).ToList();

                            foreach (var right in add.Select(Right).Where(r => !present.Contains(r.Identifier)))
                            {
                                role.AssignDeviceFunctionRight(umacDevice, right);
                            }

                            foreach (var right in remove.Select(Right).Where(r => present.Contains(r.Identifier)))
                            {
                                role.UnAssignDeviceFunctionRight(umacDevice, right);
                            }

                            var after = role.GetAssignedDeviceFunctionRights(umacDevice).Select(r => r.Identifier).ToList();

                            changes = $"; rights on '{device.Name}': {(after.Count == 0 ? "none" : string.Join(", ", after))}";
                        }

                        if (!string.IsNullOrWhiteSpace(action.NewName) && !string.Equals(action.NewName!.Trim(), role.Name, StringComparison.Ordinal))
                        {
                            role.SetName(action.NewName.Trim());
                        }

                        done.Add($"{(verb == "create" ? "created" : "updated")} role '{role.Name}'{changes}");
                    }

                    return done;
                });
        }

        /// <summary>Protects the project: from then on it opens only with a user and password. Cannot be undone.</summary>
        public List<string> ProtectProject(string? administratorName, string? password)
        {
            return Operation.Run(_logger, nameof(ProtectProject), PortalErrorCode.InvalidState,
                () =>
                {
                    var project = RequireProject();

                    if (string.IsNullOrWhiteSpace(administratorName))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, "'administratorName' is missing: the name of the user that becomes the administrator of the project.");
                    }

                    project.ProtectProject(administratorName!.Trim(), Secret(password, "'password'"));

                    return RequireUmac().ProjectUsers.Select(u => $"{u.Name}: {(u.Roles.Count == 0 ? "no roles" : string.Join(", ", u.Roles.Select(r => r.Name)))}").ToList();
                },
                ("administratorName", administratorName));
        }

        public (PasswordPolicyInfo Before, PasswordPolicyInfo After) SetPasswordPolicy(int? minimumLength, int? minimumNumericCharacters, int? minimumSpecialCharacters, bool? upperAndLowerCase,
            bool? passwordAging, int? passwordValidity, int? prewarningTime, int? passwordsBlockedForReuse)
        {
            return Operation.Run(_logger, nameof(SetPasswordPolicy), PortalErrorCode.InvalidState,
                () =>
                {
                    var policy = RequireProject().GetService<PasswordPolicyConfigurator>()
                                 ?? throw new PortalException(PortalErrorCode.NotSupported, "This project gives no access to its password policy.");

                    if (minimumLength == null && minimumNumericCharacters == null && minimumSpecialCharacters == null && upperAndLowerCase == null && passwordAging == null &&
                        passwordValidity == null && prewarningTime == null && passwordsBlockedForReuse == null)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, "Nothing to set: give at least one of the settings.");
                    }

                    PasswordPolicyInfo Read() => new PasswordPolicyInfo
                    {
                        MinimumLength = policy.MinimumLength,
                        MinimumNumericCharacters = policy.MinimumNumericCharacterLength,
                        MinimumSpecialCharacters = policy.MinimumSpecialCharacterLength,
                        UpperAndLowerCase = policy.IncludesLowerCaseAndUpperCaseCharacters,
                        PasswordAging = policy.EnablePasswordAging,
                        PasswordValidity = policy.PasswordValidity,
                        PrewarningTime = policy.PasswordValidityPrewarningTime,
                        PasswordsBlockedForReuse = policy.MinimumUserPasswordsBlockedForReuse
                    };

                    var before = Read();

                    if (minimumLength != null) { policy.MinimumLength = (short)minimumLength.Value; }
                    if (minimumNumericCharacters != null) { policy.MinimumNumericCharacterLength = (short)minimumNumericCharacters.Value; }
                    if (minimumSpecialCharacters != null) { policy.MinimumSpecialCharacterLength = (short)minimumSpecialCharacters.Value; }
                    if (upperAndLowerCase != null) { policy.IncludesLowerCaseAndUpperCaseCharacters = upperAndLowerCase.Value; }
                    if (passwordAging != null) { policy.EnablePasswordAging = passwordAging.Value; }
                    if (passwordValidity != null) { policy.PasswordValidity = (short)passwordValidity.Value; }
                    if (prewarningTime != null) { policy.PasswordValidityPrewarningTime = (short)prewarningTime.Value; }
                    if (passwordsBlockedForReuse != null) { policy.MinimumUserPasswordsBlockedForReuse = (short)passwordsBlockedForReuse.Value; }

                    return (before, Read());
                });
        }

        private static string BlockProtectionState(PlcBlock block)
        {
            string Attribute(string name)
            {
                try
                {
                    return block.GetAttribute(name)?.ToString() ?? "n/a";
                }
                catch (Exception)
                {
                    return "n/a";
                }
            }

            var write = block.GetService<PlcBlockWriteProtectionProvider>();

            return $"know-how protected: {Attribute("IsKnowHowProtected")}; write protected: {(write != null ? write.IsProtected.ToString() : Attribute("IsWriteProtected"))}" +
                   (write != null ? $"; write protection password defined: {write.IsDefined}" : string.Empty);
        }

        /// <param name="action">protect, unprotect (know-how); write_protect, write_unprotect, write_change_password; empty reads the state.</param>
        public (string Before, string After) SetBlockProtection(string softwarePath, string blockPath, string? action, string? password, string? newPassword)
        {
            return Operation.Run(_logger, nameof(SetBlockProtection), PortalErrorCode.InvalidState,
                () =>
                {
                    var block = GetBlock(softwarePath, blockPath)
                                ?? throw new PortalException(PortalErrorCode.NotFound, $"No block '{blockPath}' in '{softwarePath}'. 'plc_get_blocks' lists the blocks.");
                    var before = BlockProtectionState(block);
                    var verb = (action ?? string.Empty).Trim().ToLowerInvariant();

                    try
                    {
                        ChangeBlockProtection(block, verb, action, password, newPassword);
                    }
                    catch (Exception ex) when (ex is not PortalException && ErrorText.Describe(ex).IndexOf("password used was rejected", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"TIA Portal rejected the password for block '{block.Name}'. Nothing was changed. Ask the user for the password; do not guess. Seen on V21: right after a rejected password " +
                            "the next attempt is rejected too, even with the right one - wait some seconds before trying again.", null, ex);
                    }

                    return (before, BlockProtectionState(block));
                },
                ("softwarePath", softwarePath), ("blockPath", blockPath), ("action", action));
        }

        private static void ChangeBlockProtection(PlcBlock block, string verb, string? action, string? password, string? newPassword)
        {
            if (verb.StartsWith("write_", StringComparison.Ordinal))
            {
                var write = block.GetService<PlcBlockWriteProtectionProvider>()
                            ?? throw new PortalException(PortalErrorCode.NotSupported, $"Block '{block.Name}' takes no write protection.");

                switch (verb)
                {
                    case "write_protect":
                        if (!write.IsDefined)
                        {
                            write.Define(Secret(password, "'password'"));
                        }

                        write.Protect(Secret(password, "'password'"));
                        break;

                    case "write_unprotect":
                        write.Unprotect(Secret(password, "'password'"));
                        break;

                    case "write_change_password":
                        write.Change(Secret(password, "'password' (the present one)"), Secret(newPassword, "'newPassword'"));
                        break;

                    default:
                        throw new PortalException(PortalErrorCode.InvalidParams, $"action '{action}' is not known. Use protect, unprotect, write_protect, write_unprotect or write_change_password.");
                }
            }
            else
            {
                var knowHow = block.GetService<PlcBlockProtectionProvider>()
                              ?? throw new PortalException(PortalErrorCode.NotSupported, $"Block '{block.Name}' takes no know-how protection.");

                switch (verb)
                {
                    case "protect":
                        knowHow.Protect(Secret(password, "'password'"));
                        break;

                    case "unprotect":
                        knowHow.Unprotect(Secret(password, "'password'"));
                        break;

                    default:
                        throw new PortalException(PortalErrorCode.InvalidParams, $"action '{action}' is not known. Use protect, unprotect, write_protect, write_unprotect or write_change_password.");
                }
            }
        }
    }
}
