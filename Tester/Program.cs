using System;
using System.Reflection;

namespace Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args2) => {
                if (args2.Name.StartsWith("Siemens.Engineering")) {
                    var name = new AssemblyName(args2.Name).Name;
                    string path = @"C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48\" + name + ".dll";
                    if (System.IO.File.Exists(path)) return Assembly.LoadFrom(path);
                }
                return null;
            };

            RunTests();
        }

        static void RunTests()
        {
            try
            {
                Console.WriteLine("Connecting to TIA Portal...");
                var portal = new TiaMcpServer.Siemens.Portal();
                if (!portal.ConnectPortal()) {
                    Console.WriteLine("Failed to connect to TIA Portal.");
                    return;
                }
                
                Console.WriteLine("Finding HMI...");
                string hmiPath = "HMI (A7)/HMI_RT_3";
                Console.WriteLine($"Found HMI Software Path: {hmiPath}");

                Console.WriteLine("\nCleaning up and Testing Trend/Companion...");
                try { portal.DeleteHmiScreenItem(hmiPath, "A7", "UserTrendControl"); } catch { }
                try { portal.DeleteHmiScreenItem(hmiPath, "A7", "UserTrendCompanion"); } catch { }
                
                try {
                    Console.WriteLine("Creating UserTrendControl...");
                    try { portal.CreateHmiScreenItem(hmiPath, "A7", "HmiTrendControl", "UserTrendControl"); } catch { }
                    portal.ConfigureHmiScreenItem(hmiPath, "A7", "UserTrendControl", left: 10, top: 10, width: 900, height: 450, null, null);
                    
                    Console.WriteLine("Creating UserTrendCompanion...");
                    try { portal.CreateHmiScreenItem(hmiPath, "A7", "HmiTrendCompanion", "UserTrendCompanion"); } catch { }
                    portal.ConfigureHmiScreenItem(hmiPath, "A7", "UserTrendCompanion", left: 10, top: 470, width: 900, height: 250, null, null);
                    
                    // Bind Companion to Control using MCP method
                    string compResult = portal.ConfigureHmiTrendCompanion(hmiPath, "A7", "UserTrendCompanion", "UserTrendControl");
                    Console.WriteLine(compResult);

                    // Add Pen 1
                    string result1 = portal.ConfigureHmiTrendControl(hmiPath, "A7", "UserTrendControl", "Струм фази W", "Насосний агрегат СР1 Фаза W - Значення струму", "Interpolated", 3, "Red");
                    Console.WriteLine(result1);

                    // Add Pen 2
                    string result2 = portal.ConfigureHmiTrendControl(hmiPath, "A7", "UserTrendControl", "Потужність", "Насосний агрегат СР1 потужність", "Interpolated", 3, "Blue");
                    Console.WriteLine(result2);

                } catch(Exception e) {
                    Console.WriteLine($"Error: {e.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.ToString());
            }
        }
    }
}
