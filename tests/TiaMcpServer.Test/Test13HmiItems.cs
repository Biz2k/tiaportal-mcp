using System.Text.Json;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the value handling of unified_manage_items: turning a JSON value into what an HMI
    /// property holds, and wrapping text the way WinCC Unified stores it. These tests do not
    /// connect to TIA Portal and do not open a project.
    /// </summary>
    [TestClass]
    [TestCategory("NoTia")]
    public class Test13HmiItems
    {
        private enum Alignment
        {
            Left,
            Center,
            Right
        }

        private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

        [TestMethod]
        public void Test_1300_ConvertHmiValue_FollowsTheTypeOfTheProperty()
        {
            // A JSON number is an int or a double; the property decides what it has to become.
            Assert.AreEqual(200u, Portal.ConvertHmiValue(Json("200"), typeof(uint), "Width"));
            Assert.AreEqual(0.5, Portal.ConvertHmiValue(Json("0.5"), typeof(double), "Opacity"));
            Assert.AreEqual(0.5f, Portal.ConvertHmiValue(Json("0.5"), typeof(float), "Opacity"));
            Assert.AreEqual((byte)3, Portal.ConvertHmiValue(Json("3"), typeof(byte), "BorderWidth"));
            Assert.AreEqual(false, Portal.ConvertHmiValue(Json("false"), typeof(bool), "Visible"));
            Assert.AreEqual(true, Portal.ConvertHmiValue(Json("\"true\""), typeof(bool), "Visible"));
            Assert.AreEqual("42", Portal.ConvertHmiValue(Json("42"), typeof(string), "ProcessValue"));
            Assert.IsNull(Portal.ConvertHmiValue(Json("null"), typeof(string), "ProcessValue"));
        }

        [TestMethod]
        public void Test_1301_ConvertHmiValue_ReadsEnumsByNameOrNumber()
        {
            Assert.AreEqual(Alignment.Center, Portal.ConvertHmiValue(Json("\"center\""), typeof(Alignment), "HorizontalTextAlignment"));
            Assert.AreEqual(Alignment.Right, Portal.ConvertHmiValue(Json("2"), typeof(Alignment), "HorizontalTextAlignment"));
        }

        [TestMethod]
        public void Test_1302_ConvertHmiValue_ReadsColors()
        {
            Assert.AreEqual(System.Drawing.Color.FromArgb(255, 0, 0).ToArgb(),
                ((System.Drawing.Color)Portal.ConvertHmiValue(Json("\"#FF0000\""), typeof(System.Drawing.Color), "BackColor")!).ToArgb());
            Assert.AreEqual(System.Drawing.Color.Red,
                (System.Drawing.Color)Portal.ConvertHmiValue(Json("\"Red\""), typeof(System.Drawing.Color), "BackColor")!);
        }

        [TestMethod]
        public void Test_1303_ConvertHmiValue_NamesTheAllowedValuesOnAMismatch()
        {
            var wrongEnum = Assert.ThrowsException<PortalException>(() =>
                Portal.ConvertHmiValue(Json("\"Sideways\""), typeof(Alignment), "HorizontalTextAlignment"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, wrongEnum.Code);
            StringAssert.Contains(wrongEnum.Message, "Left, Center, Right");
            StringAssert.Contains(wrongEnum.Message, "Sideways");

            var wrongColor = Assert.ThrowsException<PortalException>(() =>
                Portal.ConvertHmiValue(Json("\"Bluish\""), typeof(System.Drawing.Color), "BackColor"));

            StringAssert.Contains(wrongColor.Message, "#FF0000");

            Assert.ThrowsException<PortalException>(() => Portal.ConvertHmiValue(Json("\"wide\""), typeof(uint), "Width"));
        }

        [TestMethod]
        public void Test_1304_ConvertHmiValue_WithoutAKnownTypeTrustsTheJsonValue()
        {
            Assert.AreEqual(42, Portal.ConvertHmiValue(Json("42"), null, "X"));
            Assert.AreEqual(1.5, Portal.ConvertHmiValue(Json("1.5"), null, "X"));
            Assert.AreEqual(true, Portal.ConvertHmiValue(Json("true"), null, "X"));
            Assert.AreEqual("text", Portal.ConvertHmiValue(Json("\"text\""), null, "X"));
            Assert.IsInstanceOfType(Portal.ConvertHmiValue(Json("\"#00FF00\""), null, "X"), typeof(System.Drawing.Color));
        }

        [TestMethod]
        public void Test_1305_FormatUnifiedText_WrapsPlainTextOnly()
        {
            // Plain text for a property that holds a Unified text document, or nothing yet.
            Assert.AreEqual("<body><p>Start</p></body>", Portal.FormatUnifiedText("Start", "<body><p>Text</p></body>"));
            Assert.AreEqual("<body><p>Start</p></body>", Portal.FormatUnifiedText("Start", ""));
            Assert.AreEqual("<body><p>Start</p></body>", Portal.FormatUnifiedText("Start", null));

            // Markup characters are data, and each line is its own paragraph.
            Assert.AreEqual("<body><p>A &lt; B &amp; C</p></body>", Portal.FormatUnifiedText("A < B & C", null));
            Assert.AreEqual("<body><p>Line 1</p><p>Line 2</p></body>", Portal.FormatUnifiedText("Line 1\r\nLine 2", null));

            // Already a document: untouched.
            Assert.AreEqual("<body><p><b>Bold</b></p></body>", Portal.FormatUnifiedText("<body><p><b>Bold</b></p></body>", null));

            // The property currently holds plain text, so plain text is what it takes.
            Assert.AreEqual("Hint", Portal.FormatUnifiedText("Hint", "Old hint"));
        }
    }
}