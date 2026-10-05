using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

class Program {
    static void Main() {
        var dir = @""src\TiaMcpServer\ModelContextProtocol"";
        var files = Directory.GetFiles(dir, ""*.cs"", SearchOption.AllDirectories);
        var tools = new SortedDictionary<string, string>();
        
        foreach(var file in files) {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++) {
                var line = lines[i];
                var match = Regex.Match(line, @""\[McpServerTool\(Name\s*=\s*\""([^\""]+)\""""");
                if (match.Success) {
                    var toolName = match.Groups[1].Value;
                    string desc = """";
                    
                    // Look around for Description attribute
                    for(int j = Math.Max(0, i-2); j <= Math.Min(lines.Length-1, i+2); j++) {
                        var descMatch = Regex.Match(lines[j], @""Description\(\""(.*?[^\\])\""\)"");
                        if (descMatch.Success) {
                            desc = descMatch.Groups[1].Value;
                            break;
                        }
                        descMatch = Regex.Match(lines[j], @""Title\s*=\s*\""(.*?)\""""");
                        if (descMatch.Success && string.IsNullOrEmpty(desc)) {
                            desc = descMatch.Groups[1].Value;
                        }
                    }
                    tools[toolName] = desc;
                }
            }
        }
        
        var mdPath = ""Implemented_Tools.md"";
        using (var sw = new StreamWriter(mdPath, false, new System.Text.UTF8Encoding(false))) {
            sw.WriteLine(""# Инструменты MCP-сервера TIA Portal"");
            sw.WriteLine();
            foreach(var kvp in tools) {
                sw.WriteLine($""`{kvp.Key}`: {kvp.Value}"");
            }
        }
    }
}
