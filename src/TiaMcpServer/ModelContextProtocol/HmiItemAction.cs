using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class HmiItemAction
    {
        public string action { get; set; }
        public string screenName { get; set; }
        public string itemName { get; set; }
        public string itemType { get; set; }
        public int? left { get; set; }
        public int? top { get; set; }
        public int? width { get; set; }
        public int? height { get; set; }
        public string text { get; set; }
        public Dictionary<string, object> properties { get; set; }
    }
}
