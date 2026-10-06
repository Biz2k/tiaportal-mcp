using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the list files of WinCC Unified, text and graphic: reading what TIA Portal
    /// exports and writing what it imports. The samples are in the form TIA Portal V21 writes.
    /// These tests do not connect to TIA Portal and do not open a project.
    /// </summary>
    [TestClass]
    public class Test16UnifiedTextLists
    {
        private const string Lists = @"#Version: 2.0

TextListContainers:
  DeviceTextList:
    ResourceListType: TextList
    ResourceLists:
      Modes:
        Entries:
          Text_list_entry_0:
            Text: MyTextLibrary.Text_0
          Text_list_entry_1:
            Value: 1
            FromValue: 1
            ToValue: 1
            Text: MyTextLibrary.Text_1
          Text_list_entry_2:
            IsDefaultEntry: True
            Text: MyTextLibrary.Text_0
          Text_list_entry_3:
            Type: Range
            Value: 9
            FromValue: 9
            ToValue: 12
            Text: MyTextLibrary.Text_1
          Text_list_entry_4:
            Type: To
            Value: -2
            ToValue: -2
            Text: MyTextLibrary.Text_1
          Text_list_entry_5:
            Type: From
            Value: 100
            FromValue: 100
            Text: MyTextLibrary.Text_1
      Other list:
        Entries:
          Text_list_entry_0:
            Value: -5
            FromValue: -5
            ToValue: -5
            Text: MyTextLibrary.Text_2
";

        private const string Texts = @"#Version: 2.0

TextLibraries:
  MyTextLibrary:
    Type: Text
    Languages:
    - uk-UA
    - en-US
    DefaultLanguage: uk-UA
    Entries:
      Text_0:
        Text:
        - Усі
        - ''
      Text_1:
        Text:
        - ""It's: one""
        - 'two # not a comment'
      Text_2:
        Text:
        - |-
          Line1
          Line2
        - plain # comment
";

        private const string Graphics = @"#Version: 2.0

GraphicListContainers:
  DeviceGraphicList:
    ResourceListType: GraphicList
    ResourceLists:
      Graphic_list_for_mcp:
        Entries:
          Graphic_list_entry_0:
            IsDefaultEntry: True
            Graphic: GraphicLibrary.APC Smart UPS
          Graphic_list_entry_1:
            Type: Range
            Value: 4
            FromValue: 4
            ToValue: 7
            Graphic: GraphicLibrary.AlarmDisplay_TP1500_Comfort_V2_TR
          Graphic_list_entry_2:
            Value: 11
            FromValue: 11
            ToValue: 11
            Graphic: GraphicLibrary.gama 300
";

        private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

        [TestMethod]
        public void Test_1600_ParseLists_JoinsEntriesWithTheirTexts()
        {
            var lists = Portal.ParseLists(Lists, Texts);

            Assert.AreEqual(2, lists.Count);
            Assert.AreEqual("Modes", lists[0].Name);
            Assert.AreEqual("text", lists[0].Kind);
            Assert.AreEqual("Other list", lists[1].Name);

            Assert.AreEqual("value", lists[0].Entries[0].Type);
            Assert.AreEqual(0L, lists[0].Entries[0].Value, "Export leaves the value keys out for 0");
            Assert.AreEqual("Усі", lists[0].Entries[0].Texts!["uk-UA"]);
            Assert.IsFalse(lists[0].Entries[0].Texts!.ContainsKey("en-US"), "A language without a text is left out");

            Assert.AreEqual(1L, lists[0].Entries[1].Value);
            Assert.AreEqual("It's: one", lists[0].Entries[1].Texts!["uk-UA"]);
            Assert.AreEqual("two # not a comment", lists[0].Entries[1].Texts!["en-US"]);
        }

        [TestMethod]
        public void Test_1601_ParseLists_ReadsEveryKindOfEntry()
        {
            var entries = Portal.ParseLists(Lists, Texts)[0].Entries;

            Assert.AreEqual("default", entries[2].Type);
            Assert.IsNull(entries[2].Value, "The default entry stands for no value of its own");

            Assert.AreEqual("range", entries[3].Type);
            Assert.AreEqual(9L, entries[3].From);
            Assert.AreEqual(12L, entries[3].To);

            Assert.AreEqual("to", entries[4].Type);
            Assert.AreEqual(-2L, entries[4].To);
            Assert.IsNull(entries[4].From);

            Assert.AreEqual("from", entries[5].Type);
            Assert.AreEqual(100L, entries[5].From);
        }

        [TestMethod]
        public void Test_1602_ParseLists_ReadsBlockTextsAndComments()
        {
            var entry = Portal.ParseLists(Lists, Texts)[1].Entries[0];

            Assert.AreEqual(-5L, entry.Value);
            Assert.AreEqual("Line1\nLine2", entry.Texts!["uk-UA"]);
            Assert.AreEqual("plain", entry.Texts!["en-US"]);
        }

        [TestMethod]
        public void Test_1603_ParseLists_ReadsGraphicLists()
        {
            var list = Portal.ParseLists(Graphics, string.Empty).Single();

            Assert.AreEqual("graphic", list.Kind);
            Assert.AreEqual("default", list.Entries[0].Type);
            Assert.AreEqual("APC Smart UPS", list.Entries[0].Graphic);
            Assert.AreEqual("range", list.Entries[1].Type);
            Assert.AreEqual("gama 300", list.Entries[2].Graphic);
            Assert.IsNull(list.Entries[2].Texts);
        }

        [TestMethod]
        public void Test_1604_WriteList_TextListIsReadBackUnchanged()
        {
            // Arrange
            var languages = new List<string> { "en-US", "ru-RU" };

            var entries = Portal.BuildListEntries(new List<UnifiedListEntry>
            {
                new UnifiedListEntry { Default = true, Text = Json("\"Other\"") },
                new UnifiedListEntry { Value = 0, Text = Json("\"Zero\"") },
                new UnifiedListEntry { Value = 2, Text = Json("{\"ru-RU\": \"Авто\"}") },
                new UnifiedListEntry { From = 10, To = 20, Text = Json("\"It's: \\\"quoted\\\" # x\\nsecond line\\\\\"") },
                new UnifiedListEntry { From = 100, Text = Json("\"high\"") },
                new UnifiedListEntry { To = -1, Text = Json("\"low\"") }
            }, languages, graphic: false);

            // Act
            var (lists, texts) = Portal.WriteList("My list: 1", entries, languages, graphic: false);
            var read = Portal.ParseLists(lists, texts!).Single();

            // Assert
            Assert.AreEqual("My list: 1", read.Name);
            Assert.IsNull(Portal.DescribeListDifference(entries, read.Entries));
            CollectionAssert.AreEqual(new[] { "default", "value", "value", "range", "from", "to" }, read.Entries.Select(e => e.Type).ToArray());
            Assert.AreEqual("Other", read.Entries[0].Texts!["ru-RU"], "A string sets every language");
            Assert.IsFalse(read.Entries[2].Texts!.ContainsKey("en-US"));
            Assert.AreEqual("It's: \"quoted\" # x\nsecond line\\", read.Entries[3].Texts!["en-US"]);
        }

        [TestMethod]
        public void Test_1605_WriteList_GraphicListIsReadBackUnchanged()
        {
            var entries = Portal.BuildListEntries(new List<UnifiedListEntry>
            {
                new UnifiedListEntry { Default = true, Graphic = "gama 300" },
                new UnifiedListEntry { From = 4, To = 7, Graphic = "Pump: on #1" }
            }, new List<string>(), graphic: true);

            var (lists, texts) = Portal.WriteList("Pumps", entries, new List<string>(), graphic: true);
            var read = Portal.ParseLists(lists, string.Empty).Single();

            Assert.IsNull(texts, "A graphic list has no text file");
            Assert.AreEqual("graphic", read.Kind);
            Assert.IsNull(Portal.DescribeListDifference(entries, read.Entries));
            Assert.AreEqual("Pump: on #1", read.Entries[1].Graphic);
        }

        [TestMethod]
        public void Test_1606_BuildListEntries_RejectsContradictions()
        {
            var languages = new List<string> { "en-US" };

            Assert.ThrowsException<PortalException>(() => Portal.BuildListEntries(null, languages, false));

            Assert.ThrowsException<PortalException>(() => Portal.BuildListEntries(new List<UnifiedListEntry>
            {
                new UnifiedListEntry { Text = Json("\"a\"") }
            }, languages, false), "An entry that stands for nothing");

            Assert.ThrowsException<PortalException>(() => Portal.BuildListEntries(new List<UnifiedListEntry>
            {
                new UnifiedListEntry { Default = true, Text = Json("\"a\"") },
                new UnifiedListEntry { Default = true, Text = Json("\"b\"") }
            }, languages, false), "Two default entries");

            Assert.ThrowsException<PortalException>(() => Portal.BuildListEntries(new List<UnifiedListEntry>
            {
                new UnifiedListEntry { Default = true, Value = 1, Text = Json("\"a\"") }
            }, languages, false), "A default entry with a value");

            Assert.ThrowsException<PortalException>(() => Portal.BuildListEntries(new List<UnifiedListEntry>
            {
                new UnifiedListEntry { Value = 1, Text = Json("\"a\"") },
                new UnifiedListEntry { Value = 1, Text = Json("\"b\"") }
            }, languages, false), "The same value twice");

            Assert.ThrowsException<PortalException>(() => Portal.BuildListEntries(new List<UnifiedListEntry>
            {
                new UnifiedListEntry { From = 5, To = 1, Text = Json("\"a\"") }
            }, languages, false), "An empty range");

            Assert.ThrowsException<PortalException>(() => Portal.BuildListEntries(new List<UnifiedListEntry>
            {
                new UnifiedListEntry { Value = 1, Text = Json("{\"de-DE\": \"eins\"}") }
            }, languages, false), "A language the project does not have");

            Assert.ThrowsException<PortalException>(() => Portal.BuildListEntries(new List<UnifiedListEntry>
            {
                new UnifiedListEntry { Value = 1, Text = Json("\"a\"") }
            }, languages, true), "A graphic list entry without a graphic");
        }

        [TestMethod]
        public void Test_1607_DescribeListDifference_NamesWhatWasLost()
        {
            var languages = new List<string> { "en-US" };

            var wanted = Portal.BuildListEntries(new List<UnifiedListEntry>
            {
                new UnifiedListEntry { Value = 1, Text = Json("\"One\"") },
                new UnifiedListEntry { From = 2, To = 5, Text = Json("\"Some\"") }
            }, languages, false);

            var dropped = new List<UnifiedListEntryInfo> { wanted[0] };
            var rangeLost = new List<UnifiedListEntryInfo> { wanted[0], new UnifiedListEntryInfo { Type = "value", Value = 2, Texts = wanted[1].Texts } };

            StringAssert.Contains(Portal.DescribeListDifference(wanted, dropped), "2 entries were sent, 1 arrived");
            StringAssert.Contains(Portal.DescribeListDifference(wanted, rangeLost), "entry for 2..5 is missing");
        }

        [TestMethod]
        public void Test_1608_SimpleYaml_ReportsWhatItCannotRead()
        {
            var ex = Assert.ThrowsException<PortalException>(() => SimpleYaml.Parse("Key: [a, b]\n"));

            StringAssert.Contains(ex.Message, "line 1");
        }
    }
}
