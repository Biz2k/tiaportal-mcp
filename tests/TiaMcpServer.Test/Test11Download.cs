using System.Collections.Generic;
using System.Linq;
using ModelContextProtocol;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the argument and result handling around download_to_plc. These tests do not
    /// connect to TIA Portal, do not open a project and never download anything.
    /// </summary>
    [TestClass]
    public class Test11Download
    {
        [TestMethod]
        public void Test_1100_ParseSelections_ReadsPairsAndIgnoresCase()
        {
            // Act
            var selections = McpServer.ParseSelections(" OverwriteSystemData = Overwrite , stopmodules=StopAll ");

            // Assert
            Assert.AreEqual(2, selections.Count);
            Assert.AreEqual("Overwrite", selections["overwritesystemdata"]);
            Assert.AreEqual("StopAll", selections["StopModules"]);
            Assert.AreEqual(0, McpServer.ParseSelections("").Count);
            Assert.AreEqual(0, McpServer.ParseSelections(null).Count);
        }

        [TestMethod]
        public void Test_1101_ParseSelections_RejectsMalformedPairs()
        {
            Assert.ThrowsException<McpException>(() => McpServer.ParseSelections("StopModules"));
            Assert.ThrowsException<McpException>(() => McpServer.ParseSelections("StopModules="));
            Assert.ThrowsException<McpException>(() => McpServer.ParseSelections("=StopAll"));
        }

        [TestMethod]
        public void Test_1102_LimitMessages_KeepsEveryErrorAndWarning()
        {
            // Arrange: 100 informational lines with an error and a warning far beyond the limit.
            var messages = Enumerable.Range(1, 100)
                .Select(i => new DownloadMessage { Depth = 1, State = "Success", Text = $"'Block_{i}' was loaded successfully." })
                .ToList();

            messages.Add(new DownloadMessage { Depth = 1, State = "Error", Text = "Block_101 could not be loaded." });
            messages.Add(new DownloadMessage { Depth = 1, State = "Warning", Text = "The CPU is in STOP." });

            // Act
            var limited = McpServer.LimitMessages(messages, 10);

            // Assert
            Assert.AreEqual(10, limited.Count(m => m.State == "Success"));
            Assert.IsTrue(limited.Any(m => m.State == "Error"), "An error must never be cut off");
            Assert.IsTrue(limited.Any(m => m.State == "Warning"), "A warning must never be cut off");
            StringAssert.StartsWith(limited.Last().Text, "90 more informational message(s)");
        }

        [TestMethod]
        public void Test_1103_LimitMessages_AddsNothingWhenEverythingFits()
        {
            var messages = new List<DownloadMessage>
            {
                new DownloadMessage { State = "Information", Text = "PLC_1" },
                new DownloadMessage { State = "Success", Text = "PLC_1 stopped." }
            };

            Assert.AreEqual(2, McpServer.LimitMessages(messages, 40).Count);
        }
    }
}