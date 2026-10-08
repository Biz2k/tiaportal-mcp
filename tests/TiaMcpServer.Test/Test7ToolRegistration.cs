using System.IO;
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
    [TestCategory("NoTia")]
    [DoNotParallelize]
    public class Test7ToolRegistration
    {
        private const string SampleReadTool = "plc_get_tags";
        private const string SampleWriteTool = "plc_create_tag";
        private const string SampleDocumentWriteTool = "plc_create_external_source";

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

        [TestMethod]
        public void Test_703_BuildTools_WithoutAllowWrite_ExcludesHardwareNetworkAndLibraryEdits()
        {
            // Arrange: every tool here changes the project and used to be registered regardless.
            var edits = new[]
            {
                "hw_create_device", "hw_plug_module", "hw_delete_device", "archive_project", "retrieve_project", "plc_manage_watch_table_entries",
                "net_connect_subnet", "net_disconnect_subnet", "net_create_io_system", "net_connect_to_io_system",
                "instantiate_master_copy", "import_objects", "unified_create_screen", "unified_delete_screen", "unified_manage_items",
                "unified_manage_faceplate", "unified_configure_trend_control",
                "unified_manage_tags", "unified_manage_tag_tables", "unified_manage_connections",
                "unified_manage_alarms", "unified_compile", "plc_replace_source", "unified_manage_alarm_classes", "unified_manage_lists", "unified_manage_screen_groups", "unified_manage_scripts", "unified_manage_tag_table_groups", "unified_manage_logs", "unified_manage_logging_tags"
            };

            // Act
            var readOnly = Program.BuildTools(allowWrite: false).Select(t => t.ProtocolTool.Name).ToList();
            var withWrite = Program.BuildTools(allowWrite: true).Select(t => t.ProtocolTool.Name).ToList();

            // Assert
            foreach (var edit in edits)
            {
                Assert.IsFalse(readOnly.Contains(edit), $"'{edit}' changes the project and must not be registered without --allow-write");
                Assert.IsTrue(withWrite.Contains(edit), $"'{edit}' must be registered with --allow-write");
            }

            Assert.IsTrue(readOnly.Contains("hw_search_catalog"), "The catalog search only reads");
            Assert.IsTrue(readOnly.Contains("hw_get_topology"), "The topology listing only reads");
        }

        [TestMethod]
        public void Test_704_BuildTools_RegistersDebugToolsOnlyOnRequest()
        {
            // Arrange
            var debugTools = new[] { "unified_debug_reflect", "unified_debug_screen_item" };

            // Act
            var normal = Program.BuildTools(allowWrite: true).Select(t => t.ProtocolTool.Name).ToList();
            var withDebug = Program.BuildTools(allowWrite: true, debugTools: true).Select(t => t.ProtocolTool.Name).ToList();
            var debugWithoutWrite = Program.BuildTools(allowWrite: false, debugTools: true).Select(t => t.ProtocolTool.Name).ToList();

            // Assert
            foreach (var tool in debugTools)
            {
                Assert.IsFalse(normal.Contains(tool), $"'{tool}' is a development tool and must stay out of the normal tool list");
                Assert.IsTrue(withDebug.Contains(tool), $"'{tool}' must be registered with --debug-tools");
            }

            Assert.IsTrue(debugWithoutWrite.Contains("unified_debug_screen_item"), "The debug tools only read, so they do not need --allow-write");
        }

        [TestMethod]
        public void Test_705_BuildTools_MatchesTheRecordedToolList()
        {
            // Arrange: docs/tools-list.txt is the reviewed list of tool names. A tool that
            // disappears or is renamed breaks every client that calls it, so it has to show up
            // as a deliberate change to that file.
            var file = FindRepositoryFile(Path.Combine("docs", "tools-list.txt"));
            var recorded = File.ReadAllLines(file).Where(l => l.Trim().Length > 0).Select(l => l.Trim()).ToList();

            // Act
            var actual = Program.BuildTools(allowWrite: true).Select(t => t.ProtocolTool.Name).OrderBy(n => n, System.StringComparer.Ordinal).ToList();

            // Assert
            var missing = recorded.Except(actual).ToList();
            var added = actual.Except(recorded).ToList();

            Assert.IsTrue(missing.Count == 0 && added.Count == 0,
                $"The registered tools differ from docs/tools-list.txt. No longer registered: [{string.Join(", ", missing)}]. " +
                $"Not recorded: [{string.Join(", ", added)}]. If the change is intended, update the file.");
        }

        private static string FindRepositoryFile(string relativePath)
        {
            var directory = new DirectoryInfo(System.AppContext.BaseDirectory);

            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath);

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            Assert.Fail($"'{relativePath}' was not found above '{System.AppContext.BaseDirectory}'.");

            return string.Empty;
        }

        private static System.Collections.Generic.List<MethodInfo> WriteToolMethods() =>
            typeof(McpServer)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<WriteToolAttribute>() != null)
                // Development tools are registered separately, see Test_704.
                .Where(m => m.GetCustomAttribute<DebugToolAttribute>() == null)
                .ToList();
    }
}
