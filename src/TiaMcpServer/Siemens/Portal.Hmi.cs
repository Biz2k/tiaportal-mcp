using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.HmiUnified;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    public class HmiScreenInfo {
        public string Name { get; set; } = string.Empty;
        public int? Width { get; set; }
        public int? Height { get; set; }
    }

    public class HmiScreenItemInfo {
        public string Name { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public int? Left { get; set; }
        public int? Top { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string? Text { get; set; }
        public string? ProcessValue { get; set; }
        public List<string> Events { get; set; } = new List<string>();
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    public class HmiTagInfo {
        public string Name { get; set; } = string.Empty;
    }

    public partial class Portal
    {
        /// <summary>
        /// Resolves the HMI software every HMI method works on. A wrong path used to come back
        /// as an empty screen or tag list, which reads like "this HMI has no screens"; and a PLC
        /// path produced a binder error deep inside the dynamic calls.
        /// </summary>
        private global::Siemens.Engineering.HW.Features.SoftwareContainer RequireHmiContainer(string softwarePath)
        {
            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "No project is open in TIA Portal");
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
        public List<HmiScreenInfo> GetHmiScreens(string softwarePath)
        {
            var screens = new List<HmiScreenInfo>();
            var softwareContainer = RequireHmiContainer(softwarePath);

            if (softwareContainer.Software is HmiTarget target)
            {
                foreach (var screen in target.ScreenFolder.Screens)
                {
                    screens.Add(CreateScreenInfo(screen));
                }
            }
            else if (softwareContainer.Software is HmiSoftware unifiedTarget)
            {
                foreach (var screen in unifiedTarget.Screens)
                {
                    screens.Add(CreateScreenInfo(screen));
                }
            }

            return screens;
        }

        private HmiScreenInfo CreateScreenInfo(dynamic screen)
        {
            var info = new HmiScreenInfo { Name = screen.Name };
            try { info.Width = (int?)screen.Width; } catch { }
            try { info.Height = (int?)screen.Height; } catch { }
            return info;
        }

        public List<HmiTagInfo> GetHmiTags(string softwarePath)
        {
            var tags = new List<HmiTagInfo>();
            var softwareContainer = RequireHmiContainer(softwarePath);

            if (softwareContainer.Software is HmiTarget target)
            {
                foreach (var table in target.TagFolder.TagTables)
                {
                    foreach (var tag in table.Tags)
                    {
                        tags.Add(new HmiTagInfo { Name = tag.Name });
                    }
                }
            }
            else if (softwareContainer.Software is HmiSoftware unifiedTarget)
            {
                foreach (var tag in unifiedTarget.Tags)
                {
                    tags.Add(new HmiTagInfo { Name = tag.Name });
                }
            }

            return tags;
        }

        public List<HmiScreenItemInfo> GetHmiScreenItems(string softwarePath, string screenName)
        {
            var softwareContainer = RequireHmiContainer(softwarePath);
            var items = new List<HmiScreenItemInfo>();


            dynamic dynSoftware = softwareContainer.Software;
            object? targetScreen = null;

            try
            {
                targetScreen = FindScreenInFolder(dynSoftware.ScreenFolder, screenName);
            }
            catch (System.Exception)
            {
                try
                {
                    foreach (var screen in dynSoftware.Screens)
                    {
                        if (screen.Name == screenName)
                        {
                            targetScreen = screen;
                            break;
                        }
                    }
                }
                catch (System.Exception)
                {
                    throw new System.Exception("Could not traverse screens on this HMI target.");
                }
            }

            if (targetScreen == null)
            {
                throw new System.Exception($"Screen '{screenName}' not found in '{softwarePath}'.");
            }

            dynamic dynScreen = targetScreen;
            try
            {
                foreach (var item in dynScreen.ScreenItems)
                {
                    items.Add(CreateScreenItemInfo(item));
                }
            }
            catch (System.Exception)
            {
                try
                {
                    foreach (var item in dynScreen.Elements)
                    {
                        items.Add(CreateScreenItemInfo(item));
                    }
                }
                catch (System.Exception)
                {
                    throw new System.Exception($"The Openness API for '{softwarePath}' does not expose ScreenItems or Elements directly for screen '{screenName}'.");
                }
            }

            return items;
        }

        private HmiScreenItemInfo CreateScreenItemInfo(dynamic item)
        {
            var info = new HmiScreenItemInfo { Name = item.Name, TypeName = item.GetType().Name };
            try { info.Left = (int?)item.Left; } catch { }
            try { info.Top = (int?)item.Top; } catch { }
            try { info.Width = (int?)item.Width; } catch { }
            try { info.Height = (int?)item.Height; } catch { }
            try { 
                var txt = item.Text;
                if (txt != null) {
                    try { 
                        var sb = new System.Text.StringBuilder();
                        foreach(var t in txt.Items) {
                            sb.Append(t.Text + " | ");
                        }
                        string res = sb.ToString().TrimEnd(' ', '|');
                        if (string.IsNullOrEmpty(res)) info.Text = txt.ToString();
                        else info.Text = res;
                    } catch { info.Text = txt.ToString(); }
                }
            } catch { }
            try { 
                var pv = item.ProcessValue;
                if (pv != null) info.ProcessValue = pv.ToString();
            } catch { }
            try {
                foreach (var evt in item.EventHandlers) {
                    info.Events.Add(evt.Name);
                }
            } catch { }
            try {
                var props = item.Properties;
                if (props != null) {
                    foreach (var prop in props) {
                                                try {
                            object val = prop.Value;
                            if (val != null) {
                                var type = val.GetType();
                                if (type.IsPrimitive || type == typeof(string)) {
                                    info.Properties[prop.Name] = val;
                                } else {
                                    info.Properties[prop.Name] = val.ToString();
                                }
                            } else {
                                info.Properties[prop.Name] = null;
                            }
                        } catch { }
                    }
                }
            } catch { }
            return info;
        }

        public List<Dictionary<string, object>> GetHmiConnections(string softwarePath)
        {
            var softwareContainer = RequireHmiContainer(softwarePath);
            dynamic dynSoftware = softwareContainer.Software;
            
            var connectionList = new List<Dictionary<string, object>>();
            dynamic? connections = null;

            try { connections = dynSoftware.GetType().GetProperty("Connections")?.GetValue(dynSoftware); } catch { }
            if (connections == null) {
                try { connections = dynSoftware.GetType().GetProperty("HmiConnections")?.GetValue(dynSoftware); } catch { }
            }

            if (connections != null) {
                foreach(var conn in connections) {
                    var connDict = new Dictionary<string, object>();
                    connDict["Name"] = conn.Name;
                    try {
                        foreach(var attr in ((dynamic)conn).GetAttributeInfos()) {
                            try {
                                var val = ((dynamic)conn).GetAttribute(attr.Name);
                                if (val != null) {
                                    if (val.GetType().IsEnum) connDict[attr.Name] = val.ToString();
                                    else connDict[attr.Name] = val.ToString();
                                }
                            } catch { }
                        }
                    } catch { }
                    connectionList.Add(connDict);
                }
            }
            return connectionList;
        }

        public Dictionary<string, object> GetHmiScreenItemProperties(string softwarePath, string screenName, string itemName)
        {
            var softwareContainer = RequireHmiContainer(softwarePath);
            dynamic dynSoftware = softwareContainer.Software;
            
            object? targetScreen = null;
            try { targetScreen = FindScreenInFolder(dynSoftware.ScreenFolder, screenName); }
            catch {
                foreach (var screen in dynSoftware.Screens) {
                    if (screen.Name == screenName) { targetScreen = screen; break; }
                }
            }
            if (targetScreen == null) throw new System.Exception($"Screen '{screenName}' not found.");

            dynamic? targetItem = null;
            if (!string.IsNullOrEmpty(itemName)) {
                foreach (var item in ((dynamic)targetScreen).ScreenItems) {
                    if (item.Name == itemName) { targetItem = item; break; }
                }
                if (targetItem == null) throw new System.Exception($"Item '{itemName}' not found on screen '{screenName}'.");
            } else {
                targetItem = targetScreen;
            }

            var props = new Dictionary<string, object>();
            try {
                var attrInfos = targetItem.GetAttributeInfos();
                foreach (var attr in attrInfos) {
                    try {
                        var val = targetItem.GetAttribute(attr.Name);
                        if (val != null) {
                            if (val.GetType().IsEnum) props[attr.Name] = val.ToString();
                            else if (val is System.Drawing.Color c) props[attr.Name] = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                            else props[attr.Name] = val.ToString(); // Keep as string or primitive
                        } else {
                            props[attr.Name] = null;
                        }
                    } catch { }
                }
            } catch (System.Exception ex) {
                throw new System.Exception($"Failed to get attributes: {ex.Message}");
            }

            // Extract Interface (specifically for Faceplates)
            var interfaceList = new List<Dictionary<string, object>>();
            try {
                var ifaceProp = targetItem.GetType().GetProperty("Interface");
                if (ifaceProp != null) {
                    var iface = ifaceProp.GetValue(targetItem);
                    foreach (var iProp in iface) {
                        var iDict = new Dictionary<string, object>();
                        iDict["__Type"] = iProp.GetType().Name;
                        foreach (var p in iProp.GetType().GetProperties()) {
                            try { iDict[p.Name] = p.GetValue(iProp)?.ToString(); } catch { }
                        }
                        interfaceList.Add(iDict);
                    }
                }
            } catch { }
            if (interfaceList.Count > 0) props["_Interface"] = interfaceList;

            // Extract Dynamizations
            var dynList = new List<Dictionary<string, object>>();
            props["_Dynamizations"] = dynList;
            try {
                foreach (var dyn in targetItem.Dynamizations) {
                    var dynProps = new Dictionary<string, object>();
                    dynProps["__Type"] = dyn.GetType().Name;
                    try {
                        foreach(var attr in ((dynamic)dyn).GetAttributeInfos()) {
                            try {
                                var val = ((dynamic)dyn).GetAttribute(attr.Name);
                                if (val != null) {
                                    if (val.GetType().IsEnum) dynProps[attr.Name] = val.ToString();
                                    else dynProps[attr.Name] = val.ToString();
                                }
                            } catch { }
                        }
                    } catch { }
                    dynList.Add(dynProps);
                }
            } catch { }

            // Extract Events
            var evtList = new List<Dictionary<string, object>>();
            props["_Events"] = evtList;
            try {
                foreach (var evt in targetItem.EventHandlers) {
                    var evtProps = new Dictionary<string, object>();
                    try { evtProps["EventType"] = ((dynamic)evt).EventType.ToString(); } catch { }
                    try { evtProps["Name"] = ((dynamic)evt).Name; } catch { }
                    
                    try {
                        var script = ((dynamic)evt).Script;
                        if (script != null) {
                            evtProps["Script"] = script.GetType().Name;
                            try { evtProps["ScriptCode"] = script.ScriptCode; } catch { }
                        }
                    } catch { }

                    evtList.Add(evtProps);
                }
            } catch { }
            
            // Extract Expressions (if any, per user request)
            var exprList = new List<string>();
            props["_Expression"] = exprList;
            try {
                foreach (var expr in targetItem.Expressions) {
                    exprList.Add(expr.Name);
                }
            } catch { }

            return props;
        }

        public object DebugScreenItem(string softwarePath, string screenName, string itemName)
        {
            var softwareContainer = RequireHmiContainer(softwarePath);
            
            dynamic dynSoftware = softwareContainer.Software;
            object? targetScreen = null;
            try {
                targetScreen = FindScreenInFolder(dynSoftware.ScreenFolder, screenName);
            } catch (System.Exception) {
                foreach (var screen in dynSoftware.Screens) { if (screen.Name == screenName) { targetScreen = screen; break; } }
            }
            if (targetScreen == null) throw new System.Exception("Screen not found");

            dynamic dynScreen = targetScreen;
            object? targetItem = null;
            if (string.IsNullOrEmpty(itemName)) {
                targetItem = targetScreen;
            } else {
                try {
                    foreach (var item in dynScreen.ScreenItems) { if (item.Name == itemName) { targetItem = item; break; } }
                } catch (System.Exception) {
                    foreach (var item in dynScreen.Elements) { if (item.Name == itemName) { targetItem = item; break; } }
                }
            }

            if (targetItem == null) throw new System.Exception("Item not found");

            var props = new List<string>();
            try {
                foreach (var prop in targetItem.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)) {
                    props.Add($"{prop.Name} = {prop.PropertyType.Name}");
                }
            } catch { }

            return new { Type = targetItem.GetType().Name, Props = props };
        }

        private object? FindScreenInFolder(dynamic folder, string screenName)
        {
            try {
                foreach (var screen in folder.Screens)
                {
                    if (screen.Name == screenName) return screen;
                }
                foreach (var subFolder in folder.Folders)
                {
                    var result = FindScreenInFolder(subFolder, screenName);
                    if (result != null) return result;
                }
            } catch (System.Exception) { }
            return null;
        }

        public List<string> GetHmiFaceplates()
        {
            var faceplates = new List<string>();
            if (_project == null) return faceplates;

            try {
                dynamic projLib = _project.ProjectLibrary;
                foreach (var type in projLib.TypeFolder.Types)
                {
                    if (type.GetType().Name.Contains("Faceplate"))
                    {
                        faceplates.Add(type.Name);
                    }
                }
            } catch { }
            return faceplates;
        }

        public List<System.Collections.Generic.Dictionary<string, object>> ListHmiLibraryTypes()
        {
            var typesList = new List<System.Collections.Generic.Dictionary<string, object>>();
            if (_project == null) return typesList;

            try {
                dynamic projLib = _project.ProjectLibrary;
                GetLibraryTypesRecursive(projLib.TypeFolder, typesList, "");
            } catch { }
            
            return typesList;
        }

        private void GetLibraryTypesRecursive(dynamic folder, List<System.Collections.Generic.Dictionary<string, object>> typesList, string path)
        {
            try {
                foreach (var type in folder.Types) {
                    var typeInfo = new System.Collections.Generic.Dictionary<string, object>();
                    typeInfo["Name"] = type.Name;
                    typeInfo["Path"] = path;
                    
                    string kind = "Unknown";
                    try { kind = type.GetType().Name; } catch { }
                    typeInfo["Kind"] = kind;

                    // Classify the target system / specific use
                    string targetSystem = "Unknown";
                    if (kind == "FaceplateLibraryType") {
                        targetSystem = "WinCC Comfort/Advanced/Professional";
                    } else if (kind == "LibraryType") {
                        // In TIA Openness, WinCC Unified Faceplates, Scripts, and Graphics fall under generic LibraryType
                        targetSystem = "WinCC Unified (Faceplate / Script / Graphic)";
                    } else if (kind == "PlcTypeLibraryType") {
                        targetSystem = "PLC Data Type (UDT)";
                    } else if (kind.Contains("MasterCopy")) {
                        targetSystem = "Master Copy";
                    } else {
                        targetSystem = kind;
                    }
                    typeInfo["TargetSystem"] = targetSystem;

                    var versions = new List<System.Collections.Generic.Dictionary<string, object>>();
                    try {
                        foreach (var v in type.Versions) {
                            var verInfo = new System.Collections.Generic.Dictionary<string, object>();
                            try { verInfo["Version"] = v.VersionNumber; } catch { }
                            try { verInfo["State"] = v.State.ToString(); } catch { }
                            
                            // For Faceplates (both Advanced and Unified), generate the ContainedType format
                            if (kind.Contains("Faceplate") || kind == "LibraryType") {
                                try { verInfo["ContainedTypeFormat"] = $"V{v.VersionNumber}\\{type.Name}"; } catch { }
                            }
                            versions.Add(verInfo);
                        }
                    } catch { }
                    
                    typeInfo["Versions"] = versions;
                    typesList.Add(typeInfo);
                }

                foreach (var subFolder in folder.Folders) {
                    GetLibraryTypesRecursive(subFolder, typesList, path + (string.IsNullOrEmpty(path) ? "" : "/") + subFolder.Name);
                }
            } catch { }
        }

        public string CreateHmiScreen(string softwarePath, string screenName)
        {
            var softwareContainer = RequireHmiContainer(softwarePath);
            dynamic dynSoftware = softwareContainer.Software;
            try
            {
                if (dynSoftware is HmiTarget) {
                    dynSoftware.ScreenFolder.Screens.Create(screenName);
                } else if (dynSoftware is HmiSoftware) {
                    dynSoftware.Screens.Create(screenName);
                } else {
                    dynSoftware.Screens.Create(screenName);
                }
                return $"Screen '{screenName}' created successfully.";
            } catch (System.Exception ex) {
                throw new System.Exception($"Failed to create screen: {ex.Message}");
            }
        }

        public string DeleteHmiScreen(string softwarePath, string screenName)
        {
            var softwareContainer = RequireHmiContainer(softwarePath);
            dynamic dynSoftware = softwareContainer.Software;
            try
            {
                dynamic? screen = null;
                if (dynSoftware is HmiTarget target) {
                    screen = FindScreenInFolder(target.ScreenFolder, screenName);
                } else {
                    foreach (var s in dynSoftware.Screens) { if (s.Name == screenName) { screen = s; break; } }
                }
                if (screen != null) { screen.Delete(); return $"Screen '{screenName}' deleted."; }
                throw new System.Exception($"Screen '{screenName}' not found.");
            } catch (System.Exception ex) {
                throw new System.Exception($"Failed to delete screen: {ex.Message}");
            }
        }

        public string CreateHmiScreenItem(string softwarePath, string screenName, string typeName, string itemName)
        {
            var softwareContainer = RequireHmiContainer(softwarePath);
            dynamic dynSoftware = softwareContainer.Software;
            dynamic? screen = null;

            if (dynSoftware is HmiTarget target) {
                screen = FindScreenInFolder(target.ScreenFolder, screenName);
            } else if (dynSoftware is HmiSoftware unified) {
                foreach (var s in unified.Screens) { if (s.Name == screenName) { screen = s; break; } }
            }

            if (screen == null) throw new System.Exception($"Screen '{screenName}' not found.");

            try {
                // WinCC Classic behavior
                screen.ScreenItems.Create(typeName, itemName);
                return $"Created '{itemName}' of type '{typeName}'.";
            } catch {
                try {
                    // WinCC Unified behavior (generic reflection)
                    object screenItems = screen.ScreenItems;
                    System.Type screenItemsType = screenItems.GetType();
                    var targetAssembly = screenItemsType.Assembly;
                    System.Type itemType = System.Linq.Enumerable.FirstOrDefault(targetAssembly.GetTypes(), t => t.Name.Equals(typeName, System.StringComparison.OrdinalIgnoreCase));
                    if (itemType != null) {
                        var createMethod = System.Linq.Enumerable.FirstOrDefault(screenItemsType.GetMethods(), m => m.Name == "Create" && m.IsGenericMethod);
                        if (createMethod != null) {
                            var genericCreate = createMethod.MakeGenericMethod(itemType);
                            genericCreate.Invoke(screenItems, new object[] { itemName });
                            return $"Created '{itemName}' of type '{typeName}' via reflection.";
                        }
                    }
                } catch { }

                try {
                    // Try Elements just in case
                    object elements = screen.Elements;
                    System.Type elementsType = elements.GetType();
                    var targetAssembly = elementsType.Assembly;
                    System.Type itemType = System.Linq.Enumerable.FirstOrDefault(targetAssembly.GetTypes(), t => t.Name.Equals(typeName, System.StringComparison.OrdinalIgnoreCase));
                    if (itemType != null) {
                        var createMethod = System.Linq.Enumerable.FirstOrDefault(elementsType.GetMethods(), m => m.Name == "Create" && m.IsGenericMethod);
                        if (createMethod != null) {
                            var genericCreate = createMethod.MakeGenericMethod(itemType);
                            genericCreate.Invoke(elements, new object[] { itemName });
                            return $"Created '{itemName}' of type '{typeName}' via reflection in Elements.";
                        }
                    }
                } catch { }

                throw new System.Exception($"Failed to create item '{itemName}'. This type of item or operation may not be supported by this HMI target.");
            }
        }

        public string CreateHmiFaceplateInstance(string softwarePath, string screenName, string instanceName, string containedTypeString)
        {
            var softwareContainer = RequireHmiContainer(softwarePath);
            dynamic dynSoftware = softwareContainer.Software;
            dynamic? screen = null;

            if (dynSoftware is HmiTarget target) {
                screen = FindScreenInFolder(target.ScreenFolder, screenName);
            } else if (dynSoftware is HmiSoftware unified) {
                foreach (var s in unified.Screens) { if (s.Name == screenName) { screen = s; break; } }
            }

            if (screen == null) throw new System.Exception($"Screen '{screenName}' not found.");

            dynamic? faceplateItem = null;
            try {
                // Determine what type to create based on Classic vs Unified
                if (dynSoftware is HmiTarget) {
                    screen.ScreenItems.Create("HmiFaceplate", instanceName); 
                    foreach (var item in screen.ScreenItems) { if (item.Name == instanceName) { faceplateItem = item; break; } }
                } else {
                    // Unified
                    CreateHmiScreenItem(softwarePath, screenName, "HmiFaceplateContainer", instanceName);
                    foreach (var item in screen.ScreenItems) { if (item.Name == instanceName) { faceplateItem = item; break; } }
                }
            } catch (System.Exception ex) {
                throw new System.Exception($"Failed to instantiate faceplate container '{instanceName}': {ex.Message}");
            }

            if (faceplateItem == null) throw new System.Exception($"Could not locate the created faceplate container '{instanceName}'.");

            try {
                faceplateItem.ContainedType = containedTypeString;
                return $"Faceplate instance '{instanceName}' created and bound to '{containedTypeString}'.";
            } catch (System.Exception ex) {
                throw new System.Exception($"Created container, but failed to assign contained type '{containedTypeString}': {ex.Message}");
            }
        }
        public string DeleteHmiScreenItem(string softwarePath, string screenName, string itemName)
        {
            var softwareContainer = RequireHmiContainer(softwarePath);
            dynamic dynSoftware = softwareContainer.Software;
            dynamic? screen = null;

            if (dynSoftware is HmiTarget target) {
                screen = FindScreenInFolder(target.ScreenFolder, screenName);
            } else if (dynSoftware is HmiSoftware unified) {
                foreach (var s in unified.Screens) { if (s.Name == screenName) { screen = s; break; } }
            }

            if (screen == null) throw new System.Exception($"Screen '{screenName}' not found.");

            try {
                foreach (var item in screen.ScreenItems) {
                    if (item.Name == itemName) {
                        item.Delete();
                        return $"Deleted '{itemName}'.";
                    }
                }
            } catch { }

            try {
                foreach (var item in screen.Elements) {
                    if (item.Name == itemName) {
                        item.Delete();
                        return $"Deleted '{itemName}'.";
                    }
                }
            } catch { }

            throw new System.Exception($"Item '{itemName}' not found on screen '{screenName}'.");
        }
        public TiaMcpServer.ModelContextProtocol.HmiItemResult ManageHmiUnifiedFaceplate(string softwarePath, string screenName, string action, string itemName, string faceplateType, System.Collections.Generic.Dictionary<string, string?> interfaceTags)
        {
            var result = new TiaMcpServer.ModelContextProtocol.HmiItemResult { Action = action, ScreenName = screenName, ItemName = itemName };
            try
            {
                var softwareContainer = RequireHmiContainer(softwarePath);
                dynamic dynSoftware = softwareContainer.Software;
                if (!(dynSoftware is global::Siemens.Engineering.HmiUnified.HmiSoftware)) throw new System.Exception("This tool is strictly for WinCC Unified faceplates. HMI target is not WinCC Unified.");

                dynamic? screen = null;
                foreach (var s in dynSoftware.Screens) { if (s.Name == screenName) { screen = s; break; } }
                if (screen == null) throw new System.Exception($"Screen '{screenName}' not found.");

                dynamic? targetItem = null;
                try { foreach (var item in screen.ScreenItems) { if (item.Name == itemName) { targetItem = item; break; } } } catch { }
                if (targetItem == null) {
                    try { foreach (var item in screen.Elements) { if (item.Name == itemName) { targetItem = item; break; } } } catch { }
                }

                if (action == "create")
                {
                    if (targetItem == null)
                    {
                        try {
                            // Unified
                            object screenItems = screen.ScreenItems;
                            System.Type screenItemsType = screenItems.GetType();
                            var targetAssembly = screenItemsType.Assembly;
                            System.Type itemType = System.Linq.Enumerable.FirstOrDefault(targetAssembly.GetTypes(), t => t.Name.Equals("HmiFaceplateContainer", System.StringComparison.OrdinalIgnoreCase));
                            if (itemType != null) {
                                var createMethod = System.Linq.Enumerable.FirstOrDefault(screenItemsType.GetMethods(), m => m.Name == "Create" && m.IsGenericMethod);
                                if (createMethod != null) {
                                    var genericCreate = createMethod.MakeGenericMethod(itemType);
                                    genericCreate.Invoke(screenItems, new object[] { itemName });
                                }
                            }
                            // Re-fetch
                            try { foreach (var item in screen.ScreenItems) { if (item.Name == itemName) { targetItem = item; break; } } } catch { }
                            if (targetItem == null) {
                                try { foreach (var item in screen.Elements) { if (item.Name == itemName) { targetItem = item; break; } } } catch { }
                            }
                        } catch (System.Exception ex) {
                            throw new System.Exception($"Failed to create HmiFaceplateContainer: {ex.Message}");
                        }
                    }
                    if (targetItem == null) throw new System.Exception("Creation failed, item not found after creation.");
                    if (!string.IsNullOrEmpty(faceplateType)) targetItem.ContainedType = faceplateType;
                }

                if (targetItem == null) throw new System.Exception($"Item '{itemName}' not found for update.");
                
                // Map interface properties
                if (interfaceTags != null && interfaceTags.Count > 0)
                {
                    string targetTypeName = ((object)targetItem).GetType().Name;
                    if (targetTypeName.IndexOf("Faceplate", System.StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Item '{itemName}' is a {targetTypeName}, not a faceplate container, so it has no faceplate interface. Use 'hmi_manage_items' for ordinary screen items.");
                    }

                    var interfaceCol = targetItem.Interface;
                    foreach (var kvp in interfaceTags)
                    {
                        dynamic? ifaceProp = null;
                        try {
                            foreach (var ip in interfaceCol) {
                                if (ip.PropertyName == kvp.Key) {
                                    ifaceProp = ip; break;
                                }
                            }
                        } catch { }

                        if (ifaceProp != null)
                        {
                            try {
                                ifaceProp.Value = kvp.Value;
                            } catch (System.Exception ex) {
                                throw new System.Exception($"Failed to set property '{kvp.Key}' to '{kvp.Value}': {ex.Message}");
                            }
                        }
                        else
                        {
                            throw new System.Exception($"Interface property '{kvp.Key}' not found on faceplate.");
                        }
                    }
                }
                result.Status = "success";
            }
            catch (System.Exception ex)
            {
                result.Status = "error";
                result.Error = ex.Message;
                if (ex.InnerException != null) result.Error += " Inner: " + ex.InnerException.Message;
            }
            return result;
        }

        public string ConfigureHmiTrendCompanion(string softwarePath, string screenName, string companionName, string sourceTrendControlName)
        {
            var softwareContainer = RequireHmiContainer(softwarePath);
            dynamic dynSoftware = softwareContainer.Software;
            if (!(dynSoftware is HmiSoftware)) throw new System.Exception("This method is only supported for WinCC Unified (HmiSoftware).");

            dynamic? screen = null;
            foreach (var s in dynSoftware.Screens) { if (s.Name == screenName) { screen = s; break; } }
            if (screen == null) throw new System.Exception($"Screen '{screenName}' not found.");

            dynamic? companion = null;
            try { foreach (var item in screen.ScreenItems) { if (item.Name == companionName) { companion = item; break; } } } catch { }
            if (companion == null) throw new System.Exception($"TrendCompanion '{companionName}' not found on screen '{screenName}'.");

            companion.SourceTrendControl = sourceTrendControlName;

            return $"TrendCompanion '{companionName}' bound successfully to '{sourceTrendControlName}'.";
        }

        public string ConfigureHmiTrendControl(string softwarePath, string screenName, string trendControlName, string trendName, string dataSource, string? trendMode = null, int? lineWidth = null, string? lineColor = null)
        {
            var results = new System.Collections.Generic.List<string>();
            var softwareContainer = RequireHmiContainer(softwarePath);
            dynamic dynSoftware = softwareContainer.Software;
            if (!(dynSoftware is HmiSoftware)) throw new System.Exception("This method is only supported for WinCC Unified (HmiSoftware).");

            dynamic? screen = null;
            foreach (var s in dynSoftware.Screens) { if (s.Name == screenName) { screen = s; break; } }
            if (screen == null) throw new System.Exception($"Screen '{screenName}' not found.");

            dynamic? trendControl = null;
            try { foreach (var item in screen.ScreenItems) { if (item.Name == trendControlName) { trendControl = item; break; } } } catch { }
            if (trendControl == null) throw new System.Exception($"TrendControl '{trendControlName}' not found on screen '{screenName}'.");

            // Ensure TrendArea exists
            dynamic? trendArea = null;
            var trendAreas = trendControl.TrendAreas;
            foreach (var a in trendAreas)
            {
                trendArea = a; 
                break; // Just grab the first one for now
            }
            if (trendArea == null)
            {
                trendArea = trendAreas.Create("Area_1");
                results.Add("Created TrendArea");
            }

            // Create or Reuse Trend
            dynamic? trend = null;
            try {
                foreach (var t in trendArea.Trends)
                {
                    var sourceY = t.DataSourceY;
                    if (sourceY is not null && string.IsNullOrEmpty((string)sourceY.Source))
                    {
                        trend = t;
                        results.Add("Reused empty Trend");
                        break;
                    }
                }
            } catch { }

            if (trend == null) {
                trend = trendArea.Trends.Create();
                results.Add("Created new Trend");
            }

            // Set Data Source
            if (!string.IsNullOrEmpty(dataSource))
            {
                trend.DataSourceY.Source = dataSource;
                results.Add("DataSourceY=" + dataSource);
            }

            // Set DisplayName (trendName)
            if (!string.IsNullOrEmpty(trendName))
            {
                try {
                    foreach(var item in trend.DisplayName.Items) {
                        item.Text = $"<body><p>{System.Security.SecurityElement.Escape(trendName)}</p></body>";
                    }
                    results.Add("DisplayName=" + trendName);
                } catch { }
            }

            if (!string.IsNullOrEmpty(trendMode))
            {
                try
                {
                    var enumType = trend.TrendMode.GetType();
                    var enumVal = System.Enum.Parse(enumType, trendMode, true);
                    trend.TrendMode = enumVal;
                    results.Add("TrendMode");
                }
                catch { }
            }

            if (lineWidth.HasValue)
            {
                try
                {
                    trend.LineWidth = (byte)lineWidth.Value;
                    results.Add("LineWidth");
                }
                catch { }
            }

            if (lineColor is { Length: > 0 })
            {
                try
                {
                    System.Drawing.Color color;
                    if (lineColor.StartsWith("#")) color = System.Drawing.ColorTranslator.FromHtml(lineColor);
                    else color = System.Drawing.Color.FromName(lineColor);
                    trend.LineColor = color;
                    results.Add("LineColor=" + lineColor);
                }
                catch { }
            }

            return $"Trend added successfully to '{trendControlName}'. Configurations applied: {string.Join(", ", results)}";
        }
        public string SetHmiUnifiedScreenItemEvent(string softwarePath, string screenName, string itemName, string eventName, string scriptCode)
        {
            var softwareContainer = RequireHmiContainer(softwarePath);
            dynamic dynSoftware = softwareContainer.Software;
            if (!(dynSoftware is HmiSoftware)) throw new System.Exception("This method is only supported for WinCC Unified (HmiSoftware).");

            dynamic? screen = null;
            foreach (var s in dynSoftware.Screens) { if (s.Name == screenName) { screen = s; break; } }
            if (screen == null) throw new System.Exception($"Screen '{screenName}' not found.");

            dynamic? targetItem = null;
            try { foreach (var item in screen.ScreenItems) { if (item.Name == itemName) { targetItem = item; break; } } } catch { }
            if (targetItem == null) throw new System.Exception($"Item '{itemName}' not found.");

            object eventHandlers = targetItem.EventHandlers;
            if (eventHandlers == null) throw new System.Exception($"Item '{itemName}' does not support EventHandlers.");
            
            var compType = eventHandlers.GetType();
            var createMethod = compType.GetMethod("Create");
            if (createMethod == null) throw new System.Exception("EventHandlers composition does not have a Create method.");
            
            var paramType = createMethod.GetParameters()[0].ParameterType;
            object enumValue;
            try {
                enumValue = System.Enum.Parse(paramType, eventName, true);
            } catch {
                throw new System.Exception($"Event '{eventName}' is not valid. Valid events are: {string.Join(", ", System.Enum.GetNames(paramType))}");
            }
            
            dynamic? eventHandler = null;
            var findMethod = compType.GetMethod("Find");
            if (findMethod != null) {
                eventHandler = findMethod.Invoke(eventHandlers, new[] { enumValue });
            }
            
            if (eventHandler == null) {
                eventHandler = createMethod.Invoke(eventHandlers, new[] { enumValue });
            }
            
            eventHandler.Script.ScriptCode = scriptCode;

            return $"Successfully set '{eventName}' event handler for item '{itemName}'.";
        }
    }
}







