using System.Collections.Generic;
using ToolHelper = TiaMcpServer.ModelContextProtocol.Helper;

namespace TiaMcpServer.Test
{
    /// <summary>The unsaved-changes flag of 'get_project', which the smoke run reads. These tests do not connect to TIA Portal.</summary>
    [TestClass]
    [TestCategory("NoTia")]
    public class Test29ProjectState
    {
        [TestMethod]
        public void Test_2900_IsModified_ReadsTheAttribute()
        {
            var modified = new List<TiaMcpServer.ModelContextProtocol.Attribute> { new() { Name = "Name", Value = "P" }, new() { Name = "IsModified", Value = true } };
            var clean = new List<TiaMcpServer.ModelContextProtocol.Attribute> { new() { Name = "IsModified", Value = false } };

            Assert.AreEqual(true, ToolHelper.IsModified(modified));
            Assert.AreEqual(false, ToolHelper.IsModified(clean));
        }

        [TestMethod]
        public void Test_2901_IsModified_IsNullWhenTheAttributeIsMissingOrNotABool()
        {
            Assert.IsNull(ToolHelper.IsModified(null));
            Assert.IsNull(ToolHelper.IsModified(new List<TiaMcpServer.ModelContextProtocol.Attribute>()));
            Assert.IsNull(ToolHelper.IsModified(new List<TiaMcpServer.ModelContextProtocol.Attribute> { new() { Name = "IsModified", Value = "yes" } }));
        }
    }
}
