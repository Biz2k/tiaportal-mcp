using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
            return Guarded(nameof(GetHmiScreens), () =>
            {
                var screens = Portal.GetHmiScreens(softwarePath);
                return new ResponseHmiScreens
                {
                    Message = $"Retrieved {screens.Count} screens from '{softwarePath}'",
                    Items = screens,
                    Meta = OkMeta()
                };
            });
        }

        [McpServerTool(Name = "GetHmiTags", Title = "Get HMI tags", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Get a list of all tags in an HMI target (WinCC Basic/Comfort/Advanced/Professional or Unified).")]
        public static ResponseHmiTags GetHmiTags(
            [Description("softwarePath: defines the path in the project structure to the HMI software")] string softwarePath)
        {
            return Guarded(nameof(GetHmiTags), () =>
            {
                var tags = Portal.GetHmiTags(softwarePath);
                return new ResponseHmiTags
                {
                    Message = $"Retrieved {tags.Count} tags from '{softwarePath}'",
                    Items = tags,
                    Meta = OkMeta()
                };
            });
        }
    }
}
