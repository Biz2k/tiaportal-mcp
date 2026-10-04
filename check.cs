using System;
using System.Reflection;
using System.Collections;
using System.Linq;
using System.Threading;

class Program {
    static void Main() {
        try {
            Console.WriteLine("Loading PLCSIM Advanced API...");
            var asm = Assembly.LoadFrom(@"C:\Program Files\Siemens\Automation\PLCSIM_V21\resources\bin\wwwroot\assets\lib\runtime\Siemens.Simatic.Simulation.Runtime.Api.x64.dll");
            var managerType = asm.GetType("Siemens.Simatic.Simulation.Runtime.SimulationRuntimeManager");
            var manager = managerType.GetProperty("LocalRuntimeManagerInstance").GetValue(null, null);
            
            Console.WriteLine("Setting NetworkMode to TCPIPMultipleAdapter...");
            var eNetworkMode = asm.GetType("Siemens.Simatic.Simulation.Runtime.ENetworkMode");
            managerType.GetProperty("NetworkMode").SetValue(manager, Enum.Parse(eNetworkMode, "TCPIPMultipleAdapter"), null);
            
            Console.WriteLine("Finding physical interface...");
            var netInterfaces = (IEnumerable)managerType.GetProperty("NetInterfaces").GetValue(manager, null);
            object selectedInterface = null;
            foreach (var ni in netInterfaces) {
                var name = ni.GetType().GetField("interfaceName").GetValue(ni).ToString();
                if (!name.Contains("Virtual") && !name.Contains("vEthernet") && !name.Contains("VMware") && name.Contains("Ethernet")) {
                    selectedInterface = ni;
                    break;
                }
            }
            
            if (selectedInterface == null) {
                Console.WriteLine("Could not find a suitable physical interface!");
                return;
            }
            
            var ifName = selectedInterface.GetType().GetField("interfaceName").GetValue(selectedInterface).ToString();
            var ifDesc = selectedInterface.GetType().GetField("interfaceDescription").GetValue(selectedInterface).ToString();
            var ifIndex = (uint)selectedInterface.GetType().GetField("interfaceIndex").GetValue(selectedInterface);
            Console.WriteLine(string.Format("Selected Interface: {0} / {1} (Index: {2})", ifName, ifDesc, ifIndex));
            
            Console.WriteLine("Registering instance...");
            var eCpuType = asm.GetType("Siemens.Simatic.Simulation.Runtime.ECPUType");
            var cpuTypeVal = Enum.Parse(eCpuType, "CPU1500_Unspecified");
            object[] regArgs = new object[] { cpuTypeVal, "AutoTestInstance" };
            var instance = managerType.GetMethod("RegisterInstance", new Type[] { eCpuType, typeof(string) }).Invoke(manager, regArgs);
            
            Console.WriteLine("Mapping IE1 to the interface...");
            var iInstanceType = asm.GetType("Siemens.Simatic.Simulation.Runtime.IInstance");
            var ePlcInterface = asm.GetType("Siemens.Simatic.Simulation.Runtime.EPLCInterface");
            var ie1 = Enum.Parse(ePlcInterface, "IE1");
            
            var setMappingMethod = iInstanceType.GetMethod("SetNetInterfaceMapping", new Type[] { ePlcInterface, typeof(uint) });
            setMappingMethod.Invoke(instance, new object[] { ie1, ifIndex });
            
            Console.WriteLine("Configuring Virtual Switch bindings...");
            try {
                var setBindingsMethod = managerType.GetMethod("SetNetInterfaceBindings", new Type[] { typeof(uint) });
                if (setBindingsMethod != null) {
                    setBindingsMethod.Invoke(manager, new object[] { 0u });
                } else {
                    var method = managerType.GetMethod("SetNetInterfaceBindings", Type.EmptyTypes);
                    if (method != null) {
                        method.Invoke(manager, null);
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("SetNetInterfaceBindings warning (might require admin): " + ex.Message);
            }
            
            Console.WriteLine("Dumping IInstance methods...");
            foreach (var method in iInstanceType.GetMethods()) {
                if (method.Name == "SetIPSuite") {
                    Console.WriteLine("Method: " + method.Name);
                    foreach (var p in method.GetParameters()) {
                        Console.WriteLine($"  Param: {p.Name} ({p.ParameterType.Name})");
                    }
                }
            }
        } catch (Exception ex) {
            Console.WriteLine("ERROR: " + ex.ToString());
        }
    }
}
