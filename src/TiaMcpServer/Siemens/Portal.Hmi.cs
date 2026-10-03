using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.HmiUnified;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    public class HmiScreenInfo {
        public string Name { get; set; } = string.Empty;
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
                    screens.Add(new HmiScreenInfo { Name = screen.Name });
                }
            }
            else if (softwareContainer.Software is HmiSoftware unifiedTarget)
            {
                foreach (var screen in unifiedTarget.Screens)
                {
                    screens.Add(new HmiScreenInfo { Name = screen.Name });
                }
            }

            return screens;
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
    }
}
