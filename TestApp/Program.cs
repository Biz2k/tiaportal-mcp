using System;
using System.Text.Json;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;
using System.Reflection;
using System.Linq;

class Program {
    static void Main(string[] args) {
        Console.WriteLine("Testing TiaMcpServer...");
        TiaMcpServer.Siemens.Openness.Initialize(21);
        AppDomain.CurrentDomain.AssemblyResolve += TiaMcpServer.Siemens.Engineering.Resolver;
        Run();
    }

    static void Run() {
        try {
            var connectResponse = McpServer.OpenTiaProject(@"C:\Users\Biz\Desktop\21474_SEVGOK_P2_v4_V21_18_26\21474_SEVGOK_P2_v4_V21.ap21");
            Console.WriteLine(JsonSerializer.Serialize(connectResponse));

            var project = McpServer.Portal.Project;

            // 1. Check Project Library for Faceplates
            Console.WriteLine("--- Project Library Types ---");
            dynamic projLib = project.ProjectLibrary;
            foreach(var type in projLib.TypeFolder.Types) {
                Console.WriteLine($"Type: {type.Name}, Class: {type.GetType().Name}");
            }

            // 2. Test Creating a Screen
            Console.WriteLine("--- Creating Screen ---");
            var softwareContainer = McpServer.Portal.GetSoftwareContainer("HMI_RT_3");
            dynamic dynSoftware = softwareContainer.Software;
            
            dynamic newScreen = null;
            try {
                newScreen = dynSoftware.Screens.Create("NewTestScreen");
                Console.WriteLine("Screen created in Unified!");
            } catch (Exception ex) {
                Console.WriteLine("Unified Create Failed: " + ex.Message);
            }

            // 3. Test Creating an Item
            if (newScreen != null) {
                try {
                    object screenItems = newScreen.ScreenItems;
                    Type screenItemsType = screenItems.GetType();
                    Console.WriteLine("ScreenItems type: " + screenItemsType.FullName);

                    // Find the type of HmiButton
                    var targetAssembly = screenItemsType.Assembly;
                    if (targetAssembly != null) {
                        Type btnType = targetAssembly.GetTypes().FirstOrDefault(t => t.Name == "HmiButton");
                        if (btnType != null) {
                            Console.WriteLine("Found HmiButton type: " + btnType.FullName);
                            var createMethod = screenItemsType.GetMethods().FirstOrDefault(m => m.Name == "Create" && m.IsGenericMethod);
                            if (createMethod != null) {
                                var genericCreate = createMethod.MakeGenericMethod(btnType);
                                var newBtn = genericCreate.Invoke(screenItems, new object[] { "MyReflectButton" });
                                Console.WriteLine("Successfully created button via reflection!");
                            } else {
                                Console.WriteLine("Create generic method not found on ScreenItems.");
                            }
                        } else {
                            Console.WriteLine("HmiButton type not found in assembly.");
                        }
                    } else {
                        Console.WriteLine("HmiUnified assembly not loaded.");
                    }
                } catch (Exception ex) {
                    Console.WriteLine("Reflection create failed: " + ex.Message);
                }
            }
        } catch (Exception ex) {
            Console.WriteLine($"Error: {ex}");
        }
    }
}
