using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the text list files of WinCC Unified: reading what TIA Portal exports and
    /// writing what it imports. The samples are in the form TIA Portal V21 writes. These tests
    /// do not connect to TIA Portal and do not open a project.
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

        private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

        [TestMethod]
        public void Test_1600_ParseTextLists_JoinsEntriesWithTheirTexts()
        {
            var lists = Portal.ParseTextLists(Lists, Texts);

            Assert.AreEqual(2, lists.Count);
            Assert.AreEqual("Modes", lists[0].Name);
            Assert.AreEqual("Other list", lists[1].Name);

            Assert.AreEqual(0L, lists[0].Entries[0].Value, "Export leaves the value keys out for 0");
            Assert.AreEqual("Усі", lists[0].Entries[0].Texts["uk-UA"]);
            Assert.IsFalse(lists[0].Entries[0].Texts.ContainsKey("en-US"), "A language without a text is left out");

            Assert.AreEqual(1L, lists[0].Entries[1].Value);
            Assert.AreEqual("It's: one", lists[0].Entries[1].Texts["uk-UA"]);
            Assert.AreEqual("two # not a comment", lists[0].Entries[1].Texts["en-US"]);
        }

        [TestMethod]
        public void Test_1601_ParseTextLists_ReadsBlockTextsAndComments()
        {
            var entry = Portal.ParseTextLists(Lists, Texts)[1].Entries[0];

            Assert.AreEqual(-5L, entry.Value);
            Assert.AreEqual("Line1\nLine2", entry.Texts["uk-UA"]);
            Assert.AreEqual("plain", entry.Texts["en-US"]);
        }

        [TestMethod]
        public void Test_1602_WriteTextList_IsReadBackUnchanged()
        {
            // Arrange
            var languages = new List<string> { "en-US", "ru-RU" };

            var entries = Portal.BuildTextListEntries(new List<UnifiedTextListEntry>
            {
                new UnifiedTextListEntry { Value = 0, Text = Json("\"Other\"") },
                new UnifiedTextListEntry { Value = 2, Text = Json("{\"ru-RU\": \"Авто\"}") },
                new UnifiedTextListEntry { Value = 3, Text = Json("\"It's: \\\"quoted\\\" # x\\nsecond line\\\\\"") }
            }, languages);

            // Act
            var (lists, texts) = Portal.WriteTextList("My list: 1", entries, languages);
            var read = Portal.ParseTextLists(lists, texts).Single();

            // Assert
            Assert.AreEqual("My list: 1", read.Name);
            Assert.IsNull(Portal.DescribeTextListDifference(entries, read.Entries));
            Assert.AreEqual("Other", read.Entries[0].Texts["ru-RU"], "A string sets every language");
            Assert.IsFalse(read.Entries[1].Texts.ContainsKey("en-US"));
            Assert.AreEqual("It's: \"quoted\" # x\nsecond line\\", read.Entries[2].Texts["en-US"]);
        }

        [TestMethod]
        public void Test_1603_BuildTextListEntries_RejectsWhatImportWouldMangle()
        {
            var languages = new List<string> { "en-US" };

            Assert.ThrowsException<PortalException>(() => Portal.BuildTextListEntries(null, languages));

            Assert.ThrowsException<PortalException>(() => Portal.BuildTextListEntries(new List<UnifiedTextListEntry>
            {
                new UnifiedTextListEntry { Text = Json("\"a\"") }
            }, languages), "An entry without a value");

            Assert.ThrowsException<PortalException>(() => Portal.BuildTextListEntries(new List<UnifiedTextListEntry>
            {
                new UnifiedTextListEntry { Value = 1, Text = Json("\"a\"") },
                new UnifiedTextListEntry { Value = 1, Text = Json("\"b\"") }
            }, languages), "The same value twice");

            Assert.ThrowsException<PortalException>(() => Portal.BuildTextListEntries(new List<UnifiedTextListEntry>
            {
                new UnifiedTextListEntry { Value = 1, Text = Json("{\"de-DE\": \"eins\"}") }
            }, languages), "A language the project does not have");
        }

        [TestMethod]
        public void Test_1604_DescribeTextListDifference_NamesWhatWasLost()
        {
            var languages = new List<string> { "en-US" };

            var wanted = Portal.BuildTextListEntries(new List<UnifiedTextListEntry>
            {
                new UnifiedTextListEntry { Value = 1, Text = Json("\"One\"") },
                new UnifiedTextListEntry { Value = 2, Text = Json("\"Two\"") }
            }, languages);

            var dropped = new List<UnifiedTextListEntryInfo> { wanted[0] };
            var valueLost = new List<UnifiedTextListEntryInfo> { wanted[0], new UnifiedTextListEntryInfo { Value = 0, Texts = wanted[1].Texts } };

            StringAssert.Contains(Portal.DescribeTextListDifference(wanted, dropped), "2 entries were sent, 1 arrived");
            StringAssert.Contains(Portal.DescribeTextListDifference(wanted, valueLost), "value 2 is missing");
        }

        [TestMethod]
        public void Test_1605_SimpleYaml_ReportsWhatItCannotRead()
        {
            var ex = Assert.ThrowsException<PortalException>(() => SimpleYaml.Parse("Key: [a, b]\n"));

            StringAssert.Contains(ex.Message, "line 1");
        }
    }
}
