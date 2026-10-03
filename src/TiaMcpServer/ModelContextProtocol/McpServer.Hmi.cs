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
    }
}
