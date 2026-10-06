using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.HmiUnified;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Siemens
{
    // WinCC Unified: batch editing of screen items - create, update, upsert, delete - with any
    // number of properties and event handlers per item.
    //
    // Callers: the unified_manage_items tool in McpServer.Unified.cs, registered unless the
    // server runs with '--read-only'. Affected API: ManageUnifiedItems supersedes the separate
    // create-item, delete-item, set-property, configure-item, set-event and trend-companion
    // operations, which were partial answers to the same question. Reads and writes no data
    // files; changes stay in the open project until it is saved.
    //
    // A property is given either a static value or a dynamization. The old tools mixed the two:
    // 'processValue' was a string that always became a tag binding, so a constant could not be
    // set through it and "42" meant "the tag named 42".
    //
    // Faceplates are not handled here. The properties of a faceplate instance are defined by
    // its faceplate type, not by the item class, so they have their own operation in
    // Portal.Unified.Faceplates.cs.
    public partial class Portal
    {
        private const string TagDynamizationType = "TagDynamization";

        private const string ScriptDynamizationType = "ScriptDynamization";

        private const string ResourceListDynamizationType = "ResourceListDynamization";

        public List<HmiItemResult> ManageUnifiedItems(string softwarePath, IList<HmiItemAction> actions)
        {
            return Operation.Run(_logger, nameof(ManageUnifiedItems), PortalErrorCode.InvalidState,
                () =>
                {
                    if (actions == null || actions.Count == 0)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            "No actions given. Pass at least one, e.g. { \"action\": \"update\", \"screenName\": \"Screen_1\", \"itemName\": \"Button_1\", \"properties\": { \"Left\": 100 } }.");
                    }

                    var software = RequireUnifiedSoftware(softwarePath);
                    var results = new List<HmiItemResult>();

                    foreach (var action in actions)
                    {
                        results.Add(ApplyHmiItemAction(software, action));
                    }

                    var failed = results.Where(r => r.Status != "success").ToList();

                    if (failed.Count > 0)
                    {
                        // All or nothing. It cannot be otherwise inside a transaction: once
                        // Openness has thrown, TIA Portal refuses to commit ("Commit of a
                        // Transaction is not allowed after an exception is thrown"), so a
                        // partly applied batch does not exist. Throwing here is what rolls it
                        // back; returning the list would end in that commit error instead.
                        var outcome = _inTransaction
                            ? "Nothing was changed: the whole batch was rolled back."
                            : "TIA Portal granted no transaction for this call, so the actions that succeeded remain applied: " +
                              (results.Count == failed.Count ? "none" : string.Join(", ", results.Where(r => r.Status == "success").Select(DescribeHmiAction))) + ".";

                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"{failed.Count} of {results.Count} action(s) failed. {outcome} " +
                            string.Join(" | ", failed.Select(r => $"{DescribeHmiAction(r)}: {r.Error}")));
                    }

                    return results;
                },
                ("softwarePath", softwarePath));
        }

        private static string DescribeHmiAction(HmiItemResult result)
        {
            return string.IsNullOrEmpty(result.ItemName)
                ? $"{result.Action} screen '{result.ScreenName}'"
                : $"{result.Action} '{result.ScreenName}/{result.ItemName}'";
        }

        private HmiItemResult ApplyHmiItemAction(HmiSoftware software, HmiItemAction action)
        {
            var result = new HmiItemResult
            {
                Action = action.Action,
                ScreenName = action.ScreenName,
                ItemName = action.ItemName,
                Applied = new List<string>(),
                Failed = new List<HmiPropertyFailure>(),
                Notes = new List<string>()
            };

            try
            {
                var verb = (action.Action ?? string.Empty).Trim().ToLowerInvariant();

                if (verb != "create" && verb != "update" && verb != "upsert" && verb != "delete")
                {
                    throw new PortalException(PortalErrorCode.InvalidParams,
                        $"Unknown action '{action.Action}'. Use 'create', 'update', 'upsert' or 'delete'.");
                }

                if (string.IsNullOrWhiteSpace(action.ScreenName))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "screenName is required.");
                }

                dynamic screen = FindUnifiedScreen(software, action.ScreenName!)
                    ?? throw new PortalException(PortalErrorCode.NotFound,
                        $"Screen '{action.ScreenName}' not found. Use 'unified_get_screens' to list the screens.");

                object target;

                if (string.IsNullOrWhiteSpace(action.ItemName))
                {
                    // No item name: the properties are meant for the screen itself.
                    if (verb != "update")
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"itemName is required for '{verb}'. Leave it empty only with 'update', to set properties of the screen itself.");
                    }

                    target = screen;
                }
                else
                {
                    object? item = FindUnifiedScreenItem(screen, action.ItemName!);

                    if (verb == "delete")
                    {
                        if (item == null)
                        {
                            throw new PortalException(PortalErrorCode.NotFound,
                                $"Item '{action.ItemName}' not found on screen '{action.ScreenName}'.");
                        }

                        ((dynamic)item).Delete();
                        result.Status = "success";

                        return result;
                    }

                    if (verb == "create" && item != null)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Item '{action.ItemName}' already exists on screen '{action.ScreenName}'. Use 'update' or 'upsert'.");
                    }

                    if (verb == "update" && item == null)
                    {
                        throw new PortalException(PortalErrorCode.NotFound,
                            $"Item '{action.ItemName}' not found on screen '{action.ScreenName}'. Use 'unified_get_screen_items' to list the items, or 'upsert' to create it.");
                    }

                    if (item == null)
                    {
                        item = CreateUnifiedScreenItem(screen, action.ItemType, action.ItemName!);
                        result.Notes.Add($"Created as {item.GetType().Name}.");
                    }

                    target = item;
                }

                var findingsBefore = ReadUnifiedFindings(target);

                if (action.Properties != null)
                {
                    foreach (var property in action.Properties)
                    {
                        try
                        {
                            var note = SetHmiItemProperty(software, target, property.Key, property.Value);

                            result.Applied.Add(property.Key);

                            if (note != null)
                            {
                                result.Notes.Add(note);
                            }
                        }
                        catch (Exception ex)
                        {
                            // One property that does not take must not hide the ones that did.
                            result.Failed.Add(new HmiPropertyFailure { Property = property.Key, Error = ErrorText.Describe(ex) });
                        }
                    }
                }

                if (action.Events != null)
                {
                    foreach (var handler in action.Events)
                    {
                        var label = "event " + handler.Key;

                        try
                        {
                            var spec = UnifiedDynamizationSpec.ParseEvent(handler.Value, handler.Key);

                            SetUnifiedEventHandler(target, handler.Key, spec);
                            result.Applied.Add(label);

                            if (spec.GlobalDefinitions != null)
                            {
                                result.Notes.Add("The global definitions area is one for all events of the screen (the script dynamizations have another one): it now holds this code for every event.");
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Failed.Add(new HmiPropertyFailure { Property = label, Error = ErrorText.Describe(ex) });
                        }
                    }
                }

                if (action.PropertyEvents != null)
                {
                    foreach (var handler in action.PropertyEvents)
                    {
                        var label = "propertyEvent " + handler.Key;

                        try
                        {
                            var spec = UnifiedDynamizationSpec.ParseEvent(handler.Value, handler.Key);

                            SetPropertyEventHandler(target, handler.Key, spec);
                            result.Applied.Add(label);

                            if (spec.GlobalDefinitions != null)
                            {
                                result.Notes.Add("The global definitions area is one for all events of the screen (the script dynamizations have another one): it now holds this code for every event.");
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Failed.Add(new HmiPropertyFailure { Property = label, Error = ErrorText.Describe(ex) });
                        }
                    }
                }

                // TIA Portal's own validation of the item: what Openness stored without a word (a screen, a graphic, a
                // list or a cycle that does not exist, a script that no tag triggers) is named there. Not after a
                // failure: the batch is lost then anyway.
                if (result.Failed.Count == 0)
                {
                    var verdict = UnifiedValidation.Judge(findingsBefore, ReadUnifiedFindings(target));

                    result.Notes.AddRange(verdict.Notes);

                    foreach (var error in verdict.Errors)
                    {
                        var hint = error.IndexOf("No tag configured", StringComparison.OrdinalIgnoreCase) >= 0
                            ? " A script dynamization runs when a tag it reads changes (trigger 'AutomaticTags'); this script reads none. Give it a \"trigger\", e.g. \"T1s\" or {\"type\": \"Tags\", \"tags\": [...]}."
                            : string.Empty;

                        result.Failed.Add(new HmiPropertyFailure { Property = "validation", Error = "TIA Portal's validation rejects the result (Openness stored the value without an error): " + error + hint });
                    }
                }

                result.Status = result.Failed.Count == 0 ? "success" : "error";

                if (result.Failed.Count > 0)
                {
                    var asked = (action.Properties?.Count ?? 0) + (action.Events?.Count ?? 0) + (action.PropertyEvents?.Count ?? 0);

                    result.Error = $"{result.Failed.Count} of {asked} settings were not applied: " +
                                   string.Join("; ", result.Failed.Select(f => $"{f.Property}: {f.Error}"));
                }
            }
            catch (Exception ex)
            {
                result.Status = "error";
                result.Error = ErrorText.Describe(ex);
                _logger?.LogWarning(ex, "HMI item action {Action} on {Screen}/{Item} failed", action.Action, action.ScreenName, action.ItemName);
            }

            return result;
        }

        /// <summary>
        /// Gives an event of an item (or screen) a script, replacing the script it had. An
        /// empty script removes the handler. Which events exist depends on the item type; the
        /// error lists them.
        /// </summary>
        private static void SetUnifiedEventHandler(object target, string eventName, UnifiedEventSpec spec)
        {
            object handlers;

            try
            {
                handlers = ((dynamic)target).EventHandlers;
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
            {
                throw new PortalException(PortalErrorCode.NotSupported, $"{target.GetType().Name} has no events.", null, ex);
            }

            var handlersType = handlers.GetType();

            var create = handlersType.GetMethod("Create")
                ?? throw new PortalException(PortalErrorCode.NotSupported, $"Events of {target.GetType().Name} cannot be set through Openness.");

            var eventType = create.GetParameters()[0].ParameterType;

            // Checked against the names first: Enum.Parse would throw, and inside a transaction
            // a thrown-and-caught exception is not free.
            var match = Enum.GetNames(eventType).FirstOrDefault(n => n.Equals(eventName, StringComparison.OrdinalIgnoreCase))
                ?? throw new PortalException(PortalErrorCode.InvalidParams,
                    $"{target.GetType().Name} has no event '{eventName}'. Available: {string.Join(", ", Enum.GetNames(eventType))}.");

            var value = Enum.Parse(eventType, match);
            var handler = handlersType.GetMethod("Find")?.Invoke(handlers, new[] { value });

            if (spec.Script == null)
            {
                if (handler != null)
                {
                    ((dynamic)handler).Delete();
                }

                return;
            }

            handler ??= create.Invoke(handlers, new[] { value })
                ?? throw new PortalException(PortalErrorCode.CreateFailed, $"Creating the '{match}' event handler returned nothing.");

            ApplyEventScript(((dynamic)handler).Script, spec);
        }

        /// <summary>The script of an event or of a change of a property; Async and the global definitions only when given.</summary>
        private static void ApplyEventScript(dynamic script, UnifiedEventSpec spec)
        {
            script.ScriptCode = spec.Script;

            if (spec.Async != null)
            {
                script.Async = spec.Async.Value;
            }

            if (spec.GlobalDefinitions != null)
            {
                script.GlobalDefinitionAreaScriptCode = spec.GlobalDefinitions;
            }
        }

        private static object CreateUnifiedScreenItem(dynamic screen, string? itemType, string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemType))
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"itemType is required to create '{itemName}', e.g. 'HmiButton', 'HmiIOField' or 'HmiTextBox'.");
            }

            object items = screen.ScreenItems;
            var itemsType = items.GetType();

            var create = itemsType.GetMethods().FirstOrDefault(m => m.Name == "Create" && m.IsGenericMethod && m.GetParameters().Length == 1)
                ?? throw new PortalException(PortalErrorCode.NotSupported, "This screen does not allow creating items through Openness.");

            // What the collection enumerates is the base class of everything that may sit on a
            // screen; only its subclasses are offered as item types.
            var baseType = itemsType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                .Select(i => i.GetGenericArguments()[0])
                .FirstOrDefault(t => t != typeof(object))
                ?? create.GetGenericArguments()[0].GetGenericParameterConstraints().FirstOrDefault();

            var candidates = itemsType.Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic && (baseType == null || baseType.IsAssignableFrom(t)))
                // The '...Base' classes are not abstract in the API, but nothing can be created from them.
                .Where(t => !t.Name.EndsWith("Base", StringComparison.Ordinal))
                .ToList();

            var type = candidates.FirstOrDefault(t => t.Name.Equals(itemType, StringComparison.OrdinalIgnoreCase));

            if (type == null)
            {
                var names = candidates.Select(t => t.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"Unknown item type '{itemType}'. Available: {string.Join(", ", names.Take(60))}{(names.Count > 60 ? ", ..." : string.Empty)}.");
            }

            return create.MakeGenericMethod(type).Invoke(items, new object[] { itemName })
                ?? throw new PortalException(PortalErrorCode.CreateFailed, $"Creating '{itemName}' returned nothing.");
        }

        /// <summary>
        /// Sets one property. <paramref name="value"/> is a plain JSON value for a static value, or
        /// an object naming what to do: { "value": x }, { "tag": "name" }, { "script": "code" },
        /// { "texts": { "en-US": "..." } } or { "dynamization": "none" }.
        /// </summary>
        /// <returns>A note worth passing on to the caller, or null.</returns>
        private string? SetHmiItemProperty(HmiSoftware software, object target, string propertyName, JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                return SetStaticHmiProperty(target, propertyName, value);
            }

            var keys = value.EnumerateObject().Select(p => p.Name).ToList();

            if (keys.Any(k => k.Equals("resourceList", StringComparison.OrdinalIgnoreCase)))
            {
                return SetResourceListDynamization(software, target, propertyName, value);
            }

            // 'tag' and 'script' with options beside them, 'expression' and 'flashing': Portal.Unified.Dynamizations.cs.
            var mainKey = UnifiedDynamizationSpec.FindMainKey(keys);

            if (mainKey != null && (keys.Count > 1 || mainKey.Equals("expression", StringComparison.OrdinalIgnoreCase) || mainKey.Equals("flashing", StringComparison.OrdinalIgnoreCase)))
            {
                return SetHmiDynamizationWithOptions(software, target, propertyName, value, mainKey);
            }

            if (keys.Count != 1)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    "A property object needs exactly one of 'value', 'tag', 'script', 'texts', 'expression', 'flashing' or 'dynamization' (a tag or script may have options beside it), or 'resourceList' with 'tag'; got: " +
                    (keys.Count == 0 ? "none" : string.Join(", ", keys)) + ".");
            }

            var payload = value.GetProperty(keys[0]);

            switch (keys[0].ToLowerInvariant())
            {
                case "value":
                    return SetStaticHmiProperty(target, propertyName, payload);

                case "texts":
                    return SetStaticHmiProperty(target, propertyName, payload);

                case "tag":
                    var tagName = RequireText(payload, "tag");

                    if (!software.Tags.Any(t => string.Equals(t.Name, tagName, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new PortalException(PortalErrorCode.NotFound,
                            $"HMI tag '{tagName}' does not exist. Use 'unified_get_tags' to list the tags.");
                    }

                    SetHmiDynamization(target, ResolveHmiPropertyName(target, propertyName), TagDynamizationType, "Tag", tagName);

                    return null;

                case "script":
                    SetHmiDynamization(target, ResolveHmiPropertyName(target, propertyName), ScriptDynamizationType, "ScriptCode", RequireText(payload, "script"));

                    return null;

                case "dynamization":
                    if (!string.Equals(RequireText(payload, "dynamization"), "none", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            "'dynamization' only takes \"none\", which removes the dynamization of the property. Use 'tag' or 'script' to add one.");
                    }

                    return RemoveHmiDynamization(target, ResolveHmiPropertyName(target, propertyName))
                        ? null
                        : $"{propertyName} had no dynamization to remove.";

                default:
                    throw new PortalException(PortalErrorCode.InvalidParams,
                        $"Unknown key '{keys[0]}'. Use 'value', 'tag', 'script', 'texts', 'expression', 'flashing', 'resourceList' with 'tag', or 'dynamization'.");
            }
        }

        /// <summary>
        /// { "resourceList": "list", "tag": "tag" }: the property shows the entry of a text or
        /// graphic list that matches the value of the tag.
        /// </summary>
        private string? SetResourceListDynamization(HmiSoftware software, object target, string propertyName, JsonElement value)
        {
            string? listName = null;
            string? tagName = null;

            foreach (var part in value.EnumerateObject())
            {
                if (part.Name.Equals("resourceList", StringComparison.OrdinalIgnoreCase))
                {
                    listName = RequireText(part.Value, "resourceList");
                }
                else if (part.Name.Equals("tag", StringComparison.OrdinalIgnoreCase))
                {
                    tagName = RequireText(part.Value, "tag");
                }
                else
                {
                    throw new PortalException(PortalErrorCode.InvalidParams,
                        $"Unknown key '{part.Name}' beside 'resourceList'. A list binding is {{ \"resourceList\": \"ListName\", \"tag\": \"HmiTag\" }}.");
                }
            }

            if (tagName == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    "'resourceList' needs 'tag', the HMI tag whose value selects the entry: { \"resourceList\": \"ListName\", \"tag\": \"HmiTag\" }.");
            }

            if (software.Tags.Find(tagName) == null)
            {
                throw new PortalException(PortalErrorCode.NotFound,
                    $"HMI tag '{tagName}' does not exist. Use 'unified_get_tags' to list the tags.");
            }

            var name = ResolveHmiPropertyName(target, propertyName);

            SetHmiDynamization(target, name, ResourceListDynamizationType, "ResourceList", listName!);
            SetHmiDynamization(target, name, ResourceListDynamizationType, "Tag", tagName);

            // Openness accepts any list name. Whether the list exists - among the lists of the HMI or as a library
            // type, named "<type> V <version>" - is for the validation of the item to say, after the writes.
            return null;
        }

        private static string RequireText(JsonElement element, string key)
        {
            if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{key}' must be a non-empty string.");
            }

            return element.GetString()!;
        }

        private string? SetStaticHmiProperty(object target, string propertyName, JsonElement value)
        {
            var name = ResolveHmiPropertyName(target, propertyName);
            var engineeringObject = (IEngineeringObject)target;

            // No try/catch around this read: inside a transaction a swallowed Openness exception
            // still forbids the commit, so a failure has to surface as a failure.
            var current = engineeringObject.GetAttribute(name);

            if (current is MultilingualText text)
            {
                SetMultilingualText(text, value);
            }
            else
            {
                engineeringObject.SetAttribute(name, ConvertHmiValue(value, current?.GetType(), name));
            }

            return FindHmiDynamization(target, name) != null
                ? $"{name} also has a dynamization, which takes precedence over the static value at runtime. Remove it with {{ \"dynamization\": \"none\" }} if the static value should apply."
                : null;
        }

        /// <summary>The attribute name as TIA Portal spells it, so callers need not match its case.</summary>
        private static string ResolveHmiPropertyName(object target, string propertyName)
        {
            var names = ((IEngineeringObject)target).GetAttributeInfos().Select(a => a.Name).ToList();

            var match = names.FirstOrDefault(n => n.Equals(propertyName, StringComparison.Ordinal))
                        ?? names.FirstOrDefault(n => n.Equals(propertyName, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                return match;
            }

            // Cheap "did you mean": one name contains the other, or they start alike.
            var stem = propertyName.Length >= 4 ? propertyName.Substring(0, 4) : propertyName;

            var close = names
                .Where(n => n.IndexOf(propertyName, StringComparison.OrdinalIgnoreCase) >= 0
                            || propertyName.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0
                            || n.StartsWith(stem, StringComparison.OrdinalIgnoreCase))
                .Take(8)
                .ToList();

            throw new PortalException(PortalErrorCode.NotFound,
                $"{target.GetType().Name} has no property '{propertyName}'. " +
                (close.Count > 0 ? $"Similar: {string.Join(", ", close)}. " : string.Empty) +
                "Use 'unified_get_screen_item_properties' to list them.");
        }

        /// <summary>
        /// A string sets every language of the text, so the text shows whatever the runtime
        /// language is; an object { "en-US": "...", "de-DE": "..." } sets just those languages.
        /// </summary>
        private static void SetMultilingualText(MultilingualText text, JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                foreach (var item in text.Items)
                {
                    item.Text = FormatUnifiedText(value.GetString() ?? string.Empty, item.Text);
                }

                return;
            }

            if (value.ValueKind != JsonValueKind.Object)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    "A text property takes a string, or { \"texts\": { \"en-US\": \"...\" } } to set single languages.");
            }

            foreach (var entry in value.EnumerateObject())
            {
                var item = text.Items.FirstOrDefault(i => string.Equals(i.Language?.Culture?.Name, entry.Name, StringComparison.OrdinalIgnoreCase))
                    ?? throw new PortalException(PortalErrorCode.NotFound,
                        $"The project has no language '{entry.Name}'. Available: {string.Join(", ", text.Items.Select(i => i.Language?.Culture?.Name))}.");

                item.Text = FormatUnifiedText(entry.Value.GetString() ?? string.Empty, item.Text);
            }
        }

        /// <summary>
        /// WinCC Unified stores the texts shown on items as a small HTML document -
        /// "&lt;body&gt;&lt;p&gt;Start&lt;/p&gt;&lt;/body&gt;" - and rejects a bare string with "The argument 'text' has an
        /// invalid format". A caller should not have to know that, so plain text is wrapped. Text
        /// that already is such a document passes through, as does text for a property that
        /// holds plain text (its current value shows which kind it is).
        /// </summary>
        internal static string FormatUnifiedText(string text, string? current)
        {
            if (text.TrimStart().StartsWith("<body", StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }

            var currentIsPlain = !string.IsNullOrEmpty(current)
                                 && !current!.TrimStart().StartsWith("<body", StringComparison.OrdinalIgnoreCase);

            if (currentIsPlain)
            {
                return text;
            }

            var paragraphs = text.Replace("\r\n", "\n").Split('\n')
                .Select(line => "<p>" + System.Security.SecurityElement.Escape(line) + "</p>");

            return "<body>" + string.Join(string.Empty, paragraphs) + "</body>";
        }

        /// <summary>
        /// Converts a JSON value to what the attribute holds. The current value of the attribute
        /// tells the type, because Openness rejects an int where it wants a uint or an enum.
        /// </summary>
        internal static object? ConvertHmiValue(JsonElement value, Type? targetType, string propertyName)
        {
            if (value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (targetType != null)
            {
                try
                {
                    if (targetType.IsEnum)
                    {
                        return value.ValueKind == JsonValueKind.Number
                            ? Enum.ToObject(targetType, value.GetInt64())
                            : Enum.Parse(targetType, value.GetString() ?? string.Empty, true);
                    }

                    if (targetType == typeof(System.Drawing.Color))
                    {
                        return ParseHmiColor(value.GetString() ?? string.Empty);
                    }

                    if (targetType == typeof(DateTime))
                    {
                        return DateTime.Parse(value.GetString() ?? string.Empty, CultureInfo.InvariantCulture, DateTimeStyles.None);
                    }

                    if (targetType == typeof(TimeSpan))
                    {
                        // "hh:mm:ss" (or "d.hh:mm:ss"), or a number of seconds.
                        return value.ValueKind == JsonValueKind.Number
                            ? TimeSpan.FromSeconds(value.GetDouble())
                            : TimeSpan.Parse(value.GetString() ?? string.Empty, CultureInfo.InvariantCulture);
                    }

                    if (targetType == typeof(bool))
                    {
                        return value.ValueKind == JsonValueKind.String ? bool.Parse(value.GetString()!) : value.GetBoolean();
                    }

                    if (targetType == typeof(string))
                    {
                        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
                    }

                    if (targetType.IsPrimitive || targetType == typeof(decimal))
                    {
                        var number = value.ValueKind == JsonValueKind.String
                            ? double.Parse(value.GetString()!, CultureInfo.InvariantCulture)
                            : value.GetDouble();

                        return Convert.ChangeType(number, targetType, CultureInfo.InvariantCulture);
                    }
                }
                catch (Exception ex) when (ex is not PortalException)
                {
                    var expected = targetType.IsEnum
                        ? $"one of {string.Join(", ", Enum.GetNames(targetType))}"
                        : targetType == typeof(System.Drawing.Color) ? "a color such as \"#FF0000\" or \"Red\""
                        : targetType == typeof(DateTime) ? "an ISO date and time such as \"2026-01-02T03:04:05\""
                        : targetType == typeof(TimeSpan) ? "a time such as \"00:00:30\" or a number of seconds" : $"a {targetType.Name}";

                    _ = ex; // the reason is fully stated below; the parser's own text adds nothing

                    throw new PortalException(PortalErrorCode.InvalidParams,
                        $"{propertyName} takes {expected}; got {value.GetRawText()}.");
                }
            }

            // The attribute could not be read, or holds a type with no obvious JSON form: let
            // the JSON value speak for itself and leave the verdict to TIA Portal.
            switch (value.ValueKind)
            {
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return value.GetBoolean();

                case JsonValueKind.Number:
                    // Two returns, not a conditional expression: '?:' would widen the int to a
                    // double and a whole number would arrive as 42.0.
                    if (value.TryGetInt32(out var integer))
                    {
                        return integer;
                    }

                    return value.GetDouble();

                case JsonValueKind.String:
                    var text = value.GetString() ?? string.Empty;

                    return text.Length is 7 or 9 && text[0] == '#' ? ParseHmiColor(text) : text;

                default:
                    throw new PortalException(PortalErrorCode.InvalidParams,
                        $"{propertyName}: {value.GetRawText()} is not a value this tool can set.");
            }
        }

        private static System.Drawing.Color ParseHmiColor(string text)
        {
            var color = text.StartsWith("#", StringComparison.Ordinal)
                ? System.Drawing.ColorTranslator.FromHtml(text)
                : System.Drawing.Color.FromName(text);

            if (!text.StartsWith("#", StringComparison.Ordinal) && !color.IsKnownColor)
            {
                throw new FormatException($"'{text}' is not a color name.");
            }

            return color;
        }

        private static object? FindHmiDynamization(object target, string propertyName)
        {
            try
            {
                foreach (var dynamization in ((dynamic)target).Dynamizations)
                {
                    if (string.Equals((string)dynamization.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        return dynamization;
                    }
                }
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {
                // This object has no dynamizations at all.
            }

            return null;
        }

        private static bool RemoveHmiDynamization(object target, string propertyName)
        {
            var existing = FindHmiDynamization(target, propertyName);

            if (existing == null)
            {
                return false;
            }

            ((dynamic)existing).Delete();

            return true;
        }

        /// <summary>
        /// Gives a property a dynamization of the given kind and sets its one defining value. An
        /// existing dynamization of the same kind is updated; one of another kind is replaced,
        /// because a property holds at most one.
        /// </summary>
        private static object SetHmiDynamization(object target, string propertyName, string dynamizationType, string valueProperty, string value)
        {
            var dynamization = GetOrCreateHmiDynamization(target, propertyName, dynamizationType);

            var setter = dynamization.GetType().GetProperty(valueProperty)
                ?? throw new PortalException(PortalErrorCode.NotSupported, $"{dynamizationType} has no '{valueProperty}' to set.");

            setter.SetValue(dynamization, value);

            return dynamization;
        }

        /// <summary>The dynamization of the given kind on a property: the one it has, or a new one (a dynamization of another kind is deleted first).</summary>
        private static object GetOrCreateHmiDynamization(object target, string propertyName, string dynamizationType)
        {
            object dynamizations;

            try
            {
                dynamizations = ((dynamic)target).Dynamizations;
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
            {
                throw new PortalException(PortalErrorCode.NotSupported,
                    $"{target.GetType().Name} does not support dynamizations.", null, ex);
            }

            var existing = FindHmiDynamization(target, propertyName);

            if (existing != null && !existing.GetType().Name.Equals(dynamizationType, StringComparison.OrdinalIgnoreCase))
            {
                ((dynamic)existing).Delete();
                existing = null;
            }

            if (existing == null)
            {
                var compositionType = dynamizations.GetType();

                var type = compositionType.Assembly.GetTypes().FirstOrDefault(t => t.Name == dynamizationType)
                    ?? throw new PortalException(PortalErrorCode.NotSupported,
                        $"This TIA Portal version offers no {dynamizationType} through Openness.");

                var create = compositionType.GetMethods().FirstOrDefault(m => m.Name == "Create" && m.IsGenericMethod && m.GetParameters().Length == 1)
                    ?? throw new PortalException(PortalErrorCode.NotSupported, "Dynamizations cannot be created through Openness here.");

                existing = create.MakeGenericMethod(type).Invoke(dynamizations, new object[] { propertyName })
                    ?? throw new PortalException(PortalErrorCode.CreateFailed, $"Creating the {dynamizationType} for {propertyName} returned nothing.");
            }

            return existing;
        }
    }
}
