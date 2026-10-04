using System;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.Download;
using Siemens.Engineering.HW;

namespace TestApp
{
    class Program
    {
        static void Main()
        {
            try
            {
                TiaMcpServer.Siemens.Openness.Initialize(21);
                AppDomain.CurrentDomain.AssemblyResolve += TiaMcpServer.Siemens.Engineering.Resolver;
                Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine("INIT ERROR: " + ex.Message);
            }
        }

        static void Run()
        {
            try
            {
                var portalWrapper = new TiaMcpServer.Siemens.Portal();
                Console.WriteLine("Connecting to Portal...");
                if (portalWrapper.ConnectPortal())
                {
                    Console.WriteLine("Calling StartPlcSimAndDownloadAll()...");
                    var result = portalWrapper.StartPlcSimAndDownloadAll();
                    Console.WriteLine("Result:");
                    Console.WriteLine(result);
                }
            } catch (Exception ex) {
                Console.WriteLine("ERROR: " + ex.ToString());
            }
        }
    }
}
