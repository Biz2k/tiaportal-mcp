using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Finds the question TIA Portal puts to the user when a program it does not know attaches: the window
    /// "Openness access". The allow list of TIA Portal goes by the hash of the exe, so every new build of the server is
    /// asked about once. The call that attaches waits for the answer without a time limit, and the window does not come
    /// to the front when TIA Portal is minimized or covered (measured 2026-10-07: a connect waited 120 s, nothing on
    /// the screen said why). Reading the titles of the windows of the TIA Portal process is all this does.
    /// </summary>
    internal static class OpennessAccessPrompt
    {
        /// <summary>Whether the TIA Portal process shows its question about Openness access right now.</summary>
        public static bool IsShown(int tiaProcessId)
        {
            var shown = false;

            try
            {
                EnumWindows((window, _) =>
                {
                    GetWindowThreadProcessId(window, out var processId);

                    if (processId != tiaProcessId || !IsWindowVisible(window))
                    {
                        return true;
                    }

                    var title = new StringBuilder(200);

                    GetWindowText(window, title, title.Capacity);

                    if (title.ToString().IndexOf("Openness", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return true;
                    }

                    shown = true;

                    return false;
                }, IntPtr.Zero);
            }
            catch (Exception)
            {
                // Not being able to look is no reason to fail the connect.
            }

            return shown;
        }

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    }
}
