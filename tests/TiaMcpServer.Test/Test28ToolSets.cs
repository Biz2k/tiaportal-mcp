using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Test
{
    /// <summary>The '--tools' start flag and the areas of the tools. These tests do not connect to TIA Portal.</summary>
    [TestClass]
    [TestCategory("NoTia")]
    public class Test28ToolSets
    {
        private static List<string> Names(IReadOnlyCollection<string>? areas) =>
            Program.BuildTools(allowWrite: true, debugTools: true, areas: areas).Select(t => t.ProtocolTool.Name).ToList();

        [TestMethod]
        public void Test_2800_EveryToolHasOneAreaOrIsAlwaysRegistered()
        {
            foreach (var name in Names(null))
            {
                var area = ToolSets.AreaOf(name);

                Assert.IsTrue(area != null ^ ToolSets.IsAlways(name),
                    $"'{name}' must be in exactly one area or in the always-registered list of ToolSets (area: '{area}')");

                if (area != null)
                {
                    Assert.IsTrue(ToolSets.Areas.Contains(area), $"'{name}' names the unknown area '{area}'");
                }
            }
        }

        [TestMethod]
        public void Test_2801_Parse_ReadsAreasAndAll()
        {
            CollectionAssert.AreEqual(new[] { "plc", "unified" }, ToolSets.Parse("unified,plc", out var unknown)!.ToList());
            Assert.AreEqual(0, unknown.Count);
            Assert.IsNull(ToolSets.Parse("all", out _), "'all' is no limit");
            Assert.IsNull(ToolSets.Parse("plc,all", out _));
            CollectionAssert.AreEqual(new[] { "plc" }, ToolSets.Parse(" PLC ", out _)!.ToList(), "case and blanks do not matter");
        }

        [TestMethod]
        public void Test_2802_Parse_ReportsUnknownAreas()
        {
            ToolSets.Parse("plc,pcl,x", out var unknown);

            CollectionAssert.AreEqual(new[] { "pcl", "x" }, unknown.ToList());
            StringAssert.Contains(ToolSets.UnknownText(unknown), "Valid areas: plc, hw, unified, library, transfer, download, security, all");
        }

        [TestMethod]
        public void Test_2803_ParseArgs_ToolsFlag()
        {
            var some = CliOptions.ParseArgs(new[] { "--tools", "plc,hw" });
            var none = CliOptions.ParseArgs(new string[0]);
            var all = CliOptions.ParseArgs(new[] { "--tools", "all" });
            var bad = CliOptions.ParseArgs(new[] { "--tools", "plc,nope" });
            var empty = CliOptions.ParseArgs(new[] { "--tools" });

            CollectionAssert.AreEqual(new[] { "plc", "hw" }, some.ToolAreas!.ToList());
            Assert.IsNull(some.ToolsError);
            Assert.IsNull(none.ToolAreas);
            Assert.IsNull(all.ToolAreas);
            StringAssert.Contains(bad.ToolsError, "nope");
            Assert.IsNotNull(empty.ToolsError, "'--tools' without a value is an error, not 'all'");
        }

        [TestMethod]
        public void Test_2804_BuildTools_KeepsTheAlwaysToolsAndTheNamedAreas()
        {
            var plc = Names(new[] { "plc" });

            Assert.IsTrue(plc.Contains("connect") && plc.Contains("get_state") && plc.Contains("doctor") && plc.Contains("get_project_tree"));
            Assert.IsTrue(plc.Contains("plc_get_blocks"));
            Assert.IsFalse(plc.Any(n => n.StartsWith("unified_") || n.StartsWith("hw_") || n.StartsWith("net_")));
            Assert.IsFalse(plc.Contains("export_objects") || plc.Contains("download_to_plc") || plc.Contains("get_libraries"));

            var transfer = Names(new[] { "transfer", "download" });

            CollectionAssert.IsSubsetOf(new[] { "export_objects", "import_objects", "preview_import", "download_to_plc", "get_download_targets" }, transfer);
            Assert.IsFalse(transfer.Any(n => n.StartsWith("plc_")));
        }

        [TestMethod]
        public void Test_2805_BuildTools_TheAreasTogetherGiveEveryTool()
        {
            var union = new HashSet<string>(Names(new string[0]));

            foreach (var area in ToolSets.Areas)
            {
                union.UnionWith(Names(new[] { area }));
            }

            var all = Names(null);

            Assert.AreEqual(0, all.Except(union).Count(), "missing: " + string.Join(", ", all.Except(union)));
            Assert.AreEqual(all.Count, all.Distinct().Count(), "duplicates: " + string.Join(", ", all.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key)));
        }

        [TestMethod]
        public void Test_2806_Describe()
        {
            Assert.AreEqual("all", ToolSets.Describe(null));
            Assert.AreEqual("plc,hw", ToolSets.Describe(new[] { "plc", "hw" }));
        }
    }
}
