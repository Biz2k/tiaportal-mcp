Connecting to TIA Portal...
Finding HMI...
Found HMI Software Path: HMI (A7)/HMI_RT_3

Cleaning up and Testing Trend/Companion...
Creating UserTrendControl...
Creating UserTrendCompanion...
TrendCompanion 'UserTrendCompanion' bound successfully to 'UserTrendControl'.
Trend added successfully to 'UserTrendControl'. Configurations applied: Reused empty Trend, DataSourceY=╨Э╨░╤Б╨╛╤Б╨╜╨╕╨╣ ╨░╨│╤А╨╡╨│╨░╤В ╨б╨а1 ╨д╨░╨╖╨░ W - ╨Ч╨╜╨░╤З╨╡╨╜╨╜╤П ╤Б╤В╤А╤Г╨╝╤Г, DisplayName=╨б╤В╤А╤Г╨╝ ╤Д╨░╨╖╨╕ W, TrendMode, LineWidth, LineColor=Red
Trend added successfully to 'UserTrendControl'. Configurations applied: Created new Trend, DataSourceY=╨Э╨░╤Б╨╛╤Б╨╜╨╕╨╣ ╨░╨│╤А╨╡╨│╨░╤В ╨б╨а1 ╨┐╨╛╤В╤Г╨╢╨╜╤Ц╤Б╤В╤М, DisplayName=╨Я╨╛╤В╤Г╨╢╨╜╤Ц╤Б╤В╤М, TrendMode, LineWidth, LineColor=Blue
--- SCREEN REFLECTION ---
[Prop] EventHandlers = HmiScreenEventHandlerComposition
[Prop] Parent = HmiSoftware
[Prop] ScreenItems = HmiScreenItemBaseComposition
[Prop] AlternateBackColor = Color
[Prop] BackColor = Color
[Prop] BackFillPattern = HmiFillPattern
[Prop] BackGraphic = String
[Prop] BackGraphicStretchMode = HmiGraphicStretchMode
[Prop] BackgroundFillMode = HmiBackgroundFillMode
[Prop] Enabled = Boolean
[Prop] Height = UInt32
[Prop] HorizontalAlignment = HmiHorizontalAlignment
[Prop] ScreenNumber = UInt16
[Prop] VerticalAlignment = HmiVerticalAlignment
[Prop] Width = UInt32
[Prop] DisplayName = MultilingualText
[Prop] Name = String
[Prop] Dynamizations = DynamizationBaseComposition
[Prop] PropertyEventHandlers = PropertyEventHandlerComposition
--- DEBUGGING EVENTHANDLERS ---
EventHandlers count: 1
  Event Type: HmiScreenEventHandler
    [Prop] Parent = Siemens.Engineering.HmiUnified.UI.Screens.HmiScreen
    [Prop] EventType = Loaded
    [Prop] Script = Siemens.Engineering.HmiUnified.UI.Dynamization.Script.ScriptDynamization
--- DEBUGGING PROPERTYEVENTHANDLERS ---
PropertyEventHandlers count: 0
--- DEBUGGING EXPRESSIONS ---
Property 'Expressions' not found via Reflection.

--- JSON OUTPUT FROM MCP ---
{
  "AlternateBackColor": "#EBEBEB",
  "BackColor": "#C0C0C0",
  "BackFillPattern": "Solid",
  "BackGraphic": "",
  "BackGraphicStretchMode": "UniformToFill",
  "BackgroundFillMode": "Window",
  "DisplayName": "Siemens.Engineering.MultilingualText",
  "Enabled": "True",
  "Height": "680",
  "HorizontalAlignment": "Left",
  "Name": "7_Trends",
  "ScreenNumber": "0",
  "VerticalAlignment": "Top",
  "Width": "1366",
  "_Dynamizations": [],
  "_Events": [
    {
      "EventType": "Loaded",
      "Script": "ScriptDynamization",
      "ScriptCode": "\r\nHMIRuntime.Tags.SysFct.SetTagValue(\u0022Curr_button_Tands\u0022, 0);\r\n"
    }
  ],
  "_Expression": []
}
