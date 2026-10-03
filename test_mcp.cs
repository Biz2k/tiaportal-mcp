using System;
using System.Linq;
using TiaMcpServer.ModelContextProtocol;
using System.Text.Json;

class Program {
    static void Main(string[] args) {
        Console.WriteLine(""Testing TiaMcpServer..."");
        try {
            var connectResponse = McpServer.ConnectProject();
            Console.WriteLine($""Connect Response: {JsonSerializer.Serialize(connectResponse)}"");

            var treeResponse = McpServer.GetProjectTree();
            Console.WriteLine($""Tree Response: {JsonSerializer.Serialize(treeResponse)}"");
            
            // Search for plc software
            var plcSoftware = McpServer.ResolveObjectPath(""PLC_1""); // Example
            Console.WriteLine($""PLC Software: {JsonSerializer.Serialize(plcSoftware)}"");
        } catch (Exception ex) {
            Console.WriteLine($""Error: {ex}"");
        }
    }
}
