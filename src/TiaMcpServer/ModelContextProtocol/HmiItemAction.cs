using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One entry of the 'actions' list of unified_manage_items.</summary>
    public class HmiItemAction
    {
        [Description("'create' (fails if the item exists), 'update' (fails if it does not), 'upsert' (creates it when missing) or 'delete'")]
        public string? Action { get; set; }

        [Description("Name of the screen")]
        public string? ScreenName { get; set; }

        [Description("Name of the item. Leave empty with 'update' to set properties or events of the screen itself")]
        public string? ItemName { get; set; }

        [Description("Type of a new item, e.g. 'HmiButton', 'HmiIOField', 'HmiTextBox', 'HmiRectangle', 'HmiTrendControl'. Needed for 'create', and for 'upsert' when the item does not exist yet. An unknown type is answered with the list of types")]
        public string? ItemType { get; set; }

        [Description("Properties to set, by name. A plain value sets a static value of the property's own type: a number, a boolean, a string, an enum member by name, a color as '#RRGGBB' or a color name. An object sets something else: {\"tag\": \"HmiTagName\"} binds the property to an HMI tag, {\"script\": \"code\"} gives it a script dynamization, {\"dynamization\": \"none\"} removes its dynamization, {\"texts\": {\"en-US\": \"...\"}} sets single languages of a text, {\"value\": x} is the long form of a static value")]
        public Dictionary<string, JsonElement>? Properties { get; set; }

        [Description("Event handlers to set, as event name -> JavaScript code, e.g. {\"Tapped\": \"HMIRuntime.UI.SysFct.ChangeScreen('Main', '.');\"}. An empty script removes the handler. Which events exist depends on the item type; an unknown event is answered with the list")]
        public Dictionary<string, string?>? Events { get; set; }
    }
}
