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
        public string Text { get; set; }
        public string ProcessValue { get; set; }
        public List<string> Events { get; set; } = new List<string>();
    }

    public class HmiTagInfo {
        public string Name { get; set; } = string.Empty;
    }

    public partial class Portal
    {
        public List<HmiScreenInfo> GetHmiScreens(string softwarePath)
        {
            var screens = new List<HmiScreenInfo>();
            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer == null) return screens;

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
            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer == null) return tags;

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
            var softwareContainer = GetSoftwareContainer(softwarePath);
            var items = new List<HmiScreenItemInfo>();

            if (softwareContainer == null || softwareContainer.Software == null)
            {
                throw new System.Exception($"HMI Software '{softwarePath}' not found");
            }

            dynamic dynSoftware = softwareContainer.Software;
            object targetScreen = null;

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
            return info;
        }

        public object DebugScreenItem(string softwarePath, string screenName, string itemName)
        {
            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer == null || softwareContainer.Software == null) throw new System.Exception("Not found");
            
            dynamic dynSoftware = softwareContainer.Software;
            object targetScreen = null;
            try {
                targetScreen = FindScreenInFolder(dynSoftware.ScreenFolder, screenName);
            } catch (System.Exception) {
                foreach (var screen in dynSoftware.Screens) { if (screen.Name == screenName) { targetScreen = screen; break; } }
            }
            if (targetScreen == null) throw new System.Exception("Screen not found");

            dynamic dynScreen = targetScreen;
            object targetItem = null;
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
    }
}
