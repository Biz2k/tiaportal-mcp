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

                    // Reflect over Screen Collections
                    var softwareContainer = portal.GetSoftwareContainer(hmiPath);
                    dynamic dynSoftware = softwareContainer.GetType().GetProperty("Software").GetValue(softwareContainer);
                    dynamic screen = null;
                    foreach(var s in dynSoftware.Screens) { if (s.Name == "7_Trends") { screen = s; break; } }
                    
                    if (screen != null) {
                        Console.WriteLine("--- SCREEN REFLECTION ---");
                        foreach (var prop in screen.GetType().GetProperties()) {
                            try {
                                var val = prop.GetValue(screen);
                                Console.WriteLine($"[Prop] {prop.Name} = {(val == null ? "null" : val.GetType().Name)}");
                            } catch { }
                        }
                    }
                    
                    Console.WriteLine("--- DEBUGGING EVENTHANDLERS ---");
                    try {
                        var evts = screen.EventHandlers;
                        Console.WriteLine($"EventHandlers count: {evts.Count}");
                        foreach (var evt in evts) {
                            try {
                                Console.WriteLine($"  Event Type: {evt.GetType().Name}");
                                foreach(var p in evt.GetType().GetProperties()) {
                                    try { Console.WriteLine($"    [Prop] {p.Name} = {p.GetValue(evt)}"); } catch (Exception ex) { Console.WriteLine($"    [Prop] {p.Name} (Error: {ex.Message})"); }
                                }
                            } catch (Exception ex) {
                                Console.WriteLine($"  Error reading event: {ex.Message}");
                            }
                        }
                    } catch (Exception e) { Console.WriteLine($"Error getting EventHandlers: {e.Message}"); }
                    
                    Console.WriteLine("--- DEBUGGING PROPERTYEVENTHANDLERS ---");
                    try {
                        var evts = screen.PropertyEventHandlers;
                        Console.WriteLine($"PropertyEventHandlers count: {evts.Count}");
                        foreach (var evt in evts) {
                            try { Console.WriteLine($"  Event: {evt.Name}"); } catch {}
                        }
                    } catch (Exception e) { Console.WriteLine($"Error getting PropertyEventHandlers: {e.Message}"); }
                    
                    Console.WriteLine("--- DEBUGGING EXPRESSIONS ---");
                    try {
                        var exprs = screen.GetType().GetProperty("Expressions");
                        if (exprs != null) {
                            var exprVal = exprs.GetValue(screen);
                            Console.WriteLine($"Expressions count: {((dynamic)exprVal).Count}");
                        } else {
                            Console.WriteLine("Property 'Expressions' not found via Reflection.");
                        }
                    } catch (Exception e) { Console.WriteLine($"Error getting Expressions: {e.Message}"); }
                    
                    Console.WriteLine("--- DEBUGGING HMI CONNECTIONS ---");
                    try {
                        var connections = dynSoftware.GetType().GetProperty("Connections");
                        if (connections != null) {
                            var connVal = connections.GetValue(dynSoftware);
                            Console.WriteLine($"Connections count: {((dynamic)connVal).Count}");
                            foreach(var conn in (dynamic)connVal) {
                                Console.WriteLine($"  Connection: {conn.Name}");
                                try {
                                    foreach(var attr in ((dynamic)conn).GetAttributeInfos()) {
                                        try { Console.WriteLine($"    [Attr] {attr.Name} = {((dynamic)conn).GetAttribute(attr.Name)}"); } catch {}
                                    }
                                } catch (Exception e) { Console.WriteLine($"    Error getting attributes: {e.Message}"); }
                            }
                        } else {
                            Console.WriteLine("Property 'Connections' not found. Checking HmiConnections...");
                            var hmiconnections = dynSoftware.GetType().GetProperty("HmiConnections");
                            if (hmiconnections != null) {
                                var connVal = hmiconnections.GetValue(dynSoftware);
                                Console.WriteLine($"HmiConnections count: {((dynamic)connVal).Count}");
                            } else {
                                Console.WriteLine("Property 'HmiConnections' not found either. Printing all Software properties:");
                                foreach (var prop in dynSoftware.GetType().GetProperties()) {
                                    Console.WriteLine($"  [Prop] {prop.Name}");
                                }
                            }
                        }
                    } catch (Exception e) { Console.WriteLine($"Error getting connections: {e.Message}"); }
                    
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
