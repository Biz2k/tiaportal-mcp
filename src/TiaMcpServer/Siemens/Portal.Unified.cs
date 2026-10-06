using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.HmiUnified;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    public class HmiScreenInfo
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Group the screen is in, '/'-separated; empty at the top level.</summary>
        public string Group { get; set; } = string.Empty;
        public int? Width { get; set; }
        public int? Height { get; set; }
    }

    public class HmiScreenItemInfo
    {
        public string Name { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public int? Left { get; set; }
        public int? Top { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string? Text { get; set; }
        public string? ProcessValue { get; set; }
        public List<string> Events { get; set; } = new List<string>();
    }

    public class HmiTagInfo
    {
        public string Name { get; set; } = string.Empty;
        public string? TagTable { get; set; }
        public string? DataType { get; set; }

        /// <summary>Name of the connection; null for an internal tag.</summary>
        public string? Connection { get; set; }
        public string? PlcTag { get; set; }
        public string? Address { get; set; }
    }

    // WinCC Unified: reading screens, items, tags and connections, creating and deleting
    // screens, and configuring trend controls. Editing screen items lives in
    // Portal.Unified.Items.cs.
    //
    // Callers: the unified_* tools in McpServer.Unified.cs. Affected API: this file replaces
    // Portal.Hmi.cs. The methods that also tried to serve WinCC Comfort / Advanced /
    // Professional now serve Unified only; what worked for the classic systems is parked in
    // Classic/Portal.HmiClassic.cs and described in docs/hmi-classic-notes.md. Reads and writes
    // no data files.
    //
    // Why Unified only: the classic systems have no object model for screens in Openness, so
    // every method here carried a second code path that could not do the job, guarded by
    // try/catch. Each tool name now says which system it is for.
    //
    // GetAttributeInfos / GetAttribute on Openness objects: audited 2026-10-06 on V21, one object
    // per kind, every attribute read, in the test project (task 05 of docs/handoff). None closed
    // TIA Portal - except the WinCC Unified ALARM (see Portal.Unified.Alarms.cs), which is never
    // touched this way. Safe: HmiScreen; every screen item type of the project (IOField,
    // SymbolicIOField, Rectangle, Circle, Line, Polyline, Polygon, ScreenWindow, TextBox, Text,
    // Button, GraphicView, CustomWidgetContainer, FaceplateContainer, AlarmControl, TrendControl,
    // TrendCompanion, Gauge, Bar, ToggleSwitch) and every other type the server can create
    // (Slider, Clock, ListBox, RadioButtonGroup, ... - 39 of 45, the rest do not exist on a panel);
    // the dynamizations Script, Tag, Expression, ResourceList and Flashing; the event handlers of
    // screens, buttons, IO fields and widgets, and the property event handler; HmiConnection (S7);
    // HmiTag and the members of structured tags; and, for the classic Openness objects behind
    // Helper.cs, device, device items (rail, CPU, switch), OB/FB/FC/DB blocks in LAD/STL/SCL,
    // PlcStruct, tag tables, PLC tags and subnets. Not audited: an item type that can only be
    // made by hand in TIA Portal and is not in the project, and connections of other drivers.
    //
    // The Unified UI classes are reached through 'dynamic': they live in an assembly this
    // project does not reference at compile time for every TIA Portal version it supports.

    public partial class Portal
    {
        /// <summary>
        /// Resolves the software container of an HMI. A wrong path used to come back as an
        /// empty screen or tag list, which reads like "this HMI has no screens".
        /// </summary>
        private global::Siemens.Engineering.HW.Features.SoftwareContainer RequireHmiContainer(string softwarePath)
        {
            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, NoProjectMessage);
            }

            var container = GetSoftwareContainer(softwarePath);

            if (container?.Software == null)
            {
                throw new PortalException(PortalErrorCode.NotFound,
                    $"No HMI software found at '{softwarePath}'. Use 'get_project_tree' to find it: the path is the device name followed by the runtime item, e.g. 'HMI_1/HMI_RT_1'.");
            }

            if (container.Software is global::Siemens.Engineering.SW.PlcSoftware)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"'{softwarePath}' is PLC software, not an HMI. Pass the path of an HMI runtime, e.g. 'HMI_1/HMI_RT_1'.");
            }

            return container;
        }

        private HmiSoftware RequireUnifiedSoftware(string softwarePath)
        {
            var software = RequireHmiContainer(softwarePath).Software;

            if (software is HmiSoftware unified)
            {
                return unified;
            }

            if (software is HmiTarget)
            {
                throw new PortalException(PortalErrorCode.NotSupported,
                    $"'{softwarePath}' is a WinCC Comfort/Advanced/Professional HMI. The unified_* tools work with WinCC Unified only; " +
                    "for the classic systems TIA Portal Openness offers no access to screens and their items beyond XML export and import.");
            }

            throw new PortalException(PortalErrorCode.NotSupported,
                $"'{softwarePath}' is not WinCC Unified HMI software ({software.GetType().Name}).");
        }

        private static object? FindUnifiedScreen(HmiSoftware software, string screenName)
        {
            // Screens in groups count: names are unique across the whole HMI (see
            // Portal.Unified.ScreenGroups.cs).
            foreach (var (screen, _) in EnumerateUnifiedScreens(software))
            {
                if (string.Equals(screen.Name, screenName, StringComparison.OrdinalIgnoreCase))
                {
                    return screen;
                }
            }

            return null;
        }

        private static object RequireUnifiedScreen(HmiSoftware software, string screenName)
        {
            if (string.IsNullOrWhiteSpace(screenName))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "screenName is required.");
            }

            return FindUnifiedScreen(software, screenName)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"Screen '{screenName}' not found. Use 'unified_get_screens' to list the screens.");
        }

        private static object? FindUnifiedScreenItem(dynamic screen, string itemName)
        {
            foreach (var item in screen.ScreenItems)
            {
                if (string.Equals((string)item.Name, itemName, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>The item, or the screen itself when <paramref name="itemName"/> is empty.</summary>
        private static object RequireUnifiedTarget(HmiSoftware software, string screenName, string? itemName)
        {
            dynamic screen = RequireUnifiedScreen(software, screenName);

            if (string.IsNullOrWhiteSpace(itemName))
            {
                return screen;
            }

            object? item = FindUnifiedScreenItem(screen, itemName!);

            return item
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"Item '{itemName}' not found on screen '{screenName}'. Use 'unified_get_screen_items' to list the items.");
        }

        #region read

        /// <param name="group">Group path ('Pumps' or 'Pumps/Big'); returns the screens of that group and of the groups in it. Empty returns every screen.</param>
        public List<HmiScreenInfo> GetUnifiedScreens(string softwarePath, string group = "")
        {
            return Operation.Run(_logger, nameof(GetUnifiedScreens), PortalErrorCode.InvalidState,
                () =>
                {
                    var screens = new List<HmiScreenInfo>();
                    var software = RequireUnifiedSoftware(softwarePath);
                    string? wanted = null;

                    if (!string.IsNullOrWhiteSpace(group))
                    {
                        // Checked first: an unknown group would otherwise read as an empty one.
                        wanted = string.Join("/", SplitGroupPath(group));
                        RequireScreenGroup(software, wanted);
                    }

                    foreach (var (screen, screenGroup) in EnumerateUnifiedScreens(software))
                    {
                        if (wanted != null
                            && !string.Equals(screenGroup, wanted, StringComparison.OrdinalIgnoreCase)
                            && !screenGroup.StartsWith(wanted + "/", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var info = new HmiScreenInfo { Name = screen.Name, Group = screenGroup };
                        dynamic dynScreen = screen;

                        try { info.Width = (int?)dynScreen.Width; } catch { }
                        try { info.Height = (int?)dynScreen.Height; } catch { }

                        screens.Add(info);
                    }

                    return screens;
                },
                ("softwarePath", softwarePath), ("group", group));
        }

        /// <param name="nameFilter">Regular expression on the tag name; empty returns every tag.</param>
        /// <param name="tagTable">Name of a tag table; empty returns the tags of every table.</param>
        public List<HmiTagInfo> GetUnifiedTags(string softwarePath, string nameFilter = "", string tagTable = "")
        {
            return Operation.Run(_logger, nameof(GetUnifiedTags), PortalErrorCode.InvalidState,
                () =>
                {
                    Regex? filter = null;

                    if (!string.IsNullOrWhiteSpace(nameFilter))
                    {
                        try
                        {
                            filter = new Regex(nameFilter, RegexOptions.IgnoreCase);
                        }
                        catch (ArgumentException ex)
                        {
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                $"'{nameFilter}' is not a valid regular expression: {ex.Message}");
                        }
                    }

                    var tags = new List<HmiTagInfo>();
                    HashSet<string>? attributes = null;

                    var software = RequireUnifiedSoftware(softwarePath);

                    var source = string.IsNullOrWhiteSpace(tagTable)
                        ? software.Tags
                        : RequireTagTable(software, tagTable.Trim()).Tags;

                    foreach (var tag in source)
                    {
                        if (filter != null && !filter.IsMatch(tag.Name))
                        {
                            continue;
                        }

                        // Which attributes a tag has is asked once, so that reading one that is
                        // missing does not cost an exception per tag on a few thousand tags.
                        attributes ??= new HashSet<string>(
                            ((IEngineeringObject)tag).GetAttributeInfos().Select(a => a.Name), StringComparer.Ordinal);

                        tags.Add(new HmiTagInfo
                        {
                            Name = tag.Name,
                            TagTable = ReadTagAttribute(tag, attributes, "TagTableName"),
                            DataType = ReadTagAttribute(tag, attributes, "DataType"),
                            Connection = InternalTagConnection.Equals(ReadTagAttribute(tag, attributes, "Connection"), StringComparison.Ordinal)
                                ? null
                                : ReadTagAttribute(tag, attributes, "Connection"),
                            PlcTag = ReadTagAttribute(tag, attributes, "PlcTag"),
                            Address = ReadTagAttribute(tag, attributes, "Address")
                        });
                    }

                    return tags;
                },
                ("softwarePath", softwarePath), ("nameFilter", nameFilter), ("tagTable", tagTable));
        }

        /// <summary>What Openness reports as the connection of a tag that has none.</summary>
        private const string InternalTagConnection = "<Internal tag>";

        private static string? ReadTagAttribute(IEngineeringObject tag, HashSet<string> attributes, string name)
        {
            if (!attributes.Contains(name))
            {
                return null;
            }

            var value = tag.GetAttribute(name)?.ToString();

            return string.IsNullOrEmpty(value) ? null : value;
        }

        public List<HmiScreenItemInfo> GetUnifiedScreenItems(string softwarePath, string screenName)
        {
            return Operation.Run(_logger, nameof(GetUnifiedScreenItems), PortalErrorCode.InvalidState,
                () =>
                {
                    dynamic screen = RequireUnifiedScreen(RequireUnifiedSoftware(softwarePath), screenName);
                    var items = new List<HmiScreenItemInfo>();

                    foreach (var item in screen.ScreenItems)
                    {
                        items.Add(DescribeUnifiedScreenItem(item));
                    }

                    return items;
                },
                ("softwarePath", softwarePath), ("screenName", screenName));
        }

        private static HmiScreenItemInfo DescribeUnifiedScreenItem(dynamic item)
        {
            var info = new HmiScreenItemInfo { Name = item.Name, TypeName = item.GetType().Name };

            // Not every item type has every one of these; a missing member is simply left out.
            try { info.Left = (int?)item.Left; } catch { }
            try { info.Top = (int?)item.Top; } catch { }
            try { info.Width = (int?)item.Width; } catch { }
            try { info.Height = (int?)item.Height; } catch { }

            try
            {
                var text = item.Text;

                if (text is not null)
                {
                    var parts = new List<string>();

                    foreach (var entry in text.Items)
                    {
                        parts.Add((string)entry.Text);
                    }

                    info.Text = string.Join(" | ", parts.Distinct());
                }
            }
            catch { }

            try
            {
                var processValue = item.ProcessValue;

                if (processValue is not null)
                {
                    info.ProcessValue = processValue.ToString();
                }
            }
            catch { }

            try
            {
                foreach (var handler in item.EventHandlers)
                {
                    info.Events.Add(handler.EventType.ToString());
                }
            }
            catch { }

            return info;
        }

        public List<Dictionary<string, object?>> GetUnifiedConnections(string softwarePath)
        {
            return Operation.Run(_logger, nameof(GetUnifiedConnections), PortalErrorCode.InvalidState,
                () =>
                {
                    var result = new List<Dictionary<string, object?>>();

                    foreach (var connection in RequireUnifiedSoftware(softwarePath).Connections)
                    {
                        var entry = new Dictionary<string, object?> { ["Name"] = connection.Name };

                        foreach (var attribute in ((IEngineeringObject)connection).GetAttributeInfos())
                        {
                            try
                            {
                                entry[attribute.Name] = ((IEngineeringObject)connection).GetAttribute(attribute.Name)?.ToString();
                            }
                            catch { }
                        }

                        entry["DriverProperties"] = DescribeDriverProperties(connection);

                        result.Add(entry);
                    }

                    return result;
                },
                ("softwarePath", softwarePath));
        }

        /// <summary>
        /// Every property of a screen item (or of the screen, with an empty item name), plus
        /// what hangs off it: the faceplate interface, dynamizations and event handlers.
        /// </summary>
        public Dictionary<string, object?> GetUnifiedScreenItemProperties(string softwarePath, string screenName, string itemName)
        {
            return Operation.Run(_logger, nameof(GetUnifiedScreenItemProperties), PortalErrorCode.InvalidState,
                () =>
                {
                    dynamic target = RequireUnifiedTarget(RequireUnifiedSoftware(softwarePath), screenName, itemName);
                    var properties = new Dictionary<string, object?>();

                    foreach (var attribute in ((IEngineeringObject)target).GetAttributeInfos())
                    {
                        try
                        {
                            object? value = ((IEngineeringObject)target).GetAttribute(attribute.Name);

                            properties[attribute.Name] = value switch
                            {
                                null => null,
                                System.Drawing.Color color => $"#{color.R:X2}{color.G:X2}{color.B:X2}",
                                MultilingualText text => string.Join(" | ", text.Items.Select(i => i.Text).Distinct()),
                                _ => value.ToString()
                            };
                        }
                        catch { }
                    }

                    if (((object)target).GetType().Name.Equals(FaceplateContainerType, StringComparison.OrdinalIgnoreCase))
                    {
                        properties["_Interface"] = DescribeFaceplateInterface((object)target);
                    }

                    var dynamizations = new List<Dictionary<string, object?>>();

                    try
                    {
                        foreach (var dynamization in target.Dynamizations)
                        {
                            var entry = new Dictionary<string, object?> { ["__Type"] = dynamization.GetType().Name };

                            foreach (var attribute in ((IEngineeringObject)dynamization).GetAttributeInfos())
                            {
                                try
                                {
                                    entry[attribute.Name] = ((IEngineeringObject)dynamization).GetAttribute(attribute.Name)?.ToString();
                                }
                                catch { }
                            }

                            dynamizations.Add(entry);
                        }
                    }
                    catch { }

                    properties["_Dynamizations"] = dynamizations;

                    var events = new List<Dictionary<string, object?>>();

                    try
                    {
                        foreach (var handler in target.EventHandlers)
                        {
                            var entry = new Dictionary<string, object?>();

                            try { entry["EventType"] = handler.EventType.ToString(); } catch { }
                            try { entry["ScriptCode"] = handler.Script.ScriptCode; } catch { }

                            events.Add(entry);
                        }
                    }
                    catch { }

                    properties["_Events"] = events;

                    return properties;
                },
                ("softwarePath", softwarePath), ("screenName", screenName), ("itemName", itemName));
        }

        #endregion

        #region screens (write)

        /// <param name="group">Group path to create the screen in; empty for the top level. The group has to exist.</param>
        public HmiScreenInfo CreateUnifiedScreen(string softwarePath, string screenName, string group = "")
        {
            return Operation.Run(_logger, nameof(CreateUnifiedScreen), PortalErrorCode.CreateFailed,
                () =>
                {
                    var software = RequireUnifiedSoftware(softwarePath);

                    if (string.IsNullOrWhiteSpace(screenName))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, "screenName is required.");
                    }

                    if (FindUnifiedScreen(software, screenName) != null)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Screen '{screenName}' already exists (screen names are unique in the whole HMI, groups included).");
                    }

                    dynamic screen = string.IsNullOrWhiteSpace(group)
                        ? software.Screens.Create(screenName)
                        : RequireScreenGroup(software, group).Screens.Create(screenName);

                    var info = new HmiScreenInfo
                    {
                        Name = screen.Name,
                        Group = string.IsNullOrWhiteSpace(group) ? string.Empty : string.Join("/", SplitGroupPath(group))
                    };

                    try { info.Width = (int?)screen.Width; } catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
                    try { info.Height = (int?)screen.Height; } catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }

                    return info;
                },
                ("softwarePath", softwarePath), ("screenName", screenName), ("group", group));
        }

        public void DeleteUnifiedScreen(string softwarePath, string screenName)
        {
            Operation.Run(_logger, nameof(DeleteUnifiedScreen), PortalErrorCode.DeleteFailed,
                () =>
                {
                    dynamic screen = RequireUnifiedScreen(RequireUnifiedSoftware(softwarePath), screenName);

                    screen.Delete();
                },
                ("softwarePath", softwarePath), ("screenName", screenName));
        }

        #endregion

        #region trend control (write)

        /// <summary>
        /// An archived tag is written '&lt;HMI tag&gt;:&lt;logging tag&gt;' (as in the trends of the PC
        /// station of the test project); Openness takes any text, so that form is checked here.
        /// A plain name is left alone: it may be an HMI tag or something else a trend can show.
        /// </summary>
        private static void CheckTrendDataSource(HmiSoftware software, string dataSource)
        {
            var colon = dataSource.IndexOf(':');

            if (colon <= 0)
            {
                return;
            }

            var tagName = dataSource.Substring(0, colon);
            var loggingName = dataSource.Substring(colon + 1);

            // The process tag may be the member of a structured tag: 'Tag.Member:LoggingTag'.
            var tag = ResolveTagPath(software, tagName, $"Data source '{dataSource}'");

            if (tag.LoggingTags.Find(loggingName) == null)
            {
                throw new PortalException(PortalErrorCode.NotFound,
                    $"Data source '{dataSource}': '{tagName}' has no logging tag '{loggingName}'. It has: {DescribeLoggingTagNames(tag)}. A structured tag keeps its logging tags on its members: use 'Tag.Member:LoggingTag'. 'unified_manage_logging_tags' creates one.");
            }
        }

        /// <summary>
        /// Adds a trend (a pen) to an HmiTrendControl and binds it to a data source. The trend
        /// lives in nested parts of the control - trend areas, then trends - which is why this
        /// is its own operation rather than a property for ManageUnifiedItems.
        /// </summary>
        public string ConfigureUnifiedTrendControl(string softwarePath, string screenName, string trendControlName, string trendName, string dataSource, string? trendMode = null, int? lineWidth = null, string? lineColor = null)
        {
            return Operation.Run(_logger, nameof(ConfigureUnifiedTrendControl), PortalErrorCode.InvalidState,
                () =>
                {
                    var applied = new List<string>();
                    dynamic trendControl = RequireUnifiedTarget(RequireUnifiedSoftware(softwarePath), screenName, trendControlName);

                    if (!((object)trendControl).GetType().Name.Equals("HmiTrendControl", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Item '{trendControlName}' is a {((object)trendControl).GetType().Name}, not an HmiTrendControl.");
                    }

                    dynamic? trendArea = null;
                    var trendAreas = trendControl.TrendAreas;

                    foreach (var area in trendAreas)
                    {
                        trendArea = area;

                        break;
                    }

                    if (trendArea is null)
                    {
                        trendArea = trendAreas.Create("Area_1");
                        applied.Add("created trend area");
                    }

                    // A new control comes with one empty trend; use it before adding another.
                    dynamic? trend = null;

                    foreach (var candidate in trendArea!.Trends)
                    {
                        var source = candidate.DataSourceY;

                        if (source is not null && string.IsNullOrEmpty((string)source.Source))
                        {
                            trend = candidate;
                            applied.Add("reused empty trend");

                            break;
                        }
                    }

                    if (trend is null)
                    {
                        trend = trendArea!.Trends.Create();
                        applied.Add("created trend");
                    }

                    if (!string.IsNullOrEmpty(dataSource))
                    {
                        CheckTrendDataSource(RequireUnifiedSoftware(softwarePath), dataSource);
                        trend!.DataSourceY.Source = dataSource;
                        applied.Add("DataSourceY=" + dataSource);
                    }

                    if (!string.IsNullOrEmpty(trendName))
                    {
                        foreach (var item in trend!.DisplayName.Items)
                        {
                            item.Text = FormatUnifiedText(trendName, (string)item.Text);
                        }

                        applied.Add("DisplayName=" + trendName);
                    }

                    if (trendMode is { Length: > 0 })
                    {
                        Type modeType = ((object)trend!.TrendMode).GetType();

                        if (!Enum.GetNames(modeType).Any(n => n.Equals(trendMode, StringComparison.OrdinalIgnoreCase)))
                        {
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                $"trendMode takes one of {string.Join(", ", Enum.GetNames(modeType))}; got '{trendMode}'.");
                        }

                        trend!.TrendMode = (dynamic)Enum.Parse(modeType, trendMode, true);
                        applied.Add("TrendMode=" + trendMode);
                    }

                    if (lineWidth.HasValue)
                    {
                        if (lineWidth.Value < 0 || lineWidth.Value > 255)
                        {
                            throw new PortalException(PortalErrorCode.InvalidParams, $"lineWidth must be between 0 and 255; got {lineWidth.Value}.");
                        }

                        trend!.LineWidth = (byte)lineWidth.Value;
                        applied.Add("LineWidth=" + lineWidth.Value);
                    }

                    if (lineColor is { Length: > 0 })
                    {
                        System.Drawing.Color color;

                        try
                        {
                            color = ParseHmiColor(lineColor);
                        }
                        catch (Exception)
                        {
                            throw new PortalException(PortalErrorCode.InvalidParams,
                                $"lineColor takes a color such as \"#FF0000\" or \"Red\"; got '{lineColor}'.");
                        }

                        trend!.LineColor = color;
                        applied.Add("LineColor=" + lineColor);
                    }

                    return $"Trend configured on '{trendControlName}': {string.Join(", ", applied)}.";
                },
                ("softwarePath", softwarePath), ("screenName", screenName), ("trendControlName", trendControlName));
        }

        #endregion

        #region debug

        /// <summary>The .NET type of a screen item (or screen) and its public properties - for developing this server.</summary>
        public object DebugUnifiedScreenItem(string softwarePath, string screenName, string itemName)
        {
            return Operation.Run(_logger, nameof(DebugUnifiedScreenItem), PortalErrorCode.InvalidState,
                () =>
                {
                    object target = RequireUnifiedTarget(RequireUnifiedSoftware(softwarePath), screenName, itemName);

                    var members = target.GetType()
                        .GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                        .Select(p => $"{p.Name} = {p.PropertyType.Name}")
                        .ToList();

                    return (object)new { Type = target.GetType().Name, Props = members };
                },
                ("softwarePath", softwarePath), ("screenName", screenName), ("itemName", itemName));
        }

        #endregion
    }
}
