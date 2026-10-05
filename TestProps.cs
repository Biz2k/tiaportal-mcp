using System;
using System.Reflection;
using Siemens.Engineering;
using Siemens.Engineering.HmiUnified;
using System.Linq;

class Program {
    static void Main() {
        var processes = TiaPortal.GetProcesses();
        var portal = processes[0].Attach();
        var proj = portal.Projects[0];
        
        dynamic target = proj.Devices[0].DeviceItems[1].DeviceItems[0]; // Need to iterate to HMI (A7)/HMI_RT_3
        // Actually, it's easier to iterate all HmiSoftware:
        foreach(var dev in proj.Devices) {
            foreach(var item in dev.DeviceItems) {
                foreach(var sub in item.DeviceItems) {
                    var svc = sub.GetService<Siemens.Engineering.HmiUnified.HmiSoftware>();
                    if (svc != null && svc.Name == "HMI_RT_3") {
                        foreach(var screen in svc.Screens) {
                            if (screen.Name == "A7") {
                                var fp = screen.ScreenItems.Find("TestFaceplate");
                                if (fp != null) {
                                    Console.WriteLine("Type: " + fp.GetType().FullName);
                                    foreach(var prop in fp.GetType().GetProperties()) {
                                        Console.WriteLine("Prop: " + prop.Name);
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
