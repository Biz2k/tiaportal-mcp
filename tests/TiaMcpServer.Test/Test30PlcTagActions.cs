using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the check 'plc_manage_tag_table_entries' makes on a batch before TIA Portal is touched.
    /// These tests do not connect to TIA Portal.
    /// </summary>
    [TestClass]
    [TestCategory("NoTia")]
    public class Test30PlcTagActions
    {
        private static readonly string[] Existing = { "Tag_A", "Tag_B" };

        [TestMethod]
        public void Test_3000_Check_AcceptsASoundBatchThatBuildsOnItself()
        {
            var actions = new[]
            {
                new PlcTagAction { Action = "create", Name = "Tag_C", DataType = "Bool", LogicalAddress = "%M1.0" },
                new PlcTagAction { Action = "update", Name = "Tag_C", NewName = "Tag_D", Comment = "x" },
                new PlcTagAction { Action = "delete", Name = "Tag_D" },
                new PlcTagAction { Action = "Upsert", Name = "tag_a", DataTypeName = "Int" },
                new PlcTagAction { Action = "create", Name = "Bare" }
            };

            Assert.AreEqual(0, PlcTagActions.Check(actions, Existing).Count);
        }

        [TestMethod]
        public void Test_3001_Check_NamesEveryWrongAction()
        {
            var actions = new[]
            {
                new PlcTagAction { Action = "create", Name = "Tag_A", DataType = "Bool", LogicalAddress = "%M1.0" },
                new PlcTagAction { Action = "update", Name = "Nope" },
                new PlcTagAction { Action = "delete", Name = "Nope" },
                new PlcTagAction { Action = "rename", Name = "Tag_A" },
                new PlcTagAction { Action = "create", Name = "" },
                new PlcTagAction { Action = "create", Name = "New1", DataType = "Bool" },
                new PlcTagAction { Action = "update", Name = "Tag_A", NewName = "Tag_B" }
            };

            var problems = PlcTagActions.Check(actions, Existing);

            Assert.AreEqual(7, problems.Count);
            StringAssert.Contains(problems[0], "already exists");
            StringAssert.Contains(problems[1], "no such tag");
            StringAssert.Contains(problems[3], "unknown action");
            StringAssert.Contains(problems[4], "'name' is required");
            StringAssert.Contains(problems[5], "together");
            StringAssert.Contains(problems[6], "'Tag_B' is taken");
        }

        [TestMethod]
        public void Test_3002_Check_RefusesUnknownFieldsInsteadOfDroppingThem()
        {
            var action = System.Text.Json.JsonSerializer.Deserialize<PlcTagAction>(
                "{\"action\": \"update\", \"name\": \"Tag_A\", \"type\": \"Int\"}",
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            var problems = PlcTagActions.Check(new[] { action }, Existing);

            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains(problems[0], "unknown field(s) 'type'");
            StringAssert.Contains(problems[0], "dataType");
            StringAssert.Contains(PlcTagActions.Check(null, Existing)[0], "No actions given");
        }

        [TestMethod]
        public void Test_3003_Rules_AcceptWhatTiaPortalWouldCompile()
        {
            bool Udt(string name) => name == "UDT_Motor";

            Assert.IsNull(PlcTagRules.Problem("Bool", "%M10.0", Udt));
            Assert.IsNull(PlcTagRules.Problem("bool", "%i0.7", Udt));
            Assert.IsNull(PlcTagRules.Problem("Bool", "%MX10.0", Udt));
            Assert.IsNull(PlcTagRules.Problem("Int", "%MW10", Udt));
            Assert.IsNull(PlcTagRules.Problem("Real", "%MD100", Udt));
            Assert.IsNull(PlcTagRules.Problem("Byte", "%QB4", Udt));
            Assert.IsNull(PlcTagRules.Problem("\"UDT_Motor\"", "%I0.0", Udt));
            Assert.IsNull(PlcTagRules.Problem("LReal", "%MD0", Udt));
            Assert.IsNull(PlcTagRules.Problem("Timer", "%T5", Udt));
            Assert.IsNull(PlcTagRules.Problem("Int", "", Udt));
            Assert.IsNull(PlcTagRules.Problem(null, "%MW10", Udt));
            Assert.IsNull(PlcTagRules.Problem(null, null, Udt));
        }

        [TestMethod]
        public void Test_3004_Rules_RefuseWhatOpennessWouldStoreSilently()
        {
            bool Udt(string name) => false;

            StringAssert.Contains(PlcTagRules.Problem("NoSuchType", "%M1.0", Udt), "neither an elementary data type");
            StringAssert.Contains(PlcTagRules.Problem("Bool", "garbage", Udt), "not an absolute address");
            StringAssert.Contains(PlcTagRules.Problem("Bool", "%M10.8", Udt), "not an absolute address");
            StringAssert.Contains(PlcTagRules.Problem("Bool", "%MW904", Udt), "needs a bit address");
            StringAssert.Contains(PlcTagRules.Problem("Int", "%M900.2", Udt), "needs a word address");
            StringAssert.Contains(PlcTagRules.Problem("Real", "%MW10", Udt), "needs a double word address");
        }
    }
}
