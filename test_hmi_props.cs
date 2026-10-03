using System;
using System.Reflection;

class Program {
    static void Main(string[] args) {
        var asm = Assembly.LoadFrom(@""C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48\Siemens.Engineering.dll"");
        var type = asm.GetType(""Siemens.Engineering.Hmi.HmiTarget"");
        if (type != null) {
            foreach (var prop in type.GetProperties()) {
                Console.WriteLine($""Property: {prop.Name} ({prop.PropertyType.Name})"");
            }
        }
    }
}
