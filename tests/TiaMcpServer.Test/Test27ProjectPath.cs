using System;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>The rules for the folder of 'save_as_project'. These tests do not connect to TIA Portal.</summary>
    [TestClass]
    [TestCategory("NoTia")]
    public class Test27ProjectPath
    {
        private static string Check(string? path, bool parentExists = true, bool targetExists = false, bool targetEmpty = true)
        {
            return ProjectPathRules.CheckNewProjectFolder(
                path,
                d => d.EndsWith("Projects", StringComparison.OrdinalIgnoreCase) ? parentExists : targetExists,
                d => targetEmpty);
        }

        [TestMethod]
        public void NewFolder_Accepted_TrailingSeparatorIgnored()
        {
            Assert.AreEqual(@"C:\Projects\NewPlant", Check(@"C:\Projects\NewPlant"));
            Assert.AreEqual(@"C:\Projects\NewPlant", Check(@"C:\Projects\NewPlant\"));
        }

        [TestMethod]
        public void EmptyExistingFolder_Accepted()
        {
            Assert.AreEqual(@"C:\Projects\NewPlant", Check(@"C:\Projects\NewPlant", targetExists: true, targetEmpty: true));
        }

        [TestMethod]
        public void ProjectExtension_RefusedWithTheCorrectedPath()
        {
            var ex = Assert.ThrowsException<PortalException>(() => Check(@"C:\Projects\Plant.ap21"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, ex.Code);
            StringAssert.Contains(ex.Message, @"Use 'C:\Projects\Plant'");
            StringAssert.Contains(ex.Message, @"C:\Projects\Plant\Plant.ap21");
            Assert.ThrowsException<PortalException>(() => Check(@"C:\Projects\Session.als21"));
        }

        [TestMethod]
        public void RelativeOrEmptyPath_InvalidParams()
        {
            Assert.AreEqual(PortalErrorCode.InvalidParams, Assert.ThrowsException<PortalException>(() => Check(@"Projects\Plant")).Code);
            Assert.AreEqual(PortalErrorCode.InvalidParams, Assert.ThrowsException<PortalException>(() => Check("  ")).Code);
            Assert.AreEqual(PortalErrorCode.InvalidParams, Assert.ThrowsException<PortalException>(() => Check(null)).Code);
        }

        [TestMethod]
        public void MissingParentFolder_NotFound()
        {
            Assert.AreEqual(PortalErrorCode.NotFound, Assert.ThrowsException<PortalException>(() => Check(@"C:\Projects\Plant", parentExists: false)).Code);
        }

        [TestMethod]
        public void NonEmptyFolder_InvalidState()
        {
            var ex = Assert.ThrowsException<PortalException>(() => Check(@"C:\Projects\Plant", targetExists: true, targetEmpty: false));

            Assert.AreEqual(PortalErrorCode.InvalidState, ex.Code);
            StringAssert.Contains(ex.Message, "not empty");
        }

        [TestMethod]
        public void ToolCallGate_OnlyTheServerLookingToolsAreFree()
        {
            Assert.IsFalse(TiaMcpServer.ModelContextProtocol.ToolCallGate.IsGatedForTest("get_state"));
            Assert.IsFalse(TiaMcpServer.ModelContextProtocol.ToolCallGate.IsGatedForTest("doctor"));
            Assert.IsTrue(TiaMcpServer.ModelContextProtocol.ToolCallGate.IsGatedForTest("close_project"));
            Assert.IsTrue(TiaMcpServer.ModelContextProtocol.ToolCallGate.IsGatedForTest("plc_get_blocks"));
        }
    }
}
