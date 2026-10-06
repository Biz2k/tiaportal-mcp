using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for how get_library_types assigns a library type to a system. These tests do not
    /// connect to TIA Portal and do not open a project; they work on the namespaces of the Openness classes.
    /// </summary>
    [TestClass]
    [TestCategory("NoTia")]
    public class Test14LibraryTypes
    {
        [TestMethod]
        public void Test_1400_ClassifyLibraryType_KnowsPlcAndClassicTypesByTheirClass()
        {
            Assert.AreEqual("plc", Portal.ClassifyLibraryType("Siemens.Engineering.SW.Types", null).System);
            Assert.AreEqual("classic", Portal.ClassifyLibraryType("Siemens.Engineering.Hmi.Faceplate", null).System);
        }

        [TestMethod]
        public void Test_1401_ClassifyLibraryType_TellsUnifiedFromUniversalByTheDeviceVersion()
        {
            var generic = "Siemens.Engineering.Library.Types";

            Assert.AreEqual("unified", Portal.ClassifyLibraryType(generic, "20.0.0.0").System);
            Assert.AreEqual("universal", Portal.ClassifyLibraryType(generic, "").System);
            Assert.AreEqual("universal", Portal.ClassifyLibraryType(generic, null).System);
        }

        [TestMethod]
        public void Test_1402_ClassifyLibraryType_UsesOnlyTheDocumentedSystems()
        {
            var generic = "Siemens.Engineering.Library.Types";

            CollectionAssert.Contains(TiaMcpServer.ModelContextProtocol.LibraryTypeInfo.Systems, Portal.ClassifyLibraryType(generic, null).System);
            CollectionAssert.Contains(TiaMcpServer.ModelContextProtocol.LibraryTypeInfo.Systems, Portal.ClassifyLibraryType(generic, "18.0.0.2").System);
            CollectionAssert.Contains(TiaMcpServer.ModelContextProtocol.LibraryTypeInfo.Systems, Portal.ClassifyLibraryType("Siemens.Engineering.SW.Types", null).System);
        }
    }
}
