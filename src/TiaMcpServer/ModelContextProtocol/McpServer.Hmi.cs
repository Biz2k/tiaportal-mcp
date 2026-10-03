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
    }
}
