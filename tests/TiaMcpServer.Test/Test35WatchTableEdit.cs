using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>The edit of the rows of a watch table on its SimaticML. These tests do not connect to TIA Portal.</summary>
    [TestClass]
    [TestCategory("NoTia")]
    public class Test35WatchTableEdit
    {
        // an export of V21 (2026-10-08): a tag row with a comment, a comment row, an address row
        private const string Export = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Document><Engineering version=""V21"" /><SW.WatchAndForceTables.PlcWatchTable ID=""0""><AttributeList><Name>MCPT_Watch</Name></AttributeList><ObjectList>
<SW.WatchAndForceTables.PlcWatchTableEntry ID=""1"" CompositionName=""Entries""><AttributeList><DisplayFormat>Bool</DisplayFormat><Name>""FirstScan""</Name></AttributeList><ObjectList><MultilingualText ID=""2"" CompositionName=""Comment""><ObjectList><MultilingualTextItem ID=""3"" CompositionName=""Items""><AttributeList><Culture>en-US</Culture><Text>first scan flag</Text></AttributeList></MultilingualTextItem><MultilingualTextItem ID=""4"" CompositionName=""Items""><AttributeList><Culture>ru-RU</Culture><Text /></AttributeList></MultilingualTextItem></ObjectList></MultilingualText></ObjectList></SW.WatchAndForceTables.PlcWatchTableEntry>
<SW.WatchAndForceTables.PlcTableCommentEntry ID=""6"" CompositionName=""Entries""><ObjectList><MultilingualText ID=""7"" CompositionName=""Comment""><ObjectList><MultilingualTextItem ID=""8"" CompositionName=""Items""><AttributeList><Culture>en-US</Culture><Text>--- section ---</Text></AttributeList></MultilingualTextItem></ObjectList></MultilingualText></ObjectList></SW.WatchAndForceTables.PlcTableCommentEntry>
<SW.WatchAndForceTables.PlcWatchTableEntry ID=""B"" CompositionName=""Entries""><AttributeList><Address>%MW10</Address><DisplayFormat>Hex</DisplayFormat></AttributeList></SW.WatchAndForceTables.PlcWatchTableEntry>
</ObjectList></SW.WatchAndForceTables.PlcWatchTable></Document>";

        private const string EmptyExport = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Document><Engineering version=""V21"" /><SW.WatchAndForceTables.PlcWatchTable ID=""0""><AttributeList><Name>MCPT_Watch</Name></AttributeList></SW.WatchAndForceTables.PlcWatchTable></Document>";

        private static readonly string[] Languages = { "en-US", "ru-RU" };

        private static WatchTableEntryAction A(string action, string? name = null, string? address = null, int? index = null, string? format = null, string? comment = null, string? value = null) =>
            new WatchTableEntryAction { Action = action, Name = name, Address = address, Index = index, DisplayFormat = format, Comment = comment, ModifyValue = value };

        private static List<string> Check(XDocument doc, params WatchTableEntryAction[] actions) =>
            PlcWatchTableEdit.Check(actions, PlcWatchTableEdit.ReadRows(doc), s => s != "NoSuchTag");

        [TestMethod]
        public void Test_3500_ReadRows_KeepsOrderKindAndFields()
        {
            var rows = PlcWatchTableEdit.ReadRows(XDocument.Parse(Export));

            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual("Watch", rows[0].Kind);
            Assert.AreEqual("\"FirstScan\"", rows[0].Name);
            Assert.AreEqual("Bool", rows[0].DisplayFormat);
            Assert.AreEqual("first scan flag", rows[0].Comment);
            Assert.AreEqual("Comment", rows[1].Kind);
            Assert.AreEqual("--- section ---", rows[1].Comment);
            Assert.AreEqual("%MW10", rows[2].Address);
            Assert.AreEqual(0, PlcWatchTableEdit.ReadRows(XDocument.Parse(EmptyExport)).Count);
        }

        [TestMethod]
        public void Test_3501_Root_OfQuotedBareAndMemberNames()
        {
            Assert.AreEqual("HMI", PlcWatchTableEdit.Root("\"HMI\".Pumps.CP_1"));
            Assert.AreEqual("FirstScan", PlcWatchTableEdit.Root("\"FirstScan\""));
            Assert.AreEqual("FirstScan", PlcWatchTableEdit.Root("FirstScan"));
            Assert.AreEqual("DB1", PlcWatchTableEdit.Root("DB1.Value[2]"));
        }

        [TestMethod]
        public void Test_3502_Check_SoundBatch_NoProblems()
        {
            var doc = XDocument.Parse(Export);
            var problems = Check(doc, A("add", name: "Motor_On", value: "true"), A("add", address: "%QW4", format: "dec_signed", index: 0), A("delete", index: 2), A("delete", name: "firstscan"), A("add", comment: "line"));

            Assert.AreEqual(0, problems.Count, string.Join(" | ", problems));
        }

        [TestMethod]
        public void Test_3503_Check_Refusals_SayWhatToChange()
        {
            var doc = XDocument.Parse(Export);

            StringAssert.Contains(Check(doc).Single(), "No actions");
            StringAssert.Contains(Check(doc, A("update", name: "x")).Single(), "'add', 'delete' or 'clear'");
            StringAssert.Contains(Check(doc, A("add", name: "NoSuchTag")).Single(), "neither a tag nor a data block");
            StringAssert.Contains(Check(doc, A("add", name: "Tag", address: "%M1.0")).Single(), "not both");
            StringAssert.Contains(Check(doc, A("add", name: "Tag", format: "Hex")).Single(), "takes its display format from the data type");
            StringAssert.Contains(Check(doc, A("add", address: "%M1.0", format: "Heks")).Single(), "Hex");
            StringAssert.Contains(Check(doc, new WatchTableEntryAction { Action = "add", Address = "%M1.0", MonitorTrigger = "Often" }).Single(), "OnceOnlyAtStart");
            StringAssert.Contains(Check(doc, A("add")).Single(), "'comment' alone");
            StringAssert.Contains(Check(doc, A("add", address: "%M1.0", index: 9)).Single(), "0..3");
            StringAssert.Contains(Check(doc, A("delete", index: 3)).Single(), "0..2");
            StringAssert.Contains(Check(doc, A("delete", name: "Other")).Single(), "no row with name");
            StringAssert.Contains(Check(doc, A("delete")).Single(), "exactly one");
            StringAssert.Contains(Check(doc, A("clear", name: "x")).Single(), "no other field");
        }

        [TestMethod]
        public void Test_3504_Check_FollowsTheBatch_RowsAddedOrDeletedEarlierCount()
        {
            var doc = XDocument.Parse(EmptyExport);

            Assert.AreEqual(0, Check(doc, A("add", address: "%M1.0"), A("delete", index: 0), A("add", address: "%M2.0", index: 0)).Count);
            StringAssert.Contains(Check(doc, A("add", address: "%M1.0"), A("clear"), A("delete", index: 0)).Single(), "no rows");
            StringAssert.Contains(Check(doc, A("add", address: "%M1.0"), A("add", address: "%m1.0"), A("delete", address: "%M1.0")).Single(), "2 rows have address");
        }

        [TestMethod]
        public void Test_3505_Check_UnknownField_Refused()
        {
            var action = System.Text.Json.JsonSerializer.Deserialize<WatchTableEntryAction>("{\"action\":\"add\",\"address\":\"%M1.0\",\"newName\":\"x\"}", new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            var problems = Check(XDocument.Parse(Export), action);

            StringAssert.Contains(problems.Single(), "unknown field(s) 'newName'");
        }

        [TestMethod]
        public void Test_3506_Apply_AddAtPositionsDeleteAndClear()
        {
            var doc = XDocument.Parse(Export);

            PlcWatchTableEdit.Apply(doc, new[] { A("add", address: "%QW4", index: 0, format: "Hex", comment: "out"), A("add", name: "Motor_On", value: "true"), A("delete", index: 2), A("delete", address: "%MW10") }, Languages);

            var rows = PlcWatchTableEdit.ReadRows(doc);

            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual("%QW4", rows[0].Address);
            Assert.AreEqual("out", rows[0].Comment);
            Assert.AreEqual("\"FirstScan\"", rows[1].Name);
            Assert.AreEqual("Motor_On", rows[2].Name);
            Assert.AreEqual("true", rows[2].ModifyValue);

            PlcWatchTableEdit.Apply(doc, new[] { A("clear") }, Languages);
            Assert.AreEqual(0, PlcWatchTableEdit.ReadRows(doc).Count);
        }

        [TestMethod]
        public void Test_3507_Apply_IntoEmptyTable_MakesTheObjectList_IdsAreUnique()
        {
            var doc = XDocument.Parse(EmptyExport);

            PlcWatchTableEdit.Apply(doc, new[] { A("add", address: "%M1.0", comment: "a"), A("add", comment: "line") }, Languages);

            var rows = PlcWatchTableEdit.ReadRows(doc);
            var ids = doc.Descendants().Where(e => e.Attribute("ID") != null).Select(e => e.Attribute("ID")!.Value).ToList();

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("Comment", rows[1].Kind);
            Assert.AreEqual(ids.Count, ids.Distinct().Count());
            Assert.AreEqual(2, doc.Descendants("Culture").Count(c => c.Value == "ru-RU"), "a comment is written for every language of the project");
        }

        [TestMethod]
        public void Test_3508_Compare_NamesWhatTiaPortalDropped_IgnoresWhatTheXmlDoesNotState()
        {
            var asked = new List<PlcWatchTableEdit.Row>
            {
                new PlcWatchTableEdit.Row { Name = "\"FirstScan\"", ModifyValue = "true" },
                new PlcWatchTableEdit.Row { Name = "\"Gone\"" }
            };
            var got = new List<PlcWatchTableEdit.Row> { new PlcWatchTableEdit.Row { Name = "\"FirstScan\"", Address = "%M1.0", DisplayFormat = "Bool", ModifyValue = "TRUE" } };

            var dropped = PlcWatchTableEdit.Compare(asked, got);

            StringAssert.Contains(dropped.Single(), "dropped: \"Gone\"");
            asked.RemoveAt(1);
            Assert.AreEqual(0, PlcWatchTableEdit.Compare(asked, got).Count);
            asked[0].ModifyValue = "false";
            StringAssert.Contains(PlcWatchTableEdit.Compare(asked, got).Single(), "modifyValue is 'TRUE'");
        }
    }
}
