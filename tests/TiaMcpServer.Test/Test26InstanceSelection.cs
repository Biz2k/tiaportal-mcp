using System.Collections.Generic;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>Choosing the TIA Portal process for 'connect'. These tests do not connect to TIA Portal.</summary>
    [TestClass]
    [TestCategory("NoTia")]
    public class Test26InstanceSelection
    {
        private static readonly List<TiaInstanceInfo> Two = new List<TiaInstanceInfo>
        {
            new TiaInstanceInfo { Id = 100, ProjectPath = @"C:\Projects\Plant\Plant.ap21", Mode = "WithUserInterface" },
            new TiaInstanceInfo { Id = 200, ProjectPath = string.Empty, Mode = "WithUserInterface" }
        };

        [TestMethod]
        public void NoCriteria_FirstInstance()
        {
            Assert.AreEqual(100, TiaInstanceSelection.Pick(Two, null, null).Id);
        }

        [TestMethod]
        public void ProcessId_PicksThatInstance()
        {
            Assert.AreEqual(200, TiaInstanceSelection.Pick(Two, 200, null).Id);
        }

        [TestMethod]
        public void ProjectPath_FullPathNameAndNameWithoutExtension()
        {
            Assert.AreEqual(100, TiaInstanceSelection.Pick(Two, null, "c:/projects/plant/PLANT.ap21").Id);
            Assert.AreEqual(100, TiaInstanceSelection.Pick(Two, null, "Plant.ap21").Id);
            Assert.AreEqual(100, TiaInstanceSelection.Pick(Two, null, "Plant").Id);
        }

        [TestMethod]
        public void NoMatch_NotFoundListsRunningInstances()
        {
            var ex = Assert.ThrowsException<PortalException>(() => TiaInstanceSelection.Pick(Two, 999, null));

            Assert.AreEqual(PortalErrorCode.NotFound, ex.Code);
            StringAssert.Contains(ex.Message, "process 100");
            StringAssert.Contains(ex.Message, "process 200");
        }

        [TestMethod]
        public void BothCriteria_InvalidParams()
        {
            var ex = Assert.ThrowsException<PortalException>(() => TiaInstanceSelection.Pick(Two, 100, "Plant"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, ex.Code);
        }

        [TestMethod]
        public void SameProjectInTwoInstances_Ambiguous()
        {
            var both = new List<TiaInstanceInfo>
            {
                new TiaInstanceInfo { Id = 1, ProjectPath = @"C:\a\Plant.ap21" },
                new TiaInstanceInfo { Id = 2, ProjectPath = @"C:\b\Plant.ap21" }
            };

            var ex = Assert.ThrowsException<PortalException>(() => TiaInstanceSelection.Pick(both, null, "Plant.ap21"));

            StringAssert.Contains(ex.Message, "use processId");
        }
    }
}
