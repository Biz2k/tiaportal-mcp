using System.Text.Json;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the options of dynamizations and events that 'unified_manage_items' takes: what is
    /// read, and what is refused before Openness is called. These tests do not connect to TIA Portal.
    /// </summary>
    [TestClass]
    [TestCategory("NoTia")]
    public class Test23UnifiedDynamizations
    {
        private static JsonElement Json(string text)
        {
            using var document = JsonDocument.Parse(text);

            return document.RootElement.Clone();
        }

        [TestMethod]
        public void Test_2300_FindMainKey_PicksTheKindThatCarriesOptions()
        {
            Assert.AreEqual("tag", UnifiedDynamizationSpec.FindMainKey(new[] { "readOnly", "tag" }));
            Assert.AreEqual("Script", UnifiedDynamizationSpec.FindMainKey(new[] { "Script", "async" }));
            Assert.AreEqual("flashing", UnifiedDynamizationSpec.FindMainKey(new[] { "flashing" }));
            Assert.IsNull(UnifiedDynamizationSpec.FindMainKey(new[] { "value" }));
        }

        [TestMethod]
        public void Test_2301_Options_RefusesAKeyThatBelongsToAnotherKind()
        {
            var value = Json("{\"script\": \"x\", \"readOnly\": true}");

            var error = Assert.ThrowsException<PortalException>(() => UnifiedDynamizationSpec.Options(value, "script", "async", "trigger"));

            StringAssert.Contains(error.Message, "'readOnly' is not an option of 'script'");
            StringAssert.Contains(error.Message, "async, trigger");
        }

        [TestMethod]
        public void Test_2302a_ParseTrigger_TypeFollowsFromTheParts()
        {
            var tags = UnifiedDynamizationSpec.ParseTrigger(Json("{\"tags\": [\"T1\"]}"));
            var cycle = UnifiedDynamizationSpec.ParseTrigger(Json("{\"cycle\": \"Custom cycle\"}"));

            Assert.AreEqual("Tags", tags.Type);
            CollectionAssert.AreEqual(new[] { "T1" }, tags.Tags);
            Assert.AreEqual("CustomCycle", cycle.Type);
            Assert.AreEqual("Custom cycle", cycle.Cycle);
        }

        [TestMethod]
        public void Test_2302b_ParseTrigger_NoTypeAndNoParts_ListsTheTypesWithoutEmptyQuotes()
        {
            var empty = Assert.ThrowsException<PortalException>(() => UnifiedDynamizationSpec.ParseTrigger(Json("{}")));
            var both = Assert.ThrowsException<PortalException>(() => UnifiedDynamizationSpec.ParseTrigger(Json("{\"tags\": [\"A\"], \"cycle\": \"C\"}")));

            StringAssert.Contains(empty.Message, "has no type");
            StringAssert.Contains(empty.Message, "AutomaticTags");
            Assert.IsFalse(empty.Message.Contains("''"));
            StringAssert.Contains(both.Message, "both");
        }

        [TestMethod]
        public void Test_2302_ParseTrigger_TakesATypeNameOrAnObject()
        {
            Assert.AreEqual("T5s", UnifiedDynamizationSpec.ParseTrigger(Json("\"t5s\"")).Type);

            var tags = UnifiedDynamizationSpec.ParseTrigger(Json("{\"type\": \"tags\", \"tags\": [\"A\", \"B\"]}"));

            Assert.AreEqual("Tags", tags.Type);
            CollectionAssert.AreEqual(new[] { "A", "B" }, tags.Tags);

            var cycle = UnifiedDynamizationSpec.ParseTrigger(Json("{\"type\": \"CustomCycle\", \"cycle\": \"Custom cycle\"}"));

            Assert.AreEqual("Custom cycle", cycle.Cycle);
        }

        [TestMethod]
        public void Test_2303_ParseTrigger_RefusesWhatTheTypeCannotUse()
        {
            StringAssert.Contains(Assert.ThrowsException<PortalException>(() => UnifiedDynamizationSpec.ParseTrigger(Json("\"T3s\""))).Message, "Types:");
            StringAssert.Contains(Assert.ThrowsException<PortalException>(() => UnifiedDynamizationSpec.ParseTrigger(Json("{\"type\": \"Tags\"}"))).Message, "needs 'tags'");
            StringAssert.Contains(Assert.ThrowsException<PortalException>(() => UnifiedDynamizationSpec.ParseTrigger(Json("{\"type\": \"CustomCycle\"}"))).Message, "needs 'cycle'");
            StringAssert.Contains(Assert.ThrowsException<PortalException>(() => UnifiedDynamizationSpec.ParseTrigger(Json("{\"type\": \"T1s\", \"tags\": [\"A\"]}"))).Message, "belongs to the type 'Tags'");
            StringAssert.Contains(Assert.ThrowsException<PortalException>(() => UnifiedDynamizationSpec.ParseTrigger(Json("{\"type\": \"T1s\", \"cycle\": \"C\"}"))).Message, "belongs to the type 'CustomCycle'");
        }

        [TestMethod]
        public void Test_2304_ParseMapping_ReadsRangeRows()
        {
            var mapping = UnifiedDynamizationSpec.ParseMapping(Json(
                "{\"type\": \"Range\", \"entries\": [{\"from\": 0, \"to\": 30, \"value\": \"#00FF00\"}, " +
                "{\"from\": 31, \"to\": 70.5, \"value\": \"Red\", \"flashing\": true, \"rate\": \"fast\", \"alternate\": \"Blue\"}]}"));

            Assert.AreEqual("range", mapping.Type);
            Assert.AreEqual(2, mapping.Entries.Count);
            Assert.AreEqual(0, mapping.Entries[0].From);
            Assert.AreEqual(70.5, mapping.Entries[1].To);
            Assert.AreEqual(true, mapping.Entries[1].Flashing);
            Assert.AreEqual("Fast", mapping.Entries[1].Rate);
            Assert.IsTrue(mapping.Entries[1].HasAlternate);
            Assert.IsFalse(mapping.Entries[0].HasAlternate);
        }

        [TestMethod]
        public void Test_2304a_ParseMapping_RowWithOnlyFromOrOnlyTo_IsNotSupportedWithTheReason()
        {
            var onlyFrom = Assert.ThrowsException<PortalException>(() =>
                UnifiedDynamizationSpec.ParseMapping(Json("{\"type\": \"range\", \"entries\": [{\"from\": 70, \"value\": \"Red\"}]}")));
            var onlyTo = Assert.ThrowsException<PortalException>(() =>
                UnifiedDynamizationSpec.ParseMapping(Json("{\"type\": \"range\", \"entries\": [{\"to\": 30, \"value\": \"Green\"}]}")));
            var neither = Assert.ThrowsException<PortalException>(() =>
                UnifiedDynamizationSpec.ParseMapping(Json("{\"type\": \"range\", \"entries\": [{\"value\": \"Green\"}]}")));

            Assert.AreEqual(PortalErrorCode.NotSupported, onlyFrom.Code);
            StringAssert.Contains(onlyFrom.Message, "read-only");
            StringAssert.Contains(onlyFrom.Message, "only 'from'");
            Assert.AreEqual(PortalErrorCode.NotSupported, onlyTo.Code);
            StringAssert.Contains(onlyTo.Message, "only 'to'");
            Assert.AreEqual(PortalErrorCode.InvalidParams, neither.Code);
        }

        [TestMethod]
        public void Test_2305_ParseMapping_ReadsSingleBitRowsAndRefusesDuplicates()
        {
            var mapping = UnifiedDynamizationSpec.ParseMapping(Json("{\"type\": \"singlebit\", \"entries\": [{\"bit\": 0, \"value\": \"Red\"}, {\"bit\": 1, \"value\": \"Green\"}]}"));

            Assert.AreEqual(0, mapping.Entries[0].Bit);
            Assert.AreEqual(1, mapping.Entries[1].Bit);

            var error = Assert.ThrowsException<PortalException>(() =>
                UnifiedDynamizationSpec.ParseMapping(Json("{\"type\": \"singlebit\", \"entries\": [{\"bit\": 1, \"value\": \"Red\"}, {\"bit\": 1, \"value\": \"Green\"}]}")));

            StringAssert.Contains(error.Message, "named twice");
        }

        [TestMethod]
        public void Test_2306_ParseMapping_RefusesTheTablesThatAreNotOffered()
        {
            var expression = Assert.ThrowsException<PortalException>(() => UnifiedDynamizationSpec.ParseMapping(Json("{\"type\": \"expression\", \"entries\": []}")));

            StringAssert.Contains(expression.Message, "closes TIA Portal");

            StringAssert.Contains(Assert.ThrowsException<PortalException>(() => UnifiedDynamizationSpec.ParseMapping(Json("{\"type\": \"bitmask\"}"))).Message, "not supported");
        }

        [TestMethod]
        public void Test_2307_ParseMapping_RefusesBadRows()
        {
            StringAssert.Contains(Assert.ThrowsException<PortalException>(() =>
                UnifiedDynamizationSpec.ParseMapping(Json("{\"type\": \"range\", \"entries\": [{\"from\": 5, \"to\": 1, \"value\": \"Red\"}]}"))).Message, "'from' above 'to'");

            StringAssert.Contains(Assert.ThrowsException<PortalException>(() =>
                UnifiedDynamizationSpec.ParseMapping(Json("{\"type\": \"range\", \"entries\": [{\"from\": 1, \"to\": 5}]}"))).Message, "needs 'value'");

            StringAssert.Contains(Assert.ThrowsException<PortalException>(() =>
                UnifiedDynamizationSpec.ParseMapping(Json("{\"type\": \"range\", \"entries\": [{\"bit\": 1, \"value\": \"Red\"}]}"))).Message, "'bit' belongs to");

            StringAssert.Contains(Assert.ThrowsException<PortalException>(() =>
                UnifiedDynamizationSpec.ParseMapping(Json("{\"type\": \"range\", \"entries\": [{\"from\": 1, \"to\": 5, \"value\": \"Red\", \"rate\": \"Quick\"}]}"))).Message, "Rates:");

            StringAssert.Contains(Assert.ThrowsException<PortalException>(() =>
                UnifiedDynamizationSpec.ParseMapping(Json("{\"type\": \"range\", \"entries\": []}"))).Message, "needs 'entries'");

            StringAssert.Contains(Assert.ThrowsException<PortalException>(() =>
                UnifiedDynamizationSpec.ParseMapping(Json("{\"type\": \"singlebit\", \"entries\": [{\"bit\": 2, \"value\": \"Red\"}]}"))).Message, "0 or 1");
        }

        [TestMethod]
        public void Test_2308_ParseMapping_NoneTakesNoRows()
        {
            Assert.AreEqual("none", UnifiedDynamizationSpec.ParseMapping(Json("{\"type\": \"none\"}")).Type);

            StringAssert.Contains(Assert.ThrowsException<PortalException>(() =>
                UnifiedDynamizationSpec.ParseMapping(Json("{\"type\": \"none\", \"entries\": [{}]}"))).Message, "takes no entries");
        }

        [TestMethod]
        public void Test_2309_ParseEvent_TakesAStringAnObjectOrNull()
        {
            Assert.AreEqual("code", UnifiedDynamizationSpec.ParseEvent(Json("\"code\""), "Tapped").Script);
            Assert.IsNull(UnifiedDynamizationSpec.ParseEvent(Json("null"), "Tapped").Script);
            Assert.IsNull(UnifiedDynamizationSpec.ParseEvent(Json("\"  \""), "Tapped").Script);

            var full = UnifiedDynamizationSpec.ParseEvent(Json("{\"script\": \"code\", \"async\": true, \"globalDefinitions\": \"const k = 5;\"}"), "Tapped");

            Assert.AreEqual("code", full.Script);
            Assert.AreEqual(true, full.Async);
            Assert.AreEqual("const k = 5;", full.GlobalDefinitions);
        }

        [TestMethod]
        public void Test_2310_ParseEvent_RefusesOptionsWithoutAScriptAndUnknownParts()
        {
            StringAssert.Contains(Assert.ThrowsException<PortalException>(() =>
                UnifiedDynamizationSpec.ParseEvent(Json("{\"async\": true}"), "Tapped")).Message, "without a 'script'");

            StringAssert.Contains(Assert.ThrowsException<PortalException>(() =>
                UnifiedDynamizationSpec.ParseEvent(Json("{\"script\": \"x\", \"sync\": true}"), "Tapped")).Message, "no part 'sync'");

            StringAssert.Contains(Assert.ThrowsException<PortalException>(() =>
                UnifiedDynamizationSpec.ParseEvent(Json("5"), "Tapped")).Message, "takes the script as a string");
        }

        [TestMethod]
        public void Test_2311_SplitPropertyEvent_DefaultsToAChangeOfTheValue()
        {
            Assert.AreEqual(("ProcessValue", "Change"), UnifiedDynamizationSpec.SplitPropertyEvent("ProcessValue"));
            Assert.AreEqual(("ProcessValue", "QualityCodeChange"), UnifiedDynamizationSpec.SplitPropertyEvent("ProcessValue.qualitycodechange"));
            Assert.AreEqual(("Left", "Change"), UnifiedDynamizationSpec.SplitPropertyEvent("Left.Change"));
            Assert.AreEqual(("Font.Size", "Change"), UnifiedDynamizationSpec.SplitPropertyEvent("Font.Size"));
        }

        [TestMethod]
        public void Test_2312_Canonical_NamesTheValidOnes()
        {
            Assert.AreEqual("RangeViolation", UnifiedDynamizationSpec.CanonicalCondition("rangeviolation"));
            StringAssert.Contains(Assert.ThrowsException<PortalException>(() => UnifiedDynamizationSpec.CanonicalCondition("Sometimes")).Message, "Never, Always, RangeViolation");
            StringAssert.Contains(Assert.ThrowsException<PortalException>(() => UnifiedDynamizationSpec.CanonicalRate("Quick")).Message, "Slow, Medium, Fast");
        }

        [TestMethod]
        public void Test_2313_FormulaTags_ReadsTheQuotedNames()
        {
            CollectionAssert.AreEqual(new[] { "Tag_1", "Tag 2.Member" }, UnifiedDynamizationSpec.FormulaTags("('Tag_1'+'Tag 2.Member')/2"));
            Assert.AreEqual(0, UnifiedDynamizationSpec.FormulaTags("2+2").Count);

            StringAssert.Contains(Assert.ThrowsException<PortalException>(() => UnifiedDynamizationSpec.FormulaTags("'Tag_1*2")).Message, "is not closed");
            StringAssert.Contains(Assert.ThrowsException<PortalException>(() => UnifiedDynamizationSpec.FormulaTags("''+1")).Message, "no tag name");
        }

        [TestMethod]
        public void Test_2314_FormulaBareWords_LeavesOutQuotedNamesAndNumbers()
        {
            CollectionAssert.AreEqual(new[] { "Tag_2", "Math.abs" }, UnifiedDynamizationSpec.FormulaBareWords("'Tag_1'*2 + Tag_2 - Math.abs(3.5)"));
            Assert.AreEqual(0, UnifiedDynamizationSpec.FormulaBareWords("'Tag_1'*2+1").Count);
        }

        [TestMethod]
        public void Test_2315_SameFormula_AllowsTheCaseCorrectionOnly()
        {
            Assert.IsTrue(UnifiedDynamizationSpec.SameFormula("'tag_1'+1", "'Tag_1'+1"));
            Assert.IsFalse(UnifiedDynamizationSpec.SameFormula("$value * 2", "'InvalidTag'"));
            Assert.IsFalse(UnifiedDynamizationSpec.SameFormula("'Tag_1'+1", null));
        }

        [TestMethod]
        public void Test_2316_Validation_RefusesOnlyTheErrorsTheWriteBrought()
        {
            var before = new[] { new UnifiedFinding("RaisedStateTag", "No trigger tag is configured.", true) };

            var after = new[]
            {
                new UnifiedFinding("RaisedStateTag", "No trigger tag is configured.", true),
                new UnifiedFinding("AlarmClass", "The object \"X\" does not exist.", true),
                new UnifiedFinding("AlarmClass", "The object \"X\" does not exist.", true),
                new UnifiedFinding("Priority", "Unusual value.", false)
            };

            var verdict = UnifiedValidation.Judge(before, after);

            CollectionAssert.AreEqual(new[] { "AlarmClass: The object \"X\" does not exist." }, verdict.Errors);
            Assert.AreEqual(2, verdict.Notes.Count);
            StringAssert.Contains(verdict.Notes[0], "not caused by this write: RaisedStateTag");
            StringAssert.Contains(verdict.Notes[1], "warns: Priority");
        }

        [TestMethod]
        public void Test_2317_Validation_ANewTextOnTheSamePropertyIsANewError()
        {
            var before = new[] { new UnifiedFinding("RaisedStateTag", "No trigger tag is configured.", true) };
            var after = new[] { new UnifiedFinding("RaisedStateTag", "The object \"T\" does not exist.", true) };

            Assert.AreEqual(1, UnifiedValidation.Judge(before, after).Errors.Count);
            Assert.AreEqual(0, UnifiedValidation.Judge(after, after).Errors.Count);
            StringAssert.Contains(UnifiedValidation.Refusal(new[] { "a", "b" }), "a | b");
        }
    }
}
