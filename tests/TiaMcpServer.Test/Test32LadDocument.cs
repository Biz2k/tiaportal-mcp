using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the SIMATIC SD document of a LAD block taken apart and put together, and for the batch of network
    /// actions of 'plc_manage_lad_networks'. These tests do not connect to TIA Portal.
    /// </summary>
    [TestClass]
    [TestCategory("NoTia")]
    public class Test32LadDocument
    {
        private const string Declaration =
            "{\r\n    S7_BlockNumber := \"14\";\r\n    S7_PreferredLanguage := \"LAD\"\r\n}\r\n" +
            "FUNCTION_BLOCK \"Demo\"\r\n    VAR_INPUT\r\n        A : Bool;\r\n    END_VAR\r\n    VAR\r\n" +
            "        {\r\n            S7_Setpoint := \"False\"\r\n        }\r\n        T1 : TON_TIME;\r\n    END_VAR\r\n\r\n" +
            "    {\r\n        S7_Language := \"LAD\";\r\n        S7_NetworkComment := \"MLC_C\";\r\n        S7_NetworkTitle := \"MLC_T\"\r\n    }\r\n" +
            "    NETWORK\r\n        RUNG wire#powerrail\r\n            Contact( #A )\r\n            Coil( \"Out1\" )\r\n        END_RUNG\r\n    END_NETWORK\r\n" +
            "    {\r\n        S7_Language := \"SCL\";\r\n        S7_NetworkTitle := \"MLC_T\"\r\n    }\r\n" +
            "    NETWORK\r\n        \"Out2\" := #A;\r\n    END_NETWORK\r\n" +
            "    { S7_Language := \"LAD\" }\r\n    NETWORK\r\n        RUNG wire#powerrail\r\n        END_RUNG\r\n    END_NETWORK\r\n" +
            "END_FUNCTION_BLOCK\r\n";

        private const string Resources =
            "MultiLingualTexts:\r\n" +
            "  - id: MLC_T\r\n    en-US: First\r\n    ru-RU: 'Первая: ''a'''\r\n" +
            "  - id: MLC_C\r\n    ru-RU: |2-\r\n      line 1\r\n        indented\r\n\r\n      line 4\r\n" +
            "  - id: MLC_T\r\n    ru-RU: Second\r\n";

        private static readonly string[] Cultures = { "ru-RU", "en-US" };

        private static LadDocument Read() => LadDocument.Parse(Declaration, Resources);

        [TestMethod]
        public void Test_3200_Parse_SplitsHeadNetworksAndTail()
        {
            var document = Read();

            Assert.AreEqual(3, document.Networks.Count);
            StringAssert.Contains(document.Head, "T1 : TON_TIME;");
            Assert.IsFalse(document.Head.Contains("S7_NetworkTitle"));
            Assert.AreEqual("END_FUNCTION_BLOCK", document.Tail);
            CollectionAssert.AreEqual(new[] { "LAD", "SCL", "LAD" }, document.Networks.Select(n => n.Language).ToList());
            StringAssert.Contains(document.Networks[0].Code, "Contact( #A )");
            Assert.AreEqual("        \"Out2\" := #A;", document.Networks[1].Code);
            Assert.IsNull(document.Networks[2].TitleId);
        }

        [TestMethod]
        public void Test_3201_Parse_GivesARepeatedIdItsOwnTextPerUse()
        {
            var document = Read();

            Assert.AreNotEqual(document.Networks[0].TitleId, document.Networks[1].TitleId);
            Assert.AreEqual("Первая: 'a'", document.TextOf(document.Networks[0].TitleId, Cultures));
            Assert.AreEqual("First", document.TextOf(document.Networks[0].TitleId, new[] { "en-US" }));
            Assert.AreEqual("Second", document.TextOf(document.Networks[1].TitleId, Cultures));
            Assert.AreEqual("line 1\n  indented\n\nline 4", document.TextOf(document.Networks[0].CommentId, Cultures));
            Assert.AreEqual(3, document.RenderResources()!.Split('\n').Count(l => l.Contains("- id:")));
        }

        [TestMethod]
        public void Test_3202_Render_KeepsWhatWasRead()
        {
            var again = LadDocument.Parse(Read().RenderDeclaration(), Read().RenderResources());

            Assert.AreEqual(Read().Head, again.Head);
            CollectionAssert.AreEqual(Read().Networks.Select(n => n.Code).ToList(), again.Networks.Select(n => n.Code).ToList());
            Assert.AreEqual("line 1\n  indented\n\nline 4", again.TextOf(again.Networks[0].CommentId, Cultures));
            Assert.AreEqual("Second", again.TextOf(again.Networks[1].TitleId, Cultures));
        }

        [TestMethod]
        public void Test_3203_QuoteLocalNames_QuotesOnlyWhatTiaPortalRefusesBare()
        {
            Assert.AreEqual("Contact( #\"Пуск\" )", LadDocument.QuoteLocalNames("Contact( #Пуск )"));
            Assert.AreEqual("Coil( #Data.\"Член\".%X0 )", LadDocument.QuoteLocalNames("Coil( #Data.Член.%X0 )"));
            Assert.AreEqual("RUNG wire#powerrail", LadDocument.QuoteLocalNames("RUNG wire#powerrail"));
            Assert.AreEqual("Contact( #\"Пуск\" )", LadDocument.QuoteLocalNames("Contact( #\"Пуск\" )"));
            Assert.AreEqual("Move( in := \"DB\".ШВ, out1 => #Run )", LadDocument.QuoteLocalNames("Move( in := \"DB\".ШВ, out1 => #Run )"));
        }

        [TestMethod]
        public void Test_3204_CleanCode_TakesTheNetworkWithOrWithoutItsWrapper()
        {
            var bare = LadDocument.CleanCode("RUNG wire#powerrail\n  Contact( #A )\nEND_RUNG\n");
            var wrapped = LadDocument.CleanCode("{ S7_Language := \"LAD\" }\nNETWORK\n    RUNG wire#powerrail\n      Contact( #A )\n    END_RUNG\nEND_NETWORK");

            Assert.AreEqual("        RUNG wire#powerrail\n          Contact( #A )\n        END_RUNG", bare);
            Assert.AreEqual(bare, wrapped);
            StringAssert.Contains(LadDocument.CleanCode("  "), "END_RUNG");
        }

        [TestMethod]
        public void Test_3205_Apply_NumbersMeanTheBlockAsItWasRead()
        {
            var document = Read();
            var actions = new List<LadNetworkAction>
            {
                new LadNetworkAction { Action = "insert", After = 0, Code = "RUNG wire#powerrail\nEND_RUNG", Title = "New first" },
                new LadNetworkAction { Action = "delete", Network = 1 },
                new LadNetworkAction { Action = "move", Network = 3, After = 0 },
                new LadNetworkAction { Action = "replace", Network = 2, Code = "\"Out2\" := NOT #A;", Title = "" },
                new LadNetworkAction { Action = "insert", Code = "\"Out2\" := TRUE;", Language = "scl", Comment = "a\nb" }
            };

            Assert.AreEqual(0, LadNetworkActions.Check(document, actions).Count);

            LadNetworkActions.Apply(document, actions, Cultures);

            // new first, then the moved third, the second, the appended one; the first is gone.
            CollectionAssert.AreEqual(new[] { 0, 3, 2, 0 }, document.Networks.Select(n => n.Original).ToList());
            Assert.AreEqual("New first", document.TextOf(document.Networks[0].TitleId, Cultures));
            Assert.IsNull(document.Networks[2].TitleId);
            StringAssert.Contains(document.Networks[2].Code, "NOT #A");
            Assert.AreEqual("SCL", document.Networks[3].Language);
            Assert.AreEqual("a\nb", document.TextOf(document.Networks[3].CommentId, Cultures));

            var again = LadDocument.Parse(document.RenderDeclaration(), document.RenderResources());

            Assert.AreEqual(4, again.Networks.Count);
            Assert.AreEqual("a\nb", again.TextOf(again.Networks[3].CommentId, Cultures));
            Assert.IsFalse(document.RenderResources()!.Contains("Second"), "the text of the removed title is not written");
        }

        [TestMethod]
        public void Test_3206_Check_NamesEveryWrongAction()
        {
            var actions = new List<LadNetworkAction>
            {
                new LadNetworkAction { Action = "replace", Network = 9, Code = "RUNG wire#powerrail\nEND_RUNG" },
                new LadNetworkAction { Action = "replace", Network = 1, Code = "Contact( #A )" },
                new LadNetworkAction { Action = "replace", Network = 2, Code = "RUNG wire#powerrail\nEND_RUNG" },
                new LadNetworkAction { Action = "delete", Network = 3 },
                new LadNetworkAction { Action = "set_title", Network = 3, Title = "x" },
                new LadNetworkAction { Action = "move", Network = 1 },
                new LadNetworkAction { Action = "insert", Network = 1, Code = "RUNG wire#powerrail\nEND_RUNG" },
                new LadNetworkAction { Action = "insert", Language = "FBD", Code = "x" },
                new LadNetworkAction { Action = "frobnicate" }
            };

            var problems = LadNetworkActions.Check(Read(), actions);

            Assert.AreEqual(8, problems.Count, string.Join(" | ", problems));
            StringAssert.Contains(problems[0], "from 1 to 3");
            StringAssert.Contains(problems[1], "has no RUNG");
            StringAssert.Contains(problems[2], "SCL network");
            StringAssert.Contains(problems[3], "already has a 'delete'");
            StringAssert.Contains(problems[4], "'after' is missing");
            StringAssert.Contains(problems[5], "takes 'after'");
            StringAssert.Contains(problems[6], "LAD or SCL");
            StringAssert.Contains(problems[7], "not known");
        }

        [TestMethod]
        public void Test_3207_LinesMissing_FindsWhatTheImportDropped()
        {
            var sent = "PID : PID_Compact := (\n  PhysicalUnit := 2,\n  Config := (\n    X := FALSE\n  )\n);\nT : Time := T#5000ms;";
            var back = "PID : PID_Compact := (\n  Config := (\n    X := FALSE\n  )\n);\nT : Time := T#5S;";

            CollectionAssert.AreEqual(new[] { "PhysicalUnit := 2", "T : Time := T#5000ms" }, LadDocument.LinesMissing(sent, back));
            Assert.AreEqual(0, LadDocument.LinesMissing(sent, sent + "\n    S7_BlockNumber := \"4\";").Count);
        }
    }
}
