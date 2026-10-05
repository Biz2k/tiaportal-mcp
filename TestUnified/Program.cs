using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

class Program {
    static async Task Main() {
        var exePath = @"C:\Users\Biz\Antigravity\MCP_TIA_Portal\src\TiaMcpServer\bin\Debug\net48\TiaMcpServer.exe";
        var psi = new ProcessStartInfo(exePath) {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        var process = Process.Start(psi);
        var req = "{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"tools/call\", \"params\": {\"name\": \"connect\", \"arguments\": {}}}\r\n";
        await process.StandardInput.WriteAsync(req);
        var buffer = new char[32768];
        var read = await process.StandardOutput.ReadAsync(buffer, 0, buffer.Length);
        
        var req2 = "{\"jsonrpc\": \"2.0\", \"id\": 2, \"method\": \"tools/call\", \"params\": {\"name\": \"plc_manage_tag_table_entries\", \"arguments\": {\"softwarePath\": \"PLC_1\", \"tagTablePath\": \"Default tag table\", \"actions\": [{\"action\": \"create\", \"name\": \"BatchTag1\", \"dataType\": \"Bool\", \"logicalAddress\": \"%M10.0\"}]}}}\r\n";
        await process.StandardInput.WriteAsync(req2);
        read = await process.StandardOutput.ReadAsync(buffer, 0, buffer.Length);
        Console.WriteLine("Manage Tags: " + new string(buffer, 0, read));

        var req3 = "{\"jsonrpc\": \"2.0\", \"id\": 3, \"method\": \"tools/call\", \"params\": {\"name\": \"plc_get_block_data\", \"arguments\": {\"softwarePath\": \"PLC_1\", \"blockName\": \"Main\", \"parts\": [\"metadata\", \"interface\"]}}}\r\n";
        await process.StandardInput.WriteAsync(req3);
        read = await process.StandardOutput.ReadAsync(buffer, 0, buffer.Length);
        Console.WriteLine("Get Block Data: " + new string(buffer, 0, read));

        process.Kill();
    }
}
