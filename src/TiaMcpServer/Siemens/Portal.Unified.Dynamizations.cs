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
    // WinCC Unified: dynamizations with their options, and the scripts that run when a property changes.
    //
    // Callers: SetHmiItemProperty (Portal.Unified.Items.cs) when a property object has options
    // beside 'tag' or 'script', or is an 'expression' or a 'flashing'; ApplyHmiItemAction for
    // 'propertyEvents'; GetUnifiedScreenItemProperties for the reading. What the options mean is
    // in UnifiedDynamizationSpec; this file does the Openness side.
    //
    // Found on TIA Portal V21 (2026-10-06, samples on the screen A7 of the test project):
    //   - ValueConverter.Formula is text over HMI tags in single quotes: 'iCP2'*2+1. Openness checks nothing
    //     in it. Stored exactly as given: a tag that does not exist ('NoSuchTag'+1), a tag without its quotes
    //     (iCP2*2), a syntax error ('iCP2' +), double quotes, function calls. Changed without an error: the
    //     case of an existing tag name is corrected ('icp2' -> 'iCP2'), and "$value * 2" becomes 'InvalidTag'.
    //     A tag and an expression dynamization behave alike. Hence CheckFormulaTags before the write and the
    //     comparison with the stored text after it.
    //   - ScriptDynamization.Trigger is an object: Type (Disabled, T100ms, T250ms, T500ms, T1s, T2s, T5s,
    //     T10s, CustomCycle, Tags, AutomaticTags), Tags (a List<string>, assigned as a whole; setting it
    //     with another type than Tags is refused: "Invalid property Call for the current trigger type")
    //     and CustomDuration, which holds the NAME of the cycle: "T2s" for that type, and for CustomCycle
    //     the name of a cycle of the HMI. Openness checks neither a cycle name nor a tag name in the trigger,
    //     so the tags are checked here. The project's cycles cannot be read: HmiSoftware has no Cycles.
    //   - Async and GlobalDefinitionAreaScriptCode sit on the ScriptDynamization and on the script of every
    //     event handler alike (IHmiScript). Async belongs to the one script. The global definitions do not:
    //     there is ONE area for all script dynamizations of a screen and ONE for all its events and property
    //     events, and writing it on one script changes it for the others (seen on a screen with four
    //     dynamizations and with a button and an I/O field). A new script dynamization has the trigger
    //     AutomaticTags until it is given another.
    //   - TagDynamization: ReadOnly and UseIndirectAddressing are plain settings. Indirect addressing is
    //     refused for any tag that is not a String (Openness: "Property is not allowed to modify").
    //   - ValueConverter: Formula can only be set after IsFormulaSelected = true ("the Formula option is
    //     deactivated"). MappingTable.ConditionType is None, Range, Bitmask, Singlebit or Expression; the entries
    //     of the kinds that are not selected stay in MappingTable.Entries, hidden, so only those of the
    //     selected kind are meant. A range row is a MappingTableEntryRange made with Create<T>() (RangeType is
    //     read-only, so From-only and To-only rows cannot be made). A single-bit table has its two rows
    //     (bit 0 and 1, MappingTableEntryBitmask) from the moment it is selected; more cannot be created.
    //     A multi-bit row cannot choose its mask (Relevant is read-only). DANGER: creating a
    //     MappingTableEntrySimple (the Expression table) throws NonRecoverableException and closes TIA Portal.
    //   - ExpressionDynamization has a ValueConverter and nothing else; FlashingDynamization takes
    //     FlashingCondition, FlashingRate, Color and AlternateColor, and only on color properties
    //     ("Dynamization type not supported" otherwise).
    //   - PropertyEventHandlers.Create(property, Change | QualityCodeChange): the property has to be one the item has
    //     ("Property Descriptor not found"), and QualityCodeChange needs a tag dynamization on it.
    public partial class Portal
    {
        private const string ExpressionDynamizationType = "ExpressionDynamization";

        private const string FlashingDynamizationType = "FlashingDynamization";

        private string? SetHmiDynamizationWithOptions(HmiSoftware software, object target, string propertyName, JsonElement value, string mainKey)
        {
            var payload = value.EnumerateObject().First(p => p.Name.Equals(mainKey, StringComparison.OrdinalIgnoreCase)).Value;
            var name = ResolveHmiPropertyName(target, propertyName);

            switch (mainKey.ToLowerInvariant())
            {
                case "tag":
                    return SetTagDynamizationWithOptions(software, target, name, value, payload);

                case "script":
                    return SetScriptDynamizationWithOptions(software, target, name, value, payload);

                case "expression":
                    return SetExpressionDynamization(software, target, name, value, payload);

                default:
                    return SetFlashingDynamization(target, name, value, payload);
            }
        }

        private string? SetTagDynamizationWithOptions(HmiSoftware software, object target, string name, JsonElement value, JsonElement payload)
        {
            var options = UnifiedDynamizationSpec.Options(value, "tag", "readOnly", "indirect", "formula", "mapping");
            var tagName = RequireText(payload, "tag");

            var hmiTag = software.Tags.FirstOrDefault(t => string.Equals(t.Name, tagName, StringComparison.OrdinalIgnoreCase))
                ?? throw new PortalException(PortalErrorCode.NotFound, $"HMI tag '{tagName}' does not exist. Use 'unified_get_tags' to list the tags.");

            bool? readOnly = options.TryGetValue("readOnly", out var readOnlyValue) ? UnifiedDynamizationSpec.ReadBool(readOnlyValue, "readOnly") : (bool?)null;
            bool? indirect = options.TryGetValue("indirect", out var indirectValue) ? UnifiedDynamizationSpec.ReadBool(indirectValue, "indirect") : (bool?)null;
            var formula = options.TryGetValue("formula", out var formulaValue) ? UnifiedDynamizationSpec.ReadText(formulaValue, "formula") : null;
            var mapping = options.TryGetValue("mapping", out var mappingValue) ? UnifiedDynamizationSpec.ParseMapping(mappingValue) : null;

            if (formula != null && mapping != null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "'formula' and 'mapping' are two ways to convert the value; give one of them.");
            }

            if (indirect == true && !IsStringDataType(hmiTag.DataType))
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"'indirect' takes a tag of the type String or WString, which holds the name of the tag to read; '{tagName}' is {hmiTag.DataType}.");
            }

            var dynamization = SetHmiDynamization(target, name, TagDynamizationType, "Tag", tagName);

            if (readOnly != null)
            {
                ((dynamic)dynamization).ReadOnly = readOnly.Value;
            }

            if (indirect != null && (bool)((dynamic)dynamization).UseIndirectAddressing != indirect.Value)
            {
                ((dynamic)dynamization).UseIndirectAddressing = indirect.Value;
            }

            ApplyValueConverter(software, target, name, dynamization, formula, mapping);

            return null;
        }

        private static bool IsStringDataType(string? dataType)
        {
            return string.Equals(dataType, "String", StringComparison.OrdinalIgnoreCase) || string.Equals(dataType, "WString", StringComparison.OrdinalIgnoreCase);
        }

        private string? SetScriptDynamizationWithOptions(HmiSoftware software, object target, string name, JsonElement value, JsonElement payload)
        {
            var options = UnifiedDynamizationSpec.Options(value, "script", "async", "globalDefinitions", "trigger");
            var code = RequireText(payload, "script");

            bool? async = options.TryGetValue("async", out var asyncValue) ? UnifiedDynamizationSpec.ReadBool(asyncValue, "async") : (bool?)null;
            var globals = options.TryGetValue("globalDefinitions", out var globalsValue) ? UnifiedDynamizationSpec.ReadText(globalsValue, "globalDefinitions") : null;
            var trigger = options.TryGetValue("trigger", out var triggerValue) ? UnifiedDynamizationSpec.ParseTrigger(triggerValue) : null;

            if (trigger != null)
            {
                foreach (var tag in trigger.Tags)
                {
                    var baseName = UnifiedTagPath.Split(tag)[0];

                    if (!software.Tags.Any(t => string.Equals(t.Name, baseName, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new PortalException(PortalErrorCode.NotFound,
                            $"Trigger tag '{tag}': HMI tag '{baseName}' does not exist. Openness would accept the name and the script would never run. Use 'unified_get_tags' to list the tags.");
                    }
                }
            }

            var dynamization = SetHmiDynamization(target, name, ScriptDynamizationType, "ScriptCode", code);

            if (async != null)
            {
                ((dynamic)dynamization).Async = async.Value;
            }

            if (globals != null)
            {
                ((dynamic)dynamization).GlobalDefinitionAreaScriptCode = globals;
            }

            var notes = new List<string>();

            if (globals != null)
            {
                notes.Add("The global definitions area is one for all script dynamizations of the screen (the events of the screen have another one): it now holds this code for every one of them.");
            }

            if (trigger == null)
            {
                return notes.Count == 0 ? null : string.Join(" ", notes);
            }

            object triggerObject = ((dynamic)dynamization).Trigger;

            SetEnumProperty(triggerObject, "Type", trigger.Type);

            if (trigger.Type == "Tags")
            {
                SetObjectProperty(triggerObject, "Tags", new List<string>(trigger.Tags));
            }

            if (trigger.Type == "CustomCycle")
            {
                // Openness stores any name; the validation of the item, run after the writes, refuses a cycle that does not exist.
                SetObjectProperty(triggerObject, "CustomDuration", trigger.Cycle);
            }

            return notes.Count == 0 ? null : string.Join(" ", notes);
        }

        private string? SetExpressionDynamization(HmiSoftware software, object target, string name, JsonElement value, JsonElement payload)
        {
            var options = UnifiedDynamizationSpec.Options(value, "expression", "mapping");

            if (payload.ValueKind != JsonValueKind.String && payload.ValueKind != JsonValueKind.Null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'expression' takes the formula as a string (or null with a 'mapping'); got {payload.GetRawText()}.");
            }

            var formula = payload.ValueKind == JsonValueKind.String ? payload.GetString() : null;
            var mapping = options.TryGetValue("mapping", out var mappingValue) ? UnifiedDynamizationSpec.ParseMapping(mappingValue) : null;

            if (string.IsNullOrWhiteSpace(formula) && mapping == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "An expression needs a formula or, with the formula null, a 'mapping'.");
            }

            if (!string.IsNullOrWhiteSpace(formula) && mapping != null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "'expression' as a formula and 'mapping' are two ways to convert the value; give one of them.");
            }

            var dynamization = GetOrCreateHmiDynamization(target, name, ExpressionDynamizationType);

            ApplyValueConverter(software, target, name, dynamization, string.IsNullOrWhiteSpace(formula) ? null : formula, mapping);

            return null;
        }

        private string? SetFlashingDynamization(object target, string name, JsonElement value, JsonElement payload)
        {
            UnifiedDynamizationSpec.Options(value, "flashing");

            if (payload.ValueKind != JsonValueKind.Object || !payload.EnumerateObject().Any())
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    "'flashing' takes an object: {\"condition\": \"Always\", \"rate\": \"Fast\", \"color\": \"#FF0000\", \"alternateColor\": \"#0000FF\"}. Conditions: " +
                    $"{string.Join(", ", UnifiedDynamizationSpec.FlashingConditions)}; rates: {string.Join(", ", UnifiedDynamizationSpec.FlashingRates)}.");
            }

            var current = ((IEngineeringObject)target).GetAttribute(name);

            if (!(current is System.Drawing.Color))
            {
                throw new PortalException(PortalErrorCode.NotSupported,
                    $"Flashing alternates between two colors, so it belongs to a color property; '{name}' is {current?.GetType().Name ?? "not one"}.");
            }

            string? condition = null;
            string? rate = null;
            object? color = null;
            object? alternate = null;

            foreach (var part in payload.EnumerateObject())
            {
                switch (part.Name.ToLowerInvariant())
                {
                    case "condition":
                        condition = UnifiedDynamizationSpec.CanonicalCondition(UnifiedDynamizationSpec.ReadText(part.Value, "flashing.condition"));

                        break;

                    case "rate":
                        rate = UnifiedDynamizationSpec.CanonicalRate(UnifiedDynamizationSpec.ReadText(part.Value, "flashing.rate"));

                        break;

                    case "color":
                        color = ConvertHmiValue(part.Value, typeof(System.Drawing.Color), "flashing.color");

                        break;

                    case "alternatecolor":
                        alternate = ConvertHmiValue(part.Value, typeof(System.Drawing.Color), "flashing.alternateColor");

                        break;

                    default:
                        throw new PortalException(PortalErrorCode.InvalidParams, $"'flashing' has no part '{part.Name}'. Parts: condition, rate, color, alternateColor.");
                }
            }

            var dynamization = GetOrCreateHmiDynamization(target, name, FlashingDynamizationType);

            if (condition != null)
            {
                SetEnumProperty(dynamization, "FlashingCondition", condition);
            }

            if (rate != null)
            {
                SetEnumProperty(dynamization, "FlashingRate", rate);
            }

            if (color != null)
            {
                SetObjectProperty(dynamization, "Color", color);
            }

            if (alternate != null)
            {
                SetObjectProperty(dynamization, "AlternateColor", alternate);
            }

            return null;
        }

        /// <summary>A formula or a table for the value converter of a tag or expression dynamization.</summary>
        private static void ApplyValueConverter(HmiSoftware software, object target, string propertyName, object dynamization, string? formula, UnifiedMappingSpec? mapping)
        {
            if (formula == null && mapping == null)
            {
                return;
            }

            if (formula != null)
            {
                CheckFormulaTags(software, formula);
            }

            dynamic converter = ((dynamic)dynamization).ValueConverter;
            object table = converter.MappingTable;

            if (formula != null)
            {
                // The order matters: the formula is refused while the option is off.
                converter.IsFormulaSelected = true;
                converter.Formula = formula;
                SetEnumProperty(table, "ConditionType", "None");

                // Openness reports no error for a formula TIA Portal cannot take: it stores 'InvalidTag' in its place
                // ("$value * 2" does that). Reading back is the only way to notice.
                var stored = (string?)converter.Formula;

                if (!UnifiedDynamizationSpec.SameFormula(formula, stored))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams,
                        $"TIA Portal did not take the formula \"{formula}\" of '{propertyName}': it stored \"{stored}\" instead. " + FormulaHelp);
                }

                return;
            }

            converter.IsFormulaSelected = false;
            ApplyMappingTable(target, propertyName, table, mapping!);
        }

        private const string FormulaHelp =
            "A formula is an expression over HMI tags, each written in single quotes: 'Tag_1'*2+1, ('Tag_1'+'Tag_2')/2. " +
            "Openness does not check the syntax; an error in it shows when the HMI is compiled in TIA Portal.";

        /// <summary>
        /// Openness stores a formula as text: a tag that does not exist and a tag without its quotes are both kept as
        /// written (probe of 06.10.2026) and fail only at runtime. The names are therefore checked here, before the write.
        /// </summary>
        private static void CheckFormulaTags(HmiSoftware software, string formula)
        {
            var existing = new HashSet<string>(software.Tags.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);

            foreach (var tag in UnifiedDynamizationSpec.FormulaTags(formula))
            {
                // A member of a structured tag is addressed as 'Tag.Member'; only the tag itself is in the collection.
                if (!existing.Contains(tag) && !existing.Contains(UnifiedTagPath.Split(tag)[0]))
                {
                    throw new PortalException(PortalErrorCode.NotFound,
                        $"Formula \"{formula}\": HMI tag '{tag}' does not exist. Openness would store the formula and it would fail at runtime. Use 'unified_get_tags' to list the tags.");
                }
            }

            var bare = UnifiedDynamizationSpec.FormulaBareWords(formula).FirstOrDefault(existing.Contains);

            if (bare != null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"Formula \"{formula}\": '{bare}' is an HMI tag written without quotes; TIA Portal would not read it as a tag. Write '{bare}' in single quotes. " + FormulaHelp);
            }
        }

        private static void ApplyMappingTable(object target, string propertyName, object table, UnifiedMappingSpec mapping)
        {
            if (mapping.Type == "none")
            {
                SetEnumProperty(table, "ConditionType", "None");

                return;
            }

            var valueType = ((IEngineeringObject)target).GetAttribute(propertyName)?.GetType();
            var entries = table.GetType().GetProperty("Entries")!.GetValue(table)!;

            if (mapping.Type == "range")
            {
                SetEnumProperty(table, "ConditionType", "Range");

                foreach (var old in EntriesOf(entries).Where(e => e.GetType().Name == "MappingTableEntryRange").ToList())
                {
                    ((dynamic)old).Delete();
                }

                var rangeType = entries.GetType().Assembly.GetTypes().First(t => t.Name == "MappingTableEntryRange");

                var create = entries.GetType().GetMethods().FirstOrDefault(m => m.Name == "Create" && m.IsGenericMethod && m.GetParameters().Length == 0)
                    ?? throw new PortalException(PortalErrorCode.NotSupported, "Table rows cannot be created through Openness here.");

                foreach (var spec in mapping.Entries)
                {
                    var row = create.MakeGenericMethod(rangeType).Invoke(entries, new object[0])
                        ?? throw new PortalException(PortalErrorCode.CreateFailed, "Creating a row of the table returned nothing.");

                    SetObjectProperty(row, "From", spec.From);
                    SetObjectProperty(row, "To", spec.To);
                    ApplyMappingRow(row, spec, valueType);
                }

                return;
            }

            SetEnumProperty(table, "ConditionType", "Singlebit");

            var bitRows = EntriesOf(entries)
                .Where(e => e.GetType().Name == "MappingTableEntryBitmask" && e.GetType().GetProperty("BitDynamizationType")?.GetValue(e)?.ToString() == "SingleBit")
                .ToList();

            foreach (var spec in mapping.Entries)
            {
                var row = bitRows.FirstOrDefault(r => Convert.ToUInt64(r.GetType().GetProperty("Condition")!.GetValue(r)) == (ulong)spec.Bit!.Value)
                    ?? throw new PortalException(PortalErrorCode.NotSupported, $"The single-bit table has no row for bit {spec.Bit}.");

                ApplyMappingRow(row, spec, valueType);
            }
        }

        private static void ApplyMappingRow(object row, UnifiedMappingEntrySpec spec, Type? valueType)
        {
            SetObjectProperty(row, "Value", ConvertHmiValue(spec.Value, valueType, "mapping value"));

            if (spec.Flashing != null)
            {
                SetObjectProperty(row, "Flashing", spec.Flashing.Value);
            }

            if (spec.Rate != null)
            {
                SetEnumProperty(row, "FlashingRate", spec.Rate);
            }

            if (spec.HasAlternate)
            {
                SetObjectProperty(row, "AlternateValue", ConvertHmiValue(spec.Alternate, valueType, "mapping alternate"));
            }
        }

        private static List<object> EntriesOf(object entries)
        {
            return ((IEnumerable)entries).Cast<object>().ToList();
        }

        private static void SetEnumProperty(object target, string propertyName, string value)
        {
            var property = target.GetType().GetProperty(propertyName)
                ?? throw new PortalException(PortalErrorCode.NotSupported, $"{target.GetType().Name} has no '{propertyName}'.");

            property.SetValue(target, Enum.Parse(property.PropertyType, value));
        }

        private static void SetObjectProperty(object target, string propertyName, object? value)
        {
            var property = target.GetType().GetProperty(propertyName)
                ?? throw new PortalException(PortalErrorCode.NotSupported, $"{target.GetType().Name} has no '{propertyName}'.");

            property.SetValue(target, value);
        }

        /// <summary>The script that runs when a property of the item changes; see 'propertyEvents' of unified_manage_items.</summary>
        private static void SetPropertyEventHandler(object target, string key, UnifiedEventSpec spec)
        {
            var (property, type) = UnifiedDynamizationSpec.SplitPropertyEvent(key);

            object handlers;

            try
            {
                handlers = ((dynamic)target).PropertyEventHandlers;
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
            {
                throw new PortalException(PortalErrorCode.NotSupported, $"{target.GetType().Name} has no property events.", null, ex);
            }

            var name = ResolveHmiPropertyName(target, property);

            if (type == "QualityCodeChange" && FindHmiDynamization(target, name)?.GetType().Name != TagDynamizationType)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"A change of the quality code of '{name}' needs a tag dynamization on it: bind it first with {{\"tag\": \"HmiTag\"}}.");
            }

            var create = handlers.GetType().GetMethod("Create")
                ?? throw new PortalException(PortalErrorCode.NotSupported, "Property events cannot be created through Openness here.");

            var eventType = Enum.Parse(create.GetParameters()[1].ParameterType, type);
            var existing = handlers.GetType().GetMethod("Find")?.Invoke(handlers, new[] { name, eventType });

            if (spec.Script == null)
            {
                if (existing != null)
                {
                    ((dynamic)existing).Delete();
                }

                return;
            }

            existing ??= create.Invoke(handlers, new[] { name, eventType })
                ?? throw new PortalException(PortalErrorCode.CreateFailed, $"Creating the {type} handler of '{name}' returned nothing.");

            ApplyEventScript(((dynamic)existing).Script, spec);
        }

        #region reading

        /// <summary>What a dynamization has beyond its plain attributes: the trigger of a script, the converter of a tag or expression.</summary>
        private static void DescribeDynamizationDetails(object dynamization, Dictionary<string, object?> entry)
        {
            var type = dynamization.GetType();

            try
            {
                var trigger = type.GetProperty("Trigger")?.GetValue(dynamization);

                if (trigger != null)
                {
                    var tags = trigger.GetType().GetProperty("Tags")?.GetValue(trigger) as IEnumerable;

                    entry["Trigger"] = new Dictionary<string, object?>
                    {
                        ["Type"] = trigger.GetType().GetProperty("Type")?.GetValue(trigger)?.ToString(),
                        ["Tags"] = tags?.Cast<object>().Select(t => t.ToString()).Where(t => !string.IsNullOrEmpty(t)).ToList(),
                        ["Cycle"] = trigger.GetType().GetProperty("CustomDuration")?.GetValue(trigger)?.ToString()
                    };
                }
            }
            catch (Exception)
            {
                // A part that cannot be read is left out; the plain attributes are there.
            }

            try
            {
                var converter = type.GetProperty("ValueConverter")?.GetValue(dynamization);

                if (converter != null)
                {
                    entry["ValueConverter"] = DescribeValueConverter(converter);
                }
            }
            catch (Exception)
            {
            }
        }

        private static Dictionary<string, object?> DescribeValueConverter(object converter)
        {
            var type = converter.GetType();
            var table = type.GetProperty("MappingTable")?.GetValue(converter);
            var condition = table?.GetType().GetProperty("ConditionType")?.GetValue(table)?.ToString();

            var result = new Dictionary<string, object?>
            {
                ["IsFormulaSelected"] = type.GetProperty("IsFormulaSelected")?.GetValue(converter),
                ["Formula"] = type.GetProperty("Formula")?.GetValue(converter)?.ToString(),
                ["ConditionType"] = condition
            };

            // Rows of the kinds that are not selected stay in the collection, hidden; only the selected kind is shown.
            var rows = new List<Dictionary<string, object?>>();

            if (table?.GetType().GetProperty("Entries")?.GetValue(table) is IEnumerable entries)
            {
                foreach (var row in entries.Cast<object>())
                {
                    var rowType = row.GetType();
                    var bits = rowType.GetProperty("BitDynamizationType")?.GetValue(row)?.ToString();

                    var active = rowType.Name switch
                    {
                        "MappingTableEntryRange" => condition == "Range",
                        "MappingTableEntryBitmask" => bits == "SingleBit" ? condition == "Singlebit" : condition == "Bitmask",
                        "MappingTableEntrySimple" => condition == "Expression",
                        _ => false
                    };

                    if (!active)
                    {
                        continue;
                    }

                    var description = new Dictionary<string, object?> { ["Kind"] = rowType.Name.Replace("MappingTableEntry", string.Empty) };

                    foreach (var name in new[] { "From", "To", "Condition", "Relevant", "Value", "AlternateValue", "Flashing", "FlashingRate" })
                    {
                        var property = rowType.GetProperty(name);

                        if (property != null)
                        {
                            description[name] = FormatMappingValue(property.GetValue(row));
                        }
                    }

                    rows.Add(description);
                }
            }

            result["Entries"] = rows;

            return result;
        }

        private static object? FormatMappingValue(object? value)
        {
            return value switch
            {
                null => null,
                System.Drawing.Color color => $"#{color.R:X2}{color.G:X2}{color.B:X2}",
                bool or int or long or ulong or double => value,
                _ => value.ToString()
            };
        }

        /// <summary>The script of an event handler with how it runs.</summary>
        private static void DescribeEventScript(object script, Dictionary<string, object?> entry)
        {
            try { entry["ScriptCode"] = script.GetType().GetProperty("ScriptCode")?.GetValue(script); } catch (Exception) { }
            try { entry["Async"] = script.GetType().GetProperty("Async")?.GetValue(script); } catch (Exception) { }
            try { entry["GlobalDefinitions"] = script.GetType().GetProperty("GlobalDefinitionAreaScriptCode")?.GetValue(script); } catch (Exception) { }
        }

        private static List<Dictionary<string, object?>> DescribePropertyEvents(object target)
        {
            var result = new List<Dictionary<string, object?>>();

            try
            {
                foreach (var handler in (IEnumerable)((dynamic)target).PropertyEventHandlers)
                {
                    var entry = new Dictionary<string, object?>();
                    var type = handler.GetType();

                    try { entry["Property"] = type.GetProperty("PropertyName")?.GetValue(handler)?.ToString(); } catch (Exception) { }
                    try { entry["EventType"] = type.GetProperty("EventType")?.GetValue(handler)?.ToString(); } catch (Exception) { }

                    var script = type.GetProperty("Script")?.GetValue(handler);

                    if (script != null)
                    {
                        DescribeEventScript(script, entry);
                    }

                    result.Add(entry);
                }
            }
            catch (Exception)
            {
                // The item has no property events.
            }

            return result;
        }

        #endregion
    }
}
