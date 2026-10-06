# A screen with an output field and a button (WinCC Unified)

1. Find the HMI path: `get_project_tree` with `structured=true` lists nodes with their `softwarePath`, e.g. `HMI Unified/HMI_RT_3`.
2. Create the tag the field shows: `unified_manage_tags`

   ```json
   { "softwarePath": "HMI Unified/HMI_RT_3",
     "actions": [ { "action": "create", "tagName": "Pump1_Speed", "properties": { "DataType": "Real" } } ] }
   ```

   A tag of a PLC needs a connection first: `unified_manage_connections` with `partner` (the HMI and the PLC need interfaces on one subnet, see [Add a PLC and connect it to a subnet](add-plc-and-subnet.md)), then `Connection` and `PlcTag` in the tag's `properties`.
3. Create the screen: `unified_create_screen` with `screenName`.
4. Put the items on it - one call, all or nothing: `unified_manage_items`

   ```json
   { "softwarePath": "HMI Unified/HMI_RT_3",
     "actions": [
       { "action": "upsert", "screenName": "Pump", "itemName": "Speed", "itemType": "HmiIOField",
         "properties": { "Left": 100, "Top": 100, "Width": 200, "IOFieldType": "Output", "ProcessValue": { "tag": "Pump1_Speed" } } },
       { "action": "upsert", "screenName": "Pump", "itemName": "Start", "itemType": "HmiButton",
         "properties": { "Left": 100, "Top": 160, "Text": "Start" },
         "events": { "Tapped": "HMIRuntime.Tags.SysFct.SetTagValue('Pump1_Speed', 1);" } } ] }
   ```

5. Check: `unified_get_screen_items` lists the items with their events; `unified_get_screen_item_properties` shows one item in full. A property with a tag binding reads back as an empty static value in the list (`processValue` is `""`); look at its dynamization in `unified_get_screen_item_properties`.
6. `save_project`.

Checked live on a temporary screen and tag; both were deleted again with `unified_delete_screen` and `unified_manage_tags` (`delete`).
