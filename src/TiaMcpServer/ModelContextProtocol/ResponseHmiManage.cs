using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseHmiManage
    {
        public string? Message { get; set; }
        public JsonObject? Meta { get; set; }
    }
}
