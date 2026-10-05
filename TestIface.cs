using System;
using System.Reflection;
using Siemens.Engineering;
using Siemens.Engineering.HmiUnified;
using System.Linq;

class Program {
    static void Main() {
        var processes = TiaPortal.GetProcesses();
        if (processes.Count == 0) return;
        var portal = processes[0].Attach();
        var proj = portal.Projects[0];
        
        foreach(var dev in proj.Devices) {
            foreach(var item in dev.DeviceItems) {
                foreach(var sub in item.DeviceItems) {
                    var svc = sub.GetService<Siemens.Engineering.HmiUnified.HmiSoftware>();
                    if (svc != null && svc.Name == "HMI_RT_3") {
                        foreach(var screen in svc.Screens) {
                            if (screen.Name == "A7") {
                                var fp = screen.ScreenItems.Find("TestFaceplate");
                                if (fp != null) {
                                    Console.WriteLine("Found TestFaceplate");
                                    var propInfo = fp.GetType().GetProperty("Interface");
                                    if (propInfo != null) {
                                        var iface = propInfo.GetValue(fp);
                                        Console.WriteLine("Interface object: " + (iface != null ? iface.GetType().Name : "null"));
                                        if (iface != null) {
                                            if (iface is System.Collections.IEnumerable en) {
                                                foreach(var child in en) {
                                                    Console.WriteLine("Child: " + child.GetType().Name);
                                                    var nameProp = child.GetType().GetProperty("Name");
                                                    if (nameProp != null) Console.WriteLine("  Name: " + nameProp.GetValue(child));
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
