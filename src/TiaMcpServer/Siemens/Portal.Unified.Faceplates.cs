using Siemens.Engineering;
using Siemens.Engineering.HmiUnified;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Siemens
{
    // WinCC Unified: faceplate instances.
    //
    // Callers: the unified_manage_faceplate tool in McpServer.Unified.cs, registered unless the
    // server runs with '--read-only'. Affected API: ManageUnifiedFaceplate replaces
    // CreateHmiFaceplateInstance and ManageHmiUnifiedFaceplate. Reads and writes no data files;
    // changes stay in the open project until it is saved.
    //
    // Why a tool of its own, when unified_manage_items edits every other item: an ordinary item
    // has the properties of its class - every HmiButton has the same ones. A faceplate instance
    // is an HmiFaceplateContainer whose parameters are the interface of its faceplate TYPE, a
    // list that differs from type to type and from version to version. So the caller cannot
    // know the names up front; this operation takes them as given, reports the interface back,
    // and names the available properties when one is not found.

    public partial class Portal
    {
        private const string FaceplateContainerType = "HmiFaceplateContainer";

        private const string TagParameterDynamizationType = "TagParameterDynamization";

        /// <param name="action">'create', 'update' or 'upsert'.</param>
        /// <param name="faceplateType">
        /// The faceplate type and version as "V0.0.2\Name" (see GetLibraryTypes). Needed
        /// when the instance is created; on an update it switches the instance to that type.
        /// </param>
        /// <param name="properties">Properties of the container itself (Left, Top, Width, ...), as in ManageUnifiedItems.</param>
        /// <param name="interfaceValues">
        /// Interface properties of the faceplate type, by name: a plain value for a static
        /// value, or { "tag": "HmiTagName" } to bind the property to a tag.
        /// </param>
        public UnifiedFaceplateResult ManageUnifiedFaceplate(
            string softwarePath,
            string screenName,
            string itemName,
            string action,
            string? faceplateType,
            IDictionary<string, JsonElement>? properties,
            IDictionary<string, JsonElement>? interfaceValues)
        {
            return Operation.Run(_logger, nameof(ManageUnifiedFaceplate), PortalErrorCode.InvalidState,
                () =>
                {
                    var verb = (action ?? string.Empty).Trim().ToLowerInvariant();

                    if (verb != "create" && verb != "update" && verb != "upsert")
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Unknown action '{action}'. Use 'create', 'update' or 'upsert'. A faceplate instance is deleted with 'unified_manage_items'.");
                    }

                    if (string.IsNullOrWhiteSpace(itemName))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, "itemName is required.");
                    }

                    var software = RequireUnifiedSoftware(softwarePath);
                    dynamic screen = RequireUnifiedScreen(software, screenName);
                    object? item = FindUnifiedScreenItem(screen, itemName);
                    var created = false;

                    if (verb == "create" && item != null)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Item '{itemName}' already exists on screen '{screenName}'. Use 'update' or 'upsert'.");
                    }

                    if (verb == "update" && item == null)
                    {
                        throw new PortalException(PortalErrorCode.NotFound,
                            $"Item '{itemName}' not found on screen '{screenName}'. Use 'upsert' to create it.");
                    }

                    if (item == null)
                    {
                        if (string.IsNullOrWhiteSpace(faceplateType))
                        {
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                "faceplateType is required to create a faceplate instance, e.g. 'V0.0.2\\MyFaceplate'. 'get_library_types' lists the types with their 'ContainedType' values.");
                        }

                        item = CreateUnifiedScreenItem(screen, FaceplateContainerType, itemName);
                        created = true;
                    }
                    else if (!item.GetType().Name.Equals(FaceplateContainerType, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Item '{itemName}' is a {item.GetType().Name}, not a faceplate instance. Use 'unified_manage_items' for ordinary items.");
                    }

                    var applied = new List<string>();
                    var failed = new List<string>();

                    // The type comes first: the interface does not exist before it is set.
                    if (!string.IsNullOrWhiteSpace(faceplateType))
                    {
                        ((IEngineeringObject)item).SetAttribute("ContainedType", NormalizeFaceplateType(faceplateType!));
                        applied.Add("ContainedType");
                    }

                    if (properties != null)
                    {
                        foreach (var property in properties)
                        {
                            try
                            {
                                SetHmiItemProperty(software, item, property.Key, property.Value);
                                applied.Add(property.Key);
                            }
                            catch (Exception ex)
                            {
                                failed.Add($"{property.Key}: {ErrorText.Describe(ex)}");
                            }
                        }
                    }

                    if (interfaceValues != null && interfaceValues.Count > 0)
                    {
                        var members = FaceplateInterface(item);

                        foreach (var value in interfaceValues)
                        {
                            try
                            {
                                SetFaceplateInterfaceValue(software, members, value.Key, value.Value);
                                applied.Add("interface " + value.Key);
                            }
                            catch (Exception ex)
                            {
                                failed.Add($"interface {value.Key}: {ErrorText.Describe(ex)}");
                            }
                        }
                    }

                    if (failed.Count > 0)
                    {
                        // All or nothing, as in ManageUnifiedItems: after an Openness exception
                        // TIA Portal refuses to commit the transaction anyway.
                        var outcome = _inTransaction
                            ? "Nothing was changed: the call was rolled back."
                            : "TIA Portal granted no transaction for this call, so what succeeded remains applied: " +
                              (applied.Count == 0 ? "nothing" : string.Join(", ", applied)) + (created ? "; the instance was created" : string.Empty) + ".";

                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"{failed.Count} setting(s) of faceplate '{itemName}' failed. {outcome} {string.Join(" | ", failed)}");
                    }

                    return new UnifiedFaceplateResult
                    {
                        ScreenName = screenName,
                        ItemName = itemName,
                        Created = created,
                        FaceplateType = ((IEngineeringObject)item).GetAttribute("ContainedType")?.ToString(),
                        Applied = applied,
                        Interface = DescribeFaceplateInterface(item)
                    };
                },
                ("softwarePath", softwarePath), ("screenName", screenName), ("itemName", itemName), ("faceplateType", faceplateType));
        }

        /// <summary>
        /// Accepts "V0.0.2\Name" as the library lists it, and the same with a doubled backslash
        /// or a forward slash, which is how the value tends to arrive after a trip through JSON.
        /// </summary>
        internal static string NormalizeFaceplateType(string faceplateType)
        {
            var text = faceplateType.Trim().Replace('/', '\\');

            while (text.Contains("\\\\"))
            {
                text = text.Replace("\\\\", "\\");
            }

            return text;
        }

        private static List<object> FaceplateInterface(object item)
        {
            IEnumerable? members;

            try
            {
                members = ((dynamic)item).Interface as IEnumerable;
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
            {
                throw new PortalException(PortalErrorCode.NotSupported, $"{item.GetType().Name} has no faceplate interface.", null, ex);
            }

            return members == null ? new List<object>() : members.Cast<object>().ToList();
        }

        private static string? InterfaceName(object member)
        {
            return member.GetType().GetProperty("PropertyName")?.GetValue(member)?.ToString();
        }

        private void SetFaceplateInterfaceValue(HmiSoftware software, List<object> members, string name, JsonElement value)
        {
            var member = members.FirstOrDefault(m => string.Equals(InterfaceName(m), name, StringComparison.OrdinalIgnoreCase));

            if (member == null)
            {
                var names = members.Select(InterfaceName).Where(n => !string.IsNullOrEmpty(n)).ToList();

                throw new PortalException(PortalErrorCode.NotFound,
                    $"The faceplate has no interface property '{name}'. " +
                    (names.Count == 0
                        ? "Its interface is empty - check that the faceplate type is set."
                        : $"Available: {string.Join(", ", names)}."));
            }

            var propertyName = InterfaceName(member) ?? name;

            if (value.ValueKind == JsonValueKind.Object)
            {
                var keys = value.EnumerateObject().Select(p => p.Name).ToList();

                if (keys.Count != 1)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams,
                        "An interface property object needs exactly one of 'value', 'tag', 'script', 'tagParameter' or 'dynamization'.");
                }

                var payload = value.GetProperty(keys[0]);

                switch (keys[0].ToLowerInvariant())
                {
                    case "value":
                        value = payload;

                        break;

                    case "tag":
                        var tagName = RequireText(payload, "tag");

                        if (!software.Tags.Any(t => string.Equals(t.Name, tagName, StringComparison.OrdinalIgnoreCase)))
                        {
                            throw new PortalException(PortalErrorCode.NotFound,
                                $"HMI tag '{tagName}' does not exist. Use 'unified_get_tags' to list the tags.");
                        }

                        SetInterfaceDynamization(member, propertyName, TagDynamizationType, "Tag", tagName,
                            "If it is a tag interface, pass the tag name as a plain value instead.");

                        return;

                    case "script":
                        SetInterfaceDynamization(member, propertyName, ScriptDynamizationType, "ScriptCode", RequireText(payload, "script"),
                            "A tag interface takes no script: pass a tag name as a plain value.");

                        return;

                    case "tagparameter":
                        SetInterfaceDynamization(member, propertyName, TagParameterDynamizationType, "DynamicTagName", RequireText(payload, "tagParameter"),
                            "Only a tag interface takes a tag parameter; bind a property interface with 'tag' or 'script'.");

                        return;

                    case "dynamization":
                        if (!string.Equals(RequireText(payload, "dynamization"), "none", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new PortalException(PortalErrorCode.InvalidParams, "'dynamization' only takes \"none\".");
                        }

                        foreach (var existing in InterfaceDynamizations(member))
                        {
                            ((dynamic)existing).Delete();
                        }

                        return;

                    default:
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Unknown key '{keys[0]}'. Use 'value', 'tag', 'script', 'tagParameter' or 'dynamization'.");
                }
            }

            var valueProperty = member.GetType().GetProperty("Value")
                ?? throw new PortalException(PortalErrorCode.NotSupported, $"Interface property '{name}' has no settable value.");

            valueProperty.SetValue(member, ConvertHmiValue(value, valueProperty.GetValue(member)?.GetType(), name));
        }

        private static List<object> InterfaceDynamizations(object member)
        {
            return member.GetType().GetProperty("Dynamizations")?.GetValue(member) is IEnumerable dynamizations
                ? dynamizations.Cast<object>().ToList()
                : new List<object>();
        }

        /// <summary>
        /// Dynamizes an interface property. The dynamization is named after the property itself.
        /// Which kinds are allowed depends on the property: a tag interface takes only a tag
        /// parameter, a property interface a tag or a script. Openness does not tell the two apart
        /// up front, so a refusal is explained with the hint for the other kind.
        /// </summary>
        private static void SetInterfaceDynamization(object member, string propertyName, string dynamizationType, string valueProperty, string value, string hint)
        {
            try
            {
                SetHmiDynamization(member, propertyName, dynamizationType, valueProperty, value);
            }
            catch (Exception ex) when (!(ex is PortalException) && ErrorText.Describe(ex).IndexOf("not supported", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new PortalException(PortalErrorCode.NotSupported,
                    $"Interface property '{propertyName}' does not accept a {dynamizationType}. {hint}", null, ex);
            }
        }

        /// <summary>The interface of a faceplate instance: each property with its value and its binding.</summary>
        private static List<UnifiedFaceplateInterfaceProperty> DescribeFaceplateInterface(object item)
        {
            var result = new List<UnifiedFaceplateInterfaceProperty>();

            foreach (var member in FaceplateInterface(item))
            {
                var entry = new UnifiedFaceplateInterfaceProperty
                {
                    Name = InterfaceName(member),
                    Kind = member.GetType().Name
                };

                try
                {
                    var value = member.GetType().GetProperty("Value")?.GetValue(member);

                    entry.Value = value?.ToString();
                    entry.ValueType = value?.GetType().Name;
                }
                catch (Exception)
                {
                    // A property without a readable value is still worth listing by name.
                }

                foreach (var dynamization in InterfaceDynamizations(member))
                {
                    entry.Dynamization = dynamization.GetType().Name;

                    var type = dynamization.GetType();

                    entry.Tag = (type.GetProperty("Tag") ?? type.GetProperty("DynamicTagName"))?.GetValue(dynamization)?.ToString();
                    entry.Script = type.GetProperty("ScriptCode")?.GetValue(dynamization)?.ToString();
                }

                result.Add(entry);
            }

            return result;
        }
    }
}
