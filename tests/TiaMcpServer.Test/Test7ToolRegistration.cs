using System.Linq;
using System.Reflection;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for which tools the server registers. The project-mutating tools share the McpServer
    /// class with the read-only tools and are marked [WriteTool]; without '--allow-write' they must
    /// not be registered at all, so they stay out of 'tools/list'. These tests do not connect to
    /// TIA Portal and do not open a project.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class Test7ToolRegistration
    {
        private const string SampleReadTool = "GetTags";
        private const string SampleWriteTool = "CreateTag";
        private const string SampleDocumentWriteTool = "ImportSources";

        [TestInitialize]
        public void ClassInit()
        {
            Engineering.TiaMajorVersion = Settings.TiaMajorVersion;
            Openness.Initialize(Engineering.TiaMajorVersion);
        }

        [TestMethod]
        public void Test_700_BuildTools_WithoutAllowWrite_ExcludesEveryWriteTool()
        {
            // Arrange
            var writeMethods = WriteToolMethods();

            // Act
            var names = Program.BuildTools(allowWrite: false).Select(t => t.ProtocolTool.Name).ToList();

            // Assert
            Assert.IsTrue(writeMethods.Count > 0, "The write tools must carry [WriteTool]");
            Assert.IsTrue(names.Contains(SampleReadTool), "Read tools are always registered");
            Assert.IsFalse(names.Contains(SampleWriteTool), "A write tool must not be registered without --allow-write");
            Assert.IsFalse(names.Contains(SampleDocumentWriteTool), "A write tool must not be registered without --allow-write");
        }

        [TestMethod]
        public void Test_701_BuildTools_WithAllowWrite_AddsExactlyTheWriteTools()
        {
            // Arrange
            var writeMethods = WriteToolMethods();

            // Act
            var withoutWrite = Program.BuildTools(allowWrite: false).ToList();
            var withWrite = Program.BuildTools(allowWrite: true).Select(t => t.ProtocolTool.Name).ToList();

            // Assert
            Assert.AreEqual(writeMethods.Count, withWrite.Count - withoutWrite.Count,
                "--allow-write must add exactly the [WriteTool] tools");
            Assert.IsTrue(withWrite.Contains(SampleWriteTool));
            Assert.IsTrue(withWrite.Contains(SampleDocumentWriteTool));
            Assert.IsTrue(withWrite.Contains(SampleReadTool));
        }

        [TestMethod]
        public void Test_702_WriteToolAttribute_IsOnlyUsedOnTools()
        {
            // Arrange
            var writeMethods = WriteToolMethods();

            // Act
            var withoutToolAttribute = writeMethods
                .Where(m => m.GetCustomAttribute<global::ModelContextProtocol.Server.McpServerToolAttribute>() == null)
                .Select(m => m.Name)
                .ToList();

            // Assert
            Assert.AreEqual(0, withoutToolAttribute.Count,
                "[WriteTool] on a method that is not a tool has no effect: " + string.Join(", ", withoutToolAttribute));
        }

        private static System.Collections.Generic.List<MethodInfo> WriteToolMethods() =>
            typeof(McpServer)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<WriteToolAttribute>() != null)
                .ToList();
    }
}
