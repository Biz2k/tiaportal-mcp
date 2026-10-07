using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Test
{
    /// <summary>The group tools a client sees unless the server is started with '--full'. These tests do not connect to TIA Portal.</summary>
    [TestClass]
    [TestCategory("NoTia")]
    public class Test33ToolGroups
    {
        private static List<global::ModelContextProtocol.Server.McpServerTool> Compact(bool allowWrite = true, IReadOnlyCollection<string>? areas = null) =>
            Program.BuildCompactTools(allowWrite, debugTools: false, areas: areas).ToList();

        [TestMethod]
        public void Test_3300_NoMoreThanFifteenTools()
        {
            var names = Compact().Select(t => t.ProtocolTool.Name).ToList();

            Assert.IsTrue(names.Count <= 15, $"{names.Count} tools: {string.Join(", ", names)}");
            Assert.AreEqual(ToolGroups.All.Count + 1, names.Count, "every group has a tool, and 'tia_help'");
            Assert.AreEqual(ToolGroups.Help, names[0]);
        }

        [TestMethod]
        public void Test_3301_EveryToolIsInExactlyOneGroup()
        {
            var all = Program.BuildTools(allowWrite: true).Select(t => t.ProtocolTool.Name).ToList();
            var listed = new List<string>();

            foreach (var tool in Compact().Where(t => t.ProtocolTool.Name != ToolGroups.Help))
            {
                var names = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("tool").GetProperty("enum").EnumerateArray().Select(e => e.GetString()!).ToList();

                Assert.IsTrue(names.Count > 0, tool.ProtocolTool.Name);
                listed.AddRange(names);

                foreach (var name in names)
                {
                    StringAssert.Contains(tool.ProtocolTool.Description, "- " + name + ": ");
                }
            }

            CollectionAssert.AreEquivalent(all, listed);
        }

        [TestMethod]
        public void Test_3302_GroupsFollowWhatAToolDoes()
        {
            Assert.AreEqual("plc_read", ToolGroups.GroupOf("plc_get_blocks", false));
            Assert.AreEqual("plc_write", ToolGroups.GroupOf("plc_replace_source", true));
            Assert.AreEqual("plc_delete", ToolGroups.GroupOf("plc_delete_block", true));
            Assert.AreEqual("hw_delete", ToolGroups.GroupOf("net_disconnect_subnet", true));
            Assert.AreEqual("hw_write", ToolGroups.GroupOf("hw_set_device_item_attributes", true));
            Assert.AreEqual("hmi_write", ToolGroups.GroupOf("unified_manage_tags", true));
            Assert.AreEqual("plc_download", ToolGroups.GroupOf("download_to_plc", true));
            Assert.AreEqual("project_read", ToolGroups.GroupOf("get_download_targets", false));
            Assert.AreEqual("project_write", ToolGroups.GroupOf("save_project", true));
            Assert.AreEqual("security_read", ToolGroups.GroupOf("sec_get_project_users", false));
            Assert.AreEqual("security_users", ToolGroups.GroupOf("sec_manage_webserver_users", true));
            Assert.AreEqual("security_users", ToolGroups.GroupOf("sec_set_password_policy", true));
            Assert.AreEqual("security_protection", ToolGroups.GroupOf("sec_set_display_password", true));
            Assert.AreEqual("security_protection", ToolGroups.GroupOf("sec_set_block_protection", true));
        }

        [TestMethod]
        public void Test_3303_ReadOnlyLeavesOnlyReadingGroups()
        {
            var tools = Compact(allowWrite: false);

            foreach (var tool in tools.Where(t => t.ProtocolTool.Name != ToolGroups.Help && t.ProtocolTool.Name != "project_write"))
            {
                Assert.AreEqual(true, tool.ProtocolTool.Annotations!.ReadOnlyHint, tool.ProtocolTool.Name);
            }

            Assert.IsFalse(tools.Any(t => t.ProtocolTool.Name == "plc_delete" || t.ProtocolTool.Name == "security_users" || t.ProtocolTool.Name == "plc_download"));
        }

        [TestMethod]
        public void Test_3304_ToolsFlagLeavesGroupsOut()
        {
            var names = Compact(areas: new[] { "plc" }).Select(t => t.ProtocolTool.Name).ToList();

            CollectionAssert.Contains(names, "plc_write");
            CollectionAssert.DoesNotContain(names, "hmi_read");
            CollectionAssert.DoesNotContain(names, "security_protection");
        }

        [TestMethod]
        public void Test_3305_ParseArgs_FullFlag()
        {
            Assert.IsTrue(CliOptions.ParseArgs(new[] { "--full" }).Full);
            Assert.IsFalse(CliOptions.ParseArgs(new string[0]).Full);
        }
    }
}
