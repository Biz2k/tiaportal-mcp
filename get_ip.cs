using System;
using System.IO;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;

class Program {
    static void Main() {
        try {
            Console.WriteLine("Connecting to TIA Portal...");
            var processes = TiaPortal.GetProcesses();
            var process = processes.FirstOrDefault(p => p.ProjectPath != null);
            if (process == null) {
                Console.WriteLine("No TIA Portal with an open project found.");
                return;
            }
            
            var portal = process.Attach();
            var project = portal.Projects.First();
            Console.WriteLine("Connected to: " + project.Name);
            
            foreach (var device in project.Devices) {
                foreach (var deviceItem in device.DeviceItems) {
                    var networkInterface = deviceItem.GetService<NetworkInterface>();
                    if (networkInterface != null) {
                        foreach (var node in networkInterface.Nodes) {
                            var ip = node.GetAttribute("Address");
                            var subnet = node.GetAttribute("SubnetMask");
                            Console.WriteLine($"Found IP: {ip}, Subnet: {subnet} on {deviceItem.Name}");
                        }
                    }
                }
            }
        } catch (Exception ex) {
            Console.WriteLine("ERROR: " + ex.ToString());
        }
    }
}
