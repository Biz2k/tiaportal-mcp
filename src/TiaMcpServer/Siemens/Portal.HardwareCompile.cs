using Siemens.Engineering.Compiler;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        /// <summary>
        /// Compiles a device: Device.GetService&lt;ICompilable&gt;().Compile(). Tried on V21 (2026-10-07): it compiles the
        /// hardware configuration AND the software on the device (a PLC with broken blocks answers with their errors
        /// under "Program blocks"). The result holds the tree "CPU / Hardware configuration / station / rack / module / object" with
        /// the texts on leaves that have no path of their own, and the warnings of other devices the compile touched
        /// (one-sided connections). Not inside a transaction: TIA Portal does not compile there.
        /// </summary>
        public CompilerResult CompileHardware(string devicePath)
        {
            return Operation.Run(_logger, nameof(CompileHardware), PortalErrorCode.InvalidState,
                () =>
                {
                    var device = RequireDevice(devicePath);
                    var compilable = device.GetService<ICompilable>()
                                     ?? throw new PortalException(PortalErrorCode.NotSupported, $"Device '{device.Name}' cannot be compiled: TIA Portal offers no compile for it.");

                    Progress(1, 1, $"compile the hardware of {device.Name}");

                    return compilable.Compile();
                },
                ("devicePath", devicePath));
        }
    }
}
