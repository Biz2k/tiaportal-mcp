using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for reading the texts WinCC Unified stores as small HTML documents, as
    /// unified_get_alarms returns them. These tests do not connect to TIA Portal and do not
    /// open a project.
    /// </summary>
    [TestClass]
    public class Test15UnifiedTexts
    {
        [TestMethod]
        public void Test_1500_PlainUnifiedText_UnwrapsTheDocument()
        {
            Assert.AreEqual("Pump fault", Portal.PlainUnifiedText("<body><p>Pump fault</p></body>"));
            Assert.AreEqual(string.Empty, Portal.PlainUnifiedText("<body><p/></body>"));
            Assert.AreEqual(string.Empty, Portal.PlainUnifiedText(null));
        }

        [TestMethod]
        public void Test_1501_PlainUnifiedText_TurnsParagraphsIntoLinesAndDecodesEntities()
        {
            Assert.AreEqual("Line 1\nLine 2", Portal.PlainUnifiedText("<body><p>Line 1</p><p>Line 2</p></body>"));
            Assert.AreEqual("T > 80 & rising", Portal.PlainUnifiedText("<body><p>T &gt; 80 &amp; rising</p></body>"));
        }

        [TestMethod]
        public void Test_1502_PlainUnifiedText_LeavesPlainTextAlone()
        {
            Assert.AreEqual("check <the> pump", Portal.PlainUnifiedText("check <the> pump"));
        }

        [TestMethod]
        public void Test_1503_PlainUnifiedText_ReadsBackWhatFormatUnifiedTextWrote()
        {
            var stored = Portal.FormatUnifiedText("Level > 80 %\nCheck valve", "<body/>");

            Assert.AreEqual("Level > 80 %\nCheck valve", Portal.PlainUnifiedText(stored));
        }
    }
}
