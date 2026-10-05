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
                    Console.WriteLine("\n--- SPAWN HMI_Analog_Valve ---");
                    string hmiPath = "HMI (A7)/HMI_RT_3";
                    string screenName = "A7"; // User said "A7"
                    string instanceName = "Test_Analog_Valve";
                    try { portal.DeleteHmiScreenItem(hmiPath, screenName, instanceName); } catch { }
                    
                    string res = portal.CreateHmiFaceplateInstance(hmiPath, screenName, instanceName, "V0.0.3\\HMI_Analog_Valve");
                    Console.WriteLine(res);
                    
                    var props = portal.GetHmiScreenItemProperties(hmiPath, screenName, instanceName);
                    if (props.ContainsKey("_Interface")) {
                        var ifaceList = props["_Interface"] as System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>;
                        foreach(var iDict in ifaceList) {
                            Console.WriteLine($"\n[Interface Element] Type: {iDict["__Type"]}");
                            foreach(var kvp in iDict) {
                                if (kvp.Key != "__Type") Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                            }
                        }
                    }
                    
                    // Now let's find the first tag to bind
                    string firstTag = "";
                    var softwareContainer = portal.GetSoftwareContainer(hmiPath);
                    dynamic dynSoftware = softwareContainer.GetType().GetProperty("Software").GetValue(softwareContainer);
                    try {
                        foreach(var tt in dynSoftware.TagFolder.TagTables) {
                            foreach(var t in tt.Tags) {
                                firstTag = t.Name;
                                break;
                            }
                            if (firstTag != "") break;
                        }
                    } catch {}
                    Console.WriteLine($"Found Tag to bind: {firstTag}");
                    
                    dynamic screen = null;
                    foreach(var s in dynSoftware.Screens) { if (s.Name == screenName) { screen = s; break; } }
                    
                    dynamic faceplateItem = null;
                    foreach(var i in screen.ScreenItems) { if (i.Name == instanceName) { faceplateItem = i; break; } }
                    
                    try {
                        string resSet = portal.SetHmiScreenItemProperty(hmiPath, screenName, instanceName, "Interface_Tag_1", firstTag);
                        Console.WriteLine(resSet);
                        
                        string resSet2 = portal.SetHmiScreenItemProperty(hmiPath, screenName, instanceName, "Valve_Name", "Main Valve");
                        Console.WriteLine(resSet2);
                    } catch (Exception ex) {
                        Console.WriteLine($"Failed to set Faceplate property: {ex.Message}");
                    }
                    
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
