using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.HmiUnified.RuntimeSettings;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Siemens
{
    // WinCC Unified: runtime settings (HmiSoftware.RuntimeSettings) and system tags.
    //
    // Callers: the tools unified_get_runtime_settings, unified_set_runtime_settings and
    // unified_get_system_tags in McpServer.Unified.cs. Reads and writes no data files.
    //
    // RuntimeSettings is one object that holds simple properties (StartScreen, ScreenResolution, AutoLogOffURL ...) and
    // nested settings objects (OpcUaServerRuntimeSettings, MaxLoginRuntimeSettings ...) from the namespace
    // Siemens.Engineering.HmiUnified.RuntimeSettings, plus LanguageAndFonts, a list of the project languages. Both
    // directions walk the typed properties, so a setting Openness adds later needs no change here. A nested property is
    // written with a dotted name, a language with its name: "OpcUaServerRuntimeSettings.MaxSessionCount",
    // "LanguageAndFonts.en-US.Enable".
    public partial class Portal
    {
        private const string RuntimeSettingsNamespace = "Siemens.Engineering.HmiUnified.RuntimeSettings";

        public Dictionary<string, object?> GetUnifiedRuntimeSettings(string softwarePath)
        {
            return Operation.Run(_logger, nameof(GetUnifiedRuntimeSettings), PortalErrorCode.InvalidState,
                () => DumpRuntimeSettings(RequireUnifiedSoftware(softwarePath).RuntimeSettings),
                ("softwarePath", softwarePath));
        }

        /// <summary>Sets settings by dotted name; the first failure is thrown, so a call applies all of them or none. Returns the names set.</summary>
        public List<string> SetUnifiedRuntimeSettings(string softwarePath, IDictionary<string, JsonElement>? properties)
        {
            return Operation.Run(_logger, nameof(SetUnifiedRuntimeSettings), PortalErrorCode.InvalidState,
                () =>
                {
                    if (properties == null || properties.Count == 0)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            "No settings given. Pass e.g. { \"StartScreen\": \"Start\", \"OpcUaServerRuntimeSettings.MaxSessionCount\": 20 }; 'unified_get_runtime_settings' shows the names.");
                    }

                    var settings = RequireUnifiedSoftware(softwarePath).RuntimeSettings;
                    var applied = new List<string>();

                    // The start screen is stored whether it exists or not; the validation of the settings says so.
                    WithUnifiedValidation(settings, new List<string>(), () =>
                    {
                        foreach (var entry in properties)
                        {
                            applied.Add(SetRuntimeSetting(settings, entry.Key, entry.Value));
                        }
                    });

                    return applied;
                },
                ("softwarePath", softwarePath));
        }

        public List<Dictionary<string, object?>> GetUnifiedSystemTags(string softwarePath, string? nameFilter)
        {
            return Operation.Run(_logger, nameof(GetUnifiedSystemTags), PortalErrorCode.InvalidState,
                () =>
                {
                    System.Text.RegularExpressions.Regex? filter = null;

                    if (!string.IsNullOrWhiteSpace(nameFilter))
                    {
                        try
                        {
                            filter = new System.Text.RegularExpressions.Regex(nameFilter!, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        }
                        catch (ArgumentException ex)
                        {
                            throw new PortalException(PortalErrorCode.InvalidParams, $"nameFilter is not a regular expression: {ex.Message}");
                        }
                    }

                    return RequireUnifiedSoftware(softwarePath).SystemTags
                        .Where(t => filter == null || filter.IsMatch(t.Name))
                        .Select(t => new Dictionary<string, object?> { ["name"] = t.Name, ["dataType"] = t.DataType })
                        .ToList();
                },
                ("softwarePath", softwarePath));
        }

        private static bool IsSimpleSetting(Type type)
        {
            return type == typeof(string) || type.IsPrimitive || type.IsEnum;
        }

        private static bool IsNestedSetting(Type type)
        {
            return type.Namespace == RuntimeSettingsNamespace && type.IsClass && !typeof(IEnumerable).IsAssignableFrom(type);
        }

        private static Dictionary<string, object?> DumpRuntimeSettings(object settings)
        {
            var result = new Dictionary<string, object?>();
            var notAvailable = new List<string>();

            foreach (var property in settings.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name))
            {
                if (property.Name == "Parent" || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                var type = property.PropertyType;

                if (IsSimpleSetting(type))
                {
                    try
                    {
                        var value = property.GetValue(settings);
                        result[property.Name] = value is Enum ? value.ToString() : value;
                    }
                    catch (TargetInvocationException)
                    {
                        // Openness refuses settings the device version of this HMI does not have (checked 2026-10-06:
                        // HmiUnifiedTagSettings.TagOptimizationActive on a V21 RT_3). They are named, not fatal.
                        notAvailable.Add(property.Name);
                    }
                }
                else if (IsNestedSetting(type))
                {
                    var nested = property.GetValue(settings);

                    if (nested != null)
                    {
                        result[property.Name] = DumpRuntimeSettings(nested);
                    }
                }
                else if (type == typeof(HmiLanguageAndFontAssociation))
                {
                    var languages = new Dictionary<string, object?>();

                    foreach (HmiLanguageAndFont language in (HmiLanguageAndFontAssociation)property.GetValue(settings)!)
                    {
                        languages[language.Language] = DumpRuntimeSettings(language);
                    }

                    result[property.Name] = languages;
                }
            }

            if (notAvailable.Count > 0)
            {
                result["NotAvailable"] = notAvailable;
            }

            return result;
        }

        private string SetRuntimeSetting(HmiRuntimeSetting settings, string dottedName, JsonElement value)
        {
            // "LanguageAndFonts.en-US.Enable": the language name holds a '-' but no '.', so a plain split works.
            var parts = dottedName.Split('.');
            object current = settings;

            for (var i = 0; i < parts.Length - 1; i++)
            {
                current = Descend(current, parts[i], dottedName);
            }

            var last = parts[parts.Length - 1];

            var writable = current.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && IsSimpleSetting(p.PropertyType)).ToList();

            var property = FindTypedProperty(writable, last, $"'{string.Join(".", parts.Take(parts.Length - 1).DefaultIfEmpty("RuntimeSettings"))}'");

            try
            {
                SetTypedProperty(current, property, ConvertHmiValue(value, property.PropertyType, dottedName));
            }
            catch (global::Siemens.Engineering.EngineeringException ex)
            {
                // Openness gives no reason here. A setting can depend on another one: MaxLoginErrors is refused
                // until EnableLockAfterNumberOfAttempts is true (checked 2026-10-06, V21 RT_3).
                throw new PortalException(PortalErrorCode.InvalidState,
                    $"'{dottedName}' was refused with {JsonSerializer.Serialize(value)}: {ex.Message.Trim()} " +
                    "The value may be out of range, or the setting may depend on another one that is off (e.g. MaxLoginRuntimeSettings.MaxLoginErrors needs EnableLockAfterNumberOfAttempts); set that in the same call.", inner: ex);
            }

            return string.Join(".", parts.Take(parts.Length - 1).Concat(new[] { property.Name }));
        }

        private static object Descend(object current, string name, string dottedName)
        {
            if (current is HmiLanguageAndFontAssociation languages)
            {
                var found = languages.Cast<HmiLanguageAndFont>().FirstOrDefault(l => l.Language.Equals(name, StringComparison.OrdinalIgnoreCase));

                return found ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"'{dottedName}': no language '{name}'. Languages: {string.Join(", ", languages.Cast<HmiLanguageAndFont>().Select(l => l.Language))}.");
            }

            var nested = current.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != "Parent" && (IsNestedSetting(p.PropertyType) || p.PropertyType == typeof(HmiLanguageAndFontAssociation)))
                .ToList();

            var property = nested.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                           ?? throw new PortalException(PortalErrorCode.NotFound,
                               $"'{dottedName}': there is no settings group '{name}'. Groups: {string.Join(", ", nested.Select(p => p.Name))}.");

            return property.GetValue(current)
                   ?? throw new PortalException(PortalErrorCode.InvalidState, $"'{dottedName}': the settings group '{name}' is not available on this HMI.");
        }
    }
}
