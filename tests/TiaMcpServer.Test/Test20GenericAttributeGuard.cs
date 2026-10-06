using System;
using System.IO;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Keeps generic attribute access (GetAttributeInfos, GetAttributes, SetAttribute) out of the
    /// code for kinds of objects it was never tried on. On a WinCC Unified alarm it closes TIA
    /// Portal together with the unsaved project; the audit of 2026-10-06 (docs/handoff, task 05)
    /// found it safe on the kinds the other files use it on. New object kinds use typed
    /// properties, and are tried with tools/openness-probe.ps1 first. These tests only read
    /// source files and do not connect to TIA Portal.
    /// </summary>
    [TestClass]
    public class Test20GenericAttributeGuard
    {
        private static readonly string[] GuardedFiles =
        {
            "Portal.Unified.Alarms.cs",
            "Portal.Unified.Logs.cs",
            "Portal.Unified.Scripts.cs",
            "Portal.Unified.ScreenGroups.cs",
            "Portal.Unified.Lists.cs"
        };

        private static string FindSourceFolder()
        {
            for (var folder = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory); folder != null; folder = folder.Parent)
            {
                var candidate = Path.Combine(folder.FullName, "src", "TiaMcpServer", "Siemens");

                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            Assert.Inconclusive("The source folder was not found from " + AppDomain.CurrentDomain.BaseDirectory);

            return string.Empty;
        }

        [TestMethod]
        public void Test_2000_GuardedFiles_UseNoGenericAttributeAccess()
        {
            var folder = FindSourceFolder();

            foreach (var name in GuardedFiles)
            {
                var path = Path.Combine(folder, name);

                Assert.IsTrue(File.Exists(path), $"{name} is expected in {folder}");

                // Comments may name the calls (that is how the danger is documented); code may not.
                foreach (var line in File.ReadAllLines(path))
                {
                    var code = line.TrimStart();

                    if (code.StartsWith("//", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (var call in new[] { "GetAttributeInfos(", ".GetAttributes(", ".SetAttribute(", ".SetAttributes(" })
                    {
                        Assert.IsFalse(code.Contains(call), $"{name} calls {call.Trim('.', '(')}: {code.Trim()}. Use typed properties; see docs/handoff/context.md, rule 2.");
                    }
                }
            }
        }
    }
}
