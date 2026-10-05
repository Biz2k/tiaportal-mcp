using System.Collections.Generic;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseHmiManageItems : ResponseMessage
    {
        public List<HmiItemResult> Results { get; set; }
        public int SuccessCount { get; set; }
    }

    public class HmiItemResult
    {
        public string Action { get; set; }
        public string ScreenName { get; set; }
        public string ItemName { get; set; }
        public string Status { get; set; }
        public string Error { get; set; }
    }
}
