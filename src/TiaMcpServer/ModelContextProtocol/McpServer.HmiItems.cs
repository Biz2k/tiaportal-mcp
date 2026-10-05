using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.Collections.Generic;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        [WriteTool]
        [McpServerTool(Name = "hmi_manage_items", Title = "Manage HMI screen items", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create, update, upsert or delete items on WinCC Unified screens, several at once, each with any number of properties. A property takes either a static value or a dynamization (tag or script) - see 'properties'. Use 'hmi_get_screen_items' and 'hmi_get_screen_item_properties' to find item and property names. A call applies all of its actions or none: if one fails, the error names it and nothing is changed. Not available for WinCC Comfort/Advanced/Professional: Openness has no access to the items of those screens")]
        public static ResponseHmiManageItems ManageHmiItems(
            [Description("softwarePath: path to the WinCC Unified HMI software, e.g. 'HMI_1/HMI_RT_1'")] string softwarePath,
            [Description("actions: the changes to make, applied in order")] List<HmiItemAction> actions)
        {
            return Guarded(nameof(ManageHmiItems), () =>
            {
                // All or nothing: Portal.ManageHmiItems throws when any action fails, which
                // rolls the transaction back, so reaching the next line means every action took.
                var results = Portal.ManageHmiItems(softwarePath, actions);

                return new ResponseHmiManageItems
                {
                    Results = results,
                    SuccessCount = results.Count,
                    Message = $"{results.Count} action(s) applied. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }
    }
}
