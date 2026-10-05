using System;
using System.Collections.Generic;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for path handling when a name contains the path separator, as TIA Portal allows for
    /// groups ("Inputs/Outputs") and stations ("S7-1500/ET200MP station_1"). These tests work on
    /// a plain in-memory tree: they do not connect to TIA Portal and do not open a project.
    /// </summary>
    [TestClass]
    public class Test9Paths
    {
        private sealed class Node
        {
            public Node(string name, params Node[] children)
            {
                Name = name;
                Children = children;
            }

            public string Name { get; }

            public Node[] Children { get; }
        }

        private static Node? Walk(Node[] roots, string path)
        {
            return Portal.WalkNamed<Node>(roots, Portal.PathSegments(path), 0, n => n.Children, n => n.Name);
        }

        [TestMethod]
        public void Test_900_EscapeSegment_RoundTrips()
        {
            Assert.AreEqual("Inputs%2FOutputs", Portal.EscapeSegment("Inputs/Outputs"));
            Assert.AreEqual("Inputs/Outputs", Portal.UnescapeSegment("Inputs%2FOutputs"));
            Assert.AreEqual("Inputs/Outputs", Portal.UnescapeSegment("Inputs%2fOutputs"));
            Assert.AreEqual("Valves", Portal.EscapeSegment("Valves"));
        }

        [TestMethod]
        public void Test_901_JoinLeafAndSplitPath_RoundTrip()
        {
            // Arrange
            var path = Portal.JoinLeaf(Portal.EscapeSegment("Inputs/Outputs"), "AI_Handler");

            // Act
            var (groupPath, leafName) = Portal.SplitPath(path);

            // Assert
            Assert.AreEqual("Inputs%2FOutputs/AI_Handler", path);
            Assert.AreEqual("Inputs%2FOutputs", groupPath);
            Assert.AreEqual("AI_Handler", leafName);
            Assert.AreEqual("A/B", Portal.SplitPath("Tables/A%2FB").LeafName);
        }

        [TestMethod]
        public void Test_902_WalkNamed_FindsAGroupWithASlashInItsName()
        {
            // Arrange
            var block = new Node("AI_Handler");
            var roots = new[] { new Node("Inputs/Outputs", block), new Node("Valves") };

            // Act + Assert
            Assert.AreSame(block, Walk(roots, "Inputs%2FOutputs/AI_Handler"), "The escaped form must resolve");
            Assert.AreSame(block, Walk(roots, "Inputs/Outputs/AI_Handler"), "The unescaped form must resolve too");
            Assert.AreSame(block, Walk(roots, "inputs/outputs/ai_handler"), "Matching stays case-insensitive");
            Assert.IsNull(Walk(roots, "Inputs/AI_Handler"));
            Assert.IsNull(Walk(roots, "Inputs/Outputs/Missing"));
        }

        [TestMethod]
        public void Test_903_WalkNamed_PrefersThePathAsWritten()
        {
            // Arrange: both readings of "Inputs/Outputs" exist.
            var nested = new Node("X");
            var merged = new Node("X");
            var roots = new[]
            {
                new Node("Inputs", new Node("Outputs", nested)),
                new Node("Inputs/Outputs", merged)
            };

            // Act + Assert
            Assert.AreSame(nested, Walk(roots, "Inputs/Outputs/X"), "The literal structure wins");
            Assert.AreSame(merged, Walk(roots, "Inputs%2FOutputs/X"), "The escaped form addresses the other group");
        }

        [TestMethod]
        public void Test_904_WalkNamed_BacktracksWhenTheFirstReadingIsADeadEnd()
        {
            // Arrange: "A" exists but has no "B/C" below it; the group "A/B" does have "C".
            var target = new Node("C");
            var roots = new[] { new Node("A", new Node("Other")), new Node("A/B", target) };

            // Act + Assert
            Assert.AreSame(target, Walk(roots, "A/B/C"));
        }

        [TestMethod]
        public void Test_905_MatchPrefix_AcceptsEscapedAndUnescapedDeviceNames()
        {
            // Arrange
            var device = Portal.PathSegments("S7-1500%2FET200MP station_1");

            // Act + Assert
            Assert.AreEqual(1, Portal.MatchPrefix(Portal.PathSegments("S7-1500%2FET200MP station_1/PLC_1"), device));
            Assert.AreEqual(2, Portal.MatchPrefix(Portal.PathSegments("S7-1500/ET200MP station_1/PLC_1"), device));
            Assert.AreEqual(-1, Portal.MatchPrefix(Portal.PathSegments("S7-1200/PLC_1"), device));
            Assert.AreEqual(0, Portal.MatchPrefix(Portal.PathSegments("PLC_1"), Array.Empty<string>()));
        }
    }
}