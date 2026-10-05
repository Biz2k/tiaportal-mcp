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
                
                try {
                    Console.WriteLine("\n--- LISTING LIBRARY TYPES ---");
                    var types = portal.ListHmiLibraryTypes();
                    foreach (var t in types) {
                        Console.WriteLine($"\nName: {t["Name"]}, Path: {t["Path"]}, Kind: {t["Kind"]}, TargetSystem: {t["TargetSystem"]}");
                        if (t.ContainsKey("Versions") && t["Versions"] is System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>> versions) {
                            foreach (var v in versions) {
                                Console.WriteLine($"  Version: {v["Version"]} (State: {v["State"]})");
                                if (v.ContainsKey("ContainedTypeFormat")) {
                                    Console.WriteLine($"  ContainedTypeFormat: {v["ContainedTypeFormat"]}");
                                }
                            }
                        }
                    }
                    
                    Console.WriteLine("\n--- SPAWNING AI_setting TO GET DETAILS ---");
                    // Assuming screen "1_Pump_station" exists on "HMI (A7)/HMI_RT_3" from previous tests
                    string hmiPath = "HMI (A7)/HMI_RT_3";
                    string screenName = "1_Pump_station";
                    string instanceName = "Temp_AI_Setting_Instance";
                    
                    try { portal.DeleteHmiScreenItem(hmiPath, screenName, instanceName); } catch { } // Clean up just in case
                    
                    string res = portal.CreateHmiFaceplateInstance(hmiPath, screenName, instanceName, "V0.0.54\\AI_setting");
                    Console.WriteLine(res);
                    
                    Console.WriteLine("\n--- DETAILS FOR Temp_AI_Setting_Instance ---");
                    var props = portal.GetHmiScreenItemProperties(hmiPath, screenName, instanceName);
                    if (props.ContainsKey("_Interface")) {
                        var ifaceList = props["_Interface"] as System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>;
                        foreach(var iDict in ifaceList) {
                            Console.WriteLine($"\n[Interface Element] Type: {iDict["__Type"]}");
                            foreach(var kvp in iDict) {
                                if (kvp.Key != "__Type") Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                            }
                        }
                    } else {
                        Console.WriteLine("No _Interface found.");
                    }
                    
                } catch(Exception e) {
                    Console.WriteLine($"Error: {e.Message}\n{e.StackTrace}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.ToString());
            }
        }
    }
}
