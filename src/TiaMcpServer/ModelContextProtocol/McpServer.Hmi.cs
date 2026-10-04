using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetHmiScreens", Title = "Get HMI screens", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Get a list of all screens in an HMI target (WinCC Basic/Comfort/Advanced/Professional or Unified).")]
        public static ResponseHmiScreens GetHmiScreens(
            [Description("softwarePath: defines the path in the project structure to the HMI software")] string softwarePath)
        {
            try
            {
                var screens = Portal.GetHmiScreens(softwarePath);
                return new ResponseHmiScreens
                {
                    Message = $"Retrieved {screens.Count} screens from '{softwarePath}'",
                    Items = screens,
                    Meta = OkMeta()
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Unexpected error retrieving HMI screens from '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "GetHmiTags", Title = "Get HMI tags", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Get a list of all tags in an HMI target (WinCC Basic/Comfort/Advanced/Professional or Unified).")]
        public static ResponseHmiTags GetHmiTags(
            [Description("softwarePath: defines the path in the project structure to the HMI software")] string softwarePath)
        {
            try
            {
                var tags = Portal.GetHmiTags(softwarePath);
                return new ResponseHmiTags
                {
                    Message = $"Retrieved {tags.Count} tags from '{softwarePath}'",
                    Items = tags,
                    Meta = OkMeta()
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Unexpected error retrieving HMI tags from '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "GetHmiScreenItems", Title = "Get HMI screen items", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Get a list of all items (buttons, IO fields, graphics, etc.) on a specific HMI screen.")]
        public static ResponseHmiScreenItems GetHmiScreenItems(
            [Description("softwarePath: defines the path in the project structure to the HMI software")] string softwarePath,
            [Description("screenName: the name of the screen to read items from")] string screenName)
        {
            try
            {
                var items = Portal.GetHmiScreenItems(softwarePath, screenName);
                return new ResponseHmiScreenItems
                {
                    Message = $"Retrieved {items.Count} items from screen '{screenName}' in '{softwarePath}'",
                    Items = items,
                    Meta = OkMeta()
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Unexpected error retrieving screen items from '{screenName}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "DebugReflect", Title = "Debug Reflect", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Reflect over an assembly")]
        public static object DebugReflect([Description("typeName")] string typeName)
        {
            try
            {
                var asm = typeof(global::Siemens.Engineering.TiaPortal).Assembly;
                var types = asm.GetTypes().Where(t => t.FullName.Contains(typeName)).Select(t => t.FullName).ToList();
                return new { message = "Success", types = types };
            }
            catch (Exception ex)
            {
                return new { message = ex.Message };
            }
        }

        [McpServerTool(Name = "DebugScreenItem", Title = "Debug Screen Item", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Reflect over a screen item")]
        public static object DebugScreenItem([Description("softwarePath")] string softwarePath, [Description("screenName")] string screenName, [Description("itemName")] string itemName)
        {
            try
            {
                return Portal.DebugScreenItem(softwarePath, screenName, itemName);
            }
            catch (Exception ex)
            {
                return new { message = ex.Message };
            }
        }
        [McpServerTool(Name = "GetLibraryFaceplates", Title = "Get Project Library Faceplates", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Get a list of all Faceplates available in the Project Library.")]
        public static object GetLibraryFaceplates()
        {
            try {
                var faceplates = Portal.GetHmiFaceplates();
                return new { Message = $"Retrieved {faceplates.Count} faceplates from Project Library", Items = faceplates, Meta = OkMeta() };
            } catch (Exception ex) {
                return new { Message = $"Unexpected error: {ex.Message}" };
            }
        }

        [McpServerTool(Name = "GetHmiScreenItemProperties", Title = "Get HMI screen item complete properties", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Get a complete dictionary of all properties dynamically available for a specific screen element via Openness. If itemName is empty, returns properties of the screen itself.")]
        public static object GetHmiScreenItemProperties(
            [Description("softwarePath: defines the path in the project structure to the HMI software")] string softwarePath,
            [Description("screenName: the name of the screen")] string screenName,
            [Description("itemName: the name of the item to inspect (leave empty to inspect the screen)")] string itemName = "")
        {
            try {
                var props = Portal.GetHmiScreenItemProperties(softwarePath, screenName, itemName);
                return new { Message = $"Retrieved {props.Count} properties from '{(string.IsNullOrEmpty(itemName) ? screenName : itemName)}'", Items = props, Meta = OkMeta() };
            } catch (Exception ex) {
                return new { Message = $"Unexpected error: {ex.Message}" };
            }
        }

        [WriteTool]
        [McpServerTool(Name = "SetHmiScreenItemProperty", Title = "Set HMI screen item generic property", Destructive = false, OpenWorld = false, UseStructuredContent = true),
         Description("Set an arbitrary property of a screen element by its exact property name (e.g. 'BackColor', 'Visible'). Use GetHmiScreenItemProperties to find the correct property names. If itemName is empty, modifies the screen itself.")]
        public static object SetHmiScreenItemProperty(
            [Description("softwarePath: path to HMI software")] string softwarePath,
            [Description("screenName: name of the screen")] string screenName,
            [Description("itemName: name of the item to modify (leave empty to modify the screen)")] string itemName,
            [Description("propertyName: exact name of the property to set (e.g. 'Visible', 'BackColor')")] string propertyName,
            [Description("propertyValue: the value to set (can be string, boolean, number, or hex color '#RRGGBB')")] JsonNode propertyValue)
        {
            try {
                // Convert JsonNode to native .NET type based on its value kind
                object nativeValue = null;
                if (propertyValue is JsonValue jval) {
                    if (jval.TryGetValue(out string s)) nativeValue = s;
                    else if (jval.TryGetValue(out bool b)) nativeValue = b;
                    else if (jval.TryGetValue(out int i)) nativeValue = i;
                    else if (jval.TryGetValue(out double d)) nativeValue = d;
                    else nativeValue = jval.ToString();
                } else if (propertyValue != null) {
                    nativeValue = propertyValue.ToString();
                }

                string msg = Portal.SetHmiScreenItemProperty(softwarePath, screenName, itemName, propertyName, nativeValue);
                return new { Message = msg, Meta = OkMeta() };
            } catch (Exception ex) {
                return new { Message = $"Unexpected error: {ex.Message}" };
            }
        }

        [McpServerTool(Name = "GetHmiConnections", Title = "Get HMI connections", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true),
         Description("List all HMI connections to PLCs with their attributes.")]
        public static object GetHmiConnections(
            [Description("softwarePath: path to HMI software")] string softwarePath)
        {
            try {
                var conn = Portal.GetHmiConnections(softwarePath);
                return new { Items = conn, Meta = OkMeta() };
            } catch (Exception ex) {
                return new { Message = $"Unexpected error: {ex.Message}" };
            }
        }

        [WriteTool]
        [McpServerTool(Name = "CreateHmiScreen", Title = "Create HMI screen", Destructive = false, OpenWorld = false, UseStructuredContent = true),
         Description("Create a new HMI screen in the given HMI target.")]
        public static object CreateHmiScreen(
            [Description("softwarePath: path to HMI software")] string softwarePath,
            [Description("screenName: name of the new screen")] string screenName)
        {
            try {
                string msg = Portal.CreateHmiScreen(softwarePath, screenName);
                return new { Message = msg, Meta = OkMeta() };
            } catch (Exception ex) {
                return new { Message = $"Unexpected error: {ex.Message}" };
            }
        }

        [WriteTool]
        [McpServerTool(Name = "DeleteHmiScreen", Title = "Delete HMI screen", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete an HMI screen from the given HMI target.")]
        public static object DeleteHmiScreen(
            [Description("softwarePath: path to HMI software")] string softwarePath,
            [Description("screenName: name of the screen to delete")] string screenName)
        {
            try {
                string msg = Portal.DeleteHmiScreen(softwarePath, screenName);
                return new { Message = msg, Meta = OkMeta() };
            } catch (Exception ex) {
                return new { Message = $"Unexpected error: {ex.Message}" };
            }
        }

        [WriteTool]
        [McpServerTool(Name = "CreateHmiScreenItem", Title = "Create HMI screen item", Destructive = false, OpenWorld = false, UseStructuredContent = true),
         Description("Create an item (e.g. HmiButton, HmiIOField, HmiFaceplate) on the given HMI screen.")]
        public static object CreateHmiScreenItem(
            [Description("softwarePath: path to HMI software")] string softwarePath,
            [Description("screenName: name of the screen")] string screenName,
            [Description("typeName: type of the item (e.g. 'HmiButton' or 'HmiFaceplate')")] string typeName,
            [Description("itemName: name of the new item")] string itemName)
        {
            try {
                string msg = Portal.CreateHmiScreenItem(softwarePath, screenName, typeName, itemName);
                return new { Message = msg, Meta = OkMeta() };
            } catch (Exception ex) {
                return new { Message = $"Unexpected error: {ex.Message}" };
            }
        }

        [WriteTool]
        [McpServerTool(Name = "DeleteHmiScreenItem", Title = "Delete HMI screen item", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete an item from the given HMI screen.")]
        public static object DeleteHmiScreenItem(
            [Description("softwarePath: path to HMI software")] string softwarePath,
            [Description("screenName: name of the screen")] string screenName,
            [Description("itemName: name of the item to delete")] string itemName)
        {
            try {
                string msg = Portal.DeleteHmiScreenItem(softwarePath, screenName, itemName);
                return new { Message = msg, Meta = OkMeta() };
            } catch (Exception ex) {
                return new { Message = $"Unexpected error: {ex.Message}" };
            }
        }

        [WriteTool]
        [McpServerTool(Name = "ConfigureHmiScreenItem", Title = "Configure HMI screen item", Destructive = false, OpenWorld = false, UseStructuredContent = true),
         Description("Configure an item (e.g. dimensions, text, processValue binding) on the given HMI screen.")]
        public static object ConfigureHmiScreenItem(
            [Description("softwarePath: path to HMI software")] string softwarePath,
            [Description("screenName: name of the screen")] string screenName,
            [Description("itemName: name of the item to configure")] string itemName,
            [Description("left: X coordinate (optional)")] int? left = null,
            [Description("top: Y coordinate (optional)")] int? top = null,
            [Description("width: Item width (optional)")] int? width = null,
            [Description("height: Item height (optional)")] int? height = null,
            [Description("processValue: tag binding or value for ProcessValue property (optional)")] string processValue = null,
            [Description("text: string for the Text property (optional)")] string text = null)
        {
            try {
                string msg = Portal.ConfigureHmiScreenItem(softwarePath, screenName, itemName, left, top, width, height, processValue, text);
                return new { Message = msg, Meta = OkMeta() };
            } catch (Exception ex) {
                return new { Message = $"Unexpected error: {ex.Message}" };
            }
        }

        [WriteTool]
        [McpServerTool(Name = "ConfigureHmiTrendControl", Title = "Configure HMI Trend Control", Destructive = false, OpenWorld = false, UseStructuredContent = true),
         Description("Add a trend/pen to an HmiTrendControl and bind it to a Data Source (e.g. Logged Tag). WinCC Unified only.")]
        public static object ConfigureHmiTrendControl(
            [Description("softwarePath: path to HMI software")] string softwarePath,
            [Description("screenName: name of the screen")] string screenName,
            [Description("trendControlName: name of the Trend Control item")] string trendControlName,
            [Description("trendName: name of the new Trend/Pen to create")] string trendName,
            [Description("dataSource: tag name or log data source")] string dataSource,
            [Description("trendMode: how to draw (Points, Interpolated, Stepped, Bar, Value) (optional)")] string trendMode = null,
            [Description("lineWidth: line width of the trend (optional)")] int? lineWidth = null,
            [Description("lineColor: color of the trend line, name (e.g. Red) or Hex (e.g. #FF0000) (optional)")] string lineColor = null)
        {
            try {
                string msg = Portal.ConfigureHmiTrendControl(softwarePath, screenName, trendControlName, trendName, dataSource, trendMode, lineWidth, lineColor);
                return new { Message = msg, Meta = OkMeta() };
            } catch (Exception ex) {
                return new { Message = $"Unexpected error: {ex.Message}" };
            }
        }

        [WriteTool]
        [McpServerTool(Name = "ConfigureHmiTrendCompanion", Title = "Configure HMI Trend Companion", Destructive = false, OpenWorld = false, UseStructuredContent = true),
         Description("Bind an HmiTrendCompanion to an HmiTrendControl. WinCC Unified only.")]
        public static object ConfigureHmiTrendCompanion(
            [Description("softwarePath: path to HMI software")] string softwarePath,
            [Description("screenName: name of the screen")] string screenName,
            [Description("companionName: name of the Trend Companion item")] string companionName,
            [Description("sourceTrendControlName: name of the source Trend Control to bind to")] string sourceTrendControlName)
        {
            try {
                string msg = Portal.ConfigureHmiTrendCompanion(softwarePath, screenName, companionName, sourceTrendControlName);
                return new { Message = msg, Meta = OkMeta() };
            } catch (Exception ex) {
                return new { Message = $"Unexpected error: {ex.Message}" };
            }
        }

        [WriteTool]
        [McpServerTool(Name = "SetHmiUnifiedScreenItemEvent", Title = "Set HMI screen item event (WinCC Unified only)", Destructive = false, OpenWorld = false, UseStructuredContent = true),
         Description("Set an event handler (like Click) with JS script for a WinCC Unified screen item.")]
        public static object SetHmiUnifiedScreenItemEvent(
            [Description("softwarePath: path to HMI software")] string softwarePath,
            [Description("screenName: name of the screen")] string screenName,
            [Description("itemName: name of the item")] string itemName,
            [Description("eventName: name of the event (e.g. 'Click')")] string eventName,
            [Description("scriptCode: JavaScript code for the event handler")] string scriptCode)
        {
            try {
                string msg = Portal.SetHmiUnifiedScreenItemEvent(softwarePath, screenName, itemName, eventName, scriptCode);
                return new { Message = msg, Meta = OkMeta() };
            } catch (Exception ex) {
                return new { Message = $"Unexpected error: {ex.Message}" };
            }
        }
    }
}
