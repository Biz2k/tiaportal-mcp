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

        [Description("Properties to set, by name. A plain value sets a static value of the property's own type: a number, a boolean, a string, an enum member by name, a color as '#RRGGBB' or a color name. An object sets something else: {\"tag\": \"HmiTagName\"} binds the property to an HMI tag, {\"script\": \"code\"} gives it a script dynamization, {\"dynamization\": \"none\"} removes its dynamization, {\"texts\": {\"en-US\": \"...\"}} sets single languages of a text, {\"value\": x} is the long form of a static value. {\"resourceList\": \"ListName\", \"tag\": \"HmiTag\"} shows the entry of a text list (on a text property) or graphic list (on a graphic property) that matches the value of the tag. Options beside 'tag': \"readOnly\" (bool), \"indirect\" (bool; the tag must be a String tag holding the name of the tag to read), \"formula\" (an expression over HMI tags, each in single quotes: \"'Tag_1'*2+1\"; the tags are checked, the syntax is not - 'unified_compile' reports a syntax error of a formula or a script) or \"mapping\" (the two exclude each other): {\"type\": \"range\", \"entries\": [{\"from\": 0, \"to\": 30, \"value\": \"#00FF00\", \"flashing\": true, \"rate\": \"Fast\", \"alternate\": \"#808080\"}]} replaces the rows of the range table (every row needs both 'from' and 'to': the rows 'up to' and 'from' a value cannot be created, because Openness gives the range type of a row as read-only; use a very large 'to' for an open end), {\"type\": \"singlebit\", \"entries\": [{\"bit\": 0, \"value\": \"Red\"}, {\"bit\": 1, \"value\": \"Green\"}]} sets the two rows of a single-bit table, {\"type\": \"bitmask\", \"entries\": [{\"bit\": 0, \"value\": \"Red\"}, {\"bit\": 4, \"value\": \"Yellow\"}]} makes a bitmask table with one row per bit (a row for a combination of bits cannot be written), {\"type\": \"none\"} switches the table off. Options beside 'script': \"async\" (bool), \"globalDefinitions\" (code), \"trigger\" (\"T100ms\", \"T250ms\", \"T500ms\", \"T1s\", \"T2s\", \"T5s\", \"T10s\", \"Disabled\", \"AutomaticTags\", or {\"type\": \"Tags\", \"tags\": [\"Tag_1\"]}, or {\"type\": \"CustomCycle\", \"cycle\": \"Cycle name\"}; a cycle that does not exist is refused). {\"expression\": \"('Tag_1'+'Tag_2')/2\"} (a formula of the same form; or null with a 'mapping' option) gives the property an expression. After the writes TIA Portal validates the item: a screen, graphic, tag, list or cycle that does not exist, and a script that no tag triggers, fail the action. {\"flashing\": {\"condition\": \"Always|Never|RangeViolation\", \"rate\": \"Slow|Medium|Fast\", \"color\": \"#RRGGBB\", \"alternateColor\": \"#RRGGBB\"}} makes a color property flash")]
        public Dictionary<string, JsonElement>? Properties { get; set; }

        [Description("Event handlers to set, as event name -> JavaScript code, e.g. {\"Tapped\": \"HMIRuntime.UI.SysFct.ChangeScreen('Main', '.');\"}. An empty script (or null) removes the handler. An object {\"script\": \"code\", \"async\": true, \"globalDefinitions\": \"const k = 5;\"} also sets how the script runs and its global definitions. Which events exist depends on the item type; an unknown event is answered with the list. Button events: Activated, Deactivated, Tapped (click with the left mouse button), KeyDown (press key), KeyUp (release key), Down (press), Up (release), ContextTapped (click with the right mouse button)")]
        public Dictionary<string, JsonElement>? Events { get; set; }

        [Description("Scripts that run when a property changes, as property name -> script (the same forms as 'events'), e.g. {\"ProcessValue\": \"Tags('T').Write(1);\"}. The key 'Property.QualityCodeChange' is for a change of the quality code, which needs a tag dynamization on the property; a plain name is a change of the value. An empty script removes the handler")]
        public Dictionary<string, JsonElement>? PropertyEvents { get; set; }
    }
}
