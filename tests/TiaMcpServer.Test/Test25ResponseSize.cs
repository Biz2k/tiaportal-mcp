using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// What keeps a long answer short: paging of list tools and depth / filter of the project tree. These tests do not
    /// connect to TIA Portal.
    /// </summary>
    [TestClass]
    [TestCategory("NoTia")]
    public class Test25ResponseSize
    {
        private const string Tree =
            "Project\n" +
            "├── Devices [Collection]\n" +
            "│   ├── Station_1 [Device]\n" +
            "│   │   ├── PLC_1 [DeviceItem]\n" +
            "│   │   └── Rail_0 [DeviceItem]\n" +
            "│   └── Station_2 [Device]\n" +
            "│       └── HMI_1 [DeviceItem]\n" +
            "└── Groups [Collection]\n" +
            "    └── G1 [Group]";

        [TestMethod]
        public void Test_2500_ListPage_CutsAndSaysHowToGetTheRest()
        {
            var all = Enumerable.Range(1, 1200).ToList();

            var page = ListPage<int>.Of(all, 500, 0);

            Assert.AreEqual(500, page.Items.Count);
            Assert.IsTrue(page.Truncated);
            StringAssert.Contains(page.Note("nameFilter"), "showing items 1 to 500 of 1200");
            StringAssert.Contains(page.Note("nameFilter"), "offset=500");

            var last = ListPage<int>.Of(all, 500, 1000);

            Assert.AreEqual(200, last.Items.Count);
            Assert.IsFalse(last.Truncated);
            StringAssert.Contains(last.Note("x"), "Items 1001 to 1200 of 1200");
        }

        [TestMethod]
        public void Test_2599_ListPage_OffsetPastTheEnd_SaysSo()
        {
            var page = ListPage<int>.Of(Enumerable.Range(1, 30).ToList(), 500, 9999);

            Assert.AreEqual(0, page.Items.Count);
            StringAssert.Contains(page.Note("x"), "past the end");
            StringAssert.Contains(page.Note("x"), "30 item(s)");
            Assert.AreEqual(string.Empty, ListPage<int>.Of(new List<int>(), 500, 0).Note("x"));
        }

        [TestMethod]
        public void Test_2501_ListPage_ZeroLimitReturnsAll_NegativeOffsetIsRefused()
        {
            var all = Enumerable.Range(1, 30).ToList();

            Assert.AreEqual(30, ListPage<int>.Of(all, 0, 0).Items.Count);
            Assert.AreEqual(string.Empty, ListPage<int>.Of(all, 0, 0).Note("x"));
            StringAssert.Contains(Assert.ThrowsException<PortalException>(() => ListPage<int>.Of(all, 10, -1)).Message, "cannot be negative");
        }

        [TestMethod]
        public void Test_2502_TreeText_DepthKeepsTheUpperLevels()
        {
            var narrowed = ProjectTreeText.Narrow(Tree, 2, null);

            Assert.AreEqual(9, narrowed.Total);
            Assert.AreEqual(6, narrowed.Kept);
            StringAssert.Contains(narrowed.Text, "Station_2 [Device]");
            Assert.IsFalse(narrowed.Text.Contains("PLC_1"));
        }

        [TestMethod]
        public void Test_2503_TreeText_FilterKeepsTheMatchAndTheLinesAboveIt()
        {
            var narrowed = ProjectTreeText.Narrow(Tree, 0, "HMI_1");
            var lines = narrowed.Text.Split('\n');

            CollectionAssert.AreEqual(new[] { "Project", "├── Devices [Collection]", "│   └── Station_2 [Device]", "│       └── HMI_1 [DeviceItem]" }, lines);
        }

        [TestMethod]
        public void Test_2504_TreeText_BadFilterIsRefused()
        {
            StringAssert.Contains(Assert.ThrowsException<PortalException>(() => ProjectTreeText.Narrow(Tree, 0, "((")).Message, "not a regular expression");
        }

        [TestMethod]
        public void Test_2505_Nodes_FilterKeepsAncestors()
        {
            var nodes = new List<ProjectNode>
            {
                new() { Level = 1, Kind = "device", Name = "S1", Path = "S1" },
                new() { Level = 2, Kind = "deviceItem", Name = "PLC_1", Path = "S1/PLC_1" },
                new() { Level = 2, Kind = "deviceItem", Name = "Rail_0", Path = "S1/Rail_0" },
                new() { Level = 1, Kind = "device", Name = "S2", Path = "S2" },
                new() { Level = 2, Kind = "deviceItem", Name = "HMI_1", Path = "S2/HMI_1" }
            };

            var kept = Portal.KeepMatching(nodes, new Regex("HMI", RegexOptions.IgnoreCase));

            CollectionAssert.AreEqual(new[] { "S2", "HMI_1" }, kept.Select(n => n.Name).ToList());
        }

        [TestMethod]
        public void Test_2506_Window_PagesAddUpToTheTotal()
        {
            var total = 235;
            var seen = 0;

            for (var offset = 0; offset < total; offset += 100)
            {
                var (start, count) = ListPage<int>.Window(total, 100, offset);

                Assert.AreEqual(offset, start);
                seen += count;
            }

            Assert.AreEqual(total, seen);
            Assert.AreEqual((235, 0), ListPage<int>.Window(total, 100, 400), "an offset past the end is an empty page");
            Assert.AreEqual((0, 235), ListPage<int>.Window(total, 0, 0), "limit 0 returns all");
            Assert.AreEqual((200, 35), ListPage<int>.Window(total, 100, 200));
            Assert.ThrowsException<PortalException>(() => ListPage<int>.Window(total, 100, -1));
        }

        [TestMethod]
        public void Test_2508_Meta_CarriesNextOffsetOnlyWhenCut()
        {
            var all = Enumerable.Range(0, 30).ToList();

            var cut = ListPage<int>.Of(all, 10, 10).Meta(new System.Text.Json.Nodes.JsonObject());
            var last = ListPage<int>.Of(all, 10, 20).Meta(new System.Text.Json.Nodes.JsonObject());

            Assert.AreEqual(30, (int)cut["total"]!);
            Assert.AreEqual(10, (int)cut["offset"]!);
            Assert.IsTrue((bool)cut["truncated"]!);
            Assert.AreEqual(20, (int)cut["nextOffset"]!);
            Assert.IsFalse((bool)last["truncated"]!);
            Assert.IsNull(last["nextOffset"], "no next page, no key");
        }

        [TestMethod]
        public void Test_2509_Paging_TwoPagesGiveTheWholeList()
        {
            var all = Enumerable.Range(0, 7).ToList();
            var first = Paging.Page(all, 4, 0);
            var second = Paging.Page(all, 4, first.NextOffset);

            CollectionAssert.AreEqual(all, first.Items.Concat(second.Items).ToList());
            Assert.IsTrue(first.Truncated);
            Assert.IsFalse(second.Truncated);
            StringAssert.Contains(first.Note("x"), "offset=4");
        }

        [TestMethod]
        public void Test_2507_Ready_ReportsTheTotalOfAListThatWasNotBuiltWhole()
        {
            var page = ListPage<int>.Ready(new List<int> { 1, 2, 3 }, 1900, 600);

            Assert.IsTrue(page.Truncated);
            StringAssert.Contains(page.Note("objectKind"), "Pass offset=603");
            Assert.AreEqual(1900, page.Total);
        }
    }
}
