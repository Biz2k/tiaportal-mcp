using System.Collections.Generic;
using System.Xml.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the text handling behind creating, copying and moving blocks: the SimaticML
    /// rewrite that lets a copy sit next to its original, the template an empty function block
    /// is imported from, and free block number lookup. These tests do not connect to TIA Portal
    /// and do not open a project.
    /// </summary>
    [TestClass]
    public class Test10BlockTransfer
    {
        private const string ExportedFb =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
            "<Document>\r\n" +
            "  <Engineering version=\"V21\" />\r\n" +
            "  <SW.Blocks.FB ID=\"0\">\r\n" +
            "    <AttributeList>\r\n" +
            "      <Interface><Sections xmlns=\"http://www.siemens.com/automation/Openness/SW/Interface/v5\">" +
            "<Section Name=\"Input\"><Member Name=\"Name\" Datatype=\"Bool\" /></Section></Sections></Interface>\r\n" +
            "      <Name>Valve_Control</Name>\r\n" +
            "      <Number>20</Number>\r\n" +
            "      <ProgrammingLanguage>LAD</ProgrammingLanguage>\r\n" +
            "    </AttributeList>\r\n" +
            "  </SW.Blocks.FB>\r\n" +
            "</Document>";

        private const string ExportedType =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
            "<Document>\r\n" +
            "  <SW.Types.PlcStruct ID=\"0\">\r\n" +
            "    <AttributeList>\r\n" +
            "      <Name>UDT_Valve</Name>\r\n" +
            "    </AttributeList>\r\n" +
            "  </SW.Types.PlcStruct>\r\n" +
            "</Document>";

        private static XElement Attributes(string xml, string objectName)
        {
            return XDocument.Parse(xml).Root!.Element(objectName)!.Element("AttributeList")!;
        }

        [TestMethod]
        public void Test_1000_RewriteExportedObject_RenamesAndRenumbersTheBlock()
        {
            // Act
            var rewritten = Portal.RewriteExportedObject(ExportedFb, "Valve_Control_Copy", 21);
            var attributes = Attributes(rewritten, "SW.Blocks.FB");

            // Assert
            Assert.AreEqual("Valve_Control_Copy", attributes.Element("Name")!.Value);
            Assert.AreEqual("21", attributes.Element("Number")!.Value);
            Assert.AreEqual("LAD", attributes.Element("ProgrammingLanguage")!.Value);
            StringAssert.StartsWith(rewritten, "<?xml");
        }

        [TestMethod]
        public void Test_1001_RewriteExportedObject_LeavesInterfaceMembersAlone()
        {
            // The interface has a member that is itself called "Name"; only the block's own
            // name element may change.
            var rewritten = Portal.RewriteExportedObject(ExportedFb, "Renamed", null);

            StringAssert.Contains(rewritten, "<Member Name=\"Name\" Datatype=\"Bool\" />");
            Assert.AreEqual("20", Attributes(rewritten, "SW.Blocks.FB").Element("Number")!.Value, "The number stays when none is given");
        }

        [TestMethod]
        public void Test_1002_RewriteExportedObject_RenamesATypeWithoutANumber()
        {
            var rewritten = Portal.RewriteExportedObject(ExportedType, "UDT_Valve_2", 5);
            var attributes = Attributes(rewritten, "SW.Types.PlcStruct");

            Assert.AreEqual("UDT_Valve_2", attributes.Element("Name")!.Value);
            Assert.IsNull(attributes.Element("Number"), "A type has no number and must not get one");
        }

        [TestMethod]
        public void Test_1003_RewriteExportedObject_RejectsADocumentWithoutAnObject()
        {
            var pex = Assert.ThrowsException<PortalException>(() =>
                Portal.RewriteExportedObject("<Document><Engineering version=\"V21\" /></Document>", "X", null));

            Assert.AreEqual(PortalErrorCode.ImportFailed, pex.Code);
        }

        [TestMethod]
        public void Test_1004_BuildFbTemplate_IsWellFormedAndEscapesTheName()
        {
            // Act
            var template = Portal.BuildFbTemplate("Pump & Valve", number: 900, language: "FBD", tiaMajorVersion: 21);

            // Assert
            var document = XDocument.Parse(template);
            var attributes = Attributes(template, "SW.Blocks.FB");

            Assert.AreEqual("V21", document.Root!.Element("Engineering")!.Attribute("version")!.Value);
            Assert.AreEqual("Pump & Valve", attributes.Element("Name")!.Value);
            Assert.AreEqual("900", attributes.Element("Number")!.Value);
            Assert.AreEqual("FBD", attributes.Element("ProgrammingLanguage")!.Value);
            Assert.IsNotNull(attributes.Element("Namespace"), "TIA Portal rejects a block document without a Namespace element");
        }

        [TestMethod]
        public void Test_1005_NextFreeNumber_SkipsUsedNumbersAndSystemObs()
        {
            Assert.AreEqual(1, Portal.NextFreeNumber(new HashSet<int>(), "DB"));
            Assert.AreEqual(1, Portal.NextFreeNumber(new HashSet<int> { 0 }, "DB"), "DB0 is not a valid number and never offered");
            Assert.AreEqual(3, Portal.NextFreeNumber(new HashSet<int> { 1, 2, 4 }, "FB"));
            Assert.AreEqual(123, Portal.NextFreeNumber(new HashSet<int> { 1, 30, 100 }, "OB"), "User OBs start at 123");
            Assert.AreEqual(124, Portal.NextFreeNumber(new HashSet<int> { 123 }, "OB"));
        }
    }
}