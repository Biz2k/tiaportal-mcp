using System;
using System.Text.Json;
using TiaMcpServer.ModelContextProtocol;

class Program {
    static void Main(string[] args) {
        Console.WriteLine("Testing TiaMcpServer...");
        try {
            var connectResponse = McpServer.ConnectProject(new ConnectArgs { Path = @"C:\Users\Biz\Desktop\21474_SEVGOK_P2_v4_V21_18_26\21474_SEVGOK_P2_v4_V21.ap21", V = "21.0" });
            Console.WriteLine(JsonSerializer.Serialize(connectResponse));

            var plcRoot = McpServer.ResolveObjectPath("PLC_1");
            Console.WriteLine($"Resolved PLC_1: {plcRoot.Path}");
            
            var compileResponse = McpServer.CompileSoftware(plcRoot.Path);
            Console.WriteLine($"Compile Software: {JsonSerializer.Serialize(compileResponse)}");
            
            string sclCode = @"
FUNCTION_BLOCK ""AI_Test_Block""
{ S7_Optimized_Access := 'TRUE' }
VERSION : 0.1
   VAR 
      MyVar : Bool;
   END_VAR
BEGIN
   #MyVar := true;
END_FUNCTION_BLOCK
";
            var generateResponse = McpServer.CreateSclBlock(plcRoot.Path, sclCode, "");
            Console.WriteLine($"Create SCL Block: {JsonSerializer.Serialize(generateResponse)}");
            
            // test HMI screens
            try {
                var hmiRoot = McpServer.ResolveObjectPath("HMI_1");
                var screens = McpServer.GetHmiScreens(hmiRoot.Path);
                Console.WriteLine($"HMI Screens: {JsonSerializer.Serialize(screens)}");
                var tags = McpServer.GetHmiTags(hmiRoot.Path);
                Console.WriteLine($"HMI Tags: {JsonSerializer.Serialize(tags)}");
            } catch (Exception ex) {
                Console.WriteLine($"HMI Test Failed: {ex.Message}");
            }

        } catch (Exception ex) {
            Console.WriteLine($"Error: {ex}");
        }
    }
}
