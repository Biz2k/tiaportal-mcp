using TiaMcpServer.Siemens;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// 'import_objects' takes 'conflict_resolution' as 'overwrite' or 'skip' and refuses the rest. These tests do not connect to it.
    /// </summary>
    [TestClass]
    [TestCategory("NoTia")]
    public class Test24ImportObjects
    {
        [TestMethod]
        public void Test_2400_Rename_IsRefusedWithAnAlternative()
        {
            var error = Assert.ThrowsException<PortalException>(() => ImportConflictResolution.ParseOverwrite("Rename"));

            StringAssert.Contains(error.Message, "'rename' is not offered");
            StringAssert.Contains(error.Message, "plc_copy_block");
        }

        [TestMethod]
        public void Test_2401_UnknownResolution_NamesTheValidOnes()
        {
            var error = Assert.ThrowsException<PortalException>(() => ImportConflictResolution.ParseOverwrite("merge"));

            StringAssert.Contains(error.Message, "Use 'overwrite' or 'skip'");
        }

        [TestMethod]
        public void Test_2402_OverwriteAndSkip_AreTakenInAnyCase()
        {
            Assert.IsTrue(ImportConflictResolution.ParseOverwrite(" Overwrite "));
            Assert.IsFalse(ImportConflictResolution.ParseOverwrite("SKIP"));
        }
    }
}
