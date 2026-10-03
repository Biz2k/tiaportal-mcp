using System;
using System.Reflection;
using System.IO;
using System.Linq;

class Program
{
    static void Main()
    {
        string path = @"c:\Users\Biz\Antigravity\MCP_TIA_Portal\src\TiaMcpServer\bin\Debug\net48\Siemens.Engineering.dll";
        var asm = Assembly.LoadFrom(path);
        
        var unifiedScreen = asm.GetType("Siemens.Engineering.HmiUnified.UI.Screen");
        if (unifiedScreen != null)
        {
            Console.WriteLine("--- WinCC Unified Screen Properties ---");
            foreach (var prop in unifiedScreen.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Console.WriteLine(string.Format("{0} ({1})", prop.Name, prop.PropertyType.Name));
            }
        }
        else
        {
            Console.WriteLine("WinCC Unified Screen type not found.");
        }

        var comfortScreen = asm.GetType("Siemens.Engineering.Hmi.Screen.Screen");
        if (comfortScreen != null)
        {
            Console.WriteLine("\n--- WinCC Comfort Screen Properties ---");
            foreach (var prop in comfortScreen.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Console.WriteLine(string.Format("{0} ({1})", prop.Name, prop.PropertyType.Name));
            }
        }
    }
}
